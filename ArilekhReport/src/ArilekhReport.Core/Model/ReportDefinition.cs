using System;
using System.Collections.Generic;
using System.Text;

using System.Xml.Serialization;

namespace ArilekhReport.Core.Model;

/// <summary>
/// Page size options. Values correspond to standard paper sizes.
/// Use <see cref="Custom"/> with explicit <see cref="ReportPageSetup.Width"/> /
/// <see cref="ReportPageSetup.Height"/> to define any size.
/// </summary>
public enum PageSize { A4, Letter, Legal, A3, Custom }

/// <summary>Page orientation.</summary>
public enum PageOrientation { Portrait, Landscape }

/// <summary>
/// Physical page dimensions and margin settings (all values in points, 1pt = 1/72 inch).
/// </summary>
public class ReportPageSetup
{
    [XmlAttribute] public PageSize Size { get; set; } = PageSize.A4;
    [XmlAttribute] public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;

    // Custom dimensions (used when Size == Custom or to override)
    [XmlAttribute] public float Width { get; set; } = 595.28f;   // A4 width  in pt
    [XmlAttribute] public float Height { get; set; } = 841.89f;   // A4 height in pt

    // Margins
    [XmlAttribute] public float MarginTop { get; set; } = 36f;   // 0.5 inch
    [XmlAttribute] public float MarginBottom { get; set; } = 36f;
    [XmlAttribute] public float MarginLeft { get; set; } = 36f;
    [XmlAttribute] public float MarginRight { get; set; } = 36f;

    /// <summary>Printable width = page width minus horizontal margins.</summary>
    [XmlIgnore]
    public float PrintableWidth => Width - MarginLeft - MarginRight;

    /// <summary>Printable height = page height minus vertical margins.</summary>
    [XmlIgnore]
    public float PrintableHeight => Height - MarginTop - MarginBottom;
}

/// <summary>
/// Report-level default style applied to all sections unless overridden.
/// </summary>
public class ReportDefaultStyle
{
    [XmlAttribute] public string FontFamily { get; set; } = "Arial";
    [XmlAttribute] public float FontSize { get; set; } = 9f;
    [XmlAttribute] public string ForeColor { get; set; } = "#000000";
}

// ─────────────────────────────────────────────────────────────────────────────
// Root report definition  (.rdx)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// The root object persisted in a .rdx XML file.
/// Describes the complete structure of a report: data sources, sections, and fields.
/// </summary>
[XmlRoot("Report", Namespace = "urn:reportdesigner")]
public class ReportDefinition
{
    // ── Metadata ──────────────────────────────────────────────────────────────

    [XmlAttribute("name")] public string Name { get; set; } = "Untitled Report";
    [XmlAttribute("description")] public string? Description { get; set; }
    [XmlAttribute("version")] public string Version { get; set; } = "1.0";
    [XmlAttribute("author")] public string Author { get; set; } = string.Empty;
    [XmlAttribute("createdAt")] public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("O");

    // ── Page layout ───────────────────────────────────────────────────────────

    public ReportPageSetup PageSetup { get; set; } = new();
    public ReportDefaultStyle DefaultStyle { get; set; } = new();

    // ── Data sources ──────────────────────────────────────────────────────────

    [XmlArray("DataSources")]
    [XmlArrayItem("DataSource")]
    public List<DataSourceDefinition> DataSources { get; set; } = [];

    // ── Sections (bands) ─────────────────────────────────────────────────────

    [XmlArray("Sections")]
    [XmlArrayItem("Section")]
    public List<SectionDefinition> Sections { get; set; } = [];

    // ── Parameters ───────────────────────────────────────────────────────────

    [XmlArray("Parameters")]
    [XmlArrayItem("Parameter")]
    public List<ReportParameter> Parameters { get; set; } = [];

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Returns sections of the given type, in document order.</summary>
    public IEnumerable<SectionDefinition> GetSections(SectionType type) =>
        Sections.Where(s => s.Type == type);

    /// <summary>Returns the first matching DataSource definition, or null.</summary>
    public DataSourceDefinition? GetDataSource(string name) =>
        DataSources.FirstOrDefault(d =>
            d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

// ─────────────────────────────────────────────────────────────────────────────
// Report parameters
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A named report parameter that can be passed in at runtime and referenced
/// in field expressions via <c>Parameters.ParamName</c>.
/// </summary>
public class ReportParameter
{
    [XmlAttribute] public string Name { get; set; } = string.Empty;
    [XmlAttribute] public FieldDataType DataType { get; set; } = FieldDataType.String;
    [XmlAttribute] public string? DefaultValue { get; set; }
    [XmlAttribute] public string? Prompt { get; set; }  // label shown in prompt dialog
    [XmlAttribute] public bool IsRequired { get; set; } = false;
}