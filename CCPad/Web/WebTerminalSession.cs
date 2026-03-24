using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CCPad.Web
{
    /// <summary>
    /// Mirrors an existing desktop terminal session over a WebSocket connection.
    /// Subscribes to the ConPtySession's OutputReceived event and forwards input back.
    /// </summary>
    internal class WebTerminalSession : IDisposable
    {
        private WebSocket? _socket;
        private SessionEntry? _entry;
        private volatile bool _disposed;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public string? AttachedSessionId { get; private set; }

        /// <summary>
        /// Run the WebSocket read loop. The client sends messages to select a session,
        /// provide input, or resize.
        /// </summary>
        public async Task RunAsync(WebSocket socket, CancellationToken ct)
        {
            _socket = socket;
            var buffer = new byte[8192];

            try
            {
                while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    if (result.MessageType != WebSocketMessageType.Text) continue;

                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleClientMessage(json);
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            finally
            {
                Detach();
            }
        }

        private void HandleClientMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var type = doc.RootElement.GetProperty("type").GetString() ?? "";

                switch (type)
                {
                    case "attach":
                        var sessionId = doc.RootElement.GetProperty("sessionId").GetString() ?? "";
                        AttachTo(sessionId);
                        break;

                    case "input":
                        var data = doc.RootElement.GetProperty("data").GetString() ?? "";
                        _entry?.WriteInput(data);
                        break;

                    case "resize":
                        // Resize is intentionally NOT forwarded to the desktop session
                        // because it would affect the desktop user's terminal size.
                        // The web client adapts to whatever size the desktop session uses.
                        break;
                }
            }
            catch { }
        }

        private void AttachTo(string sessionId)
        {
            Detach();

            var entry = TerminalSessionRegistry.Get(sessionId);
            if (entry == null)
            {
                _ = SendAsync("{\"type\":\"error\",\"message\":\"Session not found\"}");
                return;
            }

            _entry = entry;
            AttachedSessionId = sessionId;

            // 1. Send terminal size so web client locks to desktop dimensions
            _ = SendAsync($"{{\"type\":\"size\",\"cols\":{entry.Cols},\"rows\":{entry.Rows}}}");

            // 2. Replay buffered output so web client can see current TUI state
            var snapshot = entry.Buffer.GetSnapshot();
            if (snapshot.Length > 0)
            {
                var b64 = Convert.ToBase64String(snapshot);
                _ = SendAsync($"{{\"type\":\"replay\",\"data\":\"{b64}\"}}");
            }

            // 3. Subscribe to live output AFTER replay
            entry.Session.OutputReceived += OnOutput;

            // 4. Confirm attachment
            _ = SendAsync("{\"type\":\"attached\",\"sessionId\":\"" + sessionId + "\"}");
        }

        private void Detach()
        {
            if (_entry != null)
            {
                _entry.Session.OutputReceived -= OnOutput;
                _entry = null;
                AttachedSessionId = null;
            }
        }

        private void OnOutput(byte[] data)
        {
            if (_disposed || _socket?.State != WebSocketState.Open) return;
            var b64 = Convert.ToBase64String(data);
            _ = SendAsync($"{{\"type\":\"output\",\"data\":\"{b64}\"}}");
        }

        private async Task SendAsync(string message)
        {
            if (_disposed || _socket?.State != WebSocketState.Open) return;
            await _sendLock.WaitAsync();
            try
            {
                if (_socket?.State != WebSocketState.Open) return;
                var bytes = Encoding.UTF8.GetBytes(message);
                await _socket!.SendAsync(new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch { }
            finally
            {
                _sendLock.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Detach();
            _sendLock.Dispose();
            if (_socket?.State == WebSocketState.Open)
            {
                try { _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "disposed",
                    CancellationToken.None).Wait(2000); } catch { }
            }
        }
    }
}
