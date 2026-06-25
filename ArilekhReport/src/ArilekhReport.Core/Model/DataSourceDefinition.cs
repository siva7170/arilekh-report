using System;
using System.Collections.Generic;
using System.Text;

using System.Xml.Serialization;

namespace ArilekhReport.Core.Model;

// ─────────────────────────────────────────────────────────────────────────────
// Field / column descriptor
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>CLR-friendly type enum used in dataset field declarations.</summary>
public enum FieldDataType
{
    String, Int32, Int64, Decimal, Double, Float,
    Boolean, DateTime, Guid, Byte,
}

/// <summary>
/// Describes a single column in a dataset schema.
/// </summary>
//public class DataFieldDescriptor
//{
//    [XmlAttribute] public string Name { get; set; } = string.Empty;
//    [XmlAttribute] public FieldDataType DataType { get; set; } = FieldDataType.String;
//    [XmlAttribute] public bool Nullable { get; set; } = true;
//    [XmlAttribute] public string? Caption { get; set; }  // display label in designer
//}

public class DataFieldDescriptor
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("type")]
    public FieldDataType DataType { get; set; } = FieldDataType.String;

    [XmlAttribute("nullable")]
    public bool Nullable { get; set; } = true;

    [XmlAttribute("caption")]
    public string? Caption { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Sample data row (design-time preview)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A single sample data row stored in the .rds file for design-time preview.
/// Values are stored as raw XML attributes keyed by field name.
/// </summary>
public class SampleDataRow
{
    /// <summary>Dictionary of field name → string value pairs.</summary>
    [XmlAnyAttribute]
    public System.Xml.XmlAttribute[]? Values { get; set; }

    /// <summary>Returns the string value for the given field name, or null.</summary>
    public string? GetValue(string fieldName)
    {
        return Values?.FirstOrDefault(a => a.Name == fieldName)?.Value;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Dataset schema  (.rds file)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Schema definition for a single DataTable / dataset, persisted as a .rds XML file.
/// This is the design-time contract between data and the report definition.
/// </summary>
[XmlRoot("DataSet", Namespace = "urn:reportdesigner")]
public class DataSetSchema
{
    [XmlAttribute("name")] 
    public string Name { get; set; } = string.Empty;

    [XmlArray("Fields")]
    [XmlArrayItem("Field")]
    public List<DataFieldDescriptor> Fields { get; set; } = [];

    /// <summary>Sample rows for design-time preview (not used at runtime).</summary>
    [XmlArray("SampleData")]
    [XmlArrayItem("Row")]
    public List<SampleDataRow> SampleData { get; set; } = [];

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Returns the field descriptor for the given name, or null.</summary>
    public DataFieldDescriptor? GetField(string name) =>
        Fields.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Builds a <see cref="System.Data.DataTable"/> pre-populated with the sample data rows.
    /// Useful for design-time preview without a live data source.
    /// </summary>
    public System.Data.DataTable ToSampleDataTable()
    {
        var table = new System.Data.DataTable(Name);

        foreach (var field in Fields)
        {
            table.Columns.Add(field.Name, MapClrType(field.DataType));
        }

        if (SampleData is not null)
        {
            foreach (var sample in SampleData)
            {
                var row = table.NewRow();
                foreach (var field in Fields)
                {
                    var raw = sample.GetValue(field.Name);
                    row[field.Name] = raw is null
                        ? DBNull.Value
                        : ConvertValue(raw, field.DataType);
                }
                table.Rows.Add(row);
            }
        }

        return table;
    }

    private static Type MapClrType(FieldDataType dt) => dt switch
    {
        FieldDataType.Int32 => typeof(int),
        FieldDataType.Int64 => typeof(long),
        FieldDataType.Decimal => typeof(decimal),
        FieldDataType.Double => typeof(double),
        FieldDataType.Float => typeof(float),
        FieldDataType.Boolean => typeof(bool),
        FieldDataType.DateTime => typeof(DateTime),
        FieldDataType.Guid => typeof(Guid),
        FieldDataType.Byte => typeof(byte),
        _ => typeof(string),
    };

    private static object ConvertValue(string raw, FieldDataType dt)
    {
        try
        {
            return dt switch
            {
                FieldDataType.Int32 => int.Parse(raw),
                FieldDataType.Int64 => long.Parse(raw),
                FieldDataType.Decimal => decimal.Parse(raw),
                FieldDataType.Double => double.Parse(raw),
                FieldDataType.Float => float.Parse(raw),
                FieldDataType.Boolean => bool.Parse(raw),
                FieldDataType.DateTime => DateTime.Parse(raw),
                FieldDataType.Guid => Guid.Parse(raw),
                FieldDataType.Byte => byte.Parse(raw),
                _ => raw,
            };
        }
        catch
        {
            return DBNull.Value;
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DataSource reference inside a ReportDefinition
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Describes how a data source provides its data at runtime.</summary>
public enum DataSourceKind
{
    /// <summary>Returns a flat DataTable (default — most sections use this).</summary>
    DataTable,
    /// <summary>Returns a DataSet; the report uses the first matching table by name.</summary>
    DataSet,
    /// <summary>A single scalar value (int, string, decimal, etc.) — no rows.</summary>
    ScalarField,
}

/// <summary>
/// A named data-source reference inside a <see cref="ReportDefinition"/>.
/// At runtime the caller provides an <see cref="Data.IDataSourceProvider"/>
/// that resolves the name to a live <see cref="System.Data.DataTable"/>.
/// </summary>
public class DataSourceDefinition
{
    /// <summary>Logical name used by sections/fields to reference this source.</summary>
    [XmlAttribute] public string Name { get; set; } = string.Empty;

    /// <summary>How this data source delivers its data.</summary>
    [XmlAttribute] public DataSourceKind Kind { get; set; } = DataSourceKind.DataTable;

    /// <summary>
    /// For <see cref="DataSourceKind.ScalarField"/> — the CLR type of the value.
    /// </summary>
    [XmlAttribute] public FieldDataType ScalarType { get; set; } = FieldDataType.String;

    /// <summary>
    /// Path to the companion .rds schema file (relative to the .rdx file).
    /// Used at design-time for field palette and IntelliSense.
    /// </summary>
    [XmlAttribute] public string? SchemaRef { get; set; }

    /// <summary>Optional runtime query / stored procedure / REST endpoint hint.
    /// Interpreted by the concrete <see cref="Data.IDataSourceProvider"/>.</summary>
    [XmlAttribute] public string? Query { get; set; }

    /// <summary>Field definitions for DataTable/DataSet sources (schema).</summary>
    [XmlArray("Fields")]
    [XmlArrayItem("Field")]
    public List<DataFieldDescriptor> Fields { get; set; } = [];
}