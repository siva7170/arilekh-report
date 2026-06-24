using ReportDesigner.Core;
using ReportDesigner.Designer.Models;

namespace ReportDesigner.Designer.Services;

/// <summary>
/// Stack-based undo/redo using XML snapshots of the ReportDefinition.
/// Push a snapshot before any destructive change.
/// </summary>
public sealed class UndoRedoService
{
    private readonly DesignerStateService _state;
    private readonly Stack<ReportSnapshot> _undo = new();
    private readonly Stack<ReportSnapshot> _redo = new();
    private const int MaxHistory = 50;

    public UndoRedoService(DesignerStateService state) => _state = state;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Call BEFORE making a change to the report.</summary>
    public void Push(string label)
    {
        var xml = XmlReportSerializer.SerializeReport(_state.Report);
        _undo.Push(new ReportSnapshot { Label = label, Xml = xml });
        _redo.Clear();

        // Trim history
        while (_undo.Count > MaxHistory)
        {
            var arr  = _undo.ToArray();
            _undo.Clear();
            foreach (var s in arr.Take(MaxHistory).Reverse()) _undo.Push(s);
        }
    }

    public void Undo()
    {
        if (!CanUndo) return;

        // Save current state to redo stack
        var currentXml = XmlReportSerializer.SerializeReport(_state.Report);
        _redo.Push(new ReportSnapshot { Label = "redo", Xml = currentXml });

        var snapshot = _undo.Pop();
        var report   = XmlReportSerializer.DeserializeReport(snapshot.Xml);
        _state.LoadReport(report, _state.FilePath);
        _state.ShowToast($"Undo: {snapshot.Label}", ToastType.Info, 1500);
    }

    public void Redo()
    {
        if (!CanRedo) return;

        var currentXml = XmlReportSerializer.SerializeReport(_state.Report);
        _undo.Push(new ReportSnapshot { Label = "undo", Xml = currentXml });

        var snapshot = _redo.Pop();
        var report   = XmlReportSerializer.DeserializeReport(snapshot.Xml);
        _state.LoadReport(report, _state.FilePath);
        _state.ShowToast("Redo", ToastType.Info, 1500);
    }
}
