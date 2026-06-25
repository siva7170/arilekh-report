using System;
using System.Collections.Generic;
using System.Text;
using ArilekhReport.Core.Data;
using ArilekhReport.Core.Layout;
using ArilekhReport.Core.Model;
using ArilekhReport.Core.Rendering;

namespace ArilekhReport.Core;

/// <summary>
/// The main public entry point for rendering reports.
///
/// <example>
/// <code>
/// // 1. Load the report definition
/// var report = XmlReportSerializer.LoadReport("SalesReport.rdx");
///
/// // 2. Provide data
/// var provider = new DataTableProvider();
/// provider.Register("Orders", GetOrdersFromDb());
///
/// // 3. Render
/// var engine = new ReportEngine();
/// var doc    = await engine.RenderAsync(report, provider);
///
/// // 4. Export
/// var html = new HtmlExporter().Export(doc);
/// File.WriteAllText("report.html", html);
/// </code>
/// </example>
/// </summary>
public sealed class ReportEngine
{
    private readonly LayoutEngine _layout = new();

    /// <summary>
    /// Renders the report and returns a <see cref="ReportDocument"/> ready for export.
    /// </summary>
    public Task<ReportDocument> RenderAsync(
        ReportDefinition report,
        IDataSourceProvider dataProvider,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
        => _layout.RenderAsync(report, dataProvider, parameters, cancellationToken);

    /// <summary>
    /// Convenience overload for single-DataTable reports.
    /// The table is registered under its own <see cref="System.Data.DataTable.TableName"/>.
    /// </summary>
    public Task<ReportDocument> RenderAsync(
        ReportDefinition report,
        System.Data.DataTable data,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var provider = new DataTableProvider();
        provider.Register(data);
        return _layout.RenderAsync(report, provider, parameters, cancellationToken);
    }
}