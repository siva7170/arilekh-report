using Microsoft.JSInterop;
using ArilekhReport.Core;
using ArilekhReport.Designer.Models;
using ArilekhReport.Designer.Services;

namespace ArilekhReport.Designer.Interop;

/// <summary>
/// Static methods callable from JavaScript via DotNet.invokeMethodAsync.
/// These are the entry points the VS host bridge uses to push data into Blazor.
/// </summary>
public static class HostBridgeInterop
{
    // Injected once at startup by Program.cs
    private static DesignerStateService? _state;

    public static void Initialize(DesignerStateService state)
        => _state = state;

    // ── Called by JS: VS pushes .rdx XML content ─────────────────────

    [JSInvokable("LoadReportFromHost")]
    public static Task LoadReportFromHostAsync(string xml)
    {
        if (_state is null || string.IsNullOrWhiteSpace(xml))
            return Task.CompletedTask;

        try
        {
            var report = XmlReportSerializer.DeserializeReport(xml);
            _state.LoadReport(report);
            _state.ShowToast("Report loaded from Visual Studio", ToastType.Success);
        }
        catch (Exception ex)
        {
            _state.ShowToast($"Failed to load report: {ex.Message}", ToastType.Error);
        }

        return Task.CompletedTask;
    }

    // ── Called by JS: VS pushes .rds schema XML ───────────────────────

    [JSInvokable("LoadSchemaFromHost")]
    public static Task LoadSchemaFromHostAsync(string filePath, string xml)
    {
        if (_state is null || string.IsNullOrWhiteSpace(xml))
            return Task.CompletedTask;

        try
        {
            var schema = XmlReportSerializer.DeserializeSchema(xml);
            _state.LoadSchema(schema);
            _state.ShowToast($"Schema loaded: {schema.Name}", ToastType.Success);
        }
        catch (Exception ex)
        {
            _state.ShowToast($"Failed to load schema: {ex.Message}", ToastType.Error);
        }

        return Task.CompletedTask;
    }

    // ── Called by JS: VS confirms file was saved ──────────────────────

    [JSInvokable("OnSavedByHost")]
    public static Task OnSavedByHostAsync()
    {
        _state?.MarkSaved();
        _state?.ShowToast("Saved", ToastType.Success, 1500);
        return Task.CompletedTask;
    }
}
