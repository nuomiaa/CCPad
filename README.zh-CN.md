<p align="center">
  <img src="CCPad/Assets/claude.ico" width="80" alt="CC Pad logo"/>
</p>

<h1 align="center">CC Pad</h1>

<p align="center">
  Claude Code 多会话工作台 — 在单窗口中并行运行多个 Claude Code 会话。
</p>

<p align="center">
  <a href="https://ccpad.dev">官网</a> · <a href="README.md">English</a> · <a href="LICENSE">许可证 (GPL-3.0)</a>
</p>

---

## 功能特性

- **分屏** — 支持纵向和横向分屏，可拖拽调整比例。使用 `Alt+方向键` 在面板间快速切换。
- **标签页** — 每个面板支持多标签页，可拖拽排序，预热机制确保新建标签页即开即用。
- **工作区** — 将完整布局（分屏、标签页、工作目录、窗口状态）保存为 `.ccpad-workspace` 文件。启动时自动检测并恢复工作区。
- **项目快速访问** — 固定常用目录，一键在新标签页中打开。
- **Windows ConPTY** — 原生伪控制台集成，可运行任何命令行工具 — PowerShell、cmd、bash、python、node、git 等。
- **xterm.js 渲染** — 通过 WebView2 承载 xterm.js，使用 Cascadia Code 字体进行完整终端模拟。
- **Mica 背景** — 原生 Windows 11 半透明云母材质效果。
- **右键菜单集成** — 在资源管理器中右键任意文件夹即可在 CC Pad 中打开。
- **文件关联** — 双击 `.ccpad-workspace` 文件直接打开。

## 截图

<!-- TODO: 添加截图 -->

## 安装

### 安装程序（推荐）

从 [Releases](../../releases) 页面下载最新的 `CCPad-Setup-x64.exe` 并运行。

### 从源码构建

**前置条件：**
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Windows App SDK 1.8+](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
- Windows 10（Build 17763）或更高版本

```bash
# 克隆仓库
git clone https://github.com/nuomiaa/CCPad.git
cd CCPad

# 构建（Debug）
dotnet build CCPad/CCPad.csproj

# 构建（Release，x64）
dotnet publish CCPad/CCPad.csproj -c Release -r win-x64

```

支持平台：`win-x64`、`win-x86`、`win-arm64`。

## 使用

### 启动

```bash
# 在当前目录打开
CCPad.exe

# 打开指定文件夹
CCPad.exe "C:\Projects\my-app"

# 打开工作区文件
CCPad.exe my-project.ccpad-workspace
```

无参数启动时，CC Pad 会自动检测当前目录下的 `.ccpad-workspace` 文件并进入工作区模式。

### 快捷键

| 操作 | 快捷键 |
|------|--------|
| 新建标签页 | `Ctrl+T` |
| 关闭标签页 | `Ctrl+W` |
| 复制选中内容 | `Ctrl+C` |
| 向右分屏 | `Alt+Shift+=` |
| 向下分屏 | `Alt+Shift+-` |
| 面板导航 | `Alt+方向键` |
| 关闭面板 | `Ctrl+Shift+W` |

右键点击终端或标签页标题可查看更多选项。

### 工作区

工作区以 JSON 格式保存完整布局：

- **分屏布局** — 面板树结构，包含方向和比例
- **标签页状态** — 每个标签页的名称和工作目录
- **窗口状态** — 大小、位置和最大化状态

使用工作区按钮（右上角，工作区模式下可见）或右键菜单来保存/加载工作区。默认文件名为当前目录名。

### 项目管理

点击任意标签栏右侧的 **项目** 按钮来管理固定目录。添加项目后即可在所有面板中快速创建对应目录的新标签页。

## 架构

```
CCPad/
├── App.xaml.cs              # 入口，启动逻辑，右键菜单注册
├── MainWindow.xaml.cs       # 窗口管理，工作区模式
├── SplitHost.xaml.cs        # 二叉分屏树布局引擎
├── TabPanel.xaml.cs         # 标签页生命周期，项目菜单
├── TerminalPane.xaml.cs     # WebView2 + xterm.js 宿主
├── Terminal/
│   ├── ConPtySession.cs     # Windows ConPTY 进程管理
│   └── PseudoConsoleApi.cs  # ConPTY Win32 API P/Invoke 绑定
├── Controls/
│   └── GridSplitter.cs      # 可拖拽分屏比例控件
├── Settings/
│   ├── WorkspaceConfig.cs   # .ccpad-workspace 文件读写
│   └── ProjectConfig.cs     # 项目列表持久化
└── Assets/
    └── xterm/               # xterm.js 终端模拟器
```

**渲染管线：** xterm.js (JavaScript) → WebView2 (Chromium) → WinUI 3 窗口

**布局模型：** `SplitNode` 二叉树 — 叶节点为包含 `TabPanel` 的 `PaneNode`，内部节点为带方向和比例的 `SplitContainerNode`。

## 系统要求

- Windows 10 版本 1809（Build 17763）或更高版本
- WebView2 运行时（Windows 11 已内置，Windows 10 会自动安装）

## 许可证

本项目基于 [GNU 通用公共许可证 v3.0](LICENSE) 授权。

## 参与贡献

欢迎贡献代码！请先创建 Issue 讨论你想要更改的内容。

1. Fork 本仓库
2. 创建你的功能分支（`git checkout -b feature/my-feature`）
3. 提交更改
4. 推送分支并创建 Pull Request
