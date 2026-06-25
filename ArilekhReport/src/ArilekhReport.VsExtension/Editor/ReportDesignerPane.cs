using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace ArilekhReport.VsExtension.Editor
{
    [ComVisible(true)]
    public sealed class ReportDesignerPane : WindowPane, IVsWindowPane
    {
        private readonly AsyncPackage _package;
        private readonly string _filePath;
        private readonly FileType _fileType;
        private WebView2 _webView;
        private Grid _root;
        private TextBlock _loadingText;
        private bool _webViewReady;
        private string _wwwroot;

        // Use a simple fake hostname – served via WebResourceRequested
        private const string VirtualHost = "rdapp";
        private const string BaseUrl = "https://rdapp/";

        private IVsOutputWindowPane _outputPane;

        // Shared environment across all panes
        private static CoreWebView2Environment _sharedEnv;
        private static readonly SemaphoreSlim _envLock = new SemaphoreSlim(1, 1);

        public ReportDesignerPane(AsyncPackage package, string filePath, FileType fileType)
            : base(package)
        {
            _package = package;
            _filePath = filePath;
            _fileType = fileType;
        }

        protected override void Initialize()
        {
            base.Initialize();

            _root = new Grid { Background = System.Windows.Media.Brushes.Transparent };

            _loadingText = new TextBlock
            {
                Text = "Loading Report Designer…",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14,
                Foreground = System.Windows.Media.Brushes.Gray,
            };
            _root.Children.Add(_loadingText);

            _webView = new WebView2();
            _webView.Visibility = Visibility.Collapsed;
            _root.Children.Add(_webView);

            _ = InitWebViewAsync();
        }

        public override object Content => _root;

        private async System.Threading.Tasks.Task InitWebViewAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _wwwroot = GetWwwRootFolder();
                WriteToOutputWindow($"[PANE] wwwroot: {_wwwroot}");
                WriteToOutputWindow($"[PANE] index.html: {File.Exists(Path.Combine(_wwwroot, "index.html"))}");
                WriteToOutputWindow($"[PANE] _framework: {Directory.Exists(Path.Combine(_wwwroot, "_framework"))}");

                if (!Directory.Exists(_wwwroot))
                {
                    await ShowError($"DesignerApp not found:\n{_wwwroot}");
                    return;
                }

                // Shared environment
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ReportDesigner", "WebView2Cache");

                var env = await GetOrCreateSharedEnvironmentAsync(userDataFolder);
                WriteToOutputWindow("[PANE] Environment ready.");

                await _webView.EnsureCoreWebView2Async(env);
                WriteToOutputWindow("[PANE] CoreWebView2 ready.");

                var cw2 = _webView.CoreWebView2;

                // ── Serve files via WebResourceRequested ──────────────
                // This replaces SetVirtualHostNameToFolderMapping entirely.
                // Intercept all requests to https://rdapp/* and serve from wwwroot.
                cw2.AddWebResourceRequestedFilter(
                    $"{BaseUrl}*",
                    CoreWebView2WebResourceContext.All);

                cw2.WebResourceRequested += OnWebResourceRequested;
                WriteToOutputWindow("[PANE] WebResourceRequested handler registered.");

                // Inject host-bridge.js
                var bridgeJs = GetHostBridgeScript();
                if (!string.IsNullOrEmpty(bridgeJs))
                {
                    await cw2.AddScriptToExecuteOnDocumentCreatedAsync(bridgeJs);
                    WriteToOutputWindow("[PANE] host-bridge.js injected.");
                }
                else
                {
                    WriteToOutputWindow("[PANE] WARNING: host-bridge.js not found.");
                }

                // Wire events
                cw2.WebMessageReceived += OnWebMessageReceived;
                cw2.NavigationCompleted += OnNavigationCompleted;
                cw2.ProcessFailed += (s, e) =>
                    WriteToOutputWindow($"[PANE] PROCESS_FAILED: {e.ProcessFailedKind}");

                _ = cw2.CallDevToolsProtocolMethodAsync("Runtime.enable", "{}");
                cw2.GetDevToolsProtocolEventReceiver("Runtime.consoleAPICalled")
                   .DevToolsProtocolEventReceived += OnConsoleMessage;
                cw2.GetDevToolsProtocolEventReceiver("Runtime.exceptionThrown")
                   .DevToolsProtocolEventReceived += OnJsException;

                // Settings
                cw2.Settings.AreDefaultContextMenusEnabled = false;
                cw2.Settings.IsStatusBarEnabled = false;
                cw2.Settings.IsZoomControlEnabled = false;
#if DEBUG
                cw2.Settings.AreDevToolsEnabled = true;
#else
                cw2.Settings.AreDevToolsEnabled = false;
#endif

                // Navigate using our fake base URL
                var encodedPath = Uri.EscapeDataString(_filePath);
                var typeLow = _fileType.ToString().ToLowerInvariant();
                var url = $"{BaseUrl}?file={encodedPath}&type={typeLow}";

                WriteToOutputWindow($"[PANE] Navigating to: {url}");
                cw2.Navigate(url);

                _loadingText.Visibility = Visibility.Collapsed;
                _webView.Visibility = Visibility.Visible;
                _webViewReady = true;
            }
            catch (Exception ex)
            {
                WriteToOutputWindow($"[PANE] INIT ERROR: {ex}");
                await ShowError($"Failed:\n{ex.Message}");
            }
        }

        // ── Serve local files for every https://rdapp/* request ───────

        private void OnWebResourceRequested(object sender,
            CoreWebView2WebResourceRequestedEventArgs e)
        {
            try
            {
                var uri = new Uri(e.Request.Uri);
                var localPath = uri.AbsolutePath.TrimStart('/');

                // Root → serve index.html
                if (string.IsNullOrEmpty(localPath) || localPath == "/")
                    localPath = "index.html";

                var filePath = Path.Combine(_wwwroot,
                    localPath.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(filePath))
                {
                    WriteToOutputWindow($"[RES] 404: {filePath}");
                    e.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
                        null, 404, "Not Found", "Content-Type: text/plain");
                    return;
                }

                var content = File.OpenRead(filePath);
                var contentType = GetContentType(filePath);

                e.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
                    content, 200, "OK",
                    $"Content-Type: {contentType}\r\nAccess-Control-Allow-Origin: *");
            }
            catch (Exception ex)
            {
                WriteToOutputWindow($"[RES] ERROR: {ex.Message}");
            }
        }

        private static string GetContentType(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".html" => "text/html; charset=utf-8",
                ".js" => "application/javascript",
                ".wasm" => "application/wasm",
                ".json" => "application/json",
                ".css" => "text/css",
                ".png" => "image/png",
                ".ico" => "image/x-icon",
                ".svg" => "image/svg+xml",
                ".gz" => "application/gzip",
                ".br" => "application/x-br",
                ".dll" => "application/octet-stream",
                ".dat" => "application/octet-stream",
                _ => "application/octet-stream",
            };
        }

        // ── Navigation completed ──────────────────────────────────────

        private void OnNavigationCompleted(object sender,
            CoreWebView2NavigationCompletedEventArgs e)
        {
            var cw2 = _webView.CoreWebView2;
            if (!e.IsSuccess)
            {
                WriteToOutputWindow($"[PANE] NAV_ERROR: {e.WebErrorStatus} url={cw2.Source}");
                return;
            }
            WriteToOutputWindow($"[PANE] NAV_OK: {cw2.Source}");
            _ = PollForBlazorAsync();
        }

        private async System.Threading.Tasks.Task PollForBlazorAsync()
        {
            var cw2 = _webView.CoreWebView2;
            for (int i = 0; i < 60; i++)
            {
                await System.Threading.Tasks.Task.Delay(500);
                try
                {
                    var ready = await cw2.ExecuteScriptAsync(
                        "typeof window.Blazor !== 'undefined'");
                    WriteToOutputWindow($"[PANE] Poll {i + 1}: blazor={ready}");

                    if (ready == "true")
                    {
                        WriteToOutputWindow("[PANE] Blazor ready – pushing content.");
                        await System.Threading.Tasks.Task.Delay(300);
                        PushFileContent();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    WriteToOutputWindow($"[PANE] Poll error: {ex.Message}");
                }
            }
            WriteToOutputWindow("[PANE] ERROR: Blazor did not boot in 30s.");
        }

        private void PushFileContent()
        {
            if (!File.Exists(_filePath))
            {
                WriteToOutputWindow($"[PANE] File not found: {_filePath}");
                return;
            }
            var xml = File.ReadAllText(_filePath, System.Text.Encoding.UTF8);
            var escaped = EscapeForJs(xml);
            _ = _webView.CoreWebView2.ExecuteScriptAsync(
                $"window.rdHostBridge?.loadContent(\"{escaped}\");");
            WriteToOutputWindow($"[PANE] XML pushed ({xml.Length} chars).");
        }

        // ── Blazor → VS messages ──────────────────────────────────────

        private void OnWebMessageReceived(object sender,
            CoreWebView2WebMessageReceivedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var json = e.TryGetWebMessageAsString();
                WriteToOutputWindow($"[PANE] MSG: {json}");
                var msg = JsonSerializer.Deserialize<HostMessage>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (msg == null) return;

                switch (msg.Type?.ToLowerInvariant())
                {
                    case "ready": PushFileContent(); break;
                    case "dirty": UpdateDirtyState(msg.Value == "true"); break;
                    case "save": SaveFile(msg.Value); break;
                    case "openfile": OpenCompanionFile(msg.Value); break;
                }
            }
            catch { }
        }

        // ── Console / exception capture ───────────────────────────────

        private void OnConsoleMessage(object sender,
            CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
        {
            try
            {
                var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : "log";
                var sb = new System.Text.StringBuilder();
                if (root.TryGetProperty("args", out var args) &&
                    args.ValueKind == JsonValueKind.Array)
                {
                    foreach (var arg in args.EnumerateArray())
                    {
                        if (arg.TryGetProperty("value", out var v))
                            sb.Append(v).Append(' ');
                        else if (arg.TryGetProperty("description", out var d))
                            sb.Append(d.GetString()).Append(' ');
                    }
                }
                WriteToOutputWindow($"[JS:{type?.ToUpper()}] {sb.ToString().Trim()}");
            }
            catch { }
        }

        private void OnJsException(object sender,
            CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
        {
            try
            {
                var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
                var root = doc.RootElement;
                var msg = "Unknown";
                if (root.TryGetProperty("exceptionDetails", out var details))
                {
                    if (details.TryGetProperty("text", out var text))
                        msg = text.GetString();
                    if (details.TryGetProperty("exception", out var ex) &&
                        ex.TryGetProperty("description", out var desc))
                        msg = desc.GetString();
                }
                WriteToOutputWindow($"[JS:EXCEPTION] {msg}");
            }
            catch { }
        }

        // ── File operations ───────────────────────────────────────────

        private void SaveFile(string xmlContent)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (!string.IsNullOrWhiteSpace(xmlContent))
                {
                    File.WriteAllText(_filePath, xmlContent, System.Text.Encoding.UTF8);
                    UpdateDirtyState(false);
                    _ = _webView.CoreWebView2.ExecuteScriptAsync(
                        "window.rdHostBridge?.onSaved();");
                }
            }
            catch (Exception ex)
            {
                WriteToOutputWindow($"[PANE] Save error: {ex.Message}");
            }
        }

        private void OpenCompanionFile(string fileType)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var filter = fileType?.ToLower() == "rds"
                ? "Dataset Schema (*.rds)|*.rds|All Files|*.*"
                : "Report Definition (*.rdx)|*.rdx|All Files|*.*";

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                InitialDirectory = Path.GetDirectoryName(_filePath) ?? string.Empty,
            };
            if (dlg.ShowDialog() != true) return;

            var content = File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
            _ = _webView.CoreWebView2.ExecuteScriptAsync(
                $"window.rdHostBridge?.loadCompanionFile(" +
                $"\"{EscapeForJs(dlg.FileName)}\",\"{EscapeForJs(content)}\");");
        }

        private void UpdateDirtyState(bool dirty)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (GetService(typeof(SVsRunningDocumentTable))
                        is IVsRunningDocumentTable rdt)
                {
                    rdt.FindAndLockDocument(
                        (uint)_VSRDTFLAGS.RDT_NoLock, _filePath,
                        out _, out _, out _, out var cookie);
                    if (cookie != VSConstants.VSCOOKIE_NIL && dirty)
                        rdt.ModifyDocumentFlags(
                            cookie, (uint)_VSRDTFLAGS.RDT_DontSave, 0);
                }
            }
            catch { }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static string GetWwwRootFolder()
        {
            var extDir = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location) ?? string.Empty;

            var direct = Path.Combine(extDir, "DesignerApp");
            if (File.Exists(Path.Combine(direct, "index.html"))) return direct;

            var sub = Path.Combine(extDir, "DesignerApp", "wwwroot");
            if (File.Exists(Path.Combine(sub, "index.html"))) return sub;

            return direct;
        }

        private static string GetHostBridgeScript()
        {
            try
            {
                var extDir = Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                var path = Path.Combine(extDir, "Templates", "host-bridge.js");
                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }
            catch { return string.Empty; }
        }

        private static string EscapeForJs(string s) =>
            (s ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r\n", "\\n")
                .Replace("\n", "\\n")
                .Replace("\r", "\\n");

        private async System.Threading.Tasks.Task ShowError(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _loadingText.Text = message;
            _loadingText.Visibility = Visibility.Visible;
            _webView.Visibility = Visibility.Collapsed;
        }

        // ── VS Output window ──────────────────────────────────────────

        private void WriteToOutputWindow(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                try
                {
                    if (_outputPane == null)
                    {
                        var ow = ServiceProvider.GlobalProvider
                            .GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                        var g = new Guid("A1B2C3D4-9999-0000-0000-000000000001");
                        ow?.CreatePane(ref g, "Report Designer", 1, 1);
                        ow?.GetPane(ref g, out _outputPane);
                        _outputPane?.Activate();
                    }
                    _outputPane?.OutputStringThreadSafe(message + Environment.NewLine);
                }
                catch { }
            });
        }

        // ── Shared environment ────────────────────────────────────────

        private static async System.Threading.Tasks.Task<CoreWebView2Environment>
            GetOrCreateSharedEnvironmentAsync(string userDataFolder)
        {
            await _envLock.WaitAsync();
            try
            {
                if (_sharedEnv == null)
                    _sharedEnv = await CoreWebView2Environment.CreateAsync(
                        browserExecutableFolder: null,
                        userDataFolder: userDataFolder);
                return _sharedEnv;
            }
            finally
            {
                _envLock.Release();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _webView?.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class HostMessage
    {
        public string Type { get; set; }
        public string Value { get; set; }
    }
}



//using Microsoft.VisualStudio;
//using Microsoft.VisualStudio.Shell;
//using Microsoft.VisualStudio.Shell.Interop;
//using Microsoft.Web.WebView2.Core;
//using Microsoft.Web.WebView2.Wpf;
//using System;
//using System.IO;
//using System.Reflection;
//using System.Runtime.InteropServices;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Controls;

//namespace ReportDesigner.VsExtension.Editor
//{
//    [ComVisible(true)]
//    public sealed class ReportDesignerPane : WindowPane, IVsWindowPane
//    {
//        private readonly AsyncPackage _package;
//        private readonly string _filePath;
//        private readonly FileType _fileType;
//        private WebView2 _webView;
//        private Grid _root;
//        private TextBlock _loadingText;
//        private bool _webViewReady;

//        private IVsOutputWindowPane _outputPane;

//        // Virtual hostname – Blazor WASM MUST be served over https://, not file://
//        private const string VirtualHost = "app.reportdesigner";  // avoid .local (reserved mDNS)

//        public ReportDesignerPane(AsyncPackage package, string filePath, FileType fileType)
//            : base(package)
//        {
//            _package = package;
//            _filePath = filePath;
//            _fileType = fileType;
//        }

//        // ── WindowPane ────────────────────────────────────────────────

//        protected override void Initialize()
//        {
//            base.Initialize();

//            _root = new Grid { Background = System.Windows.Media.Brushes.Transparent };

//            _loadingText = new TextBlock
//            {
//                Text = "Loading Report Designer…",
//                HorizontalAlignment = HorizontalAlignment.Center,
//                VerticalAlignment = VerticalAlignment.Center,
//                FontSize = 14,
//                Foreground = System.Windows.Media.Brushes.Gray,
//            };
//            _root.Children.Add(_loadingText);

//            _webView = new WebView2();
//            _webView.Visibility = Visibility.Collapsed;
//            _root.Children.Add(_webView);

//            _ = InitWebViewAsync();
//        }

//        public override object Content => _root;

//        // ── WebView2 init ─────────────────────────────────────────────

//        //        private async System.Threading.Tasks.Task InitWebViewAsync()
//        //        {
//        //            try
//        //            {
//        //                // ── Step 1: Create environment ────────────────────────
//        //                var userDataFolder = Path.Combine(
//        //                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
//        //                    "ReportDesigner", "WebView2Cache");

//        //                WriteToOutputWindow($"[PANE] UserDataFolder: {userDataFolder}");

//        //                var env = await CoreWebView2Environment.CreateAsync(
//        //                    browserExecutableFolder: null,
//        //                    userDataFolder: userDataFolder);

//        //                // ── Step 2: Ensure CoreWebView2 ───────────────────────
//        //                await _webView.EnsureCoreWebView2Async(env);

//        //                //EnableConsoleCapture();
//        //                WriteToOutputWindow("[PANE] CoreWebView2 ready.");

//        //                var cw2 = _webView.CoreWebView2;

//        //                // ── Step 3: Resolve wwwroot folder ────────────────────

//        //                var bridgeScript = GetHostBridgeScript();
//        //                if (!string.IsNullOrEmpty(bridgeScript))
//        //                    await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(bridgeScript);

//        //                // ── Key fix: map virtual host BEFORE navigating ───────
//        //                // This serves DesignerApp\wwwroot\ as https://reportdesigner.local/
//        //                // Blazor WASM requires http(s):// – file:// will NOT work.

//        //                var wwwroot = GetWwwRootFolder();
//        //                WriteToOutputWindow($"[PANE] wwwroot folder: {wwwroot}");
//        //                WriteToOutputWindow($"[PANE] wwwroot exists: {Directory.Exists(wwwroot)}");

//        //                if (!Directory.Exists(wwwroot))
//        //                {
//        //                    await ShowError(
//        //                        $"DesignerApp folder not found:\n{wwwroot}\n\n" +
//        //                        "Run: dotnet publish ReportDesigner.Designer -c Release " +
//        //                        "-o <VsExtFolder>\\DesignerApp");
//        //                    return;
//        //                }

//        //                var indexPath = Path.Combine(wwwroot, "index.html");
//        //                WriteToOutputWindow($"[PANE] index.html exists: {File.Exists(indexPath)}");

//        //                var frameworkPath = Path.Combine(wwwroot, "_framework");
//        //                WriteToOutputWindow($"[PANE] _framework exists: {Directory.Exists(frameworkPath)}");

//        //                // ── Step 4: Map virtual host BEFORE navigating ────────
//        //                // This is critical – must happen before Navigate()

//        //                cw2.SetVirtualHostNameToFolderMapping(
//        //                    VirtualHost,
//        //                    wwwroot,
//        //                    CoreWebView2HostResourceAccessKind.Allow);

//        //                // Inject host bridge script before page loads
//        //                //var bridgeScript = GetHostBridgeScript();
//        //                //if (!string.IsNullOrEmpty(bridgeScript))
//        //                //    await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(bridgeScript);


//        //                WriteToOutputWindow($"[PANE] Virtual host mapped: https://{VirtualHost}/ -> {wwwroot}");

//        //                // ── Step 5: Inject host-bridge.js ─────────────────────
//        //                var bridgeJs = GetHostBridgeScript();
//        //                if (!string.IsNullOrEmpty(bridgeJs))
//        //                {
//        //                    await cw2.AddScriptToExecuteOnDocumentCreatedAsync(bridgeJs);
//        //                    WriteToOutputWindow("[PANE] host-bridge.js injected.");
//        //                }
//        //                else
//        //                {
//        //                    WriteToOutputWindow("[PANE] WARNING: host-bridge.js not found.");
//        //                }


//        //                // ── Step 6: Wire events ───────────────────────────────

//        //                cw2.WebMessageReceived += OnWebMessageReceived;
//        //                cw2.NavigationCompleted += OnNavigationCompleted;
//        //                cw2.ProcessFailed += (s, e) =>
//        //                    WriteToOutputWindow($"[PANE] PROCESS_FAILED: {e.ProcessFailedKind}");


//        //                // Console capture
//        //                _ = cw2.CallDevToolsProtocolMethodAsync("Runtime.enable", "{}");
//        //                var consoleReceiver = cw2.GetDevToolsProtocolEventReceiver(
//        //                    "Runtime.consoleAPICalled");
//        //                consoleReceiver.DevToolsProtocolEventReceived += OnConsoleMessage;

//        //                var exceptionReceiver = cw2.GetDevToolsProtocolEventReceiver(
//        //                    "Runtime.exceptionThrown");
//        //                exceptionReceiver.DevToolsProtocolEventReceived += OnJsException;

//        //                // ── Step 7: Settings ──────────────────────────────────
//        //                cw2.Settings.AreDefaultContextMenusEnabled = false;
//        //                cw2.Settings.IsStatusBarEnabled = false;
//        //                cw2.Settings.IsZoomControlEnabled = false;
//        //#if DEBUG
//        //                cw2.Settings.AreDevToolsEnabled = true;
//        //#else
//        //                cw2.Settings.AreDevToolsEnabled = false;
//        //#endif


//        //                //// 2. Filter ONLY for the absolute root domain requests (this prevents breaking assets)
//        //                //_webView.CoreWebView2.AddWebResourceRequestedFilter(
//        //                //    $"https://{VirtualHost}/*",
//        //                //    CoreWebView2WebResourceContext.Document); // Only intercept document/page loads

//        //                //// 3. Subscribe to the request event
//        //                //_webView.CoreWebView2.WebResourceRequested += OnWebResourceRequested;

//        //                // ── Step 8: Navigate to / (NOT /index.html) ───────────
//        //                var encodedPath = Uri.EscapeDataString(_filePath);
//        //                var typeLow = _fileType.ToString().ToLowerInvariant();
//        //                var url = $"https://{VirtualHost}/" +
//        //                                  $"?file={encodedPath}&type={typeLow}";

//        //                WriteToOutputWindow($"[PANE] Navigating to: {url}");
//        //                cw2.Navigate(url);

//        //                // Show WebView2
//        //                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
//        //                _loadingText.Visibility = Visibility.Collapsed;
//        //                _webView.Visibility = Visibility.Visible;
//        //                _webViewReady = true;
//        //            }
//        //            catch (Exception ex)
//        //            {
//        //                WriteToOutputWindow($"[PANE] INIT ERROR: {ex}");
//        //                await ShowError($"Failed to initialise WebView2:\n{ex.Message}");
//        //            }
//        //        }

//        // ── Navigation completed ──────────────────────────────────────


//        // Replace the entire InitWebViewAsync method in ReportDesignerPane.cs



//        private async System.Threading.Tasks.Task InitWebViewAsync()
//        {
//            try
//            {
//                // ALL WebView2 operations must run on the UI thread
//                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

//                var userDataFolder = Path.Combine(
//                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
//                    "ReportDesigner", "WebView2Cache");

//                WriteToOutputWindow($"[PANE] UserDataFolder: {userDataFolder}");

//                // Get or create shared environment
//                var env = await GetOrCreateSharedEnvironmentAsync(userDataFolder);
//                WriteToOutputWindow("[PANE] Environment ready.");

//                // EnsureCoreWebView2Async MUST be called on UI thread
//                await _webView.EnsureCoreWebView2Async(env);
//                WriteToOutputWindow("[PANE] CoreWebView2 ready.");

//                var cw2 = _webView.CoreWebView2;

//                // Resolve wwwroot
//                var wwwroot = GetWwwRootFolder();
//                WriteToOutputWindow($"[PANE] wwwroot: {wwwroot}");
//                WriteToOutputWindow($"[PANE] Exists: {Directory.Exists(wwwroot)}");
//                WriteToOutputWindow($"[PANE] index.html: {File.Exists(Path.Combine(wwwroot, "index.html"))}");
//                WriteToOutputWindow($"[PANE] _framework: {Directory.Exists(Path.Combine(wwwroot, "_framework"))}");

//                if (!Directory.Exists(wwwroot))
//                {
//                    await ShowError($"DesignerApp not found:\n{wwwroot}");
//                    return;
//                }

//                // SetVirtualHostNameToFolderMapping MUST be called on UI thread
//                // and MUST be called before Navigate()
//                cw2.SetVirtualHostNameToFolderMapping(
//                    VirtualHost,
//                    wwwroot,
//                    CoreWebView2HostResourceAccessKind.Allow);

//                WriteToOutputWindow($"[PANE] Mapped https://{VirtualHost}/ -> {wwwroot}");

//                // Inject host-bridge.js before any navigation
//                var bridgeJs = GetHostBridgeScript();
//                if (!string.IsNullOrEmpty(bridgeJs))
//                {
//                    await cw2.AddScriptToExecuteOnDocumentCreatedAsync(bridgeJs);
//                    WriteToOutputWindow("[PANE] host-bridge.js injected.");
//                }
//                else
//                {
//                    WriteToOutputWindow("[PANE] WARNING: host-bridge.js not found.");
//                }

//                // Wire events
//                cw2.WebMessageReceived += OnWebMessageReceived;
//                cw2.NavigationCompleted += OnNavigationCompleted;
//                cw2.ProcessFailed += (s, e) =>
//                    WriteToOutputWindow($"[PANE] PROCESS_FAILED: {e.ProcessFailedKind}");

//                // Console capture
//                _ = cw2.CallDevToolsProtocolMethodAsync("Runtime.enable", "{}");
//                cw2.GetDevToolsProtocolEventReceiver("Runtime.consoleAPICalled")
//                   .DevToolsProtocolEventReceived += OnConsoleMessage;
//                cw2.GetDevToolsProtocolEventReceiver("Runtime.exceptionThrown")
//                   .DevToolsProtocolEventReceived += OnJsException;

//                // Settings
//                cw2.Settings.AreDefaultContextMenusEnabled = false;
//                cw2.Settings.IsStatusBarEnabled = false;
//                cw2.Settings.IsZoomControlEnabled = false;
//#if DEBUG
//                cw2.Settings.AreDevToolsEnabled = true;
//#else
//        cw2.Settings.AreDevToolsEnabled = false;
//#endif

//                // Navigate — URL uses virtual host, NOT file://
//                var encodedPath = Uri.EscapeDataString(_filePath);
//                var typeLow = _fileType.ToString().ToLowerInvariant();
//                var url = $"https://{VirtualHost}/?file={encodedPath}&type={typeLow}";

//                WriteToOutputWindow($"[PANE] Navigating to: {url}");
//                cw2.Navigate(url);

//                _loadingText.Visibility = Visibility.Collapsed;
//                _webView.Visibility = Visibility.Visible;
//                _webViewReady = true;
//            }
//            catch (Exception ex)
//            {
//                WriteToOutputWindow($"[PANE] INIT ERROR: {ex}");
//                await ShowError($"Failed to initialise WebView2:\n{ex.Message}");
//            }
//        }

//        // Shared environment across all panes
//        private static CoreWebView2Environment _sharedEnv;
//        private static readonly SemaphoreSlim _envLock = new SemaphoreSlim(1, 1);

//        private static async System.Threading.Tasks.Task<CoreWebView2Environment>
//            GetOrCreateSharedEnvironmentAsync(string userDataFolder)
//        {
//            await _envLock.WaitAsync();
//            try
//            {
//                if (_sharedEnv == null)
//                {
//                    _sharedEnv = await CoreWebView2Environment.CreateAsync(
//                        browserExecutableFolder: null,
//                        userDataFolder: userDataFolder);
//                }
//                return _sharedEnv;
//            }
//            finally
//            {
//                _envLock.Release();
//            }
//        }

//        private void OnNavigationCompleted(object sender,
//            CoreWebView2NavigationCompletedEventArgs e)
//        {
//            if (!e.IsSuccess)
//            {
//                WriteToOutputWindow(
//                    $"[PANE] NAV_ERROR: {e.WebErrorStatus} url={_webView.CoreWebView2.Source}");
//                return;
//            }

//            WriteToOutputWindow($"[PANE] NAV_OK: {_webView.CoreWebView2.Source}");

//            // Poll for Blazor boot on a background task
//            _ = PollForBlazorAsync();
//        }

//        private async System.Threading.Tasks.Task PollForBlazorAsync()
//        {
//            var cw2 = _webView.CoreWebView2;

//            for (int i = 0; i < 60; i++)
//            {
//                await System.Threading.Tasks.Task.Delay(500);

//                try
//                {
//                    var ready = await cw2.ExecuteScriptAsync(
//                        "typeof window.Blazor !== 'undefined'");

//                    WriteToOutputWindow($"[PANE] Poll {i + 1}: blazor={ready}");

//                    if (ready == "true")
//                    {
//                        WriteToOutputWindow("[PANE] Blazor ready. Pushing file content.");
//                        await System.Threading.Tasks.Task.Delay(300);
//                        PushFileContent();
//                        return;
//                    }
//                }
//                catch (Exception ex)
//                {
//                    WriteToOutputWindow($"[PANE] Poll error: {ex.Message}");
//                }
//            }

//            WriteToOutputWindow("[PANE] ERROR: Blazor did not boot in 30s.");
//        }

//        // ── Navigation completed ──────────────────────────────────────

//        private void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
//        {
//            var requestUri = new Uri(e.Request.Uri);
//            string localPath = requestUri.AbsolutePath;

//            // Check if the request is targeting an actual physical asset file
//            bool isStaticAsset = Path.HasExtension(localPath);

//            if (!isStaticAsset)
//            {
//                var wwwroot = GetWwwRootFolder();
//                // This is your root navigation or a Blazor internal path. 
//                // We must manually serve your main launcher html file here.
//                string mainPagePath = Path.Combine(wwwroot, "index.html");

//                if (File.Exists(mainPagePath))
//                {
//                    try
//                    {
//                        FileStream fs = File.OpenRead(mainPagePath);

//                        // Return your main index shell wrapper back to the WebView
//                        e.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(
//                            fs,
//                            200,
//                            "OK",
//                            "Content-Type: text/html");
//                    }
//                    catch (Exception)
//                    {
//                        // Fallback state if stream access fails
//                    }
//                }
//            }
//            else
//            {
//                // CRITICAL: For static assets (.js, .css, images), 
//                // leaving e.Response null instructs WebView2 to drop back 
//                // to your SetVirtualHostNameToFolderMapping rules!
//                return;
//            }
//        }

//        // ── VS → Blazor ───────────────────────────────────────────────

//        public void SendFileToDesigner(string xmlContent)
//        {
//            if (!_webViewReady) return;
//            ThreadHelper.ThrowIfNotOnUIThread();

//            var escaped = EscapeForJs(xmlContent);
//            _ = _webView.CoreWebView2.ExecuteScriptAsync(
//                $"window.rdHostBridge?.loadContent(\"{escaped}\");");
//        }

//        private void PushFileContent()
//        {
//            if (!File.Exists(_filePath))
//            {
//                WriteToOutputWindow($"[PANE] File not found: {_filePath}");
//                return;
//            }

//            var xml = File.ReadAllText(_filePath, System.Text.Encoding.UTF8);
//            var escaped = EscapeForJs(xml);
//            _ = _webView.CoreWebView2.ExecuteScriptAsync(
//                $"window.rdHostBridge?.loadContent(\"{escaped}\");");

//            WriteToOutputWindow($"[PANE] XML pushed ({xml.Length} chars).");
//        }

//        // ── Blazor → VS messages ──────────────────────────────────────

//        private void OnWebMessageReceived(object sender,
//            CoreWebView2WebMessageReceivedEventArgs e)
//        {
//            ThreadHelper.ThrowIfNotOnUIThread();
//            try
//            {
//                var json = e.TryGetWebMessageAsString();
//                WriteToOutputWindow($"[PANE] MSG: {json}");

//                var msg = JsonSerializer.Deserialize<HostMessage>(json,
//                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
//                if (msg is null) return;

//                switch (msg.Type?.ToLowerInvariant())
//                {
//                    case "ready":
//                        // Also push on ready signal (belt and suspenders)
//                        PushFileContent();
//                        break;
//                    case "dirty":
//                        UpdateDirtyState(msg.Value == "true");
//                        break;
//                    case "save":
//                        SaveFile(msg.Value);
//                        break;
//                    case "openfile":
//                        OpenCompanionFile(msg.Value);
//                        break;
//                }
//            }
//            catch { }
//        }

//        // ── Console / exception capture ───────────────────────────────

//        private void OnConsoleMessage(object sender,
//            CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
//        {
//            try
//            {
//                var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
//                var root = doc.RootElement;
//                var type = root.TryGetProperty("type", out var t) ? t.GetString() : "log";
//                var sb = new System.Text.StringBuilder();

//                if (root.TryGetProperty("args", out var args) &&
//                    args.ValueKind == JsonValueKind.Array)
//                {
//                    foreach (var arg in args.EnumerateArray())
//                    {
//                        if (arg.TryGetProperty("value", out var v))
//                            sb.Append(v).Append(' ');
//                        else if (arg.TryGetProperty("description", out var d))
//                            sb.Append(d.GetString()).Append(' ');
//                    }
//                }

//                WriteToOutputWindow($"[JS:{type?.ToUpper()}] {sb.ToString().Trim()}");
//            }
//            catch { }
//        }

//        private void OnJsException(object sender,
//            CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
//        {
//            try
//            {
//                var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
//                var root = doc.RootElement;
//                var msg = "Unknown";

//                if (root.TryGetProperty("exceptionDetails", out var details))
//                {
//                    if (details.TryGetProperty("text", out var text))
//                        msg = text.GetString();
//                    if (details.TryGetProperty("exception", out var ex) &&
//                        ex.TryGetProperty("description", out var desc))
//                        msg = desc.GetString();
//                }

//                WriteToOutputWindow($"[JS:EXCEPTION] {msg}");
//            }
//            catch { }
//        }

//        // ── Save ──────────────────────────────────────────────────────

//        private void SaveFile(string xmlContent)
//        {
//            ThreadHelper.ThrowIfNotOnUIThread();
//            try
//            {
//                if (!string.IsNullOrWhiteSpace(xmlContent))
//                {
//                    File.WriteAllText(_filePath, xmlContent, System.Text.Encoding.UTF8);
//                    UpdateDirtyState(false);
//                    _ = _webView.CoreWebView2.ExecuteScriptAsync(
//                        "window.rdHostBridge?.onSaved();");
//                }
//            }
//            catch (Exception ex)
//            {
//                VsShellUtilities.ShowMessageBox(_package,
//                     $"Save failed: {ex.Message}", "Report Designer",
//                     OLEMSGICON.OLEMSGICON_WARNING,
//                     OLEMSGBUTTON.OLEMSGBUTTON_OK,
//                     OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
//                WriteToOutputWindow($"[PANE] Save error: {ex.Message}");
//            }
//        }

//        private void OpenCompanionFile(string fileType)
//        {
//            ThreadHelper.ThrowIfNotOnUIThread();
//            var filter = fileType?.ToLower() == "rds"
//                ? "Dataset Schema (*.rds)|*.rds|All Files|*.*"
//                : "Report Definition (*.rdx)|*.rdx|All Files|*.*";

//            var dlg = new Microsoft.Win32.OpenFileDialog
//            {
//                Filter = filter,
//                InitialDirectory = Path.GetDirectoryName(_filePath) ?? string.Empty,
//            };

//            if (dlg.ShowDialog() != true) return;

//            var content = File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
//            _ = _webView.CoreWebView2.ExecuteScriptAsync(
//                $"window.rdHostBridge?.loadCompanionFile(" +
//                $"\"{EscapeForJs(dlg.FileName)}\", \"{EscapeForJs(content)}\");");
//        }

//        private void UpdateDirtyState(bool dirty)
//        {
//            ThreadHelper.ThrowIfNotOnUIThread();
//            try
//            {
//                if (GetService(typeof(SVsRunningDocumentTable))
//                        is IVsRunningDocumentTable rdt)
//                {
//                    rdt.FindAndLockDocument(
//                        (uint)_VSRDTFLAGS.RDT_NoLock, _filePath,
//                        out _, out _, out _, out var cookie);

//                    if (cookie != VSConstants.VSCOOKIE_NIL && dirty)
//                        rdt.ModifyDocumentFlags(
//                            cookie, (uint)_VSRDTFLAGS.RDT_DontSave, 0);
//                }
//            }
//            catch { }
//        }

//        // ── Folder resolution ─────────────────────────────────────────

//        private static string GetWwwRootFolder()
//        {
//            var extDir = Path.GetDirectoryName(
//                Assembly.GetExecutingAssembly().Location) ?? string.Empty;

//            // Priority 1: DesignerApp\ contains index.html directly
//            // (dotnet publish -o DesignerApp puts wwwroot contents here)
//            var direct = Path.Combine(extDir, "DesignerApp");
//            if (File.Exists(Path.Combine(direct, "index.html")))
//                return direct;

//            // Priority 2: DesignerApp\wwwroot\
//            var sub = Path.Combine(extDir, "DesignerApp", "wwwroot");
//            if (File.Exists(Path.Combine(sub, "index.html")))
//                return sub;

//            return direct;
//        }

//        //private static string GetHostBridgeScript()
//        //{
//        //    try
//        //    {
//        //        var extDir = Path.GetDirectoryName(
//        //            Assembly.GetExecutingAssembly().Location) ?? string.Empty;
//        //        var jsPath = Path.Combine(extDir, "Templates", "host-bridge.js");
//        //        return File.Exists(jsPath) ? File.ReadAllText(jsPath) : string.Empty;
//        //    }
//        //    catch { return string.Empty; }
//        //}

//        private static string GetHostBridgeScript()
//        {
//            try
//            {
//                var extDir = Path.GetDirectoryName(
//                    Assembly.GetExecutingAssembly().Location) ?? string.Empty;
//                var path = Path.Combine(extDir, "Templates", "host-bridge.js");

//                // Temporary debug – remove after confirming
//                //System.Diagnostics.Debug.WriteLine($"[HostBridge] Looking for: {jsPath}");
//                //System.Diagnostics.Debug.WriteLine($"[HostBridge] Exists: {File.Exists(jsPath)}");

//                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
//            }
//            catch { return string.Empty; }
//        }

//        // Add this method to ReportDesignerPane.cs
//        // Call it inside InitWebViewAsync() after EnsureCoreWebView2Async()
//        // i.e. right after:  await _webView.EnsureCoreWebView2Async(env);

//        private void EnableConsoleCapture()
//        {
//            var cw2 = _webView.CoreWebView2;

//            // ── Subscribe to console events via CDP ───────────────────────────
//            _ = cw2.CallDevToolsProtocolMethodAsync("Runtime.enable", "{}");

//            var consoleReceiver = cw2.GetDevToolsProtocolEventReceiver("Runtime.consoleAPICalled");
//            consoleReceiver.DevToolsProtocolEventReceived += (s, e) =>
//            {
//                try
//                {
//                    var doc = System.Text.Json.JsonDocument.Parse(e.ParameterObjectAsJson);
//                    var root = doc.RootElement;

//                    var type = root.TryGetProperty("type", out var t) ? t.GetString() : "log";
//                    var sb = new System.Text.StringBuilder();

//                    if (root.TryGetProperty("args", out var args) &&
//                        args.ValueKind == System.Text.Json.JsonValueKind.Array)
//                    {
//                        foreach (var arg in args.EnumerateArray())
//                        {
//                            if (arg.TryGetProperty("value", out var v))
//                                sb.Append(v.ToString()).Append(' ');
//                            else if (arg.TryGetProperty("description", out var d))
//                                sb.Append(d.GetString()).Append(' ');
//                        }
//                    }

//                    WriteToOutputWindow($"[WebView2:{type?.ToUpper()}] {sb.ToString().Trim()}");
//                }
//                catch { }
//            };

//            // ── Also capture unhandled JS exceptions ──────────────────────────
//            _ = cw2.CallDevToolsProtocolMethodAsync("Runtime.setAsyncCallStackDepth",
//                    "{\"maxDepth\":32}");

//            var exceptionReceiver = cw2.GetDevToolsProtocolEventReceiver(
//                                        "Runtime.exceptionThrown");
//            exceptionReceiver.DevToolsProtocolEventReceived += (s, e) =>
//            {
//                try
//                {
//                    var doc = System.Text.Json.JsonDocument.Parse(e.ParameterObjectAsJson);
//                    var root = doc.RootElement;
//                    var msg = "Unknown exception";

//                    if (root.TryGetProperty("exceptionDetails", out var details))
//                    {
//                        if (details.TryGetProperty("text", out var text))
//                            msg = text.GetString();
//                        if (details.TryGetProperty("exception", out var ex) &&
//                            ex.TryGetProperty("description", out var desc))
//                            msg = desc.GetString();
//                    }

//                    WriteToOutputWindow($"[WebView2:EXCEPTION] {msg}");
//                }
//                catch { }
//            };

//            // ── Navigation result ─────────────────────────────────────────────
//            //cw2.NavigationCompleted += (s, e) =>
//            //{
//            //    var msg = e.IsSuccess
//            //        ? $"[WebView2:NAV_OK] {cw2.Source}"
//            //        : $"[WebView2:NAV_ERROR] Status={e.WebErrorStatus} URL={cw2.Source}";
//            //    WriteToOutputWindow(msg);
//            //};

//            // In InitWebViewAsync(), REPLACE the existing NavigationCompleted handler
//            // with this diagnostic version. Remove after debugging is done.

//            // Replace the NavigationCompleted handler in InitWebViewAsync() with this:

//            cw2.NavigationCompleted += async (s, e) =>
//            {
//                if (!e.IsSuccess)
//                {
//                    WriteToOutputWindow($"[WebView2:NAV_ERROR] Status={e.WebErrorStatus}");
//                    return;
//                }

//                WriteToOutputWindow($"[WebView2:NAV_OK] {cw2.Source}");

//                // Add this AFTER NavigationCompleted fires (inside the handler, after NAV_OK)
//                // to check if _framework files are actually accessible

//                var frameworkCheck = await _webView.CoreWebView2.ExecuteScriptAsync("""
//    (async function() {
//        var results = {};

//        // Check if key _framework files are fetchable
//        var files = [
//            '_framework/blazor.webassembly.js',
//            '_framework/blazor.boot.json',
//        ];

//        for (var f of files) {
//            try {
//                var resp = await fetch('https://reportdesigner.local/' + f);
//                results[f] = resp.status + ' ' + resp.statusText;
//            } catch(e) {
//                results[f] = 'FETCH_ERROR: ' + e.message;
//            }
//        }

//        return JSON.stringify(results);
//    })()
//    """);

//                WriteToOutputWindow($"[WebView2:FRAMEWORK_CHECK] {frameworkCheck}");

//                // Poll until Blazor WASM has fully booted (up to 30 seconds)
//                // Blazor sets window.Blazor after the WASM initialises
//                var blazorReady = false;
//                for (int i = 0; i < 60; i++)
//                {
//                    await Task.Delay(500);

//                    var check = await _webView.CoreWebView2.ExecuteScriptAsync(
//                        "typeof window.Blazor !== 'undefined'");

//                    WriteToOutputWindow($"[WebView2:POLL] attempt={i + 1} blazorReady={check}");

//                    if (check == "true")
//                    {
//                        blazorReady = true;
//                        break;
//                    }
//                }

//                if (!blazorReady)
//                {
//                    WriteToOutputWindow("[WebView2:ERROR] Blazor did not boot within 30s.");

//                    // Dump what went wrong
//                    var info = await _webView.CoreWebView2.ExecuteScriptAsync("""
//            JSON.stringify({
//                appContent: (document.getElementById('app') || {}).innerHTML?.substring(0, 500),
//                errors:     window.__blazorErrors || 'none',
//            })
//            """);
//                    WriteToOutputWindow($"[WebView2:BOOT_FAIL] {info}");
//                    return;
//                }

//                WriteToOutputWindow("[WebView2:OK] Blazor is ready.");

//                // Give DotNet interop one more moment to register [JSInvokable] methods
//                await Task.Delay(500);

//                // Now push the file content into the designer
//                if (File.Exists(_filePath))
//                {
//                    var xml = File.ReadAllText(_filePath, System.Text.Encoding.UTF8);
//                    var escaped = EscapeForJs(xml);
//                    await _webView.CoreWebView2.ExecuteScriptAsync(
//                        $"window.rdHostBridge?.loadContent(\"{escaped}\");");
//                    WriteToOutputWindow($"[WebView2:SENT] XML pushed to designer ({xml.Length} chars)");
//                }
//                else
//                {
//                    WriteToOutputWindow($"[WebView2:WARN] File not found: {_filePath}");
//                }
//            };



//            // ── Process crash ─────────────────────────────────────────────────
//            cw2.ProcessFailed += (s, e) =>
//                WriteToOutputWindow($"[WebView2:PROCESS_FAILED] Kind={e.ProcessFailedKind}");
//        }


//        // ── Utilities ─────────────────────────────────────────────────

//        private static string EscapeForJs(string s) =>
//            (s ?? string.Empty)
//                .Replace("\\", "\\\\")
//                .Replace("\"", "\\\"")
//                .Replace("\r\n", "\\n")
//                .Replace("\n", "\\n")
//                .Replace("\r", "\\n");

//        private async System.Threading.Tasks.Task ShowError(string message)
//        {
//            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
//            _loadingText.Text = message;
//            _loadingText.Visibility = Visibility.Visible;
//            _webView.Visibility = Visibility.Collapsed;
//        }

//        // ── VS Output window ──────────────────────────────────────────

//        private void WriteToOutputWindow(string message)
//        {
//            System.Diagnostics.Debug.WriteLine(message);
//            ThreadHelper.JoinableTaskFactory.Run(async () =>
//            {
//                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
//                try
//                {
//                    if (_outputPane == null)
//                    {
//                        var ow = ServiceProvider.GlobalProvider
//                            .GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
//                        var g = new Guid("A1B2C3D4-9999-0000-0000-000000000001");
//                        ow?.CreatePane(ref g, "Report Designer", 1, 1);
//                        ow?.GetPane(ref g, out _outputPane);
//                        _outputPane?.Activate();
//                    }
//                    _outputPane?.OutputStringThreadSafe(message + Environment.NewLine);
//                }
//                catch { }
//            });
//        }

//        protected override void Dispose(bool disposing)
//        {
//            if (disposing) _webView?.Dispose();
//            base.Dispose(disposing);
//        }
//    }

//    internal sealed class HostMessage
//    {
//        public string Type { get; set; }
//        public string Value { get; set; }
//    }
//}