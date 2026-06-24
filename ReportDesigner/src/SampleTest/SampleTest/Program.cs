using ReportDesigner.Core;
using ReportDesigner.Core.Data;
using ReportDesigner.Core.Export;

var report = XmlReportSerializer.LoadReport("SampleReport/SampleReport.rdx");

var schema = XmlReportSerializer.LoadSchema("SampleReport/SampleReport.rds");

// 2. Provide data (any DataTable from any source)
var provider = new DataTableProvider();
provider.Register(schema.ToSampleDataTable());   // must match DataSource Name in .rdx

// 3. Render
var engine = new ReportEngine();
var doc = await engine.RenderAsync(report, provider, new Dictionary<string, object?>
{
    ["StartDate"] = new DateTime(2026, 1, 1),
    ["ReportTitle"] = "Q1 Sales Report",
});

// 4. Export to HTML
//var html = new HtmlExporter().Export(doc);
//File.WriteAllText("report.html", html);


new PdfExporter().ExportToFile(doc, "report.pdf");

Console.WriteLine($"Rendered {doc.PageCount} pages in {doc.RenderDuration.TotalMilliseconds:F0} ms");