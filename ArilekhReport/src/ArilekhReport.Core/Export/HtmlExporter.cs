using System;
using System.Collections.Generic;
using System.Text;

using System.Text;
using ArilekhReport.Core.Model;
using ArilekhReport.Core.Rendering;

namespace ArilekhReport.Core.Export;

/// <summary>
/// Exports a <see cref="ReportDocument"/> to a self-contained HTML string.
/// Each page is rendered as a positioned `div` with absolute-positioned field divs.
/// Suitable for browser preview and print-to-PDF via browser.
/// </summary>
public sealed class HtmlExporter
{
    public HtmlExportOptions Options { get; }

    public HtmlExporter(HtmlExportOptions? options = null)
        => Options = options ?? new HtmlExportOptions();

    // ── Main export ───────────────────────────────────────────────────────────

    public string Export(ReportDocument doc)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\" />");
        sb.AppendLine($"<title>{EscapeHtml(doc.SourceDefinition?.Name ?? "Report")}</title>");
        sb.AppendLine("<style>");
        sb.Append(BuildCss(doc));
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        for (int i = 0; i < doc.Pages.Count; i++)
        {
            RenderPage(sb, doc.Pages[i], i + 1, doc.PageCount);
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    // ── Page rendering ────────────────────────────────────────────────────────

    private void RenderPage(StringBuilder sb, RenderedPage page, int pageNum, int totalPages)
    {
        // Convert pt → px  (96dpi screen: 1pt ≈ 1.333px)
        float scale = Options.PointsToPixels;

        sb.AppendLine($"<div class=\"rd-page\" style=\"width:{page.Width * scale:F1}px;" +
                      $"height:{page.Height * scale:F1}px;\"" +
                      $" data-page=\"{pageNum}\">");

        foreach (var el in page.Elements)
        {
            switch (el)
            {
                case RenderedTextElement text:
                    RenderText(sb, text, scale);
                    break;
                case RenderedRectElement rect:
                    RenderRect(sb, rect, scale);
                    break;
                case RenderedLineElement line:
                    RenderLine(sb, line, scale);
                    break;
                case RenderedEllipseElement ellipse:
                    RenderEllipse(sb, ellipse, scale);
                    break;
                case RenderedImageElement image:
                    RenderImage(sb, image, scale);
                    break;
                case RenderedChartElement chart:
                    RenderChart(sb, chart, scale);
                    break;
            }
        }

        sb.AppendLine("</div>");
    }

    private static void RenderText(StringBuilder sb, RenderedTextElement el, float scale)
    {
        var style = new StringBuilder();
        style.Append($"left:{el.X * scale:F1}px;top:{el.Y * scale:F1}px;");
        style.Append($"width:{el.Width * scale:F1}px;height:{el.Height * scale:F1}px;");

        // Rotation
        if (el.Rotation != 0f)
            style.Append($"transform:rotate({el.Rotation:F1}deg);transform-origin:center center;");

        // Vertical alignment via flex
        var vAlign = el.VerticalAlign switch
        {
            FieldVerticalAlign.Middle => "center",
            FieldVerticalAlign.Bottom => "flex-end",
            _ => "flex-start",
        };
        style.Append($"display:flex;flex-direction:column;justify-content:{vAlign};");

        if (el.Style is { } s)
        {
            if (!string.IsNullOrWhiteSpace(s.FontFamily))
                style.Append($"font-family:{EscapeHtml(s.FontFamily)};");
            if (s.FontSize.HasValue)
                style.Append($"font-size:{s.FontSize.Value * scale:F1}px;");
            if (s.Bold) style.Append("font-weight:bold;");
            if (s.Italic) style.Append("font-style:italic;");
            if (s.Underline) style.Append("text-decoration:underline;");
            if (!string.IsNullOrWhiteSpace(s.ForeColor))
                style.Append($"color:{s.ForeColor};");
            if (!string.IsNullOrWhiteSpace(s.BackColor))
                style.Append($"background:{s.BackColor};");
            if (s.PaddingLeft > 0) style.Append($"padding-left:{s.PaddingLeft * scale:F1}px;");
            if (s.PaddingRight > 0) style.Append($"padding-right:{s.PaddingRight * scale:F1}px;");
            AppendBorderCss(style, s, scale);
        }

        style.Append(el.Alignment switch
        {
            FieldAlignment.Center => "text-align:center;",
            FieldAlignment.Right => "text-align:right;",
            _ => "text-align:left;",
        });

        sb.Append($"<div class=\"rd-field\" style=\"{style}\">");
        if (!string.IsNullOrWhiteSpace(el.HyperlinkUrl))
            sb.Append($"<a href=\"{EscapeHtml(el.HyperlinkUrl)}\">{EscapeHtml(el.Text)}</a>");
        else
            sb.Append(EscapeHtml(el.Text));
        sb.AppendLine("</div>");
    }

    private static void RenderRect(StringBuilder sb, RenderedRectElement el, float scale)
    {
        var style = new StringBuilder();
        style.Append($"left:{el.X * scale:F1}px;top:{el.Y * scale:F1}px;");
        style.Append($"width:{el.Width * scale:F1}px;height:{el.Height * scale:F1}px;");
        if (!string.IsNullOrWhiteSpace(el.FillColor))
            style.Append($"background:{el.FillColor};");
        if (!string.IsNullOrWhiteSpace(el.StrokeColor))
            style.Append($"border:{el.StrokeWidth * scale:F1}px solid {el.StrokeColor};");
        if (el.BorderRadius > 0)
            style.Append($"border-radius:{el.BorderRadius}%;");
        if (el.Rotation != 0f)
            style.Append($"transform:rotate({el.Rotation:F1}deg);transform-origin:center center;");
        sb.AppendLine($"<div class=\"rd-rect\" style=\"{style}\"></div>");
    }

    private static void RenderEllipse(StringBuilder sb, RenderedEllipseElement el, float scale)
    {
        float w = el.Width * scale;
        float h = el.Height * scale;
        float sw = el.StrokeWidth * scale;
        var rot = el.Rotation != 0f ? $"transform:rotate({el.Rotation:F1}deg);transform-origin:center center;" : "";
        sb.AppendLine("<svg class=\"rd-line\" style=\"" +
                      $"left:{el.X * scale:F1}px;top:{el.Y * scale:F1}px;" +
                      $"width:{w:F1}px;height:{h:F1}px;overflow:visible;{rot}\">");
        sb.AppendLine($"<ellipse cx=\"{w / 2:F1}\" cy=\"{h / 2:F1}\" " +
                      $"rx=\"{Math.Max(0, w / 2 - sw / 2):F1}\" ry=\"{Math.Max(0, h / 2 - sw / 2):F1}\" " +
                      $"fill=\"{el.FillColor ?? "none"}\" " +
                      $"stroke=\"{el.StrokeColor ?? "#000000"}\" stroke-width=\"{sw:F1}\"/>");
        sb.AppendLine("</svg>");
    }

    private static void RenderChart(StringBuilder sb, RenderedChartElement el, float scale)
    {
        float w = el.Width * scale, h = el.Height * scale;
        float pad = 8f * scale;
        var rot = el.Rotation != 0f ? $"transform:rotate({el.Rotation:F1}deg);transform-origin:center center;" : "";

        sb.AppendLine($"<svg style=\"position:absolute;left:{el.X * scale:F1}px;top:{el.Y * scale:F1}px;" +
                      $"width:{w:F1}px;height:{h:F1}px;{rot}\" xmlns=\"http://www.w3.org/2000/svg\">");

        if (!string.IsNullOrWhiteSpace(el.BackgroundColor))
            sb.AppendLine($"<rect x=\"0\" y=\"0\" width=\"{w:F1}\" height=\"{h:F1}\" fill=\"{el.BackgroundColor}\"/>");
        if (el.ShowBorder)
            sb.AppendLine($"<rect x=\".5\" y=\".5\" width=\"{w - 1:F1}\" height=\"{h - 1:F1}\" fill=\"none\" stroke=\"{el.BorderColor}\" stroke-width=\"{el.BorderWidth * scale:F1}\"/>");

        float topY = pad;
        if (!string.IsNullOrWhiteSpace(el.Title))
        {
            sb.AppendLine($"<text x=\"{w / 2:F1}\" y=\"{topY + 10:F1}\" text-anchor=\"middle\" font-size=\"{10 * scale:F1}\" font-weight=\"bold\" fill=\"#333\">{EscapeHtml(el.Title)}</text>");
            topY += 16 * scale;
        }

        float legH = (el.ShowLegend && el.Series.Count > 0) ? 14 * scale : 0;
        float chartH = h - topY - pad - legH;
        float chartW = w - pad * 2;

        var colors = new[] { "#4472C4", "#ED7D31", "#A9D18E", "#FF0000", "#FFC000", "#5B9BD5", "#70AD47", "#7030A0" };

        switch (el.ChartType.ToLowerInvariant())
        {
            case "pie":
                {
                    int count = Math.Max(1, el.Series.Count > 0 ? el.Series.Count : el.Categories.Count);
                    float cx = w / 2f, cy = topY + chartH / 2f, r = Math.Min(chartW, chartH) / 2f - 4;
                    double angle = -Math.PI / 2;
                    double step = 2 * Math.PI / count;
                    for (int i = 0; i < count; i++)
                    {
                        var s = i < el.Series.Count ? el.Series[i] : null;
                        var col = s?.Color ?? colors[i % colors.Length];
                        double a1 = angle, a2 = angle + step;
                        float x1 = cx + r * (float)Math.Cos(a1), y1 = cy + r * (float)Math.Sin(a1);
                        float x2 = cx + r * (float)Math.Cos(a2), y2 = cy + r * (float)Math.Sin(a2);
                        int lg = step > Math.PI ? 1 : 0;
                        sb.AppendLine($"<path d=\"M{cx:F1},{cy:F1} L{x1:F1},{y1:F1} A{r:F1},{r:F1} 0 {lg} 1 {x2:F1},{y2:F1} Z\" fill=\"{col}\" stroke=\"white\" stroke-width=\"1\"/>");
                        angle = a2;
                    }
                    break;
                }
            case "bar":
            case "barhorizontal":
                {
                    bool horiz = el.ChartType.ToLowerInvariant() == "barhorizontal";
                    int count = Math.Max(1, el.Series.Count);
                    float maxV = 1f;
                    foreach (var s in el.Series) if (s.Values.Count > 0) maxV = (float)Math.Max(maxV, s.Values.Max());

                    if (horiz) sb.AppendLine($"<line x1=\"{pad:F1}\" y1=\"{topY:F1}\" x2=\"{pad:F1}\" y2=\"{topY + chartH:F1}\" stroke=\"#ccc\" stroke-width=\"0.5\"/>");
                    else sb.AppendLine($"<line x1=\"{pad:F1}\" y1=\"{topY + chartH:F1}\" x2=\"{pad + chartW:F1}\" y2=\"{topY + chartH:F1}\" stroke=\"#ccc\" stroke-width=\"0.5\"/>");

                    float groupW = horiz ? chartH / count : chartW / Math.Max(1, el.Series.FirstOrDefault()?.Values.Count ?? 1);
                    float barW = Math.Max(4, groupW * 0.7f);

                    for (int si = 0; si < el.Series.Count; si++)
                    {
                        var s = el.Series[si];
                        var col = s.Color ?? colors[si % colors.Length];
                        for (int vi = 0; vi < s.Values.Count; vi++)
                        {
                            float frac = (float)(s.Values[vi] / maxV);
                            if (horiz)
                            {
                                float by = topY + si * groupW + (groupW - barW) / 2;
                                float bw = chartW * frac;
                                sb.AppendLine($"<rect x=\"{pad:F1}\" y=\"{by:F1}\" width=\"{bw:F1}\" height=\"{barW:F1}\" fill=\"{col}\"/>");
                            }
                            else
                            {
                                float bx = pad + vi * groupW + si * (groupW / Math.Max(1, el.Series.Count));
                                float bh = chartH * frac;
                                float bw = groupW / Math.Max(1, el.Series.Count) * 0.8f;
                                sb.AppendLine($"<rect x=\"{bx:F1}\" y=\"{topY + chartH - bh:F1}\" width=\"{bw:F1}\" height=\"{bh:F1}\" fill=\"{col}\"/>");
                            }
                        }
                    }
                    break;
                }
            case "line":
                {
                    sb.AppendLine($"<line x1=\"{pad:F1}\" y1=\"{topY + chartH:F1}\" x2=\"{pad + chartW:F1}\" y2=\"{topY + chartH:F1}\" stroke=\"#ccc\" stroke-width=\"0.5\"/>");
                    float maxV = 1f;
                    foreach (var s in el.Series) if (s.Values.Count > 0) maxV = (float)Math.Max(maxV, s.Values.Max());

                    for (int si = 0; si < el.Series.Count; si++)
                    {
                        var s = el.Series[si];
                        var col = s.Color ?? colors[si % colors.Length];
                        int cnt = s.Values.Count;
                        if (cnt < 2) continue;
                        var pts = string.Join(" ", s.Values.Select((v, i) =>
                        {
                            float x = pad + (float)i / (cnt - 1) * chartW;
                            float y = topY + chartH - (float)(v / maxV) * chartH;
                            return $"{x:F1},{y:F1}";
                        }));
                        sb.AppendLine($"<polyline points=\"{pts}\" fill=\"none\" stroke=\"{col}\" stroke-width=\"{2 * scale:F1}\"/>");
                        for (int i = 0; i < cnt; i++)
                        {
                            float x = pad + (float)i / (cnt - 1) * chartW;
                            float y = topY + chartH - (float)(s.Values[i] / maxV) * chartH;
                            sb.AppendLine($"<circle cx=\"{x:F1}\" cy=\"{y:F1}\" r=\"{2.5f * scale:F1}\" fill=\"{col}\"/>");
                        }
                    }
                    break;
                }
        }

        // Legend
        if (el.ShowLegend && el.Series.Count > 0)
        {
            float lx = pad, ly = h - legH + 2;
            foreach (var (s, i) in el.Series.Select((s, i) => (s, i)))
            {
                var col = s.Color ?? colors[i % colors.Length];
                sb.AppendLine($"<rect x=\"{lx:F1}\" y=\"{ly:F1}\" width=\"{8 * scale:F1}\" height=\"{8 * scale:F1}\" fill=\"{col}\"/>");
                sb.AppendLine($"<text x=\"{lx + 10 * scale:F1}\" y=\"{ly + 8 * scale:F1}\" font-size=\"{8 * scale:F1}\" fill=\"#333\">{EscapeHtml(s.Label)}</text>");
                lx += Math.Max(35 * scale, s.Label.Length * 5 * scale + 14 * scale);
                if (lx > w - 30) break;
            }
        }

        sb.AppendLine("</svg>");
    }

    private static void RenderImage(StringBuilder sb, RenderedImageElement el, float scale)
    {
        var objFit = el.Stretch switch
        {
            "cover" => "cover",
            "fill" => "fill",
            "none" => "none",
            _ => "contain",
        };
        var rot = el.Rotation != 0f ? $"transform:rotate({el.Rotation:F1}deg);transform-origin:center center;" : "";
        sb.AppendLine($"<img class=\"rd-rect\" style=\"" +
                      $"left:{el.X * scale:F1}px;top:{el.Y * scale:F1}px;" +
                      $"width:{el.Width * scale:F1}px;height:{el.Height * scale:F1}px;" +
                      $"object-fit:{objFit};{rot}\" src=\"{EscapeHtml(el.Src)}\" alt=\"\"/>");
    }

    private static void RenderLine(StringBuilder sb, RenderedLineElement el, float scale)
    {
        float left = Math.Min(el.X, el.X2) * scale;
        float top = Math.Min(el.Y, el.Y2) * scale;
        float width = Math.Abs(el.X2 - el.X) * scale;
        float height = Math.Abs(el.Y2 - el.Y) * scale;
        float svgW = Math.Max(width, el.StrokeWidth * scale + 2);
        float svgH = Math.Max(height, el.StrokeWidth * scale + 2);
        float x1r = (el.X * scale) - left;
        float y1r = (el.Y * scale) - top;
        float x2r = (el.X2 * scale) - left;
        float y2r = (el.Y2 * scale) - top;
        var rot = el.Rotation != 0f ? $"transform:rotate({el.Rotation:F1}deg);transform-origin:center center;" : "";
        sb.AppendLine($"<svg class=\"rd-line\" style=\"" +
                      $"left:{left:F1}px;top:{top:F1}px;" +
                      $"width:{svgW:F1}px;height:{svgH:F1}px;{rot}\">");
        sb.AppendLine($"<line x1=\"{x1r:F1}\" y1=\"{y1r:F1}\"" +
                      $" x2=\"{x2r:F1}\" y2=\"{y2r:F1}\"" +
                      $" stroke=\"{el.StrokeColor}\" stroke-width=\"{el.StrokeWidth * scale:F1}\"/>");
        sb.AppendLine("</svg>");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void AppendBorderCss(StringBuilder sb, FieldStyle s, float scale)
    {
        if (s.Border == BorderSides.None) return;

        var bw = s.BorderWidth * scale;
        var bc = s.BorderColor ?? "#000000";
        var sides = new[] { ("top", BorderSides.Top), ("right", BorderSides.Right),
                            ("bottom", BorderSides.Bottom), ("left", BorderSides.Left) };

        if (s.Border == BorderSides.All)
        {
            sb.Append($"border:{bw:F1}px solid {bc};");
        }
        else
        {
            foreach (var (name, flag) in sides)
                if ((s.Border & flag) != 0)
                    sb.Append($"border-{name}:{bw:F1}px solid {bc};");
        }
    }

    private string BuildCss(ReportDocument doc)
    {
        var defaults = doc.SourceDefinition?.DefaultStyle;
        return $$"""
            body {
                margin: 0;
                padding: 20px;
                background: #e0e0e0;
                font-family: {{defaults?.FontFamily ?? "Arial"}}, sans-serif;
            }
            .rd-page {
                position: relative;
                background: white;
                margin: 0 auto 20px;
                box-shadow: 0 2px 8px rgba(0,0,0,0.15);
                overflow: hidden;
                page-break-after: always;
            }
            .rd-field {
                position: absolute;
                overflow: hidden;
                white-space: nowrap;
                font-size: {{(defaults?.FontSize ?? 9f) * Options.PointsToPixels:F1}}px;
                color: {{defaults?.ForeColor ?? "#000000"}};
                line-height: 1.2;
                box-sizing: border-box;
            }
            .rd-rect {
                position: absolute;
                box-sizing: border-box;
            }
            .rd-line {
                position: absolute;
                overflow: visible;
            }
            @media print {
                body { background: white; padding: 0; }
                .rd-page { box-shadow: none; margin: 0; }
            }
            """;
    }

    private static string EscapeHtml(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
    }
}

/// <summary>Options for the HTML exporter.</summary>
public sealed class HtmlExportOptions
{
    /// <summary>Conversion factor from points to CSS pixels (default: 1.333 for 96 dpi).</summary>
    public float PointsToPixels { get; set; } = 1.333f;
}