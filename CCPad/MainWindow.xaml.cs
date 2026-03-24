using System;
using System.Collections.Generic;
using CCPad.Settings;
using CCPad.Web;
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

        private ReleaseInfo? _releaseInfo;
        private WebTerminalServer? _webServer;

        public MainWindow()
        {
            InitializeComponent();
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
            Activated += OnFirstActivated;
            Closed += OnWindowClosed;
            InitAboutMenu();
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
                    _ = DelayedUpdateCheckAsync();
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
            _ = DelayedUpdateCheckAsync();
        }

        private async System.Threading.Tasks.Task DelayedUpdateCheckAsync()
        {
            await System.Threading.Tasks.Task.Delay(2000);
            await CheckForUpdateAsync(silent: true);
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
            picker.SuggestedFileName = System.IO.Path.GetFileName(Environment.CurrentDirectory);
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
            var presenter = appWindow?.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            var isMaximized = presenter?.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized;
            return new WorkspaceEntry
            {
                WindowWidth = appWindow?.Size.Width ?? 1200,
                WindowHeight = appWindow?.Size.Height ?? 800,
                WindowX = appWindow?.Position.X ?? -1,
                WindowY = appWindow?.Position.Y ?? -1,
                IsMaximized = isMaximized,
                Layout = _splitHost!.SnapshotLayout()
            };
        }

        private void RestoreWindowSize(WorkspaceEntry ws)
        {
            var appWindow = GetAppWindow();
            if (appWindow == null) return;

            if (ws.WindowX >= 0 && ws.WindowY >= 0)
                appWindow.Move(new Windows.Graphics.PointInt32(ws.WindowX, ws.WindowY));

            if (ws.WindowWidth > 0 && ws.WindowHeight > 0)
                appWindow.Resize(new SizeInt32(ws.WindowWidth, ws.WindowHeight));

            if (ws.IsMaximized && appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                presenter.Maximize();
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

        // ── About menu ─────────────────────────────────────────────────────

        private void InitAboutMenu()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var versionStr = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "dev";

            var aboutItem = new MenuFlyoutItem
            {
                Text = $"CC Pad v{versionStr}",
                Icon = new FontIcon { Glyph = "\uE946" },
                IsEnabled = false
            };
            AboutFlyout.Items.Add(aboutItem);

            var updateItem = new MenuFlyoutItem
            {
                Text = "检查更新",
                Icon = new FontIcon { Glyph = "\uECC5" }
            };
            updateItem.Click += async (_, _) => await CheckForUpdateAsync(silent: false);
            AboutFlyout.Items.Add(updateItem);

            _remoteMenuItem = new MenuFlyoutItem
            {
                Text = "远程终端",
                Icon = new FontIcon { Glyph = "\uE774" }
            };
            _remoteMenuItem.Click += (_, _) => OnRemoteTerminalClick();
            AboutFlyout.Items.Add(_remoteMenuItem);

            AboutFlyout.Items.Add(new MenuFlyoutSeparator());

            var githubItem = new MenuFlyoutItem
            {
                Text = "GitHub",
                Icon = new FontIcon { Glyph = "\uE774" }
            };
            githubItem.Click += async (_, _) =>
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/nuomiaa/CCPad"));
            };
            AboutFlyout.Items.Add(githubItem);

            var websiteItem = new MenuFlyoutItem
            {
                Text = "CCPad.dev",
                Icon = new FontIcon { Glyph = "\uE774" }
            };
            websiteItem.Click += async (_, _) =>
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri("https://ccpad.dev"));
            };
            AboutFlyout.Items.Add(websiteItem);
        }

        // ── Update check ────────────────────────────────────────────────

        private async System.Threading.Tasks.Task CheckForUpdateAsync(bool silent)
        {
            var info = await UpdateChecker.CheckAsync();
            if (info != null)
            {
                _releaseInfo = info;
                DispatcherQueue.TryEnqueue(() => UpdateButton.Visibility = Visibility.Visible);

                if (!silent)
                    DispatcherQueue.TryEnqueue(() => ShowUpdateDialog(info));
            }
            else if (!silent)
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    var dlg = new ContentDialog
                    {
                        Title = "检查更新",
                        Content = $"当前已是最新版本 v{UpdateChecker.GetCurrentVersion()}",
                        CloseButtonText = "确定",
                        XamlRoot = Content.XamlRoot
                    };
                    await dlg.ShowAsync();
                });
            }
        }

        private async void OnUpdateButtonClick(object sender, RoutedEventArgs e)
        {
            if (_releaseInfo != null)
                ShowUpdateDialog(_releaseInfo);
            else
                await CheckForUpdateAsync(silent: false);
        }

        private async void ShowUpdateDialog(ReleaseInfo info)
        {
            var hasAsset = info.AssetUrl != null;
            var dlg = new ContentDialog
            {
                Title = "发现新版本",
                Content = $"新版本 v{info.Version} 已发布，当前版本 v{UpdateChecker.GetCurrentVersion()}。",
                PrimaryButtonText = hasAsset ? "下载并安装" : "前往下载页",
                CloseButtonText = "稍后",
                XamlRoot = Content.XamlRoot
            };

            if (await dlg.ShowAsync() != ContentDialogResult.Primary)
                return;

            if (!hasAsset)
            {
                await Windows.System.Launcher.LaunchUriAsync(
                    new Uri(UpdateChecker.ReleasesPageUrl));
                return;
            }

            // Show progress dialog
            var cts = new System.Threading.CancellationTokenSource();
            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Width = 300,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var statusText = new TextBlock { Text = $"正在下载 {info.AssetName}..." };
            var panel = new StackPanel();
            panel.Children.Add(statusText);
            panel.Children.Add(progressBar);

            var progressDlg = new ContentDialog
            {
                Title = "下载更新",
                Content = panel,
                CloseButtonText = "取消",
                XamlRoot = Content.XamlRoot
            };
            progressDlg.CloseButtonClick += (_, _) => cts.Cancel();

            // Start download in background, show dialog concurrently
            string? localPath = null;
            Exception? downloadError = null;
            var downloadTask = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var progress = new Progress<int>(pct =>
                        DispatcherQueue.TryEnqueue(() => progressBar.Value = pct));
                    localPath = await UpdateChecker.DownloadAssetAsync(
                        info.AssetUrl!, info.AssetName!, progress, cts.Token);
                }
                catch (Exception ex)
                {
                    downloadError = ex;
                }
            });

            // Close progress dialog automatically when download finishes
            _ = downloadTask.ContinueWith(_ =>
                DispatcherQueue.TryEnqueue(() => progressDlg.Hide()),
                System.Threading.Tasks.TaskScheduler.Default);

            await progressDlg.ShowAsync();
            await downloadTask;

            if (cts.IsCancellationRequested)
                return;

            if (downloadError != null || localPath == null)
            {
                var errDlg = new ContentDialog
                {
                    Title = "下载失败",
                    Content = "下载更新时出错，是否前往下载页面手动下载？",
                    PrimaryButtonText = "前往下载页",
                    CloseButtonText = "关闭",
                    XamlRoot = Content.XamlRoot
                };
                if (await errDlg.ShowAsync() == ContentDialogResult.Primary)
                {
                    await Windows.System.Launcher.LaunchUriAsync(
                        new Uri(UpdateChecker.ReleasesPageUrl));
                }
                return;
            }

            // Launch installer and exit
            UpdateChecker.LaunchInstaller(localPath);
            Application.Current.Exit();
        }

        // ── Web server ────────────────────────────────────────────────────

        private MenuFlyoutItem? _remoteMenuItem;

        private async void OnRemoteTerminalClick()
        {
            if (_webServer?.IsRunning == true)
            {
                await ShowWebServerInfoDialog();
                return;
            }

            // Collect LAN addresses
            var addresses = WebTerminalServer.GetLanAddresses();
            if (addresses.Count == 0) addresses.Add("localhost");

            var tokenCheck = new CheckBox { Content = "启用 Token 鉴权", IsChecked = false };

            var addressCombo = new ComboBox { Width = 300 };
            foreach (var addr in addresses) addressCombo.Items.Add(addr);
            addressCombo.SelectedIndex = 0;

            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock
            {
                Text = "启动后，同一局域网内的设备可通过浏览器访问本机终端。",
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(new TextBlock { Text = "监听地址", FontSize = 12, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 150, 150, 150)) });
            panel.Children.Add(addressCombo);
            panel.Children.Add(tokenCheck);

            var dlg = new ContentDialog
            {
                Title = "启动远程终端",
                Content = panel,
                PrimaryButtonText = "启动",
                CloseButtonText = "取消",
                XamlRoot = Content.XamlRoot
            };

            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

            _webServer ??= new WebTerminalServer();
            _webServer.UseToken = tokenCheck.IsChecked == true;
            _webServer.Host = addressCombo.SelectedItem?.ToString() ?? "localhost";

            try
            {
                await _webServer.StartAsync(9220);
                UpdateRemoteMenuItem(true);
                await ShowWebServerStartedDialog();
            }
            catch (Exception ex)
            {
                var errDlg = new ContentDialog
                {
                    Title = "启动失败",
                    Content = $"无法启动远程终端服务：{ex.Message}",
                    CloseButtonText = "确定",
                    XamlRoot = Content.XamlRoot
                };
                await errDlg.ShowAsync();
            }
        }

        private async System.Threading.Tasks.Task ShowWebServerStartedDialog()
        {
            var url = _webServer!.GetAccessUrl();
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = "远程终端已启动！", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

            var urlBox = new TextBox
            {
                Text = url,
                IsReadOnly = true,
                FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
                FontSize = 13
            };
            panel.Children.Add(urlBox);

            if (_webServer.Token != null)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "Token 已启用，请勿将链接泄露给他人。",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 150, 150, 150))
                });
            }

            var dlg = new ContentDialog
            {
                Title = "远程终端",
                Content = panel,
                PrimaryButtonText = "复制链接",
                SecondaryButtonText = "在浏览器中打开",
                CloseButtonText = "确定",
                XamlRoot = Content.XamlRoot
            };

            var result = await dlg.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText(url);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
            }
        }

        private async System.Threading.Tasks.Task ShowWebServerInfoDialog()
        {
            var url = _webServer!.GetAccessUrl();
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock
            {
                Text = $"远程终端正在运行  |  连接客户端: {_webServer.ClientCount}",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            var urlBox = new TextBox
            {
                Text = url,
                IsReadOnly = true,
                FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
                FontSize = 13
            };
            panel.Children.Add(urlBox);

            var dlg = new ContentDialog
            {
                Title = "远程终端",
                Content = panel,
                PrimaryButtonText = "停止服务",
                SecondaryButtonText = "在浏览器中打开",
                CloseButtonText = "确定",
                XamlRoot = Content.XamlRoot
            };

            var result = await dlg.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await _webServer.StopAsync();
                _webServer = null;
                UpdateRemoteMenuItem(false);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
            }
        }

        private void UpdateRemoteMenuItem(bool running)
        {
            if (_remoteMenuItem == null) return;
            _remoteMenuItem.Text = running ? "远程终端 (运行中)" : "远程终端";
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
            _webServer?.Dispose();
        }
    }
}
