using ArilekhReport.Core;
using ArilekhReport.Core.Data;
using ArilekhReport.Core.Export;
using ArilekhReport.Core.Model;

namespace ArilekhReport.Designer.Services;

/// <summary>
/// Renders the current report definition using sample data from the loaded schema,
/// producing an HTML string for the preview panel.
/// </summary>
public sealed class PreviewService
{
    private readonly DesignerStateService _state;

    public PreviewService(DesignerStateService state) => _state = state;

    // ── HTML preview ──────────────────────────────────────────────────

    public async Task<string> RenderPreviewAsync()
    {
        try
        {
            var doc = await RenderDocumentAsync();
            return new HtmlExporter().Export(doc);
        }
        catch (Exception ex)
        {
            return $"<html><body style='font-family:monospace;padding:20px;color:red'>" +
                   $"<h3>Preview Error</h3><pre>{System.Net.WebUtility.HtmlEncode(ex.Message)}</pre>" +
                   $"</body></html>";
        }
    }

    // ── Full document render (shared by preview, PDF, Excel, viewer) ──

    public async Task<Core.Rendering.ReportDocument> RenderDocumentAsync()
    {
        var engine   = new ReportEngine();
        var provider = BuildProvider();
        return await engine.RenderAsync(_state.Report, provider);
    }

    // ── PDF export ────────────────────────────────────────────────────

    public async Task<byte[]> ExportPdfAsync(PdfExportOptions? options = null)
    {
        var doc = await RenderDocumentAsync();
        return new PdfExporter(options).Export(doc);
    }

    // ── Excel export ──────────────────────────────────────────────────

    public async Task<byte[]> ExportExcelAsync(ExcelExportOptions? options = null)
    {
        var doc = await RenderDocumentAsync();
        return new ExcelExporter(options).Export(doc);
    }

    // ── Provider builder ──────────────────────────────────────────────

    private IDataSourceProvider BuildProvider()
    {
        if (_state.Schema is not null)
        {
            var sp = new SampleDataProvider();
            sp.Register(_state.Schema);
            return sp;
        }

        // No schema – empty tables per declared data source
        var tp = new DataTableProvider();
        foreach (var ds in _state.Report.DataSources)
            tp.Register(new System.Data.DataTable(ds.Name));
        return tp;
    }
}
