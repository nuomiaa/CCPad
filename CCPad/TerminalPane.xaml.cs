using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using CCPad.Terminal;

namespace CCPad
{
    public sealed partial class TerminalPane : UserControl, IDisposable
    {
        private string _command = "cmd.exe /k claude";
        private string? _workingDir;
        private ConPtySession? _session;
        private bool _disposed;
        private bool _awaitingRestart;
        private bool _ready;
        private bool _sessionPending;
        private bool _focusOnFirstOutput;
        private int _cols = 120;
        private int _rows = 30;
        private TaskCompletionSource? _readyTcs;

        public event Action? NewTabRequested;
        public event Action? CloseTabRequested;
        public event Action? SplitHorizontalRequested;
        public event Action? SplitVerticalRequested;
        public event Action<string>? NavigateRequested;
        public event Action? ClosePaneRequested;
        public event Action? PaneFocused;
        public event Action<double, double>? ContextMenuRequested;

        public bool IsPrewarmed => _ready;
        public string Command => _command;
        public string? WorkingDir => _workingDir;

        public TerminalPane()
        {
            InitializeComponent();
            WebView.DefaultBackgroundColor = Windows.UI.Color.FromArgb(255, 12, 12, 12);
        }

        /// <summary>
        /// Phase 1: Initialize WebView2 and load xterm.js. No process is started.
        /// Can be called while the pane sits in a hidden container.
        /// </summary>
        public async Task PrewarmAsync()
        {
            if (!WebView.IsLoaded)
            {
                var tcs = new TaskCompletionSource();
                WebView.Loaded += (_, _) => tcs.TrySetResult();
                await tcs.Task;
            }

            await WebView.EnsureCoreWebView2Async();

            if (WebView.CoreWebView2 == null)
                throw new InvalidOperationException("WebView2 failed to initialize.");

            WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            WebView.CoreWebView2.WebMessageReceived += OnWebMessage;
            WebView.GotFocus += (_, _) => PaneFocused?.Invoke();

            string xtermFolder = GetXtermFolder();
            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "xterm.local", xtermFolder,
                CoreWebView2HostResourceAccessKind.Allow);

            _readyTcs = new TaskCompletionSource();
            WebView.CoreWebView2.NavigateToString(TerminalHtml);
            await _readyTcs.Task;
        }

        /// <summary>
        /// Phase 2: Start the ConPty session. If prewarm isn't done yet, it will
        /// start automatically once xterm reports ready.
        /// </summary>
        public void LaunchSession(string command, string? workingDir = null, bool focusOnReady = false)
        {
            _command = command;
            _workingDir = workingDir;
            _sessionPending = true;
            _focusOnFirstOutput = focusOnReady;
            if (_ready)
                StartSession();
        }

        /// <summary>
        /// Combined init for non-prewarmed path (first tab).
        /// </summary>
        public async Task InitializeAsync(string command, string? workingDir = null, bool focusOnReady = false)
        {
            _command = command;
            _workingDir = workingDir;
            _sessionPending = true;
            _focusOnFirstOutput = focusOnReady;
            await PrewarmAsync();
        }

        private static string GetXtermFolder()
        {
            try
            {
                return System.IO.Path.Combine(
                    Windows.ApplicationModel.Package.Current.InstalledLocation.Path,
                    "Assets", "xterm");
            }
            catch
            {
                return System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "xterm");
            }
        }

        private void StartSession()
        {
            _sessionPending = false;
            _session?.Dispose();
            _session = null;
            _awaitingRestart = false;
            try
            {
                _session = ConPtySession.Start(_command, _cols, _rows, _workingDir);
                _session.OutputReceived += OnOutput;
                _session.Exited += OnExited;
            }
            catch (Exception ex)
            {
                _awaitingRestart = true;
                SendOutput(Encoding.UTF8.GetBytes(
                    $"\r\n\x1b[31mFailed to start '{_command}': {ex.Message}\x1b[0m\r\n" +
                    "\x1b[33m[Press Enter to retry]\x1b[0m\r\n"));
            }
        }

        private void OnOutput(byte[] data)
        {
            SendOutput(data);
            if (_focusOnFirstOutput)
            {
                _focusOnFirstOutput = false;
                DispatcherQueue.TryEnqueue(FocusTerminal);
            }
        }

        private void OnExited()
        {
            _session = null;
            _awaitingRestart = true;
            SendOutput(Encoding.UTF8.GetBytes(
                "\r\n\x1b[33m[Process exited — press Enter to restart]\x1b[0m\r\n"));
        }

        private void SendOutput(byte[] data)
        {
            if (_disposed) return;
            string b64 = Convert.ToBase64String(data);
            string json = $"{{\"type\":\"output\",\"data\":\"{b64}\"}}";
            DispatcherQueue.TryEnqueue(() => WebView.CoreWebView2?.PostWebMessageAsString(json));
        }

        private void OnWebMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            if (_disposed) return;
            string raw = args.TryGetWebMessageAsString();
            try
            {
                var doc = JsonDocument.Parse(raw);
                string type = doc.RootElement.GetProperty("type").GetString() ?? "";

                switch (type)
                {
                    case "ready":
                        _cols = doc.RootElement.GetProperty("cols").GetInt32();
                        _rows = doc.RootElement.GetProperty("rows").GetInt32();
                        _ready = true;
                        _readyTcs?.TrySetResult();
                        if (_sessionPending)
                            StartSession();
                        break;

                    case "input":
                        string data = doc.RootElement.GetProperty("data").GetString() ?? "";
                        if (_awaitingRestart && data == "\r")
                            StartSession();
                        else
                            _session?.WriteInput(data);
                        break;

                    case "resize":
                        _cols = doc.RootElement.GetProperty("cols").GetInt32();
                        _rows = doc.RootElement.GetProperty("rows").GetInt32();
                        _session?.Resize(_cols, _rows);
                        break;

                    case "newTab":
                        DispatcherQueue.TryEnqueue(() => NewTabRequested?.Invoke());
                        break;

                    case "closeTab":
                        DispatcherQueue.TryEnqueue(() => CloseTabRequested?.Invoke());
                        break;

                    case "splitHorizontal":
                        DispatcherQueue.TryEnqueue(() => SplitHorizontalRequested?.Invoke());
                        break;

                    case "splitVertical":
                        DispatcherQueue.TryEnqueue(() => SplitVerticalRequested?.Invoke());
                        break;

                    case "navigate":
                        string dir = doc.RootElement.GetProperty("direction").GetString() ?? "";
                        DispatcherQueue.TryEnqueue(() => NavigateRequested?.Invoke(dir));
                        break;

                    case "closePane":
                        DispatcherQueue.TryEnqueue(() => ClosePaneRequested?.Invoke());
                        break;

                    case "contextMenu":
                        double cx = doc.RootElement.GetProperty("x").GetDouble();
                        double cy = doc.RootElement.GetProperty("y").GetDouble();
                        DispatcherQueue.TryEnqueue(() => ContextMenuRequested?.Invoke(cx, cy));
                        break;

                    case "copy":
                        string copyText = doc.RootElement.GetProperty("data").GetString() ?? "";
                        if (copyText.Length > 0)
                        {
                            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                            dp.SetText(copyText);
                            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
                        }
                        break;

                }
            }
            catch { }
        }

        public async void FocusTerminal()
        {
            WebView.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            if (WebView.CoreWebView2 != null)
                await WebView.CoreWebView2.ExecuteScriptAsync("term.focus()");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_session != null)
            {
                _session.OutputReceived -= OnOutput;
                _session.Exited -= OnExited;
                _session.Dispose();
                _session = null;
            }
        }

        private const string TerminalHtml = """
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8"/>
              <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                html, body { width: 100%; height: 100%; background: #0c0c0c; overflow: hidden; }
                #terminal { width: 100%; height: 100%; overflow: hidden; }
                ::-webkit-scrollbar { width: 10px; }
                ::-webkit-scrollbar-track { background: #1e1e1e; }
                ::-webkit-scrollbar-thumb { background: #424242; border-radius: 5px; }
                ::-webkit-scrollbar-thumb:hover { background: #555; }
              </style>
              <link rel="stylesheet" href="https://xterm.local/xterm.css"/>
            </head>
            <body>
              <div id="terminal"></div>
              <script src="https://xterm.local/xterm.js"></script>
              <script src="https://xterm.local/xterm-addon-fit.js"></script>
              <script>
                const term = new Terminal({
                  fontFamily: "'Cascadia Code', 'Cascadia Mono', Consolas, monospace",
                  fontSize: 14,
                  lineHeight: 1.2,
                  theme: { background: '#0c0c0c' },
                  cursorBlink: true,
                  allowProposedApi: true
                });

                const fit = new FitAddon.FitAddon();
                term.loadAddon(fit);
                term.open(document.getElementById('terminal'));
                fit.fit();

                term.attachCustomKeyEventHandler(e => {
                  if (e.type !== 'keydown') return true;
                  if (e.ctrlKey && !e.shiftKey && e.key === 'c') {
                    const sel = term.getSelection();
                    if (sel) {
                      window.chrome.webview.postMessage(JSON.stringify({ type: 'copy', data: sel }));
                      term.clearSelection();
                      return false;
                    }
                    return true;
                  }
                  if (e.ctrlKey && !e.shiftKey && e.key === 'v') {
                    return false;
                  }
                  if (e.ctrlKey && !e.shiftKey && e.key === 't') {
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'newTab' }));
                    return false;
                  }
                  if (e.ctrlKey && !e.shiftKey && e.key === 'w') {
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'closeTab' }));
                    return false;
                  }
                  if (e.altKey && e.shiftKey && e.key === '-') {
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'splitHorizontal' }));
                    return false;
                  }
                  if (e.altKey && e.shiftKey && (e.key === '=' || e.key === '+')) {
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'splitVertical' }));
                    return false;
                  }
                  if (e.altKey && !e.shiftKey && e.key.startsWith('Arrow')) {
                    const dir = e.key.replace('Arrow', '').toLowerCase();
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'navigate', direction: dir }));
                    return false;
                  }
                  if (e.ctrlKey && e.shiftKey && e.key === 'W') {
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'closePane' }));
                    return false;
                  }
                  return true;
                });

                document.addEventListener('contextmenu', e => {
                  e.preventDefault();
                  window.chrome.webview.postMessage(JSON.stringify({
                    type: 'contextMenu', x: e.screenX, y: e.screenY
                  }));
                });

                term.focus();
                window.chrome.webview.postMessage(JSON.stringify({
                  type: 'ready', cols: term.cols, rows: term.rows
                }));

                term.onData(data => {
                  window.chrome.webview.postMessage(JSON.stringify({ type: 'input', data }));
                });

                term.onResize(({ cols, rows }) => {
                  window.chrome.webview.postMessage(JSON.stringify({ type: 'resize', cols, rows }));
                });

                window.chrome.webview.addEventListener('message', e => {
                  const msg = JSON.parse(e.data);
                  if (msg.type === 'output') {
                    const bin = atob(msg.data);
                    const bytes = new Uint8Array(bin.length);
                    for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
                    term.write(bytes);
                  }
                });

                new ResizeObserver(() => fit.fit()).observe(document.getElementById('terminal'));
              </script>
            </body>
            </html>
            """;
    }
}
