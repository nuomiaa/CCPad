using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CCPad.Settings;
using CCPad.Terminal;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CCPad
{
    public sealed partial class TabPanel : UserControl
    {
        private int _tabCounter;
        private TerminalPane? _prewarmedPane;
        private List<ProjectEntry> _projects;
        private string? _defaultWorkingDir;
        private List<TabState>? _pendingRestoreTabs;
        private int _pendingRestoreActiveIndex;

        public string? DefaultWorkingDir => _defaultWorkingDir;

        public event Action<TabPanel, SplitOrientation>? SplitRequested;
        public event Action<TabPanel>? CloseRequested;
        public event Action<TabPanel>? Focused;

        private static string DefaultCmd => "claude";

        public TabPanel(List<ProjectEntry> projects)
        {
            InitializeComponent();
            _projects = projects;
            RefreshProjectFlyout();
        }

        public void UpdateProjects(List<ProjectEntry> projects)
        {
            _projects = projects;
            RefreshProjectFlyout();
        }

        // ── Public API ──────────────────────────────────────────────────

        public async Task AddFirstTab(string? projectName = null, string? workingDir = null)
        {
            if (workingDir != null)
                _defaultWorkingDir = workingDir;
            await AddNewTab(projectName, workingDir);
        }

        public void FocusCurrentTab()
        {
            if (Tabs.SelectedItem is TabViewItem item && item.Content is TerminalPane pane)
                pane.FocusTerminal();
        }

        // ── Tab management ──────────────────────────────────────────────

        private async Task AddNewTab(string? projectName = null, string? workingDir = null)
        {
            _tabCounter++;
            string cmd = DefaultCmd;

            TerminalPane pane;
            if (_prewarmedPane != null)
            {
                pane = _prewarmedPane;
                _prewarmedPane = null;
                PrewarmHost.Children.Remove(pane);

                WirePane(pane);
                var item = CreateTabItem(projectName, workingDir, pane);

                Tabs.TabItems.Add(item);
                Tabs.SelectedItem = item;

                pane.LaunchSession(cmd, workingDir, focusOnReady: true);
            }
            else
            {
                pane = new TerminalPane();
                WirePane(pane);
                var item = CreateTabItem(projectName, workingDir, pane);

                Tabs.TabItems.Add(item);
                Tabs.SelectedItem = item;

                await pane.InitializeAsync(cmd, workingDir, focusOnReady: true);
            }

            PrewarmNextPane();
        }

        private void WirePane(TerminalPane pane)
        {
            pane.NewTabRequested += async () => await AddNewTab(null, _defaultWorkingDir);
            pane.CloseTabRequested += CloseCurrentTab;
            pane.SplitHorizontalRequested += () => SplitRequested?.Invoke(this, SplitOrientation.Horizontal);
            pane.SplitVerticalRequested += () => SplitRequested?.Invoke(this, SplitOrientation.Vertical);
            pane.NavigateRequested += dir =>
            {
                var direction = dir switch
                {
                    "left" => Direction.Left,
                    "right" => Direction.Right,
                    "up" => Direction.Up,
                    "down" => Direction.Down,
                    _ => (Direction?)null
                };
                if (direction.HasValue)
                    NavigateRequested?.Invoke(this, direction.Value);
            };
            pane.ClosePaneRequested += () => CloseRequested?.Invoke(this);
            pane.PaneFocused += () => Focused?.Invoke(this);
        }

        public event Action<TabPanel, Direction>? NavigateRequested;

        private TabViewItem CreateTabItem(string? projectName, string? workingDir, TerminalPane pane)
        {
            string header = projectName
                ?? (workingDir != null ? System.IO.Path.GetFileName(workingDir) : null)
                ?? $"Terminal {_tabCounter}";
            pane.Label = header;
            var item = new TabViewItem
            {
                Header = header,
                Content = pane,
                IsClosable = true
            };

            var ctx = new MenuFlyout();

            var splitRight = new MenuFlyoutItem
            {
                Text = "向右分屏",
                Icon = new FontIcon { Glyph = "\uEA61" },
                KeyboardAcceleratorTextOverride = "Alt+Shift+="
            };
            splitRight.Click += (_, _) => SplitRequested?.Invoke(this, SplitOrientation.Vertical);

            var splitDown = new MenuFlyoutItem
            {
                Text = "向下分屏",
                Icon = new FontIcon { Glyph = "\uE745" },
                KeyboardAcceleratorTextOverride = "Alt+Shift+-"
            };
            splitDown.Click += (_, _) => SplitRequested?.Invoke(this, SplitOrientation.Horizontal);

            var closeTab = new MenuFlyoutItem
            {
                Text = "关闭标签页",
                Icon = new FontIcon { Glyph = "\uE711" },
                KeyboardAcceleratorTextOverride = "Ctrl+W"
            };
            closeTab.Click += (_, _) => CloseTab(item);

            var closeOthers = new MenuFlyoutItem
            {
                Text = "关闭其他标签页",
                Icon = new FontIcon { Glyph = "\uE89B" },
                KeyboardAcceleratorTextOverride = "Ctrl+Shift+W"
            };
            closeOthers.Click += (_, _) => CloseOtherTabs(item);

            var closeLeft = new MenuFlyoutItem
            {
                Text = "关闭左侧标签页",
                Icon = new FontIcon { Glyph = "\uE746" }
            };
            closeLeft.Click += (_, _) => CloseTabsToSide(item, left: true);

            var closeRight = new MenuFlyoutItem
            {
                Text = "关闭右侧标签页",
                Icon = new FontIcon { Glyph = "\uEA61" }
            };
            closeRight.Click += (_, _) => CloseTabsToSide(item, left: false);

            ctx.Items.Add(splitRight);
            ctx.Items.Add(splitDown);
            ctx.Items.Add(new MenuFlyoutSeparator());
            ctx.Items.Add(closeTab);
            ctx.Items.Add(closeOthers);
            //ctx.Items.Add(closeLeft);
            //ctx.Items.Add(closeRight);

            item.ContextFlyout = ctx;
            return item;
        }

        private void CloseCurrentTab()
        {
            if (Tabs.SelectedItem is TabViewItem item)
                CloseTab(item);
        }

        private void CloseOtherTabs(TabViewItem keep)
        {
            var toClose = Tabs.TabItems.Cast<TabViewItem>().Where(t => t != keep).ToList();
            foreach (var t in toClose)
            {
                if (t.Content is TerminalPane pane)
                    pane.Dispose();
                Tabs.TabItems.Remove(t);
            }
        }

        private void CloseTabsToSide(TabViewItem anchor, bool left)
        {
            int idx = Tabs.TabItems.IndexOf(anchor);
            var toClose = Tabs.TabItems.Cast<TabViewItem>()
                .Where((t, i) => left ? i < idx : i > idx).ToList();
            foreach (var t in toClose)
            {
                if (t.Content is TerminalPane pane)
                    pane.Dispose();
                Tabs.TabItems.Remove(t);
            }
        }

        private void CloseTab(TabViewItem item)
        {
            if (Tabs.TabItems.Count <= 1)
            {
                // Last tab — close the whole panel
                CloseRequested?.Invoke(this);
                return;
            }
            if (item.Content is TerminalPane pane)
                pane.Dispose();
            Tabs.TabItems.Remove(item);
        }

        private async void OnAddTab(TabView sender, object args) => await AddNewTab(null, _defaultWorkingDir);

        private void OnTabClose(TabView sender, TabViewTabCloseRequestedEventArgs args)
            => CloseTab(args.Tab);

        private async void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Tabs.SelectedItem is TabViewItem item && item.Content is TerminalPane pane)
            {
                // 等待 TabView 完成所有指针事件处理（PointerReleased）后再聚焦终端
                await Task.Delay(50);
                pane.FocusTerminal();
            }
        }

        // ── Pre-warm ────────────────────────────────────────────────────

        private async void PrewarmNextPane()
        {
            var pane = new TerminalPane();
            PrewarmHost.Children.Clear();
            PrewarmHost.Children.Add(pane);
            try
            {
                await pane.PrewarmAsync();
                _prewarmedPane = pane;
            }
            catch
            {
                _prewarmedPane = null;
            }
        }

        // ── Project config ──────────────────────────────────────────────

        private void RefreshProjectFlyout()
        {
            ProjectFlyout.Items.Clear();

            foreach (var proj in _projects)
            {
                var entry = proj;
                var item = new MenuFlyoutItem
                {
                    Text = entry.Name,
                    Icon = new FontIcon { Glyph = "\uE8B7" }
                };
                item.Click += async (_, _) => await AddNewTab(entry.Name, entry.Path);

                // Swipe-to-delete style: right-click on project to remove
                var removeItem = new MenuFlyoutItem
                {
                    Text = $"移除 {entry.Name}",
                    Icon = new FontIcon { Glyph = "\uE74D" }
                };
                removeItem.Click += (_, _) =>
                {
                    _projects.Remove(entry);
                    ProjectConfig.Save(_projects);
                    RefreshProjectFlyout();
                };

                var subFlyout = new MenuFlyout();
                subFlyout.Items.Add(removeItem);
                item.ContextFlyout = subFlyout;

                ProjectFlyout.Items.Add(item);
            }

            if (_projects.Count > 0)
                ProjectFlyout.Items.Add(new MenuFlyoutSeparator());

            var addItem = new MenuFlyoutItem
            {
                Text = "添加项目...",
                Icon = new FontIcon { Glyph = "\uE710" }
            };
            addItem.Click += async (_, _) =>
            {
                var path = await PickFolderAsync();
                if (path != null)
                {
                    var name = System.IO.Path.GetFileName(path);
                    _projects.Add(new ProjectEntry { Name = name, Path = path });
                    ProjectConfig.Save(_projects);
                    RefreshProjectFlyout();
                    await AddNewTab(name, path);
                }
            };
            ProjectFlyout.Items.Add(addItem);
        }

        // ── Folder picker ─────────────────────────────────────────────

        private async Task<string?> PickFolderAsync()
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
                (Application.Current as App)!.MainWnd);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }

        // ── Workspace snapshot / restore ─────────────────────────────────

        public int ActiveTabIndex => Tabs.SelectedIndex;

        public List<TabState> GetTabStates()
        {
            var states = new List<TabState>();
            foreach (var tabItem in Tabs.TabItems)
            {
                if (tabItem is TabViewItem tvi)
                {
                    var pane = tvi.Content as TerminalPane;
                    var dir = pane?.WorkingDir ?? _defaultWorkingDir;
                    states.Add(new TabState
                    {
                        Name = tvi.Header?.ToString() ?? "",
                        WorkingDir = string.IsNullOrEmpty(dir) ? "" : dir
                    });
                }
            }
            return states;
        }

        /// <summary>
        /// Store tab states for deferred initialization (used by workspace restore).
        /// Tabs are NOT created yet — call InitializeRestoredTabs() after the control is in the visual tree.
        /// </summary>
        public void SetPendingRestore(List<TabState>? tabs, int activeIndex)
        {
            _pendingRestoreTabs = tabs;
            _pendingRestoreActiveIndex = activeIndex;
        }

        /// <summary>
        /// Actually create the tabs from pending restore data. Must be called after the
        /// TabPanel is in the visual tree (WebView2 needs Loaded event).
        /// </summary>
        public async Task InitializeRestoredTabs()
        {
            if (_pendingRestoreTabs != null && _pendingRestoreTabs.Count > 0)
            {
                await RestoreFromStates(_pendingRestoreTabs, _pendingRestoreActiveIndex);
            }
            else
            {
                await AddFirstTab();
            }
            _pendingRestoreTabs = null;
        }

        public async Task RestoreFromStates(List<TabState> states, int activeIndex)
        {
            for (int i = 0; i < states.Count; i++)
            {
                var s = states[i];
                var name = string.IsNullOrEmpty(s.Name) ? null : s.Name;
                var dir = string.IsNullOrEmpty(s.WorkingDir) ? null : s.WorkingDir;
                if (i == 0)
                    await AddFirstTab(name, dir);
                else
                    await AddNewTab(name, dir);
            }
            if (activeIndex >= 0 && activeIndex < Tabs.TabItems.Count)
                Tabs.SelectedIndex = activeIndex;
        }

        // ── Disposal ────────────────────────────────────────────────────

        public void DisposeAll()
        {
            _prewarmedPane?.Dispose();
            _prewarmedPane = null;
            foreach (var tabItem in Tabs.TabItems)
            {
                if (tabItem is TabViewItem tvi && tvi.Content is TerminalPane pane)
                    pane.Dispose();
            }
        }
    }
}
