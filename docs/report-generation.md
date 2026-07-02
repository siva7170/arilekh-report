# Report Generation

Once you have designed your report using the Report Designer, you need to have a data source to generate the report. The data source can be a database, an API, or any other structured data format that your application supports.

As we work with .NET, we transform the data source into a DataTable, which is a tabular representation of the data. The DataTable can be populated with data from various sources, such as databases, CSV files, or in-memory collections.

As we discussed in the previous section, you can have multiple data sources in a report. Each data source can be represented as a DataTable, and you can define relationships between them if needed. And, the DataTable.TableName should match the DataSource name defined in the Report Designer. This allows the report to correctly bind the data from the DataTable to the corresponding fields in the report.

If you want to bind the ScalarField in the report, you can use the DataTable to bind the scalar field. The ScalarField is a single value that can be used in the report, such as a total count, sum, or any other calculated value. Otherwise, you may use Parameters to bind the scalar field in the report. The Parameters can be used to pass values to the report at runtime, allowing you to customize the report output based on user input or other dynamic data.

## Prequisites for Report Generation

So, to generate the report, you should have,

1. A report designed in the Report Designer with defined data sources and fields.
2. A DataTable populated with data from your chosen data source(s), with the TableName matching the DataSource name defined in the Report Designer.
3. To use ScalarField in the report, you can either bind it using the DataTable.
4. Optionally, Parameters defined in the report to pass dynamic values at runtime.


## Preparing the DataTable

```csharp
var dataTable = new DataTable();
dataTable.TableName = "Orders"; // This should match the DataSource name defined in the Report Designer
dataTable.Columns.Add("OrderId", typeof(int));
dataTable.Columns.Add("CustomerName", typeof(string));
dataTable.Columns.Add("Amount", typeof(decimal));
dataTable.Columns.Add("OrderDate", typeof(DateTime));

dataTable.Rows.Add(1, "John Doe", 100.0m, new DateTime(2023, 1, 15));
dataTable.Rows.Add(2, "Jane Smith", 200.0m, new DateTime(2023, 2, 20));
dataTable.Rows.Add(3, "Alice Johnson", 300.0m, new DateTime(2023, 3, 10));
```

## Generating the Report

To generate the report, you should load the report. And, we can use the DataTableProvider to register the DataTable with the report. Finally, we can export the report to PDF using the PdfExporter.

In the below, example, we use the sample data defined in the previous section to generate the report. You can replace the sample data with your own data source as needed.

```csharp
using ArilekhReport.Core;
using ArilekhReport.Core.Data;
using ArilekhReport.Core.Export;

// 1. Load report and schema
var report = XmlReportSerializer.LoadReport("SampleReport/SampleReport.rdx");

// 2. Prepare the DataTable with sample data
var dataTable = new DataTable();
dataTable.TableName = "Orders"; // This should match the DataSource name defined in the Report Designer
dataTable.Columns.Add("OrderId", typeof(int));
dataTable.Columns.Add("CustomerName", typeof(string));
dataTable.Columns.Add("Amount", typeof(decimal));
dataTable.Columns.Add("OrderDate", typeof(DateTime));

dataTable.Rows.Add(1, "John Doe", 100.0m, new DateTime(2023, 1, 15));
dataTable.Rows.Add(2, "Jane Smith", 200.0m, new DateTime(2023, 2, 20));
dataTable.Rows.Add(3, "Alice Johnson", 300.0m, new DateTime(2023, 3, 10));

// 3. Provide data (any DataTable from any source. )
var provider = new DataTableProvider();
provider.Register(dataTable);   // must match DataSource Name in .rdx

// 3. Export report to PDF
new PdfExporter().ExportToFile(doc, "report.pdf");

Console.WriteLine($"Rendered {doc.PageCount} pages in {doc.RenderDuration.TotalMilliseconds:F0} ms");
```

## Generating the Report in Excel, Html

To generate the report in Excel or HTML format, you can use the `ExcelExporter` or `HtmlExporter` classes provided by the ArilekhReport library. The process is similar to generating a PDF report, but you will use the respective exporter class for the desired format.

For example, to export the report to Excel, you can use the following code snippet:
```csharp
using ArilekhReport.Core;
using ArilekhReport.Core.Data;
using ArilekhReport.Core.Export;

// 1. Load report and schema
var report = XmlReportSerializer.LoadReport("SampleReport/SampleReport.rdx");

// 2. Prepare the DataTable with sample data
var dataTable = new DataTable();
dataTable.TableName = "Orders"; // This should match the DataSource name defined in the Report Designer
dataTable.Columns.Add("OrderId", typeof(int));
dataTable.Columns.Add("CustomerName", typeof(string));
dataTable.Columns.Add("Amount", typeof(decimal));
dataTable.Columns.Add("OrderDate", typeof(DateTime));

dataTable.Rows.Add(1, "John Doe", 100.0m, new DateTime(2023, 1, 15));
dataTable.Rows.Add(2, "Jane Smith", 200.0m, new DateTime(2023, 2, 20));
dataTable.Rows.Add(3, "Alice Johnson", 300.0m, new DateTime(2023, 3, 10));

// 3. Provide data (any DataTable from any source. )
var provider = new DataTableProvider();
provider.Register(dataTable);   // must match DataSource Name in .rdx

// 3. Export report to Excel
new ExcelExporter().ExportToFile(doc, "report.xlsx");

Console.WriteLine($"Rendered {doc.PageCount} pages in {doc.RenderDuration.TotalMilliseconds:F0} ms");
```

To generate the report in HTML format, you can use the following code snippet:
```csharp
using ArilekhReport.Core;
using ArilekhReport.Core.Data;
using ArilekhReport.Core.Export;

// 1. Load report and schema
var report = XmlReportSerializer.LoadReport("SampleReport/SampleReport.rdx");

// 2. Prepare the DataTable with sample data
var dataTable = new DataTable();
dataTable.TableName = "Orders"; // This should match the DataSource name defined in the Report Designer
dataTable.Columns.Add("OrderId", typeof(int));
dataTable.Columns.Add("CustomerName", typeof(string));
dataTable.Columns.Add("Amount", typeof(decimal));
dataTable.Columns.Add("OrderDate", typeof(DateTime));

dataTable.Rows.Add(1, "John Doe", 100.0m, new DateTime(2023, 1, 15));
dataTable.Rows.Add(2, "Jane Smith", 200.0m, new DateTime(2023, 2, 20));
dataTable.Rows.Add(3, "Alice Johnson", 300.0m, new DateTime(2023, 3, 10));

// 3. Provide data (any DataTable from any source. )
var provider = new DataTableProvider();
provider.Register(dataTable);   // must match DataSource Name in .rdx

// 3. Export report to Excel
var html = new HtmlExporter().Export(doc);
File.WriteAllText("report.html", html);

Console.WriteLine($"Rendered {doc.PageCount} pages in {doc.RenderDuration.TotalMilliseconds:F0} ms");
```

