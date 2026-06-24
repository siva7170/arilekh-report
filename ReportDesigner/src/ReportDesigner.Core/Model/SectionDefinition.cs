using System;
using System.Collections.Generic;
using System.Text;

using System.Xml.Serialization;

namespace ReportDesigner.Core.Model;

/// <summary>
/// Controls when a section band repeats across pages.
/// </summary>
public enum RepeatMode
{
    /// <summary>Default: section renders exactly where it falls in the flow.</summary>
    Normal,

    /// <summary>Repeat the band on every page (PageHeader / PageFooter behaviour).</summary>
    EveryPage,

    /// <summary>Print once at the beginning/end of the report only.</summary>
    Once,
}

/// <summary>
/// A horizontal band in the report layout.
/// Multiple <see cref="FieldElement"/> items are placed within the band.
/// </summary>
public class SectionDefinition
{
    // ── Identity ──────────────────────────────────────────────────────────────

    [XmlAttribute("name")] public string Name { get; set; } = string.Empty;
    [XmlAttribute("type")] public SectionType Type { get; set; }

    // ── Data binding ──────────────────────────────────────────────────────────

    /// <summary>
    /// For <see cref="SectionType.Detail"/> and group headers/footers, the name of
    /// the <see cref="DataSourceDefinition"/> that drives the repeating rows.
    /// </summary>
    [XmlAttribute("dataSource")] public string? DataSourceName { get; set; }

    /// <summary>
    /// For <see cref="SectionType.GroupHeader"/> / <see cref="SectionType.GroupFooter"/>,
    /// the field name (in the bound DataSource) whose value change starts a new group.
    /// </summary>
    [XmlAttribute("groupField")] public string? GroupField { get; set; }

    // ── Layout ────────────────────────────────────────────────────────────────

    /// <summary>Fixed band height in points.  When fields have CanGrow=true the
    /// actual rendered height may exceed this.</summary>
    [XmlAttribute("height")] public float Height { get; set; } = 20f;

    [XmlAttribute("repeat")] public RepeatMode Repeat { get; set; } = RepeatMode.Normal;

    /// <summary>Insert a page break before this section is rendered.</summary>
    [XmlAttribute("newPageBefore")] public bool NewPageBefore { get; set; }

    /// <summary>Insert a page break after this section is rendered.</summary>
    [XmlAttribute("newPageAfter")] public bool NewPageAfter { get; set; }

    /// <summary>Keep the section together with the following section – avoids
    /// orphaned headers at the bottom of a page.</summary>
    [XmlAttribute("keepTogether")] public bool KeepTogether { get; set; }

    // ── Visibility ────────────────────────────────────────────────────────────

    /// <summary>
    /// Expression that evaluates to bool; the whole band is suppressed when true.
    /// </summary>
    [XmlAttribute("suppressExpression")] public string? SuppressExpression { get; set; }

    /// <summary>
    /// When true the band is hidden (takes no space) instead of just being invisible.
    /// </summary>
    [XmlAttribute("hideWhenEmpty")] public bool HideWhenEmpty { get; set; }

    // ── Default style ─────────────────────────────────────────────────────────

    /// <summary>Style applied to all fields in this section unless overridden.</summary>
    public FieldStyle? DefaultStyle { get; set; }

    // ── Background ────────────────────────────────────────────────────────────

    [XmlAttribute("backColor")] public string? BackColor { get; set; }

    /// <summary>
    /// When true, alternating rows in a Detail section use
    /// <see cref="AlternateBackColor"/>.
    /// </summary>
    [XmlAttribute("alternateRows")] public bool AlternateRows { get; set; }
    [XmlAttribute("alternateBackColor")] public string? AlternateBackColor { get; set; }

    // ── Fields ────────────────────────────────────────────────────────────────

    [XmlArray("Fields")]
    [XmlArrayItem("Field")]
    public List<FieldElement> Fields { get; set; } = [];
}