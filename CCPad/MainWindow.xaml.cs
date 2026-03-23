using System;
using System.Collections.Generic;
using CCPad.Settings;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace CCPad
{
    public sealed partial class MainWindow : Window
    {
        private SplitHost? _splitHost;
        private string? _currentWorkspaceFile;

        public MainWindow()
        {
            InitializeComponent();
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
            Activated += OnFirstActivated;
            Closed += OnWindowClosed;
        }

        private async void OnFirstActivated(object sender, WindowActivatedEventArgs e)
        {
            Activated -= OnFirstActivated;

            var app = Application.Current as App;
            var projects = ProjectConfig.Load();
            var startDir = app?.StartupWorkingDir;
            var workspaceFile = app?.StartupWorkspaceFile;

            if (workspaceFile != null)
            {
                // Launched from .ccpad-workspace file → enter workspace mode
                var ws = WorkspaceConfig.LoadFromFile(workspaceFile);
                if (ws?.Layout != null)
                {
                    _splitHost = SplitHost.RestoreFromLayout(ws.Layout, projects);
                    RootGrid.Children.Add(_splitHost);
                    RestoreWindowSize(ws);
                    await _splitHost.InitializeTerminals();

                    _currentWorkspaceFile = workspaceFile;
                    EnterWorkspaceMode();
                    Activated += OnActivated;
                    return;
                }
            }

            // Normal launch — fresh terminal, no workspace
            _splitHost = new SplitHost(projects);
            RootGrid.Children.Add(_splitHost);
            var startName = startDir != null ? System.IO.Path.GetFileName(startDir) : null;
            await _splitHost.InitializeFirstTab(startName, startDir);

            RefreshWorkspaceFlyout();
            Activated += OnActivated;
        }

        private void OnActivated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState != WindowActivationState.Deactivated)
                _splitHost?.FocusActive();
        }

        // ── Workspace mode ───────────────────────────────────────────────

        /// <summary>
        /// Show the workspace button and update title. Only called when a
        /// .ccpad-workspace file is explicitly opened.
        /// </summary>
        private void EnterWorkspaceMode()
        {
            WorkspaceButton.Visibility = Visibility.Visible;
            UpdateTitle();
            RefreshWorkspaceFlyout();
        }

        private void RefreshWorkspaceFlyout()
        {
            WorkspaceFlyout.Items.Clear();

            if (_currentWorkspaceFile != null)
            {
                // In workspace mode — save to current file
                var saveItem = new MenuFlyoutItem
                {
                    Text = "保存工作区",
                    Icon = new FontIcon { Glyph = "\uE74E" }
                };
                saveItem.Click += (_, _) => SaveWorkspaceToCurrent();
                WorkspaceFlyout.Items.Add(saveItem);
            }

            // Always available
            var saveAsItem = new MenuFlyoutItem
            {
                Text = "保存工作区为...",
                Icon = new FontIcon { Glyph = "\uE792" }
            };
            saveAsItem.Click += async (_, _) => await SaveWorkspaceAs();
            WorkspaceFlyout.Items.Add(saveAsItem);

            var openItem = new MenuFlyoutItem
            {
                Text = "打开工作区...",
                Icon = new FontIcon { Glyph = "\uE8E5" }
            };
            openItem.Click += async (_, _) => await OpenWorkspaceFromFile();
            WorkspaceFlyout.Items.Add(openItem);

            // Current file info
            if (_currentWorkspaceFile != null)
            {
                WorkspaceFlyout.Items.Add(new MenuFlyoutSeparator());
                var infoItem = new MenuFlyoutItem
                {
                    Text = System.IO.Path.GetFileName(_currentWorkspaceFile),
                    Icon = new FontIcon { Glyph = "\uE8F1" },
                    IsEnabled = false
                };
                WorkspaceFlyout.Items.Add(infoItem);
            }
        }

        // ── Open workspace ───────────────────────────────────────────────

        private async System.Threading.Tasks.Task OpenWorkspaceFromFile()
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            picker.FileTypeFilter.Add(WorkspaceConfig.FileExtension);

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            var ws = WorkspaceConfig.LoadFromFile(file.Path);
            if (ws?.Layout == null) return;

            var projects = ProjectConfig.Load();
            if (_splitHost != null)
            {
                _splitHost.DisposeAll();
                RootGrid.Children.Remove(_splitHost);
            }

            _splitHost = SplitHost.RestoreFromLayout(ws.Layout, projects);
            RootGrid.Children.Add(_splitHost);
            RestoreWindowSize(ws);
            await _splitHost.InitializeTerminals();

            _currentWorkspaceFile = file.Path;
            EnterWorkspaceMode();
        }

        // ── Save workspace ───────────────────────────────────────────────

        private void SaveWorkspaceToCurrent()
        {
            if (_splitHost == null || _currentWorkspaceFile == null) return;
            var snapshot = CreateSnapshot();
            WorkspaceConfig.SaveToFile(_currentWorkspaceFile, snapshot);
        }

        private async System.Threading.Tasks.Task SaveWorkspaceAs()
        {
            if (_splitHost == null) return;

            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            picker.SuggestedFileName = "workspace";
            picker.FileTypeChoices.Add("CCPad 工作区", new List<string> { WorkspaceConfig.FileExtension });

            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            var snapshot = CreateSnapshot();
            if (WorkspaceConfig.SaveToFile(file.Path, snapshot))
            {
                _currentWorkspaceFile = file.Path;
                EnterWorkspaceMode();
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private WorkspaceEntry CreateSnapshot()
        {
            var appWindow = GetAppWindow();
            return new WorkspaceEntry
            {
                WindowWidth = appWindow?.Size.Width ?? 1200,
                WindowHeight = appWindow?.Size.Height ?? 800,
                Layout = _splitHost!.SnapshotLayout()
            };
        }

        private void RestoreWindowSize(WorkspaceEntry ws)
        {
            var appWindow = GetAppWindow();
            if (appWindow != null && ws.WindowWidth > 0 && ws.WindowHeight > 0)
                appWindow.Resize(new SizeInt32(ws.WindowWidth, ws.WindowHeight));
        }

        private AppWindow? GetAppWindow()
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var wid = Win32Interop.GetWindowIdFromWindow(hwnd);
                return AppWindow.GetFromWindowId(wid);
            }
            catch { return null; }
        }

        private void UpdateTitle()
        {
            if (_currentWorkspaceFile != null)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(_currentWorkspaceFile);
                Title = $"{name} — CC Pad";
            }
            else
            {
                Title = "CC Pad";
            }
        }

        // ── Auto-save on close (only if in workspace mode) ───────────────

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            if (_splitHost != null)
            {
                try
                {
                    // Only auto-save if we're in a workspace
                    if (_currentWorkspaceFile != null)
                    {
                        var snapshot = CreateSnapshot();
                        WorkspaceConfig.SaveToFile(_currentWorkspaceFile, snapshot);
                    }
                }
                catch { }

                _splitHost.DisposeAll();
            }
        }
    }
}
