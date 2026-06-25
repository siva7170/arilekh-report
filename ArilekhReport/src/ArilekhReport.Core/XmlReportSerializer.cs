using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using ArilekhReport.Core.Model;

namespace ArilekhReport.Core;

/// <summary>
/// Handles serialisation and deserialisation of <see cref="ReportDefinition"/> (.rdx)
/// and <see cref="DataSetSchema"/> (.rds) files.
/// </summary>
public static class XmlReportSerializer
{
    private static readonly XmlSerializerNamespaces _ns = BuildNamespaces();
    private static readonly XmlWriterSettings _writerSettings = new()
    {
        Indent = true,
        IndentChars = "  ",
        NewLineOnAttributes = false,
        Encoding = System.Text.Encoding.UTF8,
    };

    private static XmlSerializerNamespaces BuildNamespaces()
    {
        var ns = new XmlSerializerNamespaces();
        ns.Add("", "urn:reportdesigner");
        return ns;
    }

    // ── ReportDefinition (.rdx) ───────────────────────────────────────────────

    /// <summary>Serialises a <see cref="ReportDefinition"/> to an XML string.</summary>
    public static string SerializeReport(ReportDefinition report)
    {
        var serializer = new XmlSerializer(typeof(ReportDefinition));
        using var sw = new StringWriter();
        using var xw = XmlWriter.Create(sw, _writerSettings);
        serializer.Serialize(xw, report, _ns);
        return sw.ToString();
    }

    /// <summary>Serialises a <see cref="ReportDefinition"/> to a file at <paramref name="path"/>.</summary>
    public static void SaveReport(ReportDefinition report, string path)
    {
        var serializer = new XmlSerializer(typeof(ReportDefinition));
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var xw = XmlWriter.Create(fs, _writerSettings);
        serializer.Serialize(xw, report, _ns);
    }

    /// <summary>Deserialises a <see cref="ReportDefinition"/> from an XML string.</summary>
    public static ReportDefinition DeserializeReport(string xml)
    {
        var serializer = new XmlSerializer(typeof(ReportDefinition));
        using var sr = new StringReader(xml);
        return (ReportDefinition)serializer.Deserialize(sr)!;
    }

    /// <summary>Loads a <see cref="ReportDefinition"/> from a .rdx file.</summary>
    public static ReportDefinition LoadReport(string path)
    {
        var serializer = new XmlSerializer(typeof(ReportDefinition));
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        // Use a StreamReader that can detect byte-order-marks and provide the
        // correct encoding to the XmlReader to avoid XmlException when the
        // XML prolog encoding doesn't match the raw bytes.
        using var sr = new StreamReader(fs, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var xr = XmlReader.Create(sr);
        return (ReportDefinition)serializer.Deserialize(xr)!;
    }

    // ── DataSetSchema (.rds) ─────────────────────────────────────────────────

    /// <summary>Serialises a <see cref="DataSetSchema"/> to an XML string.</summary>
    public static string SerializeSchema(DataSetSchema schema)
    {
        var serializer = new XmlSerializer(typeof(DataSetSchema));
        using var sw = new StringWriter();
        using var xw = XmlWriter.Create(sw, _writerSettings);
        serializer.Serialize(xw, schema, _ns);
        return sw.ToString();
    }

    /// <summary>Serialises a <see cref="DataSetSchema"/> to a file.</summary>
    public static void SaveSchema(DataSetSchema schema, string path)
    {
        var serializer = new XmlSerializer(typeof(DataSetSchema));
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var xw = XmlWriter.Create(fs, _writerSettings);
        serializer.Serialize(xw, schema, _ns);
    }

    /// <summary>Deserialises a <see cref="DataSetSchema"/> from an XML string.</summary>
    public static DataSetSchema DeserializeSchema(string xml)
    {
        var serializer = new XmlSerializer(typeof(DataSetSchema));
        using var sr = new StringReader(xml);
        return (DataSetSchema)serializer.Deserialize(sr)!;
    }

    /// <summary>Loads a <see cref="DataSetSchema"/> from a .rds file.</summary>
    public static DataSetSchema LoadSchema(string path)
    {
        var serializer = new XmlSerializer(typeof(DataSetSchema));
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sr = new StreamReader(fs, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var xr = XmlReader.Create(sr);
        return (DataSetSchema)serializer.Deserialize(xr)!;
    }
}