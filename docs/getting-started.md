# Getting Started

Welcome to the Arilekh Report documentation! This guide will help you get started with using the report designer and generating reports.

## Installation

The first step is to install the ArilekhReport.Core package. You can do this using NuGet Package Manager or the .NET CLI.

### Using NuGet Package Manager

1. Open your project in Visual Studio.
2. Right-click on your project in the Solution Explorer and select "Manage NuGet Packages".
3. Search for "ArilekhReport.Core" in the Browse tab.
4. Click "Install" to add the package to your project.

### Using .NET CLI

```bash
dotnet add package ArilekhReport.Core
```

## Creating a Report

To create a report, you can use the Arilekh Report Designer. The designer allows you to visually design your reports by adding data sources, fields, and formatting options.

For detailed instructions on how to use the report designer, please refer to the [Report Designer Guide](report-designer.md).

Please use sample report to create your first report. You can find sample report in the `ArilekhReport/src/SampleTest/SampleTest/SampleReport` folder of the Arilekh Report package.

- SampleReport.rdx: This is the main report file that contains the report layout and design.

```xml
<Report xmlns="urn:reportdesigner" name="SalesReport">
  <DataSources>
    <DataSource name="Orders" schemaRef="Orders.rds" />
  </DataSources>

  <Sections>
	  <Section type="ReportHeader" height="40" printOnce="true">
		  <Field name="StartDate"/>
		  <Field name="ReportTitle"/>
	  </Section>

	  <Section type="PageHeader" height="30" repeat="EveryPage">
		  <Fields>
			  <Field name="ReportName" text="Sample Report" />
		  </Fields>
	  </Section>

    <Section type="GroupHeader" groupField="CustomerName" height="24" />

	  <Section type="Detail" height="20" runningSection="true"
			   dataSource="Orders">
		  <Fields>
			  <Field name="OrderId" left="0"/>
			  <Field name="CustomerName" left="100"/>
			  <Field name="Amount" left="200"/>
			  <Field name="OrderDate" left="300"/>
		  </Fields>
	  </Section>

    <Section type="GroupFooter" height="24">
      <Field name="TotalAmount" expression="Sum(Fields.Amount)"
             format="Currency" />
    </Section>

    <Section type="PageFooter" height="30" repeat="EveryPage">
		<Fields>
			<Field name="PageNum" expression="PAGENUMBER()" />
		</Fields>
    </Section>

    <Section type="ReportFooter" height="40" printOnce="true" />
  </Sections>
</Report>
```

## Previewing the Report

In Report Designer, you can open the `SampleReport.rdx` file to see how the report is structured and how data is bound to the report fields. You can modify the layout, add new fields, and customize the report according to your requirements. To preview the report, you should have load the sample datasource in the Report Designer.

## Exporting the Report 

Here, the DataSource `Orders` is defined. When you want to export the report to PDF, you can use the following code snippet in your application:
```csharp
using ArilekhReport.Core;
using ArilekhReport.Core.Data;
using ArilekhReport.Core.Export;

// 1. Load report and schema
var report = XmlReportSerializer.LoadReport("SampleReport/SampleReport.rdx");

var schema = XmlReportSerializer.LoadSchema("SampleReport/SampleReport.rds"); // You can find the sample datasource SampleReport.rds in ArilekhReport/src/SampleTest/SampleTest/SampleReport

// 2. Provide data (any DataTable from any source. For now, we use Sample DataSource)
var provider = new DataTableProvider();
provider.Register(schema.ToSampleDataTable());   // must match DataSource Name in .rdx

// 3. Export report to PDF
new PdfExporter().ExportToFile(doc, "report.pdf");

Console.WriteLine($"Rendered {doc.PageCount} pages in {doc.RenderDuration.TotalMilliseconds:F0} ms");
```

You can also export the report to other formats like Excel and HTML using similar methods provided by the Arilekh Report library.