using ReportDesigner.Core.Model;
using System.Text.Json;

namespace ReportDesigner.Designer.Services;

/// <summary>In-memory clipboard for copying/pasting FieldElement objects.</summary>
public sealed class ClipboardService
{
    private string? _json;

    public bool HasContent => _json is not null;

    public void Copy(FieldElement field)
    {
        _json = JsonSerializer.Serialize(field);
    }

    /// <summary>Returns a deep clone of the copied field with a new name.</summary>
    public FieldElement? Paste()
    {
        if (_json is null) return null;
        var clone = JsonSerializer.Deserialize<FieldElement>(_json);
        if (clone is null) return null;

        // Offset slightly so paste is visible
        clone.Name = $"field_{Guid.NewGuid().ToString("N")[..8]}";
        clone.X   += 8f;
        clone.Y   += 8f;
        return clone;
    }
}
