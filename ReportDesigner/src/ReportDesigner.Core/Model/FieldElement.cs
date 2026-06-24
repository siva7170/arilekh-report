using System;
using System.Collections.Generic;
using System.Text;

using System.Xml.Serialization;

namespace ReportDesigner.Core.Model;

// ─────────────────────────────────────────────────────────────────────────────
// Enumerations
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Horizontal alignment for a field's rendered value.</summary>
public enum FieldAlignment { Left, Center, Right }

/// <summary>
/// Built-in format hints that the renderer can apply without a custom format string.
/// Use <see cref="FieldFormat.Custom"/> together with <see cref="FieldElement.FormatString"/>
/// for full control.
/// </summary>
public enum FieldFormat
{
    None,
    Currency,
    Percent,
    Integer,
    Decimal2,
    Date,          // "d"  – short date
    DateTime,      // "g"  – short date + time
    LongDate,      // "D"
    Time,          // "t"
    Custom,        // use FormatString
}

/// <summary>Vertical alignment within a field box.</summary>
public enum FieldVerticalAlign { Top, Middle, Bottom }

/// <summary>How the image element gets its source data at render time.</summary>
public enum ImageSourceMode
{
    /// <summary>Static: image is embedded as a data URI directly in ImageSrc.</summary>
    Static,
    /// <summary>Expression: evaluate an expression that returns a data URI, base64 string, or URL.</summary>
    Expression,
    /// <summary>Parameter: read image bytes / data URI from a named scalar parameter.</summary>
    Parameter,
    /// <summary>DataField: read image from a DataTable column (byte[] or base64 string).</summary>
    DataField,
}

/// <summary>Supported border sides for a field box.</summary>
[Flags]
public enum BorderSides { None = 0, Top = 1, Right = 2, Bottom = 4, Left = 8, All = 15 }

/// <summary>The kind of element placed on the canvas.</summary>
public enum ElementKind
{
    Field,         // default – text / expression / static label
    Line,          // horizontal or vertical line
    Box,           // rectangle / square
    Circle,        // ellipse / circle
    Image,         // embedded or URL image
    Chart,         // embedded chart (pie / bar / line)
    CustomFormula, // user-defined calculated field
}

/// <summary>Chart type for a Chart element.</summary>
public enum ChartType { Pie, Bar, BarHorizontal, Line }

/// <summary>One data series inside a chart.</summary>
public class ChartSeries
{
    [XmlAttribute("label")] public string Label { get; set; } = string.Empty;
    [XmlAttribute("field")] public string FieldName { get; set; } = string.Empty;  // data field to aggregate
    [XmlAttribute("color")] public string Color { get; set; } = "#4472C4";
    [XmlAttribute("agg")] public string Aggregate { get; set; } = "SUM";  // SUM | COUNT | AVG | MIN | MAX
}

/// <summary>Full chart configuration attached to a Chart-kind FieldElement.</summary>
public class ChartDefinition
{
    [XmlAttribute("chartType")] public ChartType Type { get; set; } = ChartType.Bar;
    [XmlAttribute("dataSource")] public string? DataSourceName { get; set; }
    [XmlAttribute("categoryField")] public string? CategoryField { get; set; }  // X-axis / pie slice field
    [XmlAttribute("title")] public string? Title { get; set; }
    [XmlAttribute("showLegend")] public bool ShowLegend { get; set; } = true;
    [XmlAttribute("showLabels")] public bool ShowLabels { get; set; } = true;
    [XmlAttribute("showBorder")] public bool ShowBorder { get; set; } = true;
    [XmlAttribute("borderColor")] public string BorderColor { get; set; } = "#CCCCCC";
    [XmlAttribute("borderWidth")] public float BorderWidth { get; set; } = 1f;
    [XmlAttribute("bgColor")] public string? BackgroundColor { get; set; }

    [XmlArray("Series")]
    [XmlArrayItem("Serie")]
    public List<ChartSeries> Series { get; set; } = [];
}

/// <summary>A custom calculated field with formula and optional running total support.</summary>
public class CustomFieldDefinition
{
    /// <summary>Formula expression — supports +, -, *, /, (, ), field refs, and RunningTotal(fieldName).</summary>
    [XmlAttribute("formula")] public string Formula { get; set; } = string.Empty;

    /// <summary>When true the result is accumulated as a running total across rows.</summary>
    [XmlAttribute("isRunningTotal")] public bool IsRunningTotal { get; set; }

    /// <summary>Reset running total at each group change (uses group field name).</summary>
    [XmlAttribute("resetOnGroup")] public string? ResetOnGroup { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Font / Style
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Defines the visual appearance of a <see cref="FieldElement"/>.
/// All properties are optional; the renderer falls back to the section default, then report default.
/// </summary>
public class FieldStyle
{
    [XmlAttribute] public string? FontFamily { get; set; }   // e.g. "Arial"

    //[XmlAttribute] public float? FontSize { get; set; }   // pt

    [XmlIgnore]
    public float? FontSize { get; set; }

    [XmlAttribute("FontSize")]
    public float FontSizeValue
    {
        get => FontSize ?? default;
        set => FontSize = value;
    }

    [XmlAttribute] public bool Bold { get; set; }
    [XmlAttribute] public bool Italic { get; set; }
    [XmlAttribute] public bool Underline { get; set; }
    [XmlAttribute] public string? ForeColor { get; set; }   // hex "#RRGGBB" or named
    [XmlAttribute] public string? BackColor { get; set; }
    [XmlAttribute] public BorderSides Border { get; set; } = BorderSides.None;
    [XmlAttribute] public string? BorderColor { get; set; }
    [XmlAttribute] public float BorderWidth { get; set; } = 0.5f;
    [XmlAttribute] public float PaddingLeft { get; set; } = 2f;
    [XmlAttribute] public float PaddingRight { get; set; } = 2f;
}

// ─────────────────────────────────────────────────────────────────────────────
// Field element  (a single placeable item inside a section band)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A single renderable element placed inside a <see cref="SectionDefinition"/> band.
/// Can be a bound data field, a computed expression, a static label, a line, or an image.
/// </summary>
public class FieldElement
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Designer-assigned unique name within the report.</summary>
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    // ── Position &amp; size  (points, 1 pt = 1/72 inch) ─────────────────────────

    [XmlAttribute("left")] public float X { get; set; }
    [XmlAttribute("top")] public float Y { get; set; }
    [XmlAttribute("width")] public float Width { get; set; } = 100f;
    [XmlAttribute("height")] public float Height { get; set; } = 14f;

    // ── Content ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Expression that produces the field value.
    /// Examples:
    ///   "Fields.CustomerName"
    ///   "Sum(Fields.Amount)"
    ///   "PageNumber()"
    ///   "'Hello, ' + Fields.FirstName"
    ///   "Iif(Fields.Amount &gt; 0, 'Positive', 'Zero')"
    /// </summary>
    [XmlAttribute("expression")]
    public string? Expression { get; set; }

    /// <summary>
    /// Static label text (used when <see cref="Expression"/> is null/empty).
    /// </summary>
    [XmlAttribute("text")]
    public string? Text { get; set; }

    // ── Formatting ────────────────────────────────────────────────────────────

    [XmlAttribute] public FieldFormat Format { get; set; } = FieldFormat.None;
    [XmlAttribute] public string? FormatString { get; set; }   // e.g. "dd MMM yyyy", "N2"
    [XmlAttribute] public FieldAlignment Alignment { get; set; } = FieldAlignment.Left;

    // ── Appearance ────────────────────────────────────────────────────────────

    public FieldStyle? Style { get; set; }

    // ── Visibility ────────────────────────────────────────────────────────────

    /// <summary>
    /// Optional expression that evaluates to bool; field is suppressed when true.
    /// Example: "Fields.Qty == 0"
    /// </summary>
    [XmlAttribute("suppressExpression")]
    public string? SuppressExpression { get; set; }

    // ── Grow / shrink ─────────────────────────────────────────────────────────

    /// <summary>
    /// When true the field height expands to fit multi-line content
    /// (also grows the containing section band).
    /// </summary>
    [XmlAttribute("canGrow")]
    public bool CanGrow { get; set; } = false;

    // ── Hyperlink ─────────────────────────────────────────────────────────────

    /// <summary>Optional hyperlink URL – may contain field references.</summary>
    [XmlAttribute]
    public string? HyperlinkExpression { get; set; }

    // ── Element kind & shape properties ───────────────────────────────────────

    /// <summary>Determines how this element is rendered on the canvas.</summary>
    [XmlAttribute("kind")]
    public ElementKind Kind { get; set; } = ElementKind.Field;

    /// <summary>Stroke color for Line, Box, Circle (hex or named).</summary>
    [XmlAttribute("strokeColor")]
    public string? StrokeColor { get; set; }

    /// <summary>Stroke thickness in points for shapes.</summary>
    [XmlAttribute("strokeWidth")]
    public float StrokeWidth { get; set; } = 1f;

    /// <summary>Fill color for Box and Circle. Null = transparent.</summary>
    [XmlAttribute("fillColor")]
    public string? FillColor { get; set; }

    /// <summary>Border radius % for Box (0 = sharp corners, 50 = pill).</summary>
    [XmlAttribute("borderRadius")]
    public float BorderRadius { get; set; } = 0f;

    /// <summary>Image source URL or base64 data URI for Image elements.</summary>
    [XmlAttribute("imageSrc")]
    public string? ImageSrc { get; set; }

    /// <summary>How to size the image inside its box.</summary>
    [XmlAttribute("imageStretch")]
    public string ImageStretch { get; set; } = "contain";

    /// <summary>How the image source is resolved at render time.</summary>
    [XmlAttribute("imageSourceMode")]
    public ImageSourceMode ImageSourceMode { get; set; } = ImageSourceMode.Static;

    /// <summary>
    /// Expression / field name / parameter name used to resolve the image at runtime.
    /// - ImageSourceMode.Expression: full expression e.g. Fields.Logo
    /// - ImageSourceMode.Parameter:  parameter name e.g. CompanyLogo
    /// - ImageSourceMode.DataField:  column in section DataTable e.g. ProductImage
    /// Value is a base64 string, data URI, or URL — resolved to a data URI at render time.
    /// </summary>
    [XmlAttribute("imageExpression")]
    public string? ImageExpression { get; set; }

    // ── Transform ────────────────────────────────────────────────────────────

    /// <summary>Rotation angle in degrees (0–360). Applies to all element kinds.</summary>
    [XmlAttribute("rotation")]
    public float Rotation { get; set; } = 0f;

    /// <summary>Optional group/folder name for the Canvas Explorer (design-time only).</summary>
    [XmlAttribute("groupName")]
    public string? GroupName { get; set; }

    // ── Vertical alignment ───────────────────────────────────────────────────

    /// <summary>Vertical alignment of text/content within the field box.</summary>
    [XmlAttribute("verticalAlign")]
    public FieldVerticalAlign VerticalAlign { get; set; } = FieldVerticalAlign.Top;  // contain | cover | fill | none

    // ── Chart element ─────────────────────────────────────────────────────────

    /// <summary>Chart configuration – populated when Kind == Chart.</summary>
    public ChartDefinition? Chart { get; set; }

    // ── Custom formula field ──────────────────────────────────────────────────

    /// <summary>Custom formula config – populated when Kind == CustomFormula.</summary>
    public CustomFieldDefinition? CustomFormula { get; set; }
}