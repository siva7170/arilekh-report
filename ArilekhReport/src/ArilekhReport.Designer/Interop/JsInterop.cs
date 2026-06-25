using Microsoft.JSInterop;

namespace ArilekhReport.Designer.Interop;

/// <summary>
/// Thin wrapper around JS interop calls required by the designer.
/// </summary>
public sealed class JsInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public JsInterop(IJSRuntime js)
    {
        _moduleTask = new(() => js.InvokeAsync<IJSObjectReference>(
            "import", "./js/designer-interop.js").AsTask());
    }

    // ── File I/O ──────────────────────────────────────────────────────────────

    /// <summary>Opens a file-picker and returns the text content + file name.</summary>
    public async Task<(string content, string fileName)> OpenFileAsync(string accept = ".rdx,.rds,.xml")
    {
        var module = await _moduleTask.Value;
        var result = await module.InvokeAsync<FileOpenResult>("openFile", accept);
        return (result.Content, result.FileName);
    }

    /// <summary>
    /// Attaches a native JS dragstart listener that sets a properly-scaled drag ghost.
    /// Call once after the element is rendered (OnAfterRenderAsync).
    /// </summary>
    public async Task AttachScaledDragImageAsync(string elementId)
    {
        try
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("attachScaledDragImage", elementId);
        }
        catch { /* non-critical */ }
    }

    /// <summary>Opens HTML in a hidden iframe and triggers window.print() for browser Save-as-PDF.</summary>
    public async Task PrintHtmlAsPdfAsync(string html)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("printHtml", html);
    }

    /// <summary>Triggers a browser download of a base64-encoded binary file.</summary>
    public async Task DownloadBase64Async(string fileName, string base64, string mimeType)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("downloadBase64", fileName, base64, mimeType);
    }

    /// <summary>Triggers a browser download with the given text content.</summary>
    public async Task SaveFileAsync(string fileName, string content)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("saveFile", fileName, content);
    }

    // ── Clipboard ─────────────────────────────────────────────────────────────

    public async Task CopyTextAsync(string text)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("copyText", text);
    }

    // ── Element bounding rect ────────────────────────────────────────────────

    public async Task<BoundingRect> GetBoundingRectAsync(string elementId)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<BoundingRect>("getBoundingRect", elementId);
    }

    // ── WebView2 host bridge ──────────────────────────────────────────────────

    /// <summary>Notifies the VSIX host that the report XML has changed (dirty state).</summary>
    public async Task NotifyHostDirtyAsync(bool isDirty)
    {
        try
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("notifyHostDirty", isDirty);
        }
        catch { /* not in VS host — ignore */ }
    }

    /// <summary>Sends the serialized report XML to the VSIX host to write to disk.</summary>
    public async Task NotifyHostSaveAsync(string xml)
    {
        try
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync("notifyHostSave", xml);
        }
        catch { /* not in VS host — ignore */ }
    }

    /// <summary>Requests the VSIX host to open a file path.</summary>
    public async Task<string?> RequestHostFileAsync(string fileType)
    {
        try
        {
            var module = await _moduleTask.Value;
            return await module.InvokeAsync<string?>("requestHostFile", fileType);
        }
        catch { return null; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}

public record FileOpenResult(string Content, string FileName);
public record BoundingRect(double Left, double Top, double Width, double Height);
