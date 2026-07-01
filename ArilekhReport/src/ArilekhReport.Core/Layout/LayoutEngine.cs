using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using ArilekhReport.Core.Model;
using ArilekhReport.Core.Data;
using ArilekhReport.Core.Rendering;
using ArilekhReport.Core.Expressions;

namespace ArilekhReport.Core.Layout;

/// <summary>
/// Processes a <see cref="ReportDefinition"/> and a data provider into a
/// fully paginated <see cref="ReportDocument"/>.
///
/// Render order:
///   ReportHeader
///   PageHeader            ← repeated on every new page
///   [for each group]:
///     GroupHeader
///     [for each Detail row]:
///       Detail            ← running / repeating section
///     GroupFooter
///   PageFooter            ← repeated on every page before page break
///   ReportFooter
///
/// Performance notes:
///   - Expression strings are pre-processed once per unique expression (cached)
///   - FieldStyle merging caches results per field (immutable per report)
///   - Page footer height is computed once per render, not per row
///   - DataView sort avoids DataTable copy
///   - All async methods that do no real async work are made synchronous internally
/// </summary>
public sealed class LayoutEngine
{
    private readonly ExpressionEvaluator _eval = new();

    // ── Per-render caches (reset each RenderAsync call) ───────────────────────
    private float _footerHeight;
    private readonly Dictionary<FieldElement, FieldStyle> _styleCache = new();

    // ── Public entry point ────────────────────────────────────────────────────

    public async Task<ReportDocument> RenderAsync(
        ReportDefinition report,
        IDataSourceProvider dataProvider,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();

        // Reset per-render caches
        _styleCache.Clear();

        var doc = new ReportDocument { SourceDefinition = report };
        var ctx = new RenderContext(report, parameters ?? new Dictionary<string, object?>());
        var pageSetup = report.PageSetup;

        // Cache footer height once — used on every row
        _footerHeight = report.GetSections(SectionType.PageFooter).Sum(s => s.Height);

        // Pre-merge styles for all fields (done once, not per row)
        PrewarmStyleCache(report);

        // Start the first page
        StartNewPage(doc, ctx, pageSetup);

        // 1. Report Header (page 1 only)
        RenderSections(doc, ctx, pageSetup, report.GetSections(SectionType.ReportHeader));
        //await RenderStaticSectionsAsync(doc, ctx, pageSetup,
        //    report.GetSections(SectionType.ReportHeader), cancellationToken);

        // 2. Page Header on page 1
        RenderPageHeader(doc, ctx, pageSetup, report);

        // 3a. Resolve ScalarField data sources up-front so their values are
        //     available in every band (headers, footers, detail) via Fields.<name>.
        foreach (var ds in report.DataSources.Where(d => d.Kind == DataSourceKind.ScalarField))
        {
            try
            {
                var scalarTable = await dataProvider.GetDataTableAsync(
                    ds.Name, ctx.Parameters, cancellationToken);

                // Convention: first row, first column holds the scalar value.
                if (scalarTable.Rows.Count > 0 && scalarTable.Columns.Count > 0)
                {
                    var raw = scalarTable.Rows[0][0];
                    ctx.ScalarValues[ds.Name] = raw == DBNull.Value ? null : raw;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[LayoutEngine] ScalarField '{ds.Name}' could not be resolved: {ex.Message}");
            }
        }

        // 3b. Data — skip ScalarField sources when choosing the primary DataTable.
        var detailSections = report.GetSections(SectionType.Detail).ToList();
        var primaryDsName = detailSections.FirstOrDefault()?.DataSourceName
                             ?? report.DataSources
                                      .FirstOrDefault(d => d.Kind != DataSourceKind.ScalarField)
                                      ?.Name;

        if (primaryDsName is not null)
        {
            var table = await dataProvider.GetDataTableAsync(
                primaryDsName, ctx.Parameters, cancellationToken);

            ctx.CurrentTable = table;
            RenderData(doc, ctx, pageSetup, report, table, detailSections, cancellationToken);
        }

        // 4. Report Footer
        RenderSections(doc, ctx, pageSetup, report.GetSections(SectionType.ReportFooter));
        //await RenderStaticSectionsAsync(doc, ctx, pageSetup,
        //    report.GetSections(SectionType.ReportFooter), cancellationToken);

        // 5. Close last page footer
        RenderPageFooter(doc, ctx, pageSetup, report);

        // 6. Back-fill total pages (two-pass)
        var totalPages = doc.PageCount;
        for (int pi = 0; pi < doc.Pages.Count; pi++)
            BackFillTotalPages(doc.Pages[pi], totalPages);
        //foreach (var page in doc.Pages)
        //    BackFillTotalPages(page, doc.PageCount);

        doc.RenderDuration = System.Diagnostics.Stopwatch.GetElapsedTime(started);

        return doc;
    }

    // ── Pre-warm style cache ──────────────────────────────────────────────────

    private void PrewarmStyleCache(ReportDefinition report)
    {
        foreach (var section in report.Sections)
        {
            foreach (var field in section.Fields)
            {
                if (!_styleCache.ContainsKey(field))
                    _styleCache[field] = MergeStyle(field.Style, section.DefaultStyle, report.DefaultStyle);
            }
        }
    }

    // ── Data iteration ────────────────────────────────────────────────────────

    private void RenderData(
        ReportDocument doc,
        RenderContext ctx,
        ReportPageSetup pageSetup,
        ReportDefinition report,
        DataTable table,
        List<SectionDefinition> detailSections,
        CancellationToken ct)
    {
        var groupHeaderSections = report.GetSections(SectionType.GroupHeader).ToList();
        var groupFooterSections = report.GetSections(SectionType.GroupFooter).ToList();

        var groupFields = groupHeaderSections
            .Where(s => !string.IsNullOrWhiteSpace(s.GroupField))
            .Select(s => s.GroupField!)
            .Distinct()
            .ToList();

        // Sort in-place using DataView (no DataTable copy)
        DataRow[] rows;
        if (groupFields.Count > 0)
        {
            var view = table.DefaultView;
            view.Sort = string.Join(", ", groupFields);
            rows = view.Cast<DataRowView>().Select(v => v.Row).ToArray();
        }
        else
        {
            rows = new DataRow[table.Rows.Count];
            table.Rows.CopyTo(rows, 0);
        }

        int rowCount = rows.Length;
        ctx.ResetGroupAggregates();
        object? currentGroupKey = GetGroupKey(rowCount > 0 ? rows[0] : null, groupFields);

        for (int i = 0; i < rowCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            var row = rows[i];
            var rowGroupKey = GetGroupKey(row, groupFields);
            bool isGroupChange = !Equals(rowGroupKey, currentGroupKey);
            bool isFirstRow = i == 0;

            if (isGroupChange && !isFirstRow)
            {
                RenderSections(doc, ctx, pageSetup, groupFooterSections);
                ctx.ResetGroupAggregates();
                currentGroupKey = rowGroupKey;
            }

            if (isFirstRow || isGroupChange)
            {
                ctx.CurrentRow = row;
                RenderSections(doc, ctx, pageSetup, groupHeaderSections);
            }

            ctx.CurrentRow = row;
            ctx.RowIndex = i;
            ctx.AccumulateRow(row);

            bool isAlternate = (i & 1) == 1;
            foreach (var section in detailSections)
                RenderSectionBand(doc, ctx, pageSetup, section, isAlternate);
        }

        if (rowCount > 0)
            RenderSections(doc, ctx, pageSetup, groupFooterSections);
    }


    //    private async Task RenderDataAsync(
    //        ReportDocument doc,
    //        RenderContext ctx,
    //        ReportPageSetup pageSetup,
    //        ReportDefinition report,
    //        IDataSourceProvider provider,
    //        DataTable table,
    //        List<SectionDefinition> detailSections,
    //        CancellationToken ct)
    //    {
    //        var groupHeaderSections = report.GetSections(SectionType.GroupHeader).ToList();
    //        var groupFooterSections = report.GetSections(SectionType.GroupFooter).ToList();
    //
    //        // Determine group fields
    //        var groupFields = groupHeaderSections
    //            .Where(s => !string.IsNullOrWhiteSpace(s.GroupField))
    //            .Select(s => s.GroupField!)
    //            .Distinct()
    //            .ToList();
    //
    //        // Sort table by group fields if any
    //        DataView view = table.DefaultView;
    //        if (groupFields.Count > 0)
    //            view.Sort = string.Join(", ", groupFields);
    //
    //        var rows = view.ToTable().Rows.Cast<DataRow>().ToList();
    //
    //        ctx.ResetGroupAggregates();
    //        object? currentGroupKey = _getGroupKey(rows.FirstOrDefault(), groupFields);
    //
    //        for (int i = 0; i < rows.Count; i++)
    //        {
    //            ct.ThrowIfCancellationRequested();
    //            var row = rows[i];
    //            var rowGroupKey = _getGroupKey(row, groupFields);
    //
    //            bool isGroupChange = !Equals(rowGroupKey, currentGroupKey);
    //            bool isFirstRow = i == 0;
    //
    //            // Group footer for previous group
    //            if (isGroupChange && !isFirstRow)
    //            {
    //                await RenderStaticSectionsAsync(doc, ctx, pageSetup, groupFooterSections, ct);
    //                ctx.ResetGroupAggregates();
    //                currentGroupKey = rowGroupKey;
    //            }
    //
    //            // Group header
    //            if (isFirstRow || isGroupChange)
    //            {
    //                ctx.CurrentRow = row;
    //                await RenderStaticSectionsAsync(doc, ctx, pageSetup, groupHeaderSections, ct);
    //            }
    //
    //            // Accumulate row aggregates
    //            ctx.CurrentRow = row;
    //            ctx.RowIndex = i;
    //            ctx.AccumulateRow(row);
    //
    //            // Detail band(s)
    //            foreach (var section in detailSections)
    //                RenderSectionBand(doc, ctx, pageSetup, section, isAlternate: i % 2 == 1);
    //        }
    //
    //        // Final group footer
    //        if (rows.Count > 0)
    //            await RenderStaticSectionsAsync(doc, ctx, pageSetup, groupFooterSections, ct);
    //    }


    private static object? GetGroupKey(DataRow? row, List<string> groupFields)
    {
        if (row is null || groupFields.Count == 0) return null;
        if (groupFields.Count == 1)
        {
            var v = row[groupFields[0]];
            return v == DBNull.Value ? null : v;
        }
        // Multi-field group key — build composite string
        var sb = new StringBuilder();
        for (int k = 0; k < groupFields.Count; k++)
        {
            if (k > 0) sb.Append("||");
            var v = row[groupFields[k]];
            if (v != DBNull.Value) sb.Append(v);
        }
        return sb.ToString();
    }


    //    private static object? _getGroupKey(DataRow? row, List<string> groupFields)
    //    {
    //        if (row is null || groupFields.Count == 0) return null;
    //        if (groupFields.Count == 1)
    //        {
    //            var v = row[groupFields[0]];
    //            return v == DBNull.Value ? null : v;
    //        }
    //        return string.Join("||", groupFields.Select(f =>
    //        {
    //            var v = row[f];
    //            return v == DBNull.Value ? "" : v.ToString();
    //        }));
    //    }

    // ── Section rendering ─────────────────────────────────────────────────────

    // Synchronous — no async overhead for static sections
    private void RenderSections(
        ReportDocument doc,
        RenderContext ctx,
        ReportPageSetup pageSetup,
        IEnumerable<SectionDefinition> sections)
    {
        foreach (var section in sections)
            RenderSectionBand(doc, ctx, pageSetup, section, isAlternate: false);
    }

    //    private Task RenderStaticSectionsAsync(
    //        ReportDocument doc,
    //        RenderContext ctx,
    //        ReportPageSetup pageSetup,
    //        IEnumerable<SectionDefinition> sections,
    //        CancellationToken ct)
    //    {
    //        foreach (var section in sections)
    //        {
    //            ct.ThrowIfCancellationRequested();
    //            RenderSectionBand(doc, ctx, pageSetup, section, isAlternate: false);
    //        }
    //        return Task.CompletedTask;
    //    }

    private void RenderSectionBand(
        ReportDocument doc,
        RenderContext ctx,
        ReportPageSetup pageSetup,
        SectionDefinition section,
        bool isAlternate)
    {
        // Evaluate suppress expression
        if (!string.IsNullOrWhiteSpace(section.SuppressExpression) &&
            _eval.EvaluateBool(section.SuppressExpression, ctx))
            return;

        float printableH = pageSetup.PrintableHeight - _footerHeight;  // pre-cached    

        //var page = doc.Pages[^1];
        //float footerHeight = ctx.Report.GetSections(SectionType.PageFooter).Sum(s => s.Height);
        //float printableH = pageSetup.PrintableHeight - footerHeight;

        // Check if we need a new page (only for non-page-header/footer sections)
        bool isPageSection = section.Type is SectionType.PageHeader or SectionType.PageFooter;
        if (!isPageSection && ctx.CurrentY + section.Height > printableH)
        {
            RenderPageFooter(doc, ctx, pageSetup, ctx.Report);
            StartNewPage(doc, ctx, pageSetup);
            //page = doc.Pages[^1];
            RenderPageHeader(doc, ctx, pageSetup, ctx.Report);
        }

        var page = doc.Pages[^1];

        // Section background
        var backColor = isAlternate && section.AlternateRows
            ? (section.AlternateBackColor ?? section.BackColor)
            : section.BackColor;

        if (!string.IsNullOrWhiteSpace(backColor))
        {
            page.Add(new RenderedRectElement
            {
                X = pageSetup.MarginLeft,
                Y = pageSetup.MarginTop + ctx.CurrentY,
                Width = pageSetup.PrintableWidth,
                Height = section.Height,
                FillColor = backColor,
            });
        }

        // Render fields — use cached style
        var fields = section.Fields;
        int count = fields.Count;
        for (int fi = 0; fi < count; fi++)
            RenderField(page, ctx, pageSetup, fields[fi], ctx.CurrentY, section.DefaultStyle);
        //foreach (var field in section.Fields)
        //    RenderField(page, ctx, pageSetup, field, ctx.CurrentY, section.DefaultStyle);

        ctx.CurrentY += section.Height;
    }

    private void RenderField(
        RenderedPage page,
        RenderContext ctx,
        ReportPageSetup pageSetup,
        FieldElement field,
        float sectionY,
        FieldStyle? sectionDefaultStyle)
    {
        //if (field.Name == "PageNum")
        //{
        //    var hit = "me";
        //}
        // Suppress
        if (!string.IsNullOrWhiteSpace(field.SuppressExpression) &&
            _eval.EvaluateBool(field.SuppressExpression, ctx))
            return;

        float absX = pageSetup.MarginLeft + field.X;
        float absY = pageSetup.MarginTop + sectionY + field.Y;

        // ── Shape / visual elements — emit geometry, no text ─────────────────
        switch (field.Kind)
        {
            case ElementKind.Line:
                bool isHoriz = field.Width >= field.Height;
                page.Add(new RenderedLineElement
                {
                    X = absX,
                    Y = isHoriz ? absY + field.Height / 2f : absY,
                    X2 = isHoriz ? absX + field.Width : absX,
                    Y2 = isHoriz ? absY + field.Height / 2f : absY + field.Height,
                    StrokeColor = field.StrokeColor ?? "#000000",
                    StrokeWidth = field.StrokeWidth,
                    Rotation = field.Rotation,
                });
                return;

            case ElementKind.Box:
                page.Add(new RenderedRectElement
                {
                    X = absX,
                    Y = absY,
                    Width = field.Width,
                    Height = field.Height,
                    FillColor = field.FillColor,
                    StrokeColor = field.StrokeColor ?? "#000000",
                    StrokeWidth = field.StrokeWidth,
                    BorderRadius = field.BorderRadius,
                    Rotation = field.Rotation,
                });
                return;

            case ElementKind.Circle:
                page.Add(new RenderedEllipseElement
                {
                    X = absX,
                    Y = absY,
                    Width = field.Width,
                    Height = field.Height,
                    FillColor = field.FillColor,
                    StrokeColor = field.StrokeColor ?? "#000000",
                    StrokeWidth = field.StrokeWidth,
                    Rotation = field.Rotation,
                });
                return;

            case ElementKind.Image:
                {
                    // Resolve image source based on mode
                    string? src = field.ImageSourceMode switch
                    {
                        ImageSourceMode.Static => field.ImageSrc,
                        ImageSourceMode.Expression => ResolveImageExpression(field.ImageExpression, ctx),
                        ImageSourceMode.Parameter => ResolveImageParameter(field.ImageExpression, ctx),
                        ImageSourceMode.DataField => ResolveImageDataField(field.ImageExpression, ctx),
                        _ => field.ImageSrc,
                    };

                    if (!string.IsNullOrWhiteSpace(src))
                    {
                        // Normalize: if raw base64 (no data: prefix), wrap it
                        if (!src.StartsWith("data:") && !src.StartsWith("http"))
                            src = $"data:image/png;base64,{src}";

                        page.Add(new RenderedImageElement
                        {
                            X = absX,
                            Y = absY,
                            Width = field.Width,
                            Height = field.Height,
                            Src = src,
                            Stretch = field.ImageStretch,
                            Rotation = field.Rotation,
                        });
                    }
                    return;
                }

            case ElementKind.Chart:
                {
                    var chartDef = field.Chart;
                    if (chartDef is null) return;

                    var rendered = new RenderedChartElement
                    {
                        X = absX,
                        Y = absY,
                        Width = field.Width,
                        Height = field.Height,
                        Rotation = field.Rotation,
                        ChartType = chartDef.Type.ToString().ToLowerInvariant(),
                        Title = chartDef.Title,
                        ShowLegend = chartDef.ShowLegend,
                        ShowLabels = chartDef.ShowLabels,
                        ShowBorder = chartDef.ShowBorder,
                        BorderColor = chartDef.BorderColor,
                        BorderWidth = chartDef.BorderWidth,
                        BackgroundColor = chartDef.BackgroundColor,
                    };

                    if (ctx.CurrentTable is not null && chartDef.Series.Count > 0)
                    {
                        var cats = new List<string>();
                        if (!string.IsNullOrWhiteSpace(chartDef.CategoryField) &&
                            ctx.CurrentTable.Columns.Contains(chartDef.CategoryField))
                        {
                            foreach (DataRow row in ctx.CurrentTable.Rows)
                                cats.Add(row[chartDef.CategoryField]?.ToString() ?? "");
                        }
                        rendered.Categories = cats;

                        foreach (var serie in chartDef.Series)
                        {
                            var rs = new RenderedChartSeries { Label = serie.Label, Color = serie.Color ?? "#4472C4" };
                            if (!string.IsNullOrWhiteSpace(serie.FieldName) &&
                                ctx.CurrentTable.Columns.Contains(serie.FieldName))
                            {
                                foreach (DataRow row in ctx.CurrentTable.Rows)
                                    rs.Values.Add(double.TryParse(row[serie.FieldName]?.ToString(), out var v) ? v : 0);
                            }
                            rendered.Series.Add(rs);
                        }
                    }
                    page.Add(rendered);
                    return;
                }
                // Charts rendered as placeholder rect in preview
                //page.Add(new RenderedRectElement
                //{
                //    X = absX,
                //    Y = absY,
                //    Width = field.Width,
                //    Height = field.Height,
                //    FillColor = "#f5f5f5",
                //    StrokeColor = "#aaaaaa",
                //    StrokeWidth = 0.5f,
                //});
                //page.Add(new RenderedTextElement
                //{
                //    X = absX,
                //    Y = absY + field.Height / 2f - 6f,
                //    Width = field.Width,
                //    Height = 12f,
                //    Text = $"[{field.Chart?.Type.ToString() ?? "Chart"}]",
                //    Alignment = FieldAlignment.Center,
                //});
                //return;
        }

        // ── Text / field / custom formula elements ────────────────────────────

        // Resolve value
        string text;
        if (!string.IsNullOrWhiteSpace(field.Expression))
        {
            var raw = _eval.Evaluate(field.Expression, ctx);
            text = ApplyFormat(raw, field.Format, field.FormatString);
        }
        else
        {
            text = field.Text ?? string.Empty;
        }

        // Only fall back to row column lookup for plain Field kind elements,
        // and only when the column actually exists — prevents DataColumn_NotInTheTable
        // errors for shape/chart/formula elements whose names are generated GUIDs.
        if (string.IsNullOrWhiteSpace(text)
            && field.Kind == ElementKind.Field
            && ctx.CurrentRow is not null
            && ctx.CurrentTable?.Columns.Contains(field.Name) == true)
        {
            text = ctx.CurrentRow[field.Name]?.ToString() ?? string.Empty;
        }

        // Resolve hyperlink
        string? hyperlink = null;
        if (!string.IsNullOrWhiteSpace(field.HyperlinkExpression))
            hyperlink = _eval.Evaluate(field.HyperlinkExpression, ctx)?.ToString();

        // Merge styles (field → section default → report default)
        // Use pre-warmed style cache
        if (!_styleCache.TryGetValue(field, out var style))
            style = MergeStyle(field.Style, sectionDefaultStyle, ctx.Report.DefaultStyle);
        //var style = MergeStyle(field.Style, sectionDefaultStyle, ctx.Report.DefaultStyle);

        page.Add(new RenderedTextElement
        {
            X = absX,
            Y = absY,
            Width = field.Width,
            Height = field.Height,
            Text = text,
            Alignment = field.Alignment,
            VerticalAlign = field.VerticalAlign,
            Rotation = field.Rotation,
            Style = style,
            HyperlinkUrl = hyperlink,
        });
    }

    // ── Page management ───────────────────────────────────────────────────────

    private void StartNewPage(ReportDocument doc, RenderContext ctx, ReportPageSetup pageSetup)
    {
        doc.AddPage(pageSetup.Width, pageSetup.Height);
        ctx.CurrentY = 0f;
        ctx.PageNumber = doc.Pages.Count;
    }

    private void RenderPageHeader(
        ReportDocument doc, RenderContext ctx,
        ReportPageSetup pageSetup, ReportDefinition report)
    {
        var sections = report.GetSections(SectionType.PageHeader);
        foreach (var section in sections)
            RenderSectionBand(doc, ctx, pageSetup, section, false);
        //foreach (var section in report.GetSections(SectionType.PageHeader))
        //    RenderSectionBand(doc, ctx, pageSetup, section, isAlternate: false);
    }

    private void RenderPageFooter(
        ReportDocument doc, RenderContext ctx,
        ReportPageSetup pageSetup, ReportDefinition report)
    {
        // Page footer is pinned to the bottom margin

        float savedY = ctx.CurrentY;
        ctx.CurrentY = pageSetup.PrintableHeight - _footerHeight;
        var sections = report.GetSections(SectionType.PageFooter);
        foreach (var section in sections)
            RenderSectionBand(doc, ctx, pageSetup, section, false);
        ctx.CurrentY = savedY;

        //float savedY = ctx.CurrentY;
        //ctx.CurrentY = pageSetup.PrintableHeight
        //             - report.GetSections(SectionType.PageFooter).Sum(s => s.Height);

        //foreach (var section in report.GetSections(SectionType.PageFooter))
        //    RenderSectionBand(doc, ctx, pageSetup, section, isAlternate: false);

        //ctx.CurrentY = savedY;
    }

    // ── Two-pass total-page back-fill ─────────────────────────────────────────

    private static void BackFillTotalPages(RenderedPage page, int totalPages)
    {
        var elements = page.Elements;
        int count = elements.Count;
        var totalStr = totalPages.ToString();
        for (int i = 0; i < count; i++)
        {
            if (elements[i] is RenderedTextElement { Text: "?" } te)
                te.Text = totalStr;
        }
        // Replace placeholder text "?" with actual total pages
        //foreach (var el in page.Elements.OfType<RenderedTextElement>())
        //{
        //    if (el.Text == "?")
        //        el.Text = totalPages.ToString();
        //}
    }

    // ── Formatting ────────────────────────────────────────────────────────────

    private static string ApplyFormat(object? value, FieldFormat format, string? formatString)
    {
        if (value is null or DBNull) return string.Empty;
        //if (value is null) return string.Empty;


        if (format == FieldFormat.Custom && !string.IsNullOrWhiteSpace(formatString))
            return value is IFormattable f ? f.ToString(formatString, null) : value.ToString()!;

        //if (format == FieldFormat.Custom && !string.IsNullOrWhiteSpace(formatString))
        //{
        //    if (value is IFormattable f) return f.ToString(formatString, null);
        //    return value.ToString() ?? string.Empty;
        //}

        return format switch
        {
            FieldFormat.Currency => value is IFormattable cf ? cf.ToString("C", null) : value.ToString()!,
            FieldFormat.Percent => value is IFormattable pf ? pf.ToString("P", null) : value.ToString()!,
            FieldFormat.Integer => value is IFormattable nf ? nf.ToString("N0", null) : value.ToString()!,
            FieldFormat.Decimal2 => value is IFormattable d2 ? d2.ToString("N2", null) : value.ToString()!,
            FieldFormat.Date => value is IFormattable df ? df.ToString("d", null) : value.ToString()!,
            FieldFormat.DateTime => value is IFormattable dtf ? dtf.ToString("g", null) : value.ToString()!,
            FieldFormat.LongDate => value is IFormattable ldf ? ldf.ToString("D", null) : value.ToString()!,
            FieldFormat.Time => value is IFormattable tf ? tf.ToString("t", null) : value.ToString()!,
            _ => value.ToString() ?? string.Empty,
        };
    }

    // ── Style merging (result cached by PrewarmStyleCache) ────────────────────

    private static FieldStyle MergeStyle(
        FieldStyle? field,
        FieldStyle? sectionDefault,
        ReportDefaultStyle reportDefault)
    {
        return new FieldStyle
        {
            FontFamily = field?.FontFamily ?? sectionDefault?.FontFamily ?? reportDefault.FontFamily,
            FontSize = field?.FontSize ?? sectionDefault?.FontSize ?? reportDefault.FontSize,
            Bold = field?.Bold ?? sectionDefault?.Bold ?? false,
            Italic = field?.Italic ?? sectionDefault?.Italic ?? false,
            Underline = field?.Underline ?? sectionDefault?.Underline ?? false,
            ForeColor = field?.ForeColor ?? sectionDefault?.ForeColor ?? reportDefault.ForeColor,
            BackColor = field?.BackColor ?? sectionDefault?.BackColor,
            Border = field?.Border ?? sectionDefault?.Border ?? BorderSides.None,
            BorderColor = field?.BorderColor ?? sectionDefault?.BorderColor,
            BorderWidth = field?.BorderWidth ?? sectionDefault?.BorderWidth ?? 0.5f,
            PaddingLeft = field?.PaddingLeft ?? sectionDefault?.PaddingLeft ?? 2f,
            PaddingRight = field?.PaddingRight ?? sectionDefault?.PaddingRight ?? 2f,
        };
    }

    // ── Image source resolvers ────────────────────────────────────────────────

    private string? ResolveImageExpression(string? expression, RenderContext ctx)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;
        try { return ToImageSrc(_eval.Evaluate(expression, ctx)); }
        catch { return null; }
    }

    private static string? ResolveImageParameter(string? paramName, RenderContext ctx)
    {
        if (string.IsNullOrWhiteSpace(paramName)) return null;
        return ctx.Parameters.TryGetValue(paramName, out var val) ? ToImageSrc(val) : null;

        //if (ctx.Parameters.TryGetValue(paramName, out var val))
        //    return ToImageSrc(val);
        //return null;
    }

    private static string? ResolveImageDataField(string? columnName, RenderContext ctx)
    {
        if (string.IsNullOrWhiteSpace(columnName) || ctx.CurrentRow is null) return null;
        if (ctx.CurrentTable?.Columns.Contains(columnName) != true) return null;
        return ToImageSrc(ctx.CurrentRow[columnName]);
        //if (!ctx.CurrentTable?.Columns.Contains(columnName) ?? true) return null;
        //var val = ctx.CurrentRow[columnName];
        //return ToImageSrc(val);
    }

    /// <summary>
    /// Normalises a raw value to a usable image src:
    /// - byte[] → base64 data URI (png assumed)
    /// - string starting with data: or http → used as-is
    /// - other string → treated as base64, wrapped as data URI
    /// </summary>
    private static string? ToImageSrc(object? val) => val switch
    {
        null or DBNull => null,
        byte[] bytes when bytes.Length > 0 => $"data:image/png;base64,{Convert.ToBase64String(bytes)}",
        string s when s.StartsWith("data:") => s,
        string s when s.StartsWith("http") => s,
        string s when !string.IsNullOrWhiteSpace(s) => $"data:image/png;base64,{s}",
        _ => null,
    };
}