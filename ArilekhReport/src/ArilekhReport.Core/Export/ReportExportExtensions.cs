using ArilekhReport.Core;
using ArilekhReport.Core.Data;
using ArilekhReport.Core.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace ArilekhReport.Core.Export
{
    public static class ReportExportExtensions
    {
        // ── PDF ───────────────────────────────────────────────────────────

        /// <summary>Render and export to PDF bytes.</summary>
        public static async Task<byte[]> ExportToPdfAsync(
            this ReportEngine engine,
            ReportDefinition report,
            IDataSourceProvider provider,
            PdfExportOptions? options = null,
            IReadOnlyDictionary<string, object?>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var doc = await engine.RenderAsync(report, provider, parameters, cancellationToken);
            var exporter = new PdfExporter(options);
            return exporter.Export(doc);
        }

        /// <summary>Render and export to a PDF file.</summary>
        public static async Task ExportToPdfFileAsync(
            this ReportEngine engine,
            ReportDefinition report,
            IDataSourceProvider provider,
            string outputPath,
            PdfExportOptions? options = null,
            IReadOnlyDictionary<string, object?>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var doc = await engine.RenderAsync(report, provider, parameters, cancellationToken);
            var exporter = new PdfExporter(options);
            exporter.ExportToFile(doc, outputPath);
        }

        // ── Excel ─────────────────────────────────────────────────────────

        /// <summary>Render and export to Excel bytes.</summary>
        public static async Task<byte[]> ExportToExcelAsync(
            this ReportEngine engine,
            ReportDefinition report,
            IDataSourceProvider provider,
            ExcelExportOptions? options = null,
            IReadOnlyDictionary<string, object?>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var doc = await engine.RenderAsync(report, provider, parameters, cancellationToken);
            var exporter = new ExcelExporter(options);
            return exporter.Export(doc);
        }

        /// <summary>Render and export to an Excel file.</summary>
        public static async Task ExportToExcelFileAsync(
            this ReportEngine engine,
            ReportDefinition report,
            IDataSourceProvider provider,
            string outputPath,
            ExcelExportOptions? options = null,
            IReadOnlyDictionary<string, object?>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var doc = await engine.RenderAsync(report, provider, parameters, cancellationToken);
            var exporter = new ExcelExporter(options);
            exporter.ExportToFile(doc, outputPath);
        }

        // ── HTML (already exists, convenience overload) ───────────────────

        /// <summary>Render and export to a self-contained HTML string.</summary>
        public static async Task<string> ExportToHtmlAsync(
            this ReportEngine engine,
            ReportDefinition report,
            IDataSourceProvider provider,
            HtmlExportOptions? options = null,
            IReadOnlyDictionary<string, object?>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var doc = await engine.RenderAsync(report, provider, parameters, cancellationToken);
            var exporter = new HtmlExporter(options);
            return exporter.Export(doc);
        }

        // ── Generic exporter ──────────────────────────────────────────────

        /// <summary>Render and export using any <see cref="IReportExporter"/>.</summary>
        public static async Task<byte[]> ExportAsync(
            this ReportEngine engine,
            ReportDefinition report,
            IDataSourceProvider provider,
            IReportExporter exporter,
            IReadOnlyDictionary<string, object?>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var doc = await engine.RenderAsync(report, provider, parameters, cancellationToken);
            return exporter.Export(doc);
        }
    }

}
