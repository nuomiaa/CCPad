<p align="center">
  <img src="CCPad/Assets/Square150x150Logo.scale-200.png" width="80" alt="CC Pad logo"/>
</p>

<h1 align="center">CC Pad</h1>

<p align="center">
  A multi-session Claude Code workbench — run multiple Claude Code sessions in one window.
</p>

<p align="center">
  <a href="https://ccpad.dev">Website</a> · <a href="README.zh-CN.md">中文文档</a> · <a href="LICENSE">License (GPL-3.0)</a>
</p>

---

## Features

- **Split Panes** — Vertical and horizontal splits with draggable dividers. Navigate between panes with `Alt+Arrow` keys.
- **Tabs** — Multiple tabs per pane, reorderable, with tab prewarming for instant creation.
- **Workspaces** — Save and restore your entire layout (splits, tabs, working directories, window state) as `.ccpad-workspace` files. Auto-detects workspace files on startup.
- **Project Quick-Access** — Pin frequently-used directories for one-click new tabs.
- **Windows ConPTY** — Native pseudo-console integration. Runs any CLI tool — PowerShell, cmd, bash, python, node, git, etc.
- **xterm.js Rendering** — Full terminal emulation via xterm.js hosted in WebView2, with Cascadia Code font.
- **Mica Backdrop** — Native Windows 11 translucent material.
- **Context Menu Integration** — Right-click any folder in Explorer to open it in CC Pad.
- **File Association** — Double-click `.ccpad-workspace` files to open them directly.

## Screenshots

<!-- TODO: Add screenshots -->

## Installation

### Installer (Recommended)

Download the latest `CCPad-Setup-x64.exe` from the [Releases](../../releases) page and run it.

### Build from Source

**Prerequisites:**
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Windows App SDK 1.8+](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
- Windows 10 (Build 17763) or later

```bash
# Clone
git clone https://github.com/nuomiaa/CCPad.git
cd CCPad

# Build (Debug)
dotnet build CCPad/CCPad.csproj

# Build (Release, x64)
dotnet publish CCPad/CCPad.csproj -c Release -r win-x64

# Build installer (requires Inno Setup 6)
build.bat
```

Supported targets: `win-x64`, `win-x86`, `win-arm64`.

## Usage

### Launch

```bash
# Open in current directory
CCPad.exe

# Open a specific folder
CCPad.exe "C:\Projects\my-app"

# Open a workspace file
CCPad.exe my-project.ccpad-workspace
```

When launched without arguments, CC Pad auto-detects `.ccpad-workspace` files in the current directory and enters workspace mode.

### Keyboard Shortcuts

| Action | Shortcut |
|--------|----------|
| New tab | `Ctrl+T` |
| Close tab | `Ctrl+W` |
| Copy selection | `Ctrl+C` |
| Split right | `Alt+Shift+=` |
| Split down | `Alt+Shift+-` |
| Navigate panes | `Alt+Arrow Keys` |
| Close pane | `Ctrl+Shift+W` |

Right-click the terminal or a tab header for additional options.

### Workspaces

Workspaces save your complete layout as a JSON file:

- **Split layout** — Pane tree structure with orientations and ratios
- **Tab states** — Name and working directory for each tab
- **Window state** — Size, position, and maximized state

Use the workspace button (top-right, visible in workspace mode) or the context menu to save/load workspaces. The default filename is the current directory name.

### Projects

Click the **Projects** button in any tab strip footer to manage pinned directories. Adding a project makes it available as a quick-launch option across all panes.

## Architecture

```
CCPad/
├── App.xaml.cs              # Entry point, startup, context menu registration
├── MainWindow.xaml.cs       # Window management, workspace mode
├── SplitHost.xaml.cs        # Binary split-tree layout engine
├── TabPanel.xaml.cs         # Tab lifecycle, project menu
├── TerminalPane.xaml.cs     # WebView2 + xterm.js host
├── Terminal/
│   ├── ConPtySession.cs     # Windows ConPTY process management
│   └── PseudoConsoleApi.cs  # P/Invoke bindings for ConPTY
├── Controls/
│   └── GridSplitter.cs      # Draggable split ratio control
├── Settings/
│   ├── WorkspaceConfig.cs   # .ccpad-workspace file I/O
│   └── ProjectConfig.cs     # Project list persistence
└── Assets/
    └── xterm/               # xterm.js terminal emulator
```

**Rendering pipeline:** xterm.js (JavaScript) → WebView2 (Chromium) → WinUI 3 window

**Layout model:** Binary tree of `SplitNode` — each leaf is a `PaneNode` containing a `TabPanel`, each internal node is a `SplitContainerNode` with orientation and ratio.

## System Requirements

- Windows 10 version 1809 (Build 17763) or later
- WebView2 Runtime (bundled with Windows 11, auto-installed on Windows 10)

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).

## Contributing

Contributions are welcome! Please open an issue first to discuss what you'd like to change.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes
4. Push to the branch and open a Pull Request
