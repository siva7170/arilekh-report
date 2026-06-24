using ReportDesigner.Core.Model;

namespace ReportDesigner.Designer.Models;

// ─────────────────────────────────────────────────────────────────────────────
// Selection
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Tracks what is currently selected on the canvas.</summary>
public class DesignerSelection
{
    public SectionDefinition? Section { get; set; }
    public FieldElement?       Field   { get; set; }

    /// <summary>All fields in the current multi-selection (includes Field when non-null).</summary>
    public HashSet<FieldElement> SelectedFields { get; } = new();

    public bool HasSection    => Section is not null;
    public bool HasField      => Field   is not null;
    public bool IsMultiSelect => SelectedFields.Count > 1;
    public bool IsEmpty       => Section is null && Field is null && SelectedFields.Count == 0;

    public void Clear()
    {
        Section        = null;
        Field          = null;
        SelectedFields.Clear();
    }

    public void Select(SectionDefinition section)
    {
        Section = section;
        Field   = null;
        SelectedFields.Clear();
    }

    public void Select(SectionDefinition section, FieldElement field)
    {
        Section = section;
        Field   = field;
        SelectedFields.Clear();
        SelectedFields.Add(field);
    }

    /// <summary>Toggle field in multi-selection (Ctrl+click).</summary>
    public void ToggleField(SectionDefinition section, FieldElement field)
    {
        Section = section;
        if (SelectedFields.Contains(field))
        {
            SelectedFields.Remove(field);
            Field = SelectedFields.Count > 0 ? SelectedFields.Last() : null;
        }
        else
        {
            SelectedFields.Add(field);
            Field = field;
        }
    }

    public bool IsFieldSelected(FieldElement f) => SelectedFields.Contains(f);
}

// ─────────────────────────────────────────────────────────────────────────────
// Drag state
// ─────────────────────────────────────────────────────────────────────────────

public enum DragMode { None, MoveField, ResizeField, ResizeSection, PaletteField }

/// <summary>Tracks an active drag operation on the canvas.</summary>
public class DragState
{
    public DragMode Mode { get; set; } = DragMode.None;

    // The element being dragged
    public SectionDefinition? TargetSection { get; set; }
    public FieldElement? TargetField { get; set; }

    // For palette drags – the field name being dragged from the palette
    public string? PaletteFieldName { get; set; }

    // For shape palette drags
    public ElementKind? PaletteShapeKind { get; set; }

    // For chart palette drags
    public ChartType? PaletteChartKind { get; set; }

    // For custom formula drags
    public bool PaletteIsCustomFormula { get; set; }

    // Mouse offset from element top-left at drag start
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }

    // Original values at drag start (for undo)
    public float OrigX { get; set; }
    public float OrigY { get; set; }

    // Client coords at drag start – used to compute delta for MoveField
    public float DragStartClientX { get; set; }
    public float DragStartClientY { get; set; }
    public float OrigWidth { get; set; }
    public float OrigHeight { get; set; }

    // Original positions of ALL selected fields at drag start (for multi-select move)
    public Dictionary<FieldElement, (float X, float Y)> OrigPositions { get; } = new();

    // Which resize handle is active (Right = width only, Bottom = height only)
    public ResizeHandle ActiveHandle { get; set; } = ResizeHandle.None;

    public bool IsActive => Mode != DragMode.None;

    public void Reset()
    {
        Mode = DragMode.None;
        TargetSection = null;
        TargetField = null;
        PaletteFieldName = null;
        PaletteShapeKind = null;
        PaletteChartKind = null;
        PaletteIsCustomFormula = false;
        OffsetX = OffsetY = 0;
        DragStartClientX = DragStartClientY = 0;
        OrigPositions.Clear();
        ActiveHandle = ResizeHandle.None;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Undo / redo snapshot
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A lightweight snapshot of the report definition for undo/redo.</summary>
public class ReportSnapshot
{
    public string Label { get; set; } = string.Empty;
    public string Xml { get; set; } = string.Empty;   // serialised ReportDefinition
}

// ─────────────────────────────────────────────────────────────────────────────
// Designer page mode
// ─────────────────────────────────────────────────────────────────────────────

public enum DesignerMode { Design, Preview }

// ─────────────────────────────────────────────────────────────────────────────
// Resize handle position
// ─────────────────────────────────────────────────────────────────────────────

public enum ResizeHandle { None, Right, Bottom, BottomRight }

// ─────────────────────────────────────────────────────────────────────────────
// Palette item
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>An item in the field palette – either a data field or a special element.</summary>
public class PaletteItem
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = "📊";
    public bool IsSpecial { get; set; }
    public string? Expression { get; set; }
    public FieldDataType DataType { get; set; } = FieldDataType.String;
    public ElementKind? ShapeKind { get; set; }
    public ChartType? ChartKind { get; set; }       // set for chart palette items
    public bool IsCustomFormula { get; set; }        // set for custom formula item
}

// ─────────────────────────────────────────────────────────────────────────────
// Toast notification
// ─────────────────────────────────────────────────────────────────────────────

public enum ToastType { Info, Success, Warning, Error }

public class ToastMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public ToastType Type { get; set; } = ToastType.Info;
    public int DurationMs { get; set; } = 3000;
}

// ─────────────────────────────────────────────────────────────────────────────
// Context menu
// ─────────────────────────────────────────────────────────────────────────────

public class ContextMenuState
{
    public bool IsVisible { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }

    // Target — either a field (right-click on field) or a section (right-click on band)
    public SectionDefinition? Section { get; private set; }
    public FieldElement?      Field   { get; private set; }

    public bool IsFieldMenu   => Field   is not null;
    public bool IsSectionMenu => Field   is null && Section is not null;

    public void Show(double x, double y, SectionDefinition? section, FieldElement? field)
    {
        X = x; Y = y;
        Section = section;
        Field   = field;
        IsVisible = true;
    }

    public void Hide()
    {
        IsVisible = false;
        Section   = null;
        Field     = null;
    }
}
