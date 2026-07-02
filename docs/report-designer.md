# Report Designer

To work with the Arilekh Report, you need to have a basic understanding of how to create and customize reports. The Report Designer allows you to design reports by adding various elements such as text, images, tables, and charts. You can also apply formatting options to enhance the visual appeal of your reports.

## Getting Started

First find the Report Designer in your Arilekh Repository and open it to begin creating your report. It is web based application. So, you need to run the Arilekh server and access the Report Designer through your web browser.

Once you have opened the Report Designer, you will see a blank canvas where you can start designing your report. You can add elements by dragging and dropping them onto the canvas.

## Understanding the Interface

In the Report Designer interface, you will find several sections.

- **Header/Menu bar**: This section contains options for creating a new report, saving your work, opening sample data source, undo, redo, Zoom In/Out the Canvas, Report's section Bands, Preview button  and accessing other features of the Report Designer. 
- **Left Panel**: This panel contains tabbed sections for Field Elements, Data Sources and Canvas Explorer. You can switch between these tabs to access different functionalities.
	- **Field Elements**: This tab allows you to add various elements to your report, such as data source fields, text, image, line, rectangle, circle, charts, expression fields. You can drag and drop these elements onto the canvas to customize your report layout.
	- **Data Sources**: This tab allows you to manage the data source which can be used in the report. It does not mean you create connection between the data source and the report. Instead, you can define the data source fields can be used in the report. In the data source management, you can manage DataSet, DataTable, ScalarField. You can have multiple data sources in a report. You can also define relationships between different data sources if needed. Each field in the data source can be managed like renaming, changing data type, and more.
	- **Canvas Explorer**: This tab provides a hierarchical view of the elements on the canvas, allowing you to easily navigate and manage your report design. In complex reports, you can have multiple bands and each band can have multiple elements. You can select any element from the Canvas Explorer to edit its properties. Even you can select any element from the canvas and it will be highlighted in the Canvas Explorer. You can also use the Canvas Explorer to rearrange elements by dragging and dropping them within the hierarchy.
- **Canvas**: This is the main area where you design your report. You can drag and drop elements from the Field Elements tab onto the canvas to create your report layout. You can also resize and reposition elements as needed. From the **Header/Menu bar**, you can select the report's section bands to design different sections of the report, such as the report header/footer, page header/footer, multiple group section band and detail sections. Each section can have its own layout and elements. The report header/footer section is used for displaying information that appears at the beginning or end of the report, such as the report title, company logo, or summary information. The page header/footer section is used for displaying information that appears at the top or bottom of each page, such as page numbers or column headers. The detail section is where the main content of the report is displayed, such as data from the data source. Group section band are used to organize data into logical groups, allowing you to display summary information or group-specific details. You can have multiple group sections in a report, each with its own layout and elements.
- **Properties Panel**: This panel displays the properties of the selected element on the canvas. You can modify properties such as size, position, font, color, and other formatting options. The properties panel allows you to customize the appearance and behavior of each element in your report. To change report properties like, Report page size, orientation, margins, and other settings, pleae click on canvas blank area and the properties panel will show the report properties.


## Interface Structure

- **Header/Menu bar**: 
  - **New**
  - **Save**
  - **Schema**
  - **Undo**
  - **Redo**
  - **Report Section Bands**
	- **Report Header**
	- **Page Header**
	- **Group Header**
	- **Detail**
	- **Group Footer**
	- **Page Footer**
	- **Report Footer**
  - **Zoom In/Out**
  - **Snap/Ruler**
  - **Light/Dark Mode**
  - **Design/Preview Mode**
- **Left Panel**: 
  - **Field Elements**:
	- **Data Source Fields**
	- **Charts & Formula**:
	   - **Pie Chart**
	   - **Bar Chart (Vertical)**
	   - **Bar Chart (Horizontal)**
	   - **Line Chart**
	   - **Custom Formula**
	- **Shapes**:
	   - **Line**
	   - **Box**
	   - **Circle**
	   - **Image**
	- **Special Fields**:
	   - **Page Number**
	   - **Total Pages**
	   - **Today**
	   - **Now**
	   - **Row Number**
	   - **Static Text**
	- **Functions**:
	   - **Sum**
	   - **Count**
	   - **Average**
	   - **Min**
	   - **Max**
	   - **If condition**
	   - **ISNULL**
	   - **Format**
	   - **Round**
	   - **Upper**
	   - **Lower**
	   - **Trim**
	   - **Len**
	   - **Substring**
	   - **Concat**
  - **Data Sources**: 
	- **DataSource/Schema Editor**
  - **Canvas Explorer**:
- **Canvas**:
- **Properties Panel**:
  - **Report Settings** (Click on canvas blank area to show report properties)
  - **Page Setup** (Click on canvas blank area to show report properties)
  - **Default Styles**
  - **Position & Size** - X, Y, Width, Height
  - **Content** - Expression, StaticText
  - **Format** - Different format, Alignment, Rotation
  - **Style** - Font family, Font size, Font style (Bold, Italic, Underline), Font color, Background color
  - **Shape** - Stroke color, Stroke width, Fill color, Border radius
  - **Image** - Image source, Image quality, Image stretch
  - **Behavior** - Can Grow, Suppress When, Hyperlink


## Designing Your Report

To design your report, follow these steps:

1. **Create a New Report**: Click on the "New" button in the Header/Menu bar to start a new report design.
2. **Add Data Sources**: Use the Data Sources tab to define the data sources that will be used in your report. You can add multiple data sources and define relationships between them if needed.
3. **Design the Report Layout**: Drag and drop elements from the Field Elements tab onto the canvas to create your report layout. You can also resize and reposition elements as needed.
4. **Set Report Properties**: Click on the canvas blank area to display the report properties in the Properties Panel. Modify properties such as page size, orientation, and margins as needed.
5. **Preview the Report**: Click on the Preview button in the Header/Menu bar to view your report design. Note: You should load the sample data source before previewing the report to see how it will look with actual data.
6. **Save Your Report**: Click on the "Save" button in the Header/Menu bar to save your report design. You can choose to save it as a new report or overwrite an existing report.

Once all the steps are completed for report designing, you can use the report in your application.