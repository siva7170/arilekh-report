using ArilekhReport.Core;
using ArilekhReport.Core.Model;
using ArilekhReport.Designer.Models;

namespace ArilekhReport.Designer.Services;

/// <summary>
/// Central state service – single source of truth for the designer.
/// All components inject this and subscribe to <see cref="OnChanged"/> to re-render.
/// </summary>
public sealed class DesignerStateService
{
    // ── Report & schema ───────────────────────────────────────────────────────

    public ReportDefinition  Report    { get; private set; } = CreateDefaultReport();
    public DataSetSchema?    Schema    { get; private set; }
    public string?           FilePath  { get; private set; }   // path on disk (VS host)
    public bool              IsDirty   { get; private set; }

    public string? HostFilePath { get; private set; }

    public bool IsVsHosted => !string.IsNullOrEmpty(HostFilePath);

    public void SetHostFilePath(string path)
    {
        HostFilePath = path;
        // Update the report name to match the file
        if (!string.IsNullOrEmpty(path))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrEmpty(name))
                Report.Name = name;
        }
    }

    public string GetReportXml()
    => ArilekhReport.Core.XmlReportSerializer.SerializeReport(Report);

    // ── Mode ──────────────────────────────────────────────────────────────────

    public DesignerMode Mode { get; private set; } = DesignerMode.Design;

    // ── Selection ─────────────────────────────────────────────────────────────

    public DesignerSelection Selection { get; } = new();

    // ── Drag ──────────────────────────────────────────────────────────────────

    public DragState Drag { get; } = new();

    // ── Canvas zoom ───────────────────────────────────────────────────────────

    public float Zoom     { get; private set; } = 1.0f;    // 1.0 = 100%
    public float PtToPx   => Zoom * 1.333f;                // points → CSS pixels

    // ── Grid / snap ───────────────────────────────────────────────────────────

    public bool  SnapToGrid  { get; private set; } = true;
    public float GridSize    { get; private set; } = 4f;   // points

    // ── Ruler ─────────────────────────────────────────────────────────────────

    public bool ShowRuler { get; private set; } = true;

    // ── Change notification ───────────────────────────────────────────────────

    public event Action? OnChanged;
    public event Action<ToastMessage>? OnToast;

    public void NotifyChanged()
    {
        IsDirty = true;
        OnChanged?.Invoke();
    }

    public void NotifyChangedNoMark()   // layout refresh without marking dirty
        => OnChanged?.Invoke();

    // ── Report operations ─────────────────────────────────────────────────────

    public void LoadReport(ReportDefinition report, string? filePath = null)
    {
        Report   = report;
        FilePath = filePath;
        IsDirty  = false;
        Selection.Clear();
        Drag.Reset();
        OnChanged?.Invoke();
    }

    public void LoadSchema(DataSetSchema schema)
    {
        Schema = schema;
        OnChanged?.Invoke();
    }

    public void MarkSaved(string? filePath = null)
    {
        if (filePath is not null) FilePath = filePath;
        IsDirty = false;
        OnChanged?.Invoke();
    }

    public void NewReport()
    {
        Report   = CreateDefaultReport();
        Schema   = null;
        FilePath = null;
        IsDirty  = false;
        Selection.Clear();
        OnChanged?.Invoke();
    }

    // ── Mode switch ───────────────────────────────────────────────────────────

    public void SetMode(DesignerMode mode)
    {
        Mode = mode;
        OnChanged?.Invoke();
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    public void SelectSection(SectionDefinition section)
    {
        Selection.Select(section);
        OnChanged?.Invoke();
    }

    public void SelectField(SectionDefinition section, FieldElement field)
    {
        Selection.Select(section, field);
        OnChanged?.Invoke();
    }

    public void ToggleFieldSelection(SectionDefinition section, FieldElement field)
    {
        Selection.ToggleField(section, field);
        OnChanged?.Invoke();
    }

    public void MoveSelectedFields(float deltaXPt, float deltaYPt)
    {
        foreach (var f in Selection.SelectedFields)
        {
            f.X = Snap(f.X + deltaXPt);
            f.Y = Snap(f.Y + deltaYPt);
        }
        NotifyChanged();
    }

    public void ClearSelection()
    {
        Selection.Clear();
        OnChanged?.Invoke();
    }

    // ── Theme ──────────────────────────────────────────────────────────────────

    public bool IsDarkMode { get; private set; } = true;

    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        OnChanged?.Invoke();
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    public ContextMenuState ContextMenu { get; } = new();

    public void ShowContextMenu(double clientX, double clientY,
                                SectionDefinition? section, FieldElement? field)
    {
        ContextMenu.Show(clientX, clientY, section, field);
        OnChanged?.Invoke();
    }

    public void HideContextMenu()
    {
        if (!ContextMenu.IsVisible) return;
        ContextMenu.Hide();
        OnChanged?.Invoke();
    }

    // ── Zoom ──────────────────────────────────────────────────────────────────

    public void SetZoom(float zoom)
    {
        Zoom = Math.Clamp(zoom, 0.25f, 3f);
        OnChanged?.Invoke();
    }

    public void ZoomIn()  => SetZoom(Zoom + 0.1f);
    public void ZoomOut() => SetZoom(Zoom - 0.1f);
    public void ZoomReset() => SetZoom(1.0f);

    // ── Grid ──────────────────────────────────────────────────────────────────

    public void ToggleSnap() { SnapToGrid = !SnapToGrid; OnChanged?.Invoke(); }
    public void ToggleRuler() { ShowRuler = !ShowRuler; OnChanged?.Invoke(); }

    // ── Snap helper ───────────────────────────────────────────────────────────

    public float Snap(float value)
        => SnapToGrid ? MathF.Round(value / GridSize) * GridSize : value;

    // ── Section operations ────────────────────────────────────────────────────

    public void AddSection(SectionType type)
    {
        if (Report.Sections.Any(s => s.Type == type &&
            type is SectionType.ReportHeader or SectionType.ReportFooter
                  or SectionType.PageHeader  or SectionType.PageFooter))
        {
            ShowToast($"A {type} section already exists.", ToastType.Warning);
            return;
        }

        var section = new SectionDefinition
        {
            Name   = $"{type}_{Guid.NewGuid().ToString("N")[..8]}",       //  $"{type}_{Guid.NewGuid().ToString("N")[..8]}",
            Type   = type,
            Height = 30f,
        };

        // Insert in logical order
        var insertIdx = GetInsertIndex(type);
        Report.Sections.Insert(insertIdx, section);
        Selection.Select(section);
        NotifyChanged();
    }

    public void DeleteSection(SectionDefinition section)
    {
        Report.Sections.Remove(section);
        if (Selection.Section == section) Selection.Clear();
        NotifyChanged();
    }

    public void ResizeSection(SectionDefinition section, float newHeight)
    {
        section.Height = Math.Max(10f, Snap(newHeight));
        NotifyChanged();
    }

    // ── Field operations ──────────────────────────────────────────────────────

    public FieldElement AddField(SectionDefinition section, float x, float y,
                                  string? fieldName = null, string? expression = null)
    {
        var field = new FieldElement
        {
            Name       = fieldName ?? $"field_{Guid.NewGuid().ToString("N")[..8]}",
            X          = Snap(x),
            Y          = Snap(y),
            Width      = 100f,
            Height     = 14f,
            Expression = expression ?? (fieldName is not null ? $"Fields.{fieldName}" : null),
        };

        section.Fields.Add(field);
        Selection.Select(section, field);
        NotifyChanged();
        return field;
    }

    public FieldElement AddShapeElement(SectionDefinition section, float x, float y, ElementKind kind)
    {
        // Default sizes and stroke per shape kind
        float w = kind == ElementKind.Line ? 72f : 60f;
        float h = kind == ElementKind.Line ? 0f  : 60f;

        var field = new FieldElement
        {
            Name        = $"{kind.ToString().ToLower()}_{Guid.NewGuid().ToString("N")[..8]}",
            X           = Snap(x),
            Y           = Snap(y),
            Width       = w,
            Height      = Math.Max(4f, h),
            Kind        = kind,
            StrokeColor = "#000000",
            StrokeWidth = 1f,
            FillColor   = null,   // transparent by default
        };

        section.Fields.Add(field);
        Selection.Select(section, field);
        NotifyChanged();
        return field;
    }

    public void DeleteField(SectionDefinition section, FieldElement field)
    {
        section.Fields.Remove(field);
        if (Selection.Field == field) Selection.Clear();
        NotifyChanged();
    }

    public void MoveField(SectionDefinition section, FieldElement field, float newX, float newY)
    {
        field.X = Math.Max(0f, Snap(newX));
        field.Y = Math.Max(0f, Snap(newY));
        NotifyChanged();
    }

    public void ResizeField(FieldElement field, float newWidth, float newHeight)
    {
        field.Width  = Math.Max(10f, Snap(newWidth));
        field.Height = Math.Max(8f,  Snap(newHeight));
        NotifyChanged();
    }

    // ── Toast ─────────────────────────────────────────────────────────────────

    public void ShowToast(string text, ToastType type = ToastType.Info, int durationMs = 3000)
        => OnToast?.Invoke(new ToastMessage { Text = text, Type = type, DurationMs = durationMs });

    // ── Palette items ─────────────────────────────────────────────────────────

    public List<PaletteItem> GetPaletteItems()
    {
        var items = new List<PaletteItem>();
        var seen  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Fields from loaded .rds schema (legacy single-schema path)
        if (Schema is not null)
        {
            foreach (var f in Schema.Fields)
            {
                if (seen.Add(f.Name))
                    items.Add(new PaletteItem
                    {
                        Name       = f.Name,
                        Label      = f.Caption ?? f.Name,
                        Icon       = DataTypeIcon(f.DataType),
                        DataType   = f.DataType,
                        Expression = $"Fields.{f.Name}",
                    });
            }
        }

        // Fields defined directly on DataSourceDefinition entries
        foreach (var ds in Report.DataSources)
        {
            foreach (var f in ds.Fields)
            {
                if (seen.Add($"{ds.Name}.{f.Name}"))
                    items.Add(new PaletteItem
                    {
                        Name       = $"{ds.Name}_{f.Name}",
                        Label      = $"{f.Caption ?? f.Name} ({ds.Name})",
                        Icon       = DataTypeIcon(f.DataType),
                        DataType   = f.DataType,
                        Expression = $"Fields.{f.Name}",
                    });
            }
            // Scalar field
            if (ds.Kind == DataSourceKind.ScalarField && seen.Add(ds.Name))
            {
                items.Add(new PaletteItem
                {
                    Name       = ds.Name,
                    Label      = ds.Name,
                    Icon       = DataTypeIcon(ds.ScalarType),
                    DataType   = ds.ScalarType,
                    Expression = $"Fields.{ds.Name}",
                });
            }
        }

        return items;
    }

    public List<PaletteItem> GetSpecialItems() =>
    [
        new() { Name="PageNumber",  Label="Page Number",  Icon="📄", IsSpecial=true, Expression="PageNumber()"  },
        new() { Name="TotalPages",  Label="Total Pages",  Icon="📑", IsSpecial=true, Expression="TotalPages()"  },
        new() { Name="Today",       Label="Today",        Icon="📅", IsSpecial=true, Expression="Today()"       },
        new() { Name="Now",         Label="Now",          Icon="🕐", IsSpecial=true, Expression="Now()"         },
        new() { Name="RowNumber",   Label="Row Number",   Icon="🔢", IsSpecial=true, Expression="RowNumber()"   },
        new() { Name="StaticText",  Label="Static Text",  Icon="🏷️", IsSpecial=true, Expression=null            },
    ];

    public FieldElement AddChartElement(SectionDefinition section, float x, float y, ChartType chartType)
    {
        var field = new FieldElement
        {
            Name   = $"chart_{Guid.NewGuid().ToString("N")[..8]}",
            X      = Snap(x),
            Y      = Snap(y),
            Width  = 200f,
            Height = 120f,
            Kind   = ElementKind.Chart,
            Chart  = new ChartDefinition
            {
                Type       = chartType,
                ShowBorder = true,
                ShowLegend = true,
                ShowLabels = true,
                Series     =
                [
                    new ChartSeries { Label = "Series 1", FieldName = "", Color = "#4472C4", Aggregate = "SUM" }
                ]
            }
        };
        section.Fields.Add(field);
        Selection.Select(section, field);
        NotifyChanged();
        return field;
    }

    public FieldElement AddCustomFieldElement(SectionDefinition section, float x, float y)
    {
        var field = new FieldElement
        {
            Name          = $"custom_{Guid.NewGuid().ToString("N")[..8]}",
            X             = Snap(x),
            Y             = Snap(y),
            Width         = 100f,
            Height        = 14f,
            Kind          = ElementKind.CustomFormula,
            CustomFormula = new CustomFieldDefinition { Formula = "" }
        };
        section.Fields.Add(field);
        Selection.Select(section, field);
        NotifyChanged();
        return field;
    }

    public List<PaletteItem> GetChartItems() =>
    [
        new() { Name="ChartPie",     Label="Pie Chart",        Icon="Pie",  IsSpecial=true, ChartKind=ChartType.Pie           },
        new() { Name="ChartBar",     Label="Bar Chart (V)",    Icon="Bar",  IsSpecial=true, ChartKind=ChartType.Bar           },
        new() { Name="ChartBarH",    Label="Bar Chart (H)",    Icon="HBar", IsSpecial=true, ChartKind=ChartType.BarHorizontal },
        new() { Name="ChartLine",    Label="Line Chart",       Icon="Line", IsSpecial=true, ChartKind=ChartType.Line          },
        new() { Name="CustomField",  Label="Custom Formula",   Icon="Fx",   IsSpecial=true, IsCustomFormula=true              },
    ];

    public List<PaletteItem> GetShapeItems() =>
    [
        new() { Name="ShapeLine",   Label="Line",      Icon="Line",   IsSpecial=true, Expression=null, ShapeKind=ElementKind.Line   },
        new() { Name="ShapeBox",    Label="Box",       Icon="Box",    IsSpecial=true, Expression=null, ShapeKind=ElementKind.Box    },
        new() { Name="ShapeCircle", Label="Circle",    Icon="Circle", IsSpecial=true, Expression=null, ShapeKind=ElementKind.Circle },
        new() { Name="ShapeImage",  Label="Image Box", Icon="Img",    IsSpecial=true, Expression=null, ShapeKind=ElementKind.Image  },
    ];

    public List<PaletteItem> GetFunctionItems() =>
    [
        new() { Name="FnSum",       Label="SUM(field)",          Icon="Sum",  IsSpecial=true, Expression="SUM([Field])"               },
        new() { Name="FnCount",     Label="COUNT(field)",        Icon="N",    IsSpecial=true, Expression="COUNT([Field])"             },
        new() { Name="FnAvg",       Label="AVG(field)",          Icon="Avg",  IsSpecial=true, Expression="AVG([Field])"               },
        new() { Name="FnMin",       Label="MIN(field)",          Icon="Min",  IsSpecial=true, Expression="MIN([Field])"               },
        new() { Name="FnMax",       Label="MAX(field)",          Icon="Max",  IsSpecial=true, Expression="MAX([Field])"               },
        new() { Name="FnIIF",       Label="IIF(cond, t, f)",    Icon="?",    IsSpecial=true, Expression="IIF([Condition], \"\", \"\")" },
        new() { Name="FnIsNull",    Label="ISNULL(field, val)", Icon="Null", IsSpecial=true, Expression="ISNULL([Field], \"\")"      },
        new() { Name="FnFormat",    Label="FORMAT(field, fmt)", Icon="Fmt",  IsSpecial=true, Expression="FORMAT([Field], \"\")"      },
        new() { Name="FnRound",     Label="ROUND(field, n)",    Icon="~",    IsSpecial=true, Expression="ROUND([Field], 2)"          },
        new() { Name="FnUpper",     Label="UPPER(field)",       Icon="AA",   IsSpecial=true, Expression="UPPER([Field])"             },
        new() { Name="FnLower",     Label="LOWER(field)",       Icon="aa",   IsSpecial=true, Expression="LOWER([Field])"             },
        new() { Name="FnTrim",      Label="TRIM(field)",        Icon="Trm",  IsSpecial=true, Expression="TRIM([Field])"              },
        new() { Name="FnLen",       Label="LEN(field)",         Icon="Len",  IsSpecial=true, Expression="LEN([Field])"               },
        new() { Name="FnSubstring", Label="SUBSTRING(f,s,len)", Icon="Sub",  IsSpecial=true, Expression="SUBSTRING([Field], 0, 10)"  },
        new() { Name="FnConcat",    Label="CONCAT(a, b, ...)",  Icon="+",    IsSpecial=true, Expression="CONCAT([Field], \"\")"      },
    ];

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string DataTypeIcon(FieldDataType dt) => dt switch
    {
        FieldDataType.Int32 or
        FieldDataType.Int64 or
        FieldDataType.Decimal or
        FieldDataType.Double or
        FieldDataType.Float  => "🔢",
        FieldDataType.Boolean => "☑️",
        FieldDataType.DateTime => "📅",
        _ => "🔤",
    };

    private int GetInsertIndex(SectionType type)
    {
        // Desired order: ReportHeader, PageHeader, GroupHeader, Detail, GroupFooter, PageFooter, ReportFooter
        int desired = (int)type;
        for (int i = 0; i < Report.Sections.Count; i++)
        {
            if ((int)Report.Sections[i].Type > desired) return i;
        }
        return Report.Sections.Count;
    }

    private static ReportDefinition CreateDefaultReport() => new()
    {
        Name    = "NewReport",
        Version = "1.0",
        PageSetup = new ReportPageSetup(),
        Sections =
        [
            new SectionDefinition
            {
                Name   = "PageHeader1",
                Type   = SectionType.PageHeader,
                Height = 30f,
                Fields = [ new FieldElement
                {
                    Name="Title", Text="Report Title",
                    X=0, Y=6, Width=200, Height=16,
                    Style = new FieldStyle { Bold=true, FontSize=14f }
                }]
            },
            new SectionDefinition
            {
                Name   = "Detail1",
                Type   = SectionType.Detail,
                Height = 18f,
                Fields = []
            },
            new SectionDefinition
            {
                Name   = "PageFooter1",
                Type   = SectionType.PageFooter,
                Height = 20f,
                Fields = [ new FieldElement
                {
                    Name="PageNum", Expression="PageNumber()",
                    X=400, Y=4, Width=100, Height=12,
                    Alignment=FieldAlignment.Right
                }]
            },
        ]
    };
}
