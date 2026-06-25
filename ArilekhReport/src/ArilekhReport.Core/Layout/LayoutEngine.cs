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
/// </summary>
public sealed class LayoutEngine
{
    private readonly ExpressionEvaluator _eval = new();

    // ── Public entry point ────────────────────────────────────────────────────

    public async Task<ReportDocument> RenderAsync(
        ReportDefinition report,
        IDataSourceProvider dataProvider,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var doc = new ReportDocument { SourceDefinition = report };
        var ctx = new RenderContext(report, parameters ?? new Dictionary<string, object?>());
        var pageSetup = report.PageSetup;

        // Start the first page
        StartNewPage(doc, ctx, pageSetup);

        // 1. Report Header (page 1 only)
        await RenderStaticSectionsAsync(doc, ctx, pageSetup,
            report.GetSections(SectionType.ReportHeader), cancellationToken);

        // 1b. Page Header on first page
        RenderPageHeader(doc, ctx, pageSetup, report);

        // 2. Identify primary data source (first Detail section's DataSource)
        var detailSections = report.GetSections(SectionType.Detail).ToList();
        var primaryDsName = detailSections.FirstOrDefault()?.DataSourceName
                             ?? report.DataSources.FirstOrDefault()?.Name;

        if (primaryDsName is not null)
        {
            var table = await dataProvider.GetDataTableAsync(
                primaryDsName, ctx.Parameters, cancellationToken);

            ctx.CurrentTable = table;
            await RenderDataAsync(doc, ctx, pageSetup, report, dataProvider,
                table, detailSections, cancellationToken);
        }

        // 3. Report Footer
        await RenderStaticSectionsAsync(doc, ctx, pageSetup,
            report.GetSections(SectionType.ReportFooter), cancellationToken);

        // Close last page footer
        RenderPageFooter(doc, ctx, pageSetup, report);

        // Back-fill total pages
        foreach (var page in doc.Pages)
            BackFillTotalPages(page, doc.PageCount);

        doc.RenderDuration = System.Diagnostics.Stopwatch
            .GetElapsedTime(started);

        return doc;
    }

    // ── Data iteration ────────────────────────────────────────────────────────

    private async Task RenderDataAsync(
        ReportDocument doc,
        RenderContext ctx,
        ReportPageSetup pageSetup,
        ReportDefinition report,
        IDataSourceProvider provider,
        DataTable table,
        List<SectionDefinition> detailSections,
        CancellationToken ct)
    {
        var groupHeaderSections = report.GetSections(SectionType.GroupHeader).ToList();
        var groupFooterSections = report.GetSections(SectionType.GroupFooter).ToList();

        // Determine group fields
        var groupFields = groupHeaderSections
            .Where(s => !string.IsNullOrWhiteSpace(s.GroupField))
            .Select(s => s.GroupField!)
            .Distinct()
            .ToList();

        // Sort table by group fields if any
        DataView view = table.DefaultView;
        if (groupFields.Count > 0)
            view.Sort = string.Join(", ", groupFields);

        var rows = view.ToTable().Rows.Cast<DataRow>().ToList();

        ctx.ResetGroupAggregates();
        object? currentGroupKey = _getGroupKey(rows.FirstOrDefault(), groupFields);

        for (int i = 0; i < rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var row = rows[i];
            var rowGroupKey = _getGroupKey(row, groupFields);

            bool isGroupChange = !Equals(rowGroupKey, currentGroupKey);
            bool isFirstRow = i == 0;

            // Group footer for previous group
            if (isGroupChange && !isFirstRow)
            {
                await RenderStaticSectionsAsync(doc, ctx, pageSetup, groupFooterSections, ct);
                ctx.ResetGroupAggregates();
                currentGroupKey = rowGroupKey;
            }

            // Group header
            if (isFirstRow || isGroupChange)
            {
                ctx.CurrentRow = row;
                await RenderStaticSectionsAsync(doc, ctx, pageSetup, groupHeaderSections, ct);
            }

            // Accumulate row aggregates
            ctx.CurrentRow = row;
            ctx.RowIndex = i;
            ctx.AccumulateRow(row);

            // Detail band(s)
            foreach (var section in detailSections)
                RenderSectionBand(doc, ctx, pageSetup, section, isAlternate: i % 2 == 1);
        }

        // Final group footer
        if (rows.Count > 0)
            await RenderStaticSectionsAsync(doc, ctx, pageSetup, groupFooterSections, ct);
    }

    private static object? _getGroupKey(DataRow? row, List<string> groupFields)
    {
        if (row is null || groupFields.Count == 0) return null;
        if (groupFields.Count == 1)
        {
            var v = row[groupFields[0]];
            return v == DBNull.Value ? null : v;
        }
        return string.Join("||", groupFields.Select(f =>
        {
            var v = row[f];
            return v == DBNull.Value ? "" : v.ToString();
        }));
    }

    // ── Section rendering ─────────────────────────────────────────────────────

    private Task RenderStaticSectionsAsync(
        ReportDocument doc,
        RenderContext ctx,
        ReportPageSetup pageSetup,
        IEnumerable<SectionDefinition> sections,
        CancellationToken ct)
    {
        foreach (var section in sections)
        {
            ct.ThrowIfCancellationRequested();
            RenderSectionBand(doc, ctx, pageSetup, section, isAlternate: false);
        }
        return Task.CompletedTask;
    }

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

        var page = doc.Pages[^1];
        float footerHeight = ctx.Report.GetSections(SectionType.PageFooter).Sum(s => s.Height);
        float printableH = pageSetup.PrintableHeight - footerHeight;

        // Check if we need a new page (only for non-page-header/footer sections)
        bool isPageSection = section.Type is SectionType.PageHeader or SectionType.PageFooter;
        if (!isPageSection && ctx.CurrentY + section.Height > printableH)
        {
            RenderPageFooter(doc, ctx, pageSetup, ctx.Report);
            StartNewPage(doc, ctx, pageSetup);
            page = doc.Pages[^1];
            RenderPageHeader(doc, ctx, pageSetup, ctx.Report);
        }

        // Background
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

        // Render each field
        foreach (var field in section.Fields)
            RenderField(page, ctx, pageSetup, field, ctx.CurrentY, section.DefaultStyle);

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
        if (field.Name == "PageNum")
        {
            var hit = "me";
        }
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
                // Charts rendered as placeholder rect in preview
                page.Add(new RenderedRectElement
                {
                    X = absX,
                    Y = absY,
                    Width = field.Width,
                    Height = field.Height,
                    FillColor = "#f5f5f5",
                    StrokeColor = "#aaaaaa",
                    StrokeWidth = 0.5f,
                });
                page.Add(new RenderedTextElement
                {
                    X = absX,
                    Y = absY + field.Height / 2f - 6f,
                    Width = field.Width,
                    Height = 12f,
                    Text = $"[{field.Chart?.Type.ToString() ?? "Chart"}]",
                    Alignment = FieldAlignment.Center,
                });
                return;
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
        var style = MergeStyle(field.Style, sectionDefaultStyle, ctx.Report.DefaultStyle);

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
        var page = doc.AddPage(pageSetup.Width, pageSetup.Height);
        ctx.CurrentY = 0f;
        ctx.PageNumber = doc.Pages.Count;
    }

    private void RenderPageHeader(
        ReportDocument doc, RenderContext ctx,
        ReportPageSetup pageSetup, ReportDefinition report)
    {
        foreach (var section in report.GetSections(SectionType.PageHeader))
            RenderSectionBand(doc, ctx, pageSetup, section, isAlternate: false);
    }

    private void RenderPageFooter(
        ReportDocument doc, RenderContext ctx,
        ReportPageSetup pageSetup, ReportDefinition report)
    {
        // Page footer is pinned to the bottom margin
        float savedY = ctx.CurrentY;
        ctx.CurrentY = pageSetup.PrintableHeight
                     - report.GetSections(SectionType.PageFooter).Sum(s => s.Height);

        foreach (var section in report.GetSections(SectionType.PageFooter))
            RenderSectionBand(doc, ctx, pageSetup, section, isAlternate: false);

        ctx.CurrentY = savedY;
    }

    // ── Two-pass total-page back-fill ─────────────────────────────────────────

    private static void BackFillTotalPages(RenderedPage page, int totalPages)
    {
        // Replace placeholder text "?" with actual total pages
        foreach (var el in page.Elements.OfType<RenderedTextElement>())
        {
            if (el.Text == "?")
                el.Text = totalPages.ToString();
        }
    }

    // ── Formatting ────────────────────────────────────────────────────────────

    private static string ApplyFormat(object? value, FieldFormat format, string? formatString)
    {
        if (value is null) return string.Empty;

        if (format == FieldFormat.Custom && !string.IsNullOrWhiteSpace(formatString))
        {
            if (value is IFormattable f) return f.ToString(formatString, null);
            return value.ToString() ?? string.Empty;
        }

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

    // ── Style merging ─────────────────────────────────────────────────────────

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
        try
        {
            var val = _eval.Evaluate(expression, ctx);
            return ToImageSrc(val);
        }
        catch { return null; }
    }

    private static string? ResolveImageParameter(string? paramName, RenderContext ctx)
    {
        if (string.IsNullOrWhiteSpace(paramName)) return null;
        if (ctx.Parameters.TryGetValue(paramName, out var val))
            return ToImageSrc(val);
        return null;
    }

    private static string? ResolveImageDataField(string? columnName, RenderContext ctx)
    {
        if (string.IsNullOrWhiteSpace(columnName) || ctx.CurrentRow is null) return null;
        if (!ctx.CurrentTable?.Columns.Contains(columnName) ?? true) return null;
        var val = ctx.CurrentRow[columnName];
        return ToImageSrc(val);
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