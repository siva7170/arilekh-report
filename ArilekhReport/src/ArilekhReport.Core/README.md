# Arilekh Report

ArilekhReport.Core is the core library of the Arilekh Reporting Platform. It provides report definitions, data binding, report processing, rendering infrastructure, and extensible APIs for building enterprise-grade reporting solutions in .NET applications.

## Install Package

Through .NET CLI

```
dotnet add package ArilekhReport.Core --version 1.0.1
```

PMC

```
NuGet\Install-Package ArilekhReport.Core -Version 1.0.1
```


## Usage

```csharp
using ArilekhReport.Core;
using ArilekhReport.Core.Data;
using ArilekhReport.Core.Export;

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
```

## Methods

Will be updated soon

## How can we use it?

We can use arilekh report to generate report and save into locally or we can render it through the `arilekh-report-viewer` angular library (Currently, we have viewer for angular only).

To integrate Arilekh Report in Angular and .NET web api, please refer `demo-app` in `arilekh-report-viewer` and `ArilekhReport.WebApi`.