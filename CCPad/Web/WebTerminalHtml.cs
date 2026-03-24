namespace CCPad.Web
{
    internal static class WebTerminalHtml
    {
        public static string GetHtml(string token) => $$"""
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no"/>
  <title>CC Pad — Remote Terminal</title>
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    html, body { width: 100%; height: 100%; background: #0c0c0c; overflow: hidden; font-family: 'Segoe UI', sans-serif; }

    #app { display: flex; width: 100%; height: 100%; overflow: hidden; }

    /* Sidebar */
    #sidebar {
      width: 220px; min-width: 220px; height: 100%;
      background: #181818; border-right: 1px solid #333;
      display: flex; flex-direction: column;
      transition: margin-left 0.2s; flex-shrink: 0;
    }
    #sidebar.collapsed { margin-left: -220px; }
    #sidebar-header {
      padding: 12px 14px; font-size: 13px; font-weight: 600; color: #ccc;
      border-bottom: 1px solid #333;
      display: flex; justify-content: space-between; align-items: center;
    }
    #sidebar-header button {
      background: none; border: none; color: #888; cursor: pointer; font-size: 16px; padding: 2px 6px;
    }
    #sidebar-header button:hover { color: #fff; }
    #session-list { flex: 1; overflow-y: auto; padding: 6px 0; }
    .session-item {
      padding: 10px 14px; cursor: pointer; border-left: 3px solid transparent;
      transition: background 0.15s;
    }
    .session-item:hover { background: #252525; }
    .session-item.active { background: #333; border-left-color: #888; }
    .session-label { font-size: 13px; color: #ddd; font-weight: 500; }
    .session-dir { font-size: 11px; color: #666; margin-top: 2px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .no-sessions { padding: 20px 14px; color: #555; font-size: 12px; text-align: center; }

    /* Toggle button */
    #toggle-btn {
      position: fixed; left: 6px; top: 6px; z-index: 10;
      background: #252525; border: 1px solid #444; color: #aaa;
      cursor: pointer; font-size: 14px; padding: 4px 8px; border-radius: 4px;
      display: none;
    }
    #toggle-btn:hover { background: #333; color: #fff; }
    #sidebar.collapsed ~ #toggle-btn { display: block; }

    /* Terminal area — strictly fills remaining space, no overflow */
    #terminal-wrap {
      flex: 1; min-width: 0;
      display: flex; flex-direction: column;
      height: 100%; overflow: hidden;
    }
    #terminal {
      flex: 1;
      overflow: hidden;
    }

    /* Status bar — fixed height at bottom */
    #status-bar {
      height: 26px; min-height: 26px; line-height: 26px;
      background: #1e1e1e; color: #888;
      font-size: 12px; padding: 0 12px;
      display: flex; justify-content: space-between; align-items: center;
      border-top: 1px solid #333; flex-shrink: 0;
    }
    .connected { color: #aaa; }
    .disconnected { color: #666; }
    .connecting { color: #888; }

    /* Touch pad — fixed bottom-right, container transparent to touch */
    #touch-pad {
      position: fixed; right: 16px; bottom: 36px; z-index: 20;
      opacity: 0.4; transition: opacity 0.2s;
      pointer-events: none;
    }
    #touch-pad:hover, #touch-pad:active { opacity: 0.85; }
    #dpad {
      display: grid;
      grid-template-columns: 44px 44px 44px;
      grid-template-rows: 44px 44px;
      gap: 3px;
    }
    .dpad-btn {
      width: 44px; height: 44px;
      background: #333; border: 1px solid #555; border-radius: 6px;
      color: #ccc; font-size: 18px;
      cursor: pointer; display: flex; align-items: center; justify-content: center;
      user-select: none; -webkit-user-select: none;
      touch-action: manipulation;
      pointer-events: auto;
    }
    .dpad-btn:active { background: #555; }
    .dpad-btn.bksp  { grid-column: 1; grid-row: 1; font-size: 15px; }
    .dpad-btn.up    { grid-column: 2; grid-row: 1; }
    .dpad-btn.enter { grid-column: 3; grid-row: 1; font-size: 15px; }
    .dpad-btn.left  { grid-column: 1; grid-row: 2; }
    .dpad-btn.down  { grid-column: 2; grid-row: 2; }
    .dpad-btn.right { grid-column: 3; grid-row: 2; }

    ::-webkit-scrollbar { width: 8px; }
    ::-webkit-scrollbar-track { background: #1e1e1e; }
    ::-webkit-scrollbar-thumb { background: #424242; border-radius: 4px; }
    ::-webkit-scrollbar-thumb:hover { background: #555; }
  </style>
  <link rel="stylesheet" href="/xterm/xterm.css"/>
</head>
<body>
  <div id="app">
    <div id="sidebar">
      <div id="sidebar-header">
        <span>CC Pad Remote</span>
        <button onclick="toggleSidebar()" title="折叠">✕</button>
      </div>
      <div id="session-list"></div>
    </div>
    <button id="toggle-btn" onclick="toggleSidebar()" title="展开侧边栏">☰</button>
    <div id="terminal-wrap">
      <div id="terminal"></div>
      <div id="status-bar">
        <span id="status">Connecting...</span>
        <span id="session-info"></span>
      </div>
    </div>
  </div>

  <!-- Touch controls -->
  <div id="touch-pad">
    <div id="dpad">
      <button class="dpad-btn bksp" onmousedown="event.preventDefault();sendKey('\x7f')" ontouchend="sendKey('\x7f')" ontouchstart="event.preventDefault()">⌫</button>
      <button class="dpad-btn up" onmousedown="event.preventDefault();sendKey('\x1b[A')" ontouchend="sendKey('\x1b[A')" ontouchstart="event.preventDefault()">▲</button>
      <button class="dpad-btn enter" onmousedown="event.preventDefault();sendKey('\r')" ontouchend="sendKey('\r')" ontouchstart="event.preventDefault()">⏎</button>
      <button class="dpad-btn left" onmousedown="event.preventDefault();sendKey('\x1b[D')" ontouchend="sendKey('\x1b[D')" ontouchstart="event.preventDefault()">◀</button>
      <button class="dpad-btn down" onmousedown="event.preventDefault();sendKey('\x1b[B')" ontouchend="sendKey('\x1b[B')" ontouchstart="event.preventDefault()">▼</button>
      <button class="dpad-btn right" onmousedown="event.preventDefault();sendKey('\x1b[C')" ontouchend="sendKey('\x1b[C')" ontouchstart="event.preventDefault()">▶</button>
    </div>
  </div>

  <script src="/xterm/xterm.js"></script>
  <script src="/xterm/xterm-addon-fit.js"></script>
  <script>
    const TOKEN = '{{token}}';
    const statusEl = document.getElementById('status');
    const sessionInfoEl = document.getElementById('session-info');
    const sessionListEl = document.getElementById('session-list');
    let ws, currentSessionId = null, sessions = [];
    let lockedCols = 0, lockedRows = 0;

    const term = new Terminal({
      fontFamily: "'Cascadia Code', 'Cascadia Mono', Consolas, monospace",
      fontSize: 14, lineHeight: 1.2,
      theme: { background: '#0c0c0c' },
      cursorBlink: true, allowProposedApi: true
    });
    const fit = new FitAddon.FitAddon();
    term.loadAddon(fit);
    term.open(document.getElementById('terminal'));
    fit.fit();

    function setStatus(text, cls) { statusEl.textContent = text; statusEl.className = cls; }

    function toggleSidebar() {
      document.getElementById('sidebar').classList.toggle('collapsed');
      if (!lockedCols) setTimeout(() => fit.fit(), 250);
    }

    async function refreshSessions() {
      try {
        const res = await fetch(`/api/sessions?token=${TOKEN}`);
        if (!res.ok) return;
        sessions = await res.json();
        renderSessionList();
      } catch {}
    }

    function renderSessionList() {
      if (sessions.length === 0) {
        sessionListEl.innerHTML = '<div class="no-sessions">没有活跃的终端会话</div>';
        return;
      }
      sessionListEl.innerHTML = sessions.map(s => `
        <div class="session-item ${s.id === currentSessionId ? 'active' : ''}"
             onclick="attachSession('${s.id}')" title="${s.workingDir || ''}">
          <div class="session-label">${escHtml(s.label || s.command)}</div>
          <div class="session-dir">${escHtml(s.workingDir ? s.workingDir.split('\\\\').pop() : '')}</div>
        </div>
      `).join('');
    }

    function escHtml(s) {
      return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
    }

    function attachSession(id) {
      if (id === currentSessionId) return;
      currentSessionId = id;
      term.reset();
      lockedCols = 0;
      lockedRows = 0;
      if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ type: 'attach', sessionId: id }));
      }
      const s = sessions.find(x => x.id === id);
      sessionInfoEl.textContent = s ? (s.label || s.command) : '';
      renderSessionList();
      term.focus();
    }

    function connect() {
      const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
      ws = new WebSocket(`${proto}//${location.host}/ws?token=${TOKEN}`);
      setStatus('Connecting...', 'connecting');

      ws.onopen = () => {
        setStatus('Connected', 'connected');
        refreshSessions().then(() => {
          if (sessions.length > 0 && !currentSessionId) {
            attachSession(sessions[0].id);
          } else if (currentSessionId) {
            ws.send(JSON.stringify({ type: 'attach', sessionId: currentSessionId }));
          }
        });
      };

      ws.onmessage = (e) => {
        const msg = JSON.parse(e.data);
        if (msg.type === 'output') {
          const bin = atob(msg.data);
          const bytes = new Uint8Array(bin.length);
          for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
          term.write(bytes);
        } else if (msg.type === 'attached') {
          currentSessionId = msg.sessionId;
          renderSessionList();
          term.focus();
        } else if (msg.type === 'size') {
          lockedCols = msg.cols;
          lockedRows = msg.rows;
          term.resize(msg.cols, msg.rows);
          const s = sessions.find(x => x.id === currentSessionId);
          sessionInfoEl.textContent = (s ? (s.label || s.command) : '') + ` [${msg.cols}x${msg.rows}]`;
        } else if (msg.type === 'replay') {
          term.reset();
          const bin = atob(msg.data);
          const bytes = new Uint8Array(bin.length);
          for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
          term.write(bytes);
        } else if (msg.type === 'error') {
          term.write('\r\n\x1b[31m' + (msg.message || 'Error') + '\x1b[0m\r\n');
        }
      };

      ws.onclose = () => {
        setStatus('Disconnected — reconnecting...', 'disconnected');
        setTimeout(connect, 2000);
      };
      ws.onerror = () => ws.close();
    }

    function sendKey(seq) {
      if (ws && ws.readyState === WebSocket.OPEN)
        ws.send(JSON.stringify({ type: 'input', data: seq }));
    }

    term.onData(data => {
      if (ws && ws.readyState === WebSocket.OPEN)
        ws.send(JSON.stringify({ type: 'input', data }));
    });

    new ResizeObserver(() => {
      if (!lockedCols) fit.fit();
    }).observe(document.getElementById('terminal'));
    term.focus();
    connect();

    setInterval(refreshSessions, 3000);
  </script>
</body>
</html>
""";
    }
}
