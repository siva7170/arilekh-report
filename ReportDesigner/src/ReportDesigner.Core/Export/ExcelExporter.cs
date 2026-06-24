using ClosedXML.Excel;
using ReportDesigner.Core.Model;
using ReportDesigner.Core.Rendering;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReportDesigner.Core.Export
{
    public sealed class ExcelExporter : IReportExporter
    {
        public ExcelExportOptions Options { get; }

        public string FileExtension => ".xlsx";
        public string MimeType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public ExcelExporter(ExcelExportOptions? options = null)
            => Options = options ?? new ExcelExportOptions();

        // ── IReportExporter ───────────────────────────────────────────────

        public byte[] Export(ReportDocument document)
        {
            using var wb = BuildWorkbook(document);
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        public void ExportToFile(ReportDocument document, string filePath)
        {
            using var wb = BuildWorkbook(document);
            wb.SaveAs(filePath);
        }

        // ── Workbook builder ──────────────────────────────────────────────

        private XLWorkbook BuildWorkbook(ReportDocument report)
        {
            var wb = new XLWorkbook();
            var sourceReport = report.SourceDefinition;

            if (Options.DataSheetMode)
            {
                // ── Data mode: one worksheet per data source, flat table ──
                BuildDataSheets(wb, report, sourceReport);
            }
            else
            {
                // ── Layout mode: reproduce visual layout per page ─────────
                for (int i = 0; i < report.Pages.Count; i++)
                {
                    var sheetName = report.Pages.Count == 1
                        ? "Report"
                        : $"Page {i + 1}";
                    BuildLayoutSheet(wb, report.Pages[i], sheetName);
                }
            }

            return wb;
        }

        // ── Data sheet mode ───────────────────────────────────────────────

        /// <summary>
        /// Extracts text elements from Detail sections into a clean flat table.
        /// Best for data analysis; one row per Detail band instance.
        /// </summary>
        private void BuildDataSheets(
            XLWorkbook wb,
            ReportDocument report,
            ReportDefinition? sourceDef)
        {
            // Collect all text elements grouped by Y-band clusters
            // Strategy: group RenderedTextElement rows by their Y coordinate proximity
            // Elements within Options.RowHeightTolerance pts of each other = same row

            var ws = wb.AddWorksheet(
                TruncateSheetName(report.SourceDefinition?.Name ?? "Report"));

            // Gather all text elements across all pages
            var allTexts = report.Pages
                .SelectMany(p => p.Elements.OfType<RenderedTextElement>())
                .OrderBy(t => t.Y)
                .ThenBy(t => t.X)
                .ToList();

            if (!allTexts.Any()) return;

            // Group into logical rows by Y proximity
            var rows = GroupIntoRows(allTexts, Options.RowHeightTolerance);

            // Determine column positions from X coordinates of the first few rows
            var colPositions = DetectColumns(rows.Take(10).ToList());

            // Write header row if we have a source definition
            int excelRow = 1;
            if (sourceDef is not null)
            {
                var detailSections = sourceDef.Sections
                    .Where(s => s.Type == SectionType.Detail)
                    .ToList();

                if (detailSections.Any())
                {
                    var headerFields = detailSections[0].Fields
                        .OrderBy(f => f.X)
                        .ToList();

                    for (int c = 0; c < headerFields.Count; c++)
                    {
                        var cell = ws.Cell(excelRow, c + 1);
                        cell.Value = headerFields[c].Name;
                        StyleHeaderCell(cell);
                    }
                    excelRow++;
                }
            }

            // Write data rows
            foreach (var row in rows)
            {
                var sorted = row.OrderBy(t => t.X).ToList();
                for (int c = 0; c < sorted.Count; c++)
                {
                    var cell = ws.Cell(excelRow, c + 1);
                    SetCellValue(cell, sorted[c]);
                    ApplyTextStyle(cell, sorted[c]);
                }
                excelRow++;
            }

            // Auto-fit columns
            ws.Columns().AdjustToContents(1, 50);

            // Freeze first row if we wrote a header
            if (sourceDef is not null)
                ws.SheetView.FreezeRows(1);
        }

        // ── Layout sheet mode ─────────────────────────────────────────────

        /// <summary>
        /// Reproduces the visual layout of each page as closely as possible in Excel.
        /// Uses approximate row/column sizing based on element positions.
        /// </summary>
        private void BuildLayoutSheet(XLWorkbook wb, RenderedPage page, string name)
        {
            var ws = wb.AddWorksheet(TruncateSheetName(name));

            // Scale: 1 Excel row ≈ Options.PtPerRow points, 1 col ≈ Options.PtPerCol points
            double ptPerRow = Options.PtPerRow;
            double ptPerCol = Options.PtPerCol;

            var textElements = page.Elements.OfType<RenderedTextElement>().ToList();

            foreach (var text in textElements)
            {
                if (string.IsNullOrWhiteSpace(text.Text)) continue;

                int row = Math.Max(1, (int)Math.Round(text.Y / ptPerRow) + 1);
                int col = Math.Max(1, (int)Math.Round(text.X / ptPerCol) + 1);

                var cell = ws.Cell(row, col);

                // Don't overwrite non-empty cells (field overlap)
                if (!cell.IsEmpty()) continue;

                SetCellValue(cell, text);
                ApplyTextStyle(cell, text);

                // Set approximate row height and column width
                var xlRow = ws.Row(row);
                var xlCol = ws.Column(col);

                var desiredH = text.Height / ptPerRow * 15;  // Excel row height in points
                if (xlRow.Height < desiredH) xlRow.Height = desiredH;

                var desiredW = text.Width / ptPerCol;
                if (xlCol.Width < desiredW) xlCol.Width = desiredW;
            }

            // Background fills from rect elements
            foreach (var rect in page.Elements.OfType<RenderedRectElement>())
            {
                if (string.IsNullOrWhiteSpace(rect.FillColor)) continue;

                int row1 = Math.Max(1, (int)Math.Round(rect.Y / ptPerRow) + 1);
                int col1 = Math.Max(1, (int)Math.Round(rect.X / ptPerCol) + 1);
                int row2 = Math.Max(row1, (int)Math.Round((rect.Y + rect.Height) / ptPerRow) + 1);
                int col2 = Math.Max(col1, (int)Math.Round((rect.X + rect.Width) / ptPerCol) + 1);

                try
                {
                    var range = ws.Range(row1, col1, row2, col2);
                    range.Style.Fill.BackgroundColor = XLColor.FromHtml(rect.FillColor);
                }
                catch { /* skip invalid colour */ }
            }
        }

        // ── Cell value & style helpers ────────────────────────────────────

        private static void SetCellValue(IXLCell cell, RenderedTextElement text)
        {
            var raw = text.Text;
            if (string.IsNullOrEmpty(raw)) return;

            // Try to store as number for sortability
            if (decimal.TryParse(raw.Replace(",", "").TrimStart('$', '£', '€'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var num))
            {
                cell.Value = num;
            }
            else if (DateTime.TryParse(raw, out var dt))
            {
                cell.Value = dt;
            }
            else
            {
                cell.Value = raw;
            }
        }

        private static void ApplyTextStyle(IXLCell cell, RenderedTextElement text)
        {
            var s = text.Style;
            if (s is null) return;

            if (!string.IsNullOrWhiteSpace(s.FontFamily))
                cell.Style.Font.FontName = s.FontFamily;
            if (s.FontSize.HasValue)
                cell.Style.Font.FontSize = s.FontSize.Value;
            if (s.Bold) cell.Style.Font.Bold = true;
            if (s.Italic) cell.Style.Font.Italic = true;
            if (s.Underline) cell.Style.Font.Underline = XLFontUnderlineValues.Single;

            if (!string.IsNullOrWhiteSpace(s.ForeColor))
            {
                try { cell.Style.Font.FontColor = XLColor.FromHtml(s.ForeColor); }
                catch { }
            }
            if (!string.IsNullOrWhiteSpace(s.BackColor))
            {
                try { cell.Style.Fill.BackgroundColor = XLColor.FromHtml(s.BackColor); }
                catch { }
            }

            cell.Style.Alignment.Horizontal = text.Alignment switch
            {
                FieldAlignment.Center => XLAlignmentHorizontalValues.Center,
                FieldAlignment.Right => XLAlignmentHorizontalValues.Right,
                _ => XLAlignmentHorizontalValues.Left,
            };

            // Border
            if (s.Border != BorderSides.None)
            {
                var borderStyle = XLBorderStyleValues.Thin;
                if ((s.Border & BorderSides.Top) != 0) cell.Style.Border.TopBorder = borderStyle;
                if ((s.Border & BorderSides.Bottom) != 0) cell.Style.Border.BottomBorder = borderStyle;
                if ((s.Border & BorderSides.Left) != 0) cell.Style.Border.LeftBorder = borderStyle;
                if ((s.Border & BorderSides.Right) != 0) cell.Style.Border.RightBorder = borderStyle;
            }
        }

        private static void StyleHeaderCell(IXLCell cell)
        {
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F3864");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        }

        // ── Row grouping ──────────────────────────────────────────────────

        private static List<List<RenderedTextElement>> GroupIntoRows(
            List<RenderedTextElement> elements,
            float tolerance)
        {
            var rows = new List<List<RenderedTextElement>>();
            var current = new List<RenderedTextElement>();
            float lastY = float.MinValue;

            foreach (var el in elements)
            {
                if (current.Count == 0 || Math.Abs(el.Y - lastY) <= tolerance)
                {
                    current.Add(el);
                    lastY = el.Y;
                }
                else
                {
                    rows.Add(current);
                    current = new List<RenderedTextElement> { el };
                    lastY = el.Y;
                }
            }
            if (current.Count > 0) rows.Add(current);
            return rows;
        }

        private static List<float> DetectColumns(List<List<RenderedTextElement>> rows)
        {
            return rows.SelectMany(r => r)
                       .Select(t => t.X)
                       .Distinct()
                       .OrderBy(x => x)
                       .ToList();
        }

        private static string TruncateSheetName(string name)
        {
            // Excel sheet names max 31 chars, no special chars
            var clean = new string(name.Where(c =>
                c != ':' && c != '\\' && c != '/' && c != '?' &&
                c != '*' && c != '[' && c != ']').ToArray());
            return clean.Length > 31 ? clean[..31] : clean;
        }
    }

    /// <summary>Options for the Excel exporter.</summary>
    public sealed class ExcelExportOptions
    {
        /// <summary>
        /// When true: extract data into a clean flat table (best for analysis).
        /// When false: reproduce visual layout (best for print-like output).
        /// </summary>
        public bool DataSheetMode { get; set; } = true;

        /// <summary>Y-coordinate tolerance (pts) for grouping elements into the same row.</summary>
        public float RowHeightTolerance { get; set; } = 4f;

        /// <summary>Points per Excel row height unit (layout mode only).</summary>
        public double PtPerRow { get; set; } = 14.0;

        /// <summary>Points per Excel column width unit (layout mode only).</summary>
        public double PtPerCol { get; set; } = 8.0;
    }

}
