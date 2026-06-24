using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using ReportDesigner.Designer.Interop;
using ReportDesigner.Designer.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<ReportDesigner.Designer.App>("#app");

builder.Services.AddScoped<DesignerStateService>();
builder.Services.AddScoped<ClipboardService>();
builder.Services.AddScoped<UndoRedoService>();
builder.Services.AddScoped<PreviewService>();
builder.Services.AddScoped<JsInterop>();
builder.Services.AddScoped<ReportViewerService>();

var host = builder.Build();

// Initialize static JS-invokable interop with the scoped state service
var state = host.Services.GetRequiredService<DesignerStateService>();
HostBridgeInterop.Initialize(state);

// Wire up the VS host bridge if running inside WebView2
try
{
    var js = host.Services.GetRequiredService<IJSRuntime>();

    var isVsHost = await js.InvokeAsync<bool>(
        "eval", "!!(window.chrome && window.chrome.webview)");

    if (isVsHost)
    {
        var filePath = await js.InvokeAsync<string?>(
            "eval",
            "new URLSearchParams(window.location.search).get('file')");

        if (!string.IsNullOrEmpty(filePath))
            state.SetHostFilePath(Uri.UnescapeDataString(filePath));

        await js.InvokeVoidAsync("eval", """
            if (window.rdHostBridge) {
                window.rdHostBridge.onLoadContent(function(xml) {
                    DotNet.invokeMethodAsync(
                        'ReportDesigner.Designer', 'LoadReportFromHost', xml);
                });
                window.rdHostBridge.onLoadCompanion(function(path, xml) {
                    DotNet.invokeMethodAsync(
                        'ReportDesigner.Designer', 'LoadSchemaFromHost', path, xml);
                });
                window.rdHostBridge.onSavedCallback(function() {
                    DotNet.invokeMethodAsync(
                        'ReportDesigner.Designer', 'OnSavedByHost');
                });
                console.log('[Blazor] Host bridge handlers registered.');
            } else {
                console.warn('[Blazor] rdHostBridge not found.');
            }
            """);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[HostBridge] Wire-up error: {ex.Message}");
}

await host.RunAsync();