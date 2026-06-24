using System;
using System.Collections.Generic;
using System.Text;
using ReportDesigner.Core.Model;

namespace ReportDesigner.Core.Rendering;

// ─────────────────────────────────────────────────────────────────────────────
// Rendered element types
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Base class for all rendered elements placed on a <see cref="RenderedPage"/>.
/// All position/size values are in points (1 pt = 1/72 inch).
/// </summary>
public abstract class RenderedElement
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float Rotation { get; set; }   // degrees
    public FieldStyle? Style { get; set; }
}

/// <summary>A text cell – the most common rendered element.</summary>
public sealed class RenderedTextElement : RenderedElement
{
    public string             Text          { get; set; } = string.Empty;
    public FieldAlignment     Alignment     { get; set; } = FieldAlignment.Left;
    public FieldVerticalAlign VerticalAlign { get; set; } = FieldVerticalAlign.Top;
    public string?            HyperlinkUrl  { get; set; }
}

/// <summary>A solid or transparent filled rectangle (used for section backgrounds, borders).</summary>
public sealed class RenderedRectElement : RenderedElement
{
    public string? FillColor   { get; set; }
    public string? StrokeColor { get; set; }
    public float   StrokeWidth { get; set; } = 0.5f;
    public float   BorderRadius { get; set; } = 0f;  // percentage (0=sharp, 50=pill)
}

/// <summary>A rendered line segment.</summary>
public sealed class RenderedLineElement : RenderedElement
{
    public float X2          { get; set; }
    public float Y2          { get; set; }
    public string StrokeColor { get; set; } = "#000000";
    public float  StrokeWidth { get; set; } = 0.5f;
}

/// <summary>A rendered ellipse / circle.</summary>
public sealed class RenderedEllipseElement : RenderedElement
{
    public string? FillColor   { get; set; }
    public string? StrokeColor { get; set; }
    public float   StrokeWidth { get; set; } = 0.5f;
}

/// <summary>A rendered image.</summary>
public sealed class RenderedImageElement : RenderedElement
{
    public string Src     { get; set; } = string.Empty;
    public string Stretch { get; set; } = "contain";  // contain | cover | fill | none
}

// ─────────────────────────────────────────────────────────────────────────────
// Page
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A single rendered page containing a flat list of <see cref="RenderedElement"/> objects.
/// </summary>
public sealed class RenderedPage
{
    public int PageNumber { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

    /// <summary>All elements on this page, in paint order (back to front).</summary>
    public List<RenderedElement> Elements { get; } = [];

    public void Add(RenderedElement element) => Elements.Add(element);
}

// ─────────────────────────────────────────────────────────────────────────────
// Document
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// The fully rendered report document – a list of pages ready for export or display.
/// </summary>
public sealed class ReportDocument
{
    /// <summary>Original report definition that produced this document.</summary>
    public ReportDefinition? SourceDefinition { get; set; }

    /// <summary>All pages in render order.</summary>
    public List<RenderedPage> Pages { get; } = [];

    /// <summary>Total number of pages.</summary>
    public int PageCount => Pages.Count;

    /// <summary>Metadata generated at render time.</summary>
    public string RenderedAt { get; set; } = DateTime.UtcNow.ToString("O");

    /// <summary>Total render time (informational).</summary>
    public TimeSpan RenderDuration { get; set; }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public RenderedPage AddPage(float width, float height)
    {
        var page = new RenderedPage
        {
            PageNumber = Pages.Count + 1,
            Width = width,
            Height = height,
        };
        Pages.Add(page);
        return page;
    }
}