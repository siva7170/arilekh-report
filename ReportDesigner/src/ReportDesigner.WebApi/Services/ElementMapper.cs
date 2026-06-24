using ReportDesigner.Core.Model;
using ReportDesigner.Core.Rendering;
using ReportDesigner.WebApi.DTOs;

namespace ReportDesigner.WebApi.Services;

/// <summary>Maps Core rendering types to JSON-serialisable DTOs.</summary>
public static class ElementMapper
{
    public static PageResponse ToPageResponse(RenderedPage page, int totalPages) => new()
    {
        PageNumber = page.PageNumber,
        TotalPages = totalPages,
        WidthPt    = page.Width,
        HeightPt   = page.Height,
        Elements   = page.Elements.Select(ToDto).ToList(),
    };

    public static ElementDto ToDto(RenderedElement el) => el switch
    {
        RenderedTextElement    t => TextDto(t),
        RenderedRectElement    r => RectDto(r),
        RenderedLineElement    l => LineDto(l),
        RenderedEllipseElement e => EllipseDto(e),
        RenderedImageElement   i => ImageDto(i),
        _                        => new ElementDto { Type = "unknown" },
    };

    // ── Element type mappers ───────────────────────────────────────────────────

    private static ElementDto TextDto(RenderedTextElement t) => new()
    {
        Type         = "text",
        X            = t.X,  Y = t.Y,
        Width        = t.Width, Height = t.Height,
        Rotation     = t.Rotation,
        Text         = t.Text,
        Alignment    = t.Alignment.ToString().ToLowerInvariant(),
        VerticalAlign = t.VerticalAlign.ToString().ToLowerInvariant(),
        HyperlinkUrl = t.HyperlinkUrl,
        Style        = MapStyle(t.Style),
    };

    private static ElementDto RectDto(RenderedRectElement r) => new()
    {
        Type         = "rect",
        X            = r.X, Y = r.Y,
        Width        = r.Width, Height = r.Height,
        Rotation     = r.Rotation,
        FillColor    = r.FillColor,
        StrokeColor  = r.StrokeColor,
        StrokeWidth  = r.StrokeWidth,
        BorderRadius = r.BorderRadius,
    };

    private static ElementDto LineDto(RenderedLineElement l) => new()
    {
        Type        = "line",
        X           = l.X,  Y  = l.Y,
        X2          = l.X2, Y2 = l.Y2,
        Width       = Math.Abs(l.X2 - l.X),
        Height      = Math.Abs(l.Y2 - l.Y),
        Rotation    = l.Rotation,
        StrokeColor = l.StrokeColor,
        StrokeWidth = l.StrokeWidth,
    };

    private static ElementDto EllipseDto(RenderedEllipseElement e) => new()
    {
        Type        = "ellipse",
        X           = e.X, Y = e.Y,
        Width       = e.Width, Height = e.Height,
        Rotation    = e.Rotation,
        FillColor   = e.FillColor,
        StrokeColor = e.StrokeColor,
        StrokeWidth = e.StrokeWidth,
    };

    private static ElementDto ImageDto(RenderedImageElement i) => new()
    {
        Type     = "image",
        X        = i.X,  Y = i.Y,
        Width    = i.Width, Height = i.Height,
        Rotation = i.Rotation,
        Src      = i.Src,
        Stretch  = i.Stretch,
    };

    // ── Style ──────────────────────────────────────────────────────────────────

    private static StyleDto? MapStyle(FieldStyle? s) => s is null ? null : new StyleDto
    {
        FontFamily   = s.FontFamily,
        FontSize     = s.FontSize,
        Bold         = s.Bold,
        Italic       = s.Italic,
        Underline    = s.Underline,
        ForeColor    = s.ForeColor,
        BackColor    = s.BackColor,
        PaddingLeft  = s.PaddingLeft,
        PaddingRight = s.PaddingRight,
    };
}
