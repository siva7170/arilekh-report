using System;
using System.Collections.Generic;
using System.Text;

using System.Text;
using ReportDesigner.Core.Model;
using ReportDesigner.Core.Rendering;

namespace ReportDesigner.Core.Export;

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
            _                         => "flex-start",
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
            FieldAlignment.Right  => "text-align:right;",
            _                     => "text-align:left;",
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
        float w  = el.Width  * scale;
        float h  = el.Height * scale;
        float sw = el.StrokeWidth * scale;
        var rot  = el.Rotation != 0f ? $"transform:rotate({el.Rotation:F1}deg);transform-origin:center center;" : "";
        sb.AppendLine("<svg class=\"rd-line\" style=\"" +
                      $"left:{el.X * scale:F1}px;top:{el.Y * scale:F1}px;" +
                      $"width:{w:F1}px;height:{h:F1}px;overflow:visible;{rot}\">");
        sb.AppendLine($"<ellipse cx=\"{w/2:F1}\" cy=\"{h/2:F1}\" " +
                      $"rx=\"{Math.Max(0, w/2 - sw/2):F1}\" ry=\"{Math.Max(0, h/2 - sw/2):F1}\" " +
                      $"fill=\"{el.FillColor ?? "none"}\" " +
                      $"stroke=\"{el.StrokeColor ?? "#000000"}\" stroke-width=\"{sw:F1}\"/>");
        sb.AppendLine("</svg>");
    }

    private static void RenderImage(StringBuilder sb, RenderedImageElement el, float scale)
    {
        var objFit = el.Stretch switch
        {
            "cover" => "cover",
            "fill"  => "fill",
            "none"  => "none",
            _       => "contain",
        };
        var rot = el.Rotation != 0f ? $"transform:rotate({el.Rotation:F1}deg);transform-origin:center center;" : "";
        sb.AppendLine($"<img class=\"rd-rect\" style=\"" +
                      $"left:{el.X * scale:F1}px;top:{el.Y * scale:F1}px;" +
                      $"width:{el.Width * scale:F1}px;height:{el.Height * scale:F1}px;" +
                      $"object-fit:{objFit};{rot}\" src=\"{EscapeHtml(el.Src)}\" alt=\"\"/>");
    }

    private static void RenderLine(StringBuilder sb, RenderedLineElement el, float scale)
    {
        float left   = Math.Min(el.X,  el.X2)  * scale;
        float top    = Math.Min(el.Y,  el.Y2)  * scale;
        float width  = Math.Abs(el.X2 - el.X)  * scale;
        float height = Math.Abs(el.Y2 - el.Y)  * scale;
        float svgW   = Math.Max(width,  el.StrokeWidth * scale + 2);
        float svgH   = Math.Max(height, el.StrokeWidth * scale + 2);
        float x1r    = (el.X  * scale) - left;
        float y1r    = (el.Y  * scale) - top;
        float x2r    = (el.X2 * scale) - left;
        float y2r    = (el.Y2 * scale) - top;
        var rot      = el.Rotation != 0f ? $"transform:rotate({el.Rotation:F1}deg);transform-origin:center center;" : "";
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