using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TabPilot
{
    internal static class DesktopApp
    {
        static string dataDirectory;
        internal static string PortableDataDirectory { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data"); } }
        internal static string DataDirectory
        {
            get { return dataDirectory ?? (dataDirectory = Storage.SelectDirectory(PortableDataDirectory,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabPilot"))); }
        }
        internal static int Run(bool hidden)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string identity = WindowsIdentity.GetCurrent().User.Value;
            bool created;
            using (var mutex = new Mutex(true, @"Local\TabPilot.Desktop." + identity, out created))
            using (var signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\TabPilot.Show." + identity))
            {
                if (!created) { signal.Set(); return 0; }
                try
                {
                    using (var context = new DesktopContext(signal, hidden)) Application.Run(context);
                }
                finally { mutex.ReleaseMutex(); }
            }
            return 0;
        }
    }
    internal sealed class DesktopContext : ApplicationContext
    {
        readonly PopupForm popup = new PopupForm();
        readonly EventWaitHandle signal;
        readonly System.Windows.Forms.Timer messages = new System.Windows.Forms.Timer { Interval = 150 };
        readonly System.Windows.Forms.Timer saveSettings = new System.Windows.Forms.Timer { Interval = 350 };
        readonly NotifyIcon tray = new NotifyIcon();
        readonly DesktopSettings settings;
        readonly PendingSettings pendingSettings;
        readonly GlobalHotkey hotkey;
        readonly ToolStripMenuItem shortcutMenu = new ToolStripMenuItem();
        readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
        readonly ToolStripMenuItem languageMenu = new ToolStripMenuItem("Language / 语言");
        Snapshot snapshot;
        List<DesktopTab> tabs = new List<DesktopTab>();
        bool scanning, selecting, exiting, deferredShow, stateDirty;
        bool restoringPlacement, hasShown, settingsNeedSave;
        string lastProblem, lastActionProblem;
        internal DesktopContext(EventWaitHandle signal, bool hidden)
        {
            this.signal = signal;
            Directory.CreateDirectory(DesktopApp.DataDirectory);
            settings = LoadSettings();
            UiText.SetLanguage(settings.Language);
            settings.Language = UiText.Language;
            lastProblem = UiText.RelocalizeMessage(lastProblem);
            popup.ApplyLanguage();
            pendingSettings = new PendingSettings(delegate { Storage.Write(Path.Combine(DesktopApp.DataDirectory, "settings.json"), Program.Serialize(settings), true); });
            if (settingsNeedSave) { pendingSettings.Changed(); FlushSettings(); }
            hotkey = new GlobalHotkey(popup.Handle);
            popup.Hotkey = hotkey;
            string registrationError;
            if (!hotkey.Set(settings.HotkeyModifiers, settings.HotkeyKey, out registrationError))
            {
                lastProblem = registrationError;
                foreach (int alternative in new[] { (int)Keys.Q, (int)Keys.T, (int)Keys.F8 })
                {
                    string ignored;
                    if (!hotkey.Set(3, alternative, out ignored)) continue;
                    settings.HotkeyModifiers = 3; settings.HotkeyKey = alternative;
                    SaveSettings();
                    lastProblem = UiText.F("原快捷键被占用，已改用 {0}。", GlobalHotkey.Describe(3, alternative));
                    break;
                }
            }
            popup.HotkeyText = GlobalHotkey.Describe(settings.HotkeyModifiers, settings.HotkeyKey);
            popup.HotkeyRequested += Toggle;
            popup.RefreshRequested += delegate { lastActionProblem = null; Refresh(); };
            popup.ActivateRequested += ActivateTab;
            popup.DetailsRequested += ShowDetails;
            popup.ShortcutRequested += ChangeShortcut;
            popup.LanguageRequested += ChangeLanguage;
            popup.ExitRequested += Quit;
            popup.VisibleChanged += delegate { if (!popup.Visible) FlushSettings(); WriteState(); };
            popup.LocationChanged += delegate { TrackPlacement(); };
            popup.SizeChanged += delegate { TrackPlacement(); };
            popup.ResizeEnd += delegate { TrackPlacement(); FlushSettings(); };
            popup.DpiSettled += delegate { TrackPlacement(); WriteState(); };
            popup.ViewChanged += delegate { WriteState(); };
            popup.FormClosed += delegate { ExitThread(); };
            var menu = trayMenu;
            menu.Items.Add(UiText.T("打开 TabPilot"), null, delegate { ShowPopup(); });
            menu.Items.Add(UiText.T("刷新标签"), null, delegate { ShowPopup(false); Refresh(); });
            shortcutMenu.Click += delegate { ChangeShortcut(); };
            menu.Items.Add(shortcutMenu);
            languageMenu.DropDownItems.Add("简体中文", null, delegate { ChangeLanguage("zh-CN"); });
            languageMenu.DropDownItems.Add("English", null, delegate { ChangeLanguage("en"); });
            menu.Items.Add(languageMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(UiText.T("退出 TabPilot"), null, delegate { Quit(); });
            tray.Icon = SystemIcons.Application;
            tray.Text = "TabPilot · " + popup.HotkeyText;
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Toggle(); };
            tray.Visible = true;
            UpdateMenuLanguage();
            messages.Tick += delegate { if (signal.WaitOne(0)) ShowPopup(); if (stateDirty) WriteState(); };
            saveSettings.Tick += delegate { FlushSettings(); WriteState(); };
            messages.Start();
            popup.BeginInvoke(new Action(delegate
            {
                if (!hidden || lastProblem != null) ShowPopup();
                else Refresh();
            }));
        }
        DesktopSettings LoadSettings()
        {
            string path = Path.Combine(DesktopApp.DataDirectory, "settings.json");
            string oldPath = Path.Combine(DesktopApp.PortableDataDirectory, "settings.json");
            if (!File.Exists(path) && !File.Exists(path + ".bak") &&
                !String.Equals(path, oldPath, StringComparison.OrdinalIgnoreCase) && (File.Exists(oldPath) || File.Exists(oldPath + ".bak")))
            {
                path = oldPath; settingsNeedSave = true;
            }
            string notice;
            var loaded = Storage.ReadSettings(path, out notice);
            if (notice != null) { lastProblem = notice; settingsNeedSave = true; }
            return loaded;
        }
        void SaveSettings()
        {
            pendingSettings.Changed(); FlushSettings();
        }
        void TrackPlacement()
        {
            if (restoringPlacement || exiting || !hasShown || !popup.Visible || popup.DpiChanging || popup.WindowState != FormWindowState.Normal) return;
            var current = popup.CapturePlacement();
            if (PopupPlacement.Same(settings.PopupPlacement, current)) return;
            settings.PopupPlacement = current;
            pendingSettings.Changed();
            saveSettings.Stop(); saveSettings.Interval = 350; saveSettings.Start();
        }
        void FlushSettings()
        {
            saveSettings.Stop();
            if (pendingSettings == null || !pendingSettings.Dirty) return;
            if (!pendingSettings.Flush() && !exiting) { saveSettings.Interval = 2000; saveSettings.Start(); }
            UpdateStatus();
        }
        void UpdateStatus()
        {
            if (popup.IsDisposed || exiting) return;
            var problems = new[] { pendingSettings == null ? null : pendingSettings.Error,
                hotkey != null && !hotkey.Registered ? UiText.T("快捷键未注册，可从托盘修改。") : null,
                lastActionProblem, lastProblem }.Where(p => !String.IsNullOrEmpty(p)).Select(UiText.RelocalizeMessage).Distinct().ToArray();
            var feedback = ScanFeedback.From(snapshot);
            if (problems.Length > 0) popup.SetStatus(String.Join(UiText.T("；"), problems), true);
            else if (scanning && !feedback.Warning) popup.SetStatus(UiText.T("正在更新标签，可继续搜索已有结果…"), false, snapshot != null);
            else popup.SetStatus(feedback.Summary, feedback.Warning, snapshot != null);
        }
        void Toggle()
        {
            if (popup.SuppressDeactivate) return;
            if (selecting) { deferredShow = true; return; }
            if (popup.Visible) popup.Hide(); else ShowPopup();
        }
        void ShowPopup(bool clearSearch = true)
        {
            if (exiting) return;
            if (selecting) { deferredShow = true; return; }
            if (!popup.Visible)
            {
                restoringPlacement = true;
                try { popup.RestorePlacement(settings.PopupPlacement, Screen.FromHandle(Native.GetForegroundWindow())); }
                finally { restoringPlacement = false; }
            }
            if (clearSearch) popup.ClearSearch();
            popup.Show();
            hasShown = true;
            TrackPlacement(); FlushSettings();
            popup.Activate();
            Native.SetForegroundWindow(popup.Handle);
            popup.FocusSearch();
            UpdateStatus();
            Refresh();
            WriteState();
        }
        async void Refresh()
        {
            if (scanning || selecting || exiting) return;
            scanning = true;
            popup.SetScanning(true);
            UpdateStatus();
            WriteState();
            try
            {
                var captured = await Task.Run(delegate { return Program.Capture(15000); });
                if (exiting) return;
                snapshot = captured;
                tabs = DesktopModel.Build(snapshot, settings);
                popup.SetData(snapshot, tabs);
                lastProblem = null;
                TrySave("latest-snapshot.json", snapshot);
            }
            catch (Exception e)
            {
                if (exiting) return;
                lastProblem = UiText.T("读取标签失败，可以按 F5 重试。");
                TrySave("last-error.json", new { Message = BrowserTree.Error(e), CapturedUtc = DateTime.UtcNow.ToString("o") });
            }
            finally
            {
                scanning = false;
                if (!exiting) { popup.SetScanning(false); UpdateStatus(); WriteState(); }
            }
        }
        async void ActivateTab(DesktopTab tab)
        {
            if (tab == null || selecting || snapshot == null || exiting) return;
            // Bind this operation to this exact snapshot even if a background refresh completes later.
            var selectedSnapshot = snapshot;
            selecting = true;
            popup.SetBusy(true);
            popup.Hide();
            WriteState();
            try
            {
                var result = await Task.Run(delegate { return Program.Select(selectedSnapshot, tab.Tab.Id); });
                if (exiting) return;
                TrySave("last-selection.json", result);
                if (result.Status == "Passed")
                {
                    DesktopModel.Remember(settings, tab);
                    SaveSettings();
                    foreach (var other in tabs.Where(t => t.Window.Handle == tab.Window.Handle)) other.Tab.Selected = other.Identity == tab.Identity;
                    tab.Window.Minimized = false;
                    tabs = DesktopModel.Build(snapshot, settings);
                    popup.SetData(snapshot, tabs);
                    lastProblem = null;
                    lastActionProblem = null;
                    UpdateStatus();
                }
                else
                {
                    lastProblem = FriendlyFailure(result);
                    lastActionProblem = lastProblem;
                    UpdateStatus();
                    // A completed selection must not steal focus back because a later highlight failed.
                    deferredShow |= SelectionFeedback.ReopenAfterFailure(result);
                }
            }
            catch (Exception e)
            {
                lastProblem = UiText.T("切换失败，请刷新后重试。");
                lastActionProblem = lastProblem;
                TrySave("last-error.json", new { Message = BrowserTree.Error(e) });
                UpdateStatus();
                deferredShow = true;
            }
            finally
            {
                selecting = false;
                if (!exiting)
                {
                    popup.SetBusy(false);
                    if (deferredShow) { deferredShow = false; ShowPopup(false); }
                    WriteState();
                }
            }
        }
        static string FriendlyFailure(SelectionResult result)
        {
            if (result.FailureCode != null && result.FailureCode.StartsWith("STALE", StringComparison.Ordinal))
                return UiText.T("这个标签或窗口已经变化，请刷新后重新选择。");
            if (result.FailureCode == "FOREGROUND_DENIED" || result.FailureCode == "FOREGROUND_LOST")
                return UiText.T("Windows 未允许激活目标窗口，请关闭目标窗口上的弹窗后重试。");
            if (result.FailureCode == "HIGHLIGHT_UNVERIFIED") return UiText.T("切换已尝试，但高亮或前台核验未通过，请检查目标窗口。");
            return UiText.T("暂时无法切到这个标签，请刷新后重试。");
        }
        void ChangeShortcut()
        {
            if (selecting) return;
            popup.SuppressDeactivate = true;
            try
            {
                ShowPopup(false);
                using (var dialog = new HotkeyDialog(settings.HotkeyModifiers, settings.HotkeyKey))
                {
                    if (dialog.ShowDialog(popup) != DialogResult.OK) return;
                    string error;
                    if (!hotkey.Set(dialog.Modifiers, dialog.Key, out error)) { MessageBox.Show(popup, error, UiText.T("快捷键不可用"), MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                    settings.HotkeyModifiers = dialog.Modifiers; settings.HotkeyKey = dialog.Key;
                    SaveSettings();
                    popup.HotkeyText = GlobalHotkey.Describe(dialog.Modifiers, dialog.Key);
                    UpdateMenuLanguage();
                    tray.Text = "TabPilot · " + popup.HotkeyText;
                    lastProblem = null;
                    UpdateStatus();
                    WriteState();
                }
            }
            finally { popup.SuppressDeactivate = false; popup.FocusSearch(); }
        }
        void ShowDetails()
        {
            popup.SuppressDeactivate = true;
            try
            {
                string details = popup.StatusText + Environment.NewLine + UiText.T("数据目录：") + DesktopApp.DataDirectory + Environment.NewLine + ScanFeedback.Details(snapshot);
                string path = Path.Combine(DesktopApp.DataDirectory, "last-selection.json");
                if (File.Exists(path))
                {
                    try
                    {
                        var result = Program.Read<SelectionResult>(path);
                        if (result.Status != "Passed") details += Environment.NewLine + result.FailureCode + ": " + result.Detail;
                    }
                    catch (Exception e) { details += Environment.NewLine + UiText.T("操作记录读取失败：") + e.Message; }
                }
                MessageBox.Show(popup, details, UiText.T("TabPilot · 操作详情"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally { popup.SuppressDeactivate = false; }
        }
        void UpdateMenuLanguage()
        {
            trayMenu.Items[0].Text = UiText.T("打开 TabPilot");
            trayMenu.Items[1].Text = UiText.T("刷新标签");
            shortcutMenu.Text = UiText.F("修改快捷键…（{0}）", popup.HotkeyText);
            trayMenu.Items[5].Text = UiText.T("退出 TabPilot");
            ((ToolStripMenuItem)languageMenu.DropDownItems[0]).Checked = UiText.Language == "zh-CN";
            ((ToolStripMenuItem)languageMenu.DropDownItems[1]).Checked = UiText.Language == "en";
        }
        void ChangeLanguage(string language)
        {
            if (!UiText.Supported(language) || selecting || UiText.Language == language) return;
            UiText.SetLanguage(language);
            settings.Language = language;
            lastProblem = UiText.RelocalizeMessage(lastProblem);
            lastActionProblem = UiText.RelocalizeMessage(lastActionProblem);
            popup.ApplyLanguage();
            popup.HotkeyText = GlobalHotkey.Describe(settings.HotkeyModifiers, settings.HotkeyKey);
            tray.Text = "TabPilot · " + popup.HotkeyText;
            if (snapshot != null) { tabs = DesktopModel.Build(snapshot, settings); popup.SetData(snapshot, tabs); }
            SaveSettings();
            UpdateMenuLanguage(); UpdateStatus(); WriteState();
        }
        void WriteState()
        {
            if (exiting || popup.IsDisposed) return;
            stateDirty = !TrySave("ui-state.json", new { Visible = popup.Visible, Scanning = scanning, Selecting = selecting,
                WindowHandle = Native.Handle(popup.Handle), Dpi = Native.GetDpiForWindow(popup.Handle), HotkeyRegistered = hotkey.Registered,
                HotkeyModifiers = settings.HotkeyModifiers, HotkeyKey = settings.HotkeyKey,
                Hotkey = popup.HotkeyText, Mode = popup.Mode.ToString(), Query = popup.Query, Total = tabs.Count,
                HotkeyHint = popup.HotkeyHintText, Version = AppVersion.Informational,
                SelectedId = popup.SelectedId, ListRevision = popup.ListRevision,
                Bounds = new Box { Left = popup.Left, Top = popup.Top, Right = popup.Right, Bottom = popup.Bottom },
                SavedPlacement = settings.PopupPlacement,
                DataDirectory = DesktopApp.DataDirectory, SettingsPending = pendingSettings.Dirty, SettingsError = pendingSettings.Error,
                Language = UiText.Language,
                ScanWarning = ScanFeedback.From(snapshot).Warning,
                Displayed = popup.DisplayedTabs.Select(t => new { t.Tab.Id, t.Title, t.Subtitle, t.MonitorLabel, t.Identity }).ToArray(),
                GroupHeadings = popup.GroupHeadings.ToArray(), Status = popup.StatusText, CapturedUtc = DateTime.UtcNow.ToString("o") });
        }
        static bool TrySave(string filename, object value)
        { try { Program.Save(Path.Combine(DesktopApp.DataDirectory, filename), value); return true; } catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; } }
        void Quit()
        {
            if (exiting) return;
            TrackPlacement(); FlushSettings();
            exiting = true;
            messages.Stop();
            Program.StopWorkers();
            hotkey.Dispose();
            tray.Visible = false;
            popup.AllowClose = true;
            popup.Close();
            ExitThread();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                exiting = true;
                FlushSettings(); saveSettings.Dispose();
                messages.Dispose(); hotkey.Dispose(); tray.Dispose(); trayMenu.Dispose();
                Program.StopWorkers();
                popup.AllowClose = true; popup.Dispose();
            }
            base.Dispose(disposing);
        }
    }
    internal sealed class PopupForm : Form
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr hwnd, IntPtr rect, IntPtr region, uint flags);
        readonly Label heading = new Label { Text = "TabPilot", AutoSize = false };
        readonly Label count = new Label { TextAlign = ContentAlignment.MiddleRight, AutoSize = false };
        readonly SearchField search = new SearchField();
        readonly QuietButton refresh = new QuietButton { Text = "刷新", AccessibleName = "刷新标签" };
        readonly QuietButton shortcut = new QuietButton { Text = "快捷键", AccessibleName = "设置呼出快捷键" };
        readonly QuietButton language = new QuietButton { Text = "中文 / EN", AccessibleName = "Language / 语言" };
        readonly ContextMenuStrip languages = new ContextMenuStrip();
        readonly QuietButton close = new QuietButton { Text = "×", AccessibleName = "隐藏 TabPilot" };
        readonly QuietButton[] modes = { new QuietButton { Text = "全部" }, new QuietButton { Text = "按屏幕" }, new QuietButton { Text = "最近使用" } };
        readonly TabRows rows = new TabRows();
        readonly Label empty = new Label { TextAlign = ContentAlignment.MiddleCenter, AutoSize = false };
        readonly Label hint = new Label { Text = "↑↓ 选择 · Enter 定位 · Esc 隐藏", AutoSize = false };
        readonly Label hotkeyHint = new Label { AutoSize = false, AutoEllipsis = true };
        readonly Label status = new Label { AutoSize = false };
        readonly QuietButton details = new QuietButton { Text = "详情", AccessibleName = "查看操作详情" };
        readonly ToolTip tips = new ToolTip();
        readonly List<Font> ownedFonts = new List<Font>();
        readonly Dictionary<string, Font> fontCache = new Dictionary<string, Font>();
        Snapshot snapshot;
        List<DesktopTab> tabs = new List<DesktopTab>();
        bool scanning, busy, layingOut, filtering, changingDpi, repaintQueued;
        Size roundedSize;
        int roundedDpi;
        string hotkeyText = "Ctrl + Alt + 空格";
        internal bool AllowClose, SuppressDeactivate;
        internal GlobalHotkey Hotkey;
        internal bool DpiChanging { get { return changingDpi; } }
        internal event Action HotkeyRequested, RefreshRequested, DetailsRequested, ViewChanged, ShortcutRequested, ExitRequested;
        internal event Action DpiSettled;
        internal event Action<DesktopTab> ActivateRequested;
        internal event Action<string> LanguageRequested;
        internal TabViewMode Mode { get; private set; }
        internal string Query { get { return search.Input.Text; } }
        internal int SelectedId { get { return rows.CurrentTab == null ? 0 : rows.CurrentTab.Tab.Id; } }
        internal int ListRevision { get { return rows.RebuildCount; } }
        internal string StatusText { get { return status.Text; } }
        internal string HotkeyHintText { get { return hotkeyHint.Text; } }
        internal List<DesktopTab> DisplayedTabs { get; private set; }
        internal List<string> GroupHeadings { get; private set; }
        internal string HotkeyText { get { return hotkeyText; } set { hotkeyText = value; UpdateHotkeyHint(); } }
        internal PopupForm()
        {
            Text = "TabPilot — 标签切换";
            AccessibleName = "TabPilot 标签切换器";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.White;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.Opaque | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            AllowTransparency = false;
            ClientSize = new Size(900, 660);
            DisplayedTabs = new List<DesktopTab>(); GroupHeadings = new List<string>();
            Controls.AddRange(new Control[] { heading, count, search, refresh, shortcut, language, close, rows, empty, hotkeyHint, hint, status, details });
            for (int i = 0; i < modes.Length; i++)
            {
                int mode = i;
                modes[i].Click += delegate { SetMode((TabViewMode)mode); };
                Controls.Add(modes[i]);
            }
            search.Input.TextChanged += delegate { Filter(); };
            refresh.Click += delegate { if (RefreshRequested != null) RefreshRequested(); };
            close.Click += delegate { Hide(); };
            shortcut.Click += delegate { if (ShortcutRequested != null) ShortcutRequested(); };
            languages.Items.Add("简体中文", null, delegate { if (LanguageRequested != null) LanguageRequested("zh-CN"); });
            languages.Items.Add("English", null, delegate { if (LanguageRequested != null) LanguageRequested("en"); });
            languages.Opening += delegate { SuppressDeactivate = true; };
            languages.Closed += delegate { SuppressDeactivate = false; if (Visible && Native.GetForegroundWindow() != Handle) Hide(); };
            language.Click += delegate { languages.Show(language, new Point(0, language.Height)); };
            details.Click += delegate { if (DetailsRequested != null) DetailsRequested(); };
            rows.ActivatedTab += delegate(DesktopTab tab) { if (!busy && ActivateRequested != null) ActivateRequested(tab); };
            rows.SelectedIndexChanged += delegate { if (!filtering && ViewChanged != null) ViewChanged(); };
            Deactivate += delegate { if (!SuppressDeactivate && !busy) Hide(); };
            heading.MouseDown += DragHeader;
            MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Y < ScalePx(46)) DragHeader(sender, e); };
            tips.SetToolTip(count, "显示当前能读取到的标签；部分折叠组或窗口可能有遗漏。");
            tips.SetToolTip(refresh, "重新读取 Chrome 标签（F5）");
            tips.SetToolTip(modes[2], "通过 TabPilot 成功切换过的标签，最近使用的排在前面。");
            ApplyLanguage();
        }
        internal void ApplyLanguage()
        {
            Text = UiText.T("TabPilot — 标签切换"); AccessibleName = UiText.T("TabPilot 标签切换器");
            refresh.Text = UiText.T("刷新"); refresh.AccessibleName = UiText.T("刷新标签");
            shortcut.Text = UiText.T("快捷键"); shortcut.AccessibleName = UiText.T("设置呼出快捷键");
            close.AccessibleName = UiText.T("隐藏 TabPilot");
            modes[0].Text = UiText.T("全部"); modes[1].Text = UiText.T("按屏幕"); modes[2].Text = UiText.T("最近使用");
            hint.Text = UiText.T("↑↓ 选择 · Enter 定位 · Esc 隐藏");
            UpdateHotkeyHint();
            details.Text = UiText.T("详情"); details.AccessibleName = UiText.T("查看操作详情");
            rows.AccessibleName = UiText.T("Chrome 标签列表"); search.ApplyLanguage();
            tips.SetToolTip(count, UiText.T("显示当前能读取到的标签；部分折叠组或窗口可能有遗漏。"));
            tips.SetToolTip(refresh, UiText.T("重新读取 Chrome 标签（F5）"));
            tips.SetToolTip(modes[2], UiText.T("通过 TabPilot 成功切换过的标签，最近使用的排在前面。"));
            ((ToolStripMenuItem)languages.Items[0]).Checked = UiText.Language == "zh-CN";
            ((ToolStripMenuItem)languages.Items[1]).Checked = UiText.Language == "en";
            if (snapshot != null) count.Text = UiText.F("{0} 个屏幕 · 读取到 {1} 个标签", snapshot.Monitors.Count, tabs.Count);
            UpdateEmptyState(); LayoutControls();
        }
        void DragHeader(object sender, MouseEventArgs e)
        { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero); } }
        internal void PlaceOn(Screen screen)
        {
            var work = screen.WorkingArea;
            Location = new Point(work.Left + 24, work.Top + 24);
            float scale = Native.GetDpiForWindow(Handle) / 96f;
            int width = Math.Min((int)(900 * scale), work.Width - (int)(48 * scale));
            int height = Math.Min((int)(660 * scale), work.Height - (int)(48 * scale));
            SetBounds(work.Left + (work.Width - width) / 2, work.Top + (work.Height - height) / 2, width, height);
            LayoutControls();
        }
        internal void RestorePlacement(SavedPopupPlacement saved, Screen firstUseScreen)
        {
            if (!PopupPlacement.Valid(saved)) { PlaceOn(firstUseScreen); return; }
            var monitor = PopupPlacement.Monitor(saved, Native.Monitors(), firstUseScreen.DeviceName);
            Location = new Point(monitor.WorkArea.Left + 24, monitor.WorkArea.Top + 24);
            uint dpi = Math.Max(96, Native.GetDpiForWindow(Handle));
            Bounds = PopupPlacement.Restore(saved, monitor.WorkArea, dpi);
            LayoutControls();
        }
        internal SavedPopupPlacement CapturePlacement()
        {
            var screen = Screen.FromRectangle(Bounds);
            var work = screen.WorkingArea;
            return new SavedPopupPlacement { MonitorDevice = screen.DeviceName, Dpi = Math.Max(96, Native.GetDpiForWindow(Handle)),
                Bounds = new Box { Left = Left, Top = Top, Right = Right, Bottom = Bottom },
                WorkArea = new Box { Left = work.Left, Top = work.Top, Right = work.Right, Bottom = work.Bottom } };
        }
        int ScalePx(float value) { return (int)Math.Round(value * (IsHandleCreated ? Math.Max(96, Native.GetDpiForWindow(Handle)) / 96f : 1)); }
        Font UiFont(float pixels, FontStyle style = FontStyle.Regular)
        {
            string key = ScalePx(pixels) + ":" + style;
            Font font;
            if (fontCache.TryGetValue(key, out font)) return font;
            font = new Font("Microsoft YaHei UI", ScalePx(pixels), style, GraphicsUnit.Pixel);
            fontCache.Add(key, font); ownedFonts.Add(font); return font;
        }
        void LayoutControls()
        {
            if (layingOut || changingDpi || IsDisposed || Disposing) return;
            layingOut = true;
            SuspendLayout();
            try
            {
                float s = IsHandleCreated ? Native.GetDpiForWindow(Handle) / 96f : 1;
                int p = ScalePx(20), width = ClientSize.Width - p * 2;
                heading.SetBounds(p, ScalePx(14), ScalePx(155), ScalePx(32)); heading.Font = UiFont(21, FontStyle.Bold);
                close.SetBounds(ClientSize.Width - p - ScalePx(26), ScalePx(16), ScalePx(26), ScalePx(28)); close.Font = UiFont(21);
                refresh.SetBounds(close.Left - ScalePx(68), ScalePx(16), ScalePx(62), ScalePx(28)); refresh.Font = UiFont(12);
                shortcut.SetBounds(refresh.Left - ScalePx(82), ScalePx(16), ScalePx(76), ScalePx(28)); shortcut.Font = UiFont(12);
                language.SetBounds(shortcut.Left - ScalePx(84), ScalePx(16), ScalePx(78), ScalePx(28)); language.Font = UiFont(12);
                count.SetBounds(heading.Right, ScalePx(17), Math.Max(1, language.Left - heading.Right - ScalePx(10)), ScalePx(27)); count.Font = UiFont(12); count.ForeColor = UiPaint.Muted;
                search.UiScale = s; search.Input.Font = UiFont(17);
                search.SetBounds(p, ScalePx(59), width, ScalePx(48)); search.Relayout();
                for (int i = 0; i < modes.Length; i++)
                {
                    modes[i].UiScale = s; modes[i].Font = UiFont(15);
                    modes[i].ForeColor = UiPaint.Muted;
                    modes[i].SetBounds(p + width * i / 3, ScalePx(123), width / 3 - ScalePx(4), ScalePx(34));
                }
                rows.SetMetrics(s, UiFont(18), UiFont(12.5f));
                rows.SetBounds(p, ScalePx(172), width, Math.Max(1, ClientSize.Height - ScalePx(282)));
                empty.Bounds = rows.Bounds; empty.Font = UiFont(14); empty.ForeColor = UiPaint.Muted;
                hotkeyHint.SetBounds(p, ClientSize.Height - ScalePx(90), width, ScalePx(22)); hotkeyHint.Font = UiFont(12); hotkeyHint.ForeColor = UiPaint.Ink;
                hint.SetBounds(p, ClientSize.Height - ScalePx(62), width, ScalePx(22)); hint.Font = UiFont(12); hint.ForeColor = UiPaint.Muted;
                status.SetBounds(p, ClientSize.Height - ScalePx(34), width - ScalePx(56), ScalePx(22)); status.Font = UiFont(11);
                details.SetBounds(ClientSize.Width - p - ScalePx(48), ClientSize.Height - ScalePx(34), ScalePx(48), ScalePx(22)); details.Font = UiFont(11);
                int dpi = (int)Math.Round(s * 96);
                if (roundedSize != Size || roundedDpi != dpi)
                {
                    roundedSize = Size; roundedDpi = dpi;
                    var oldRegion = Region;
                    using (var path = UiPaint.Rounded(new RectangleF(0, 0, Width, Height), ScalePx(18))) Region = new Region(path);
                    if (oldRegion != null) oldRegion.Dispose();
                }
                Invalidate();
            }
            finally { ResumeLayout(false); layingOut = false; }
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); if (search != null) LayoutControls(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.White);
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var path = UiPaint.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), ScalePx(18)))
            using (var pen = new Pen(UiPaint.Line, 1)) e.Graphics.DrawPath(pen, path);
            using (var pen = new Pen(UiPaint.Line, 1)) e.Graphics.DrawLine(pen, ScalePx(20), Height - ScalePx(105), Width - ScalePx(20), Height - ScalePx(105));
        }
        protected override CreateParams CreateParams
        {
            get
            {
                var p = base.CreateParams;
                p.ClassStyle |= 0x20000;
                // Whole-window composition can retain an empty surface across mixed-DPI moves.
                // Keep the root opaque; controls buffer their own drawing and update only changed rows.
                return p;
            }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && Hotkey != null && Hotkey.Matches(m.WParam))
            { if (HotkeyRequested != null) HotkeyRequested(); return; }
            if (m.Msg == 0x02E0 && search != null)
            {
                var suggested = (Native.RECT)Marshal.PtrToStructure(m.LParam, typeof(Native.RECT));
                changingDpi = true;
                SuspendLayout();
                try
                {
                    base.WndProc(ref m);
                    var bounds = Rectangle.FromLTRB(suggested.Left, suggested.Top, suggested.Right, suggested.Bottom);
                    if (Bounds != bounds) Bounds = bounds;
                }
                finally { changingDpi = false; ResumeLayout(false); }
                LayoutControls();
                if (DpiSettled != null) DpiSettled();
                if (!repaintQueued && IsHandleCreated && !IsDisposed)
                {
                    repaintQueued = true;
                    BeginInvoke(new Action(delegate
                    {
                        repaintQueued = false;
                        if (IsDisposed || Disposing) return;
                        LayoutControls();
                        if (Visible) RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, 0x0585);
                        if (DpiSettled != null) DpiSettled();
                    }));
                }
                return;
            }
            base.WndProc(ref m);
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Hide(); return true; }
            if (keyData == Keys.Down) { rows.MoveSelection(1); return true; }
            if (keyData == Keys.Up) { rows.MoveSelection(-1); return true; }
            if (keyData == Keys.Enter) { if (!busy && rows.CurrentTab != null && ActivateRequested != null) ActivateRequested(rows.CurrentTab); return true; }
            if (keyData == Keys.F5 || keyData == (Keys.Control | Keys.R)) { if (RefreshRequested != null) RefreshRequested(); return true; }
            if (keyData == (Keys.Control | Keys.D1)) { SetMode(TabViewMode.All); return true; }
            if (keyData == (Keys.Control | Keys.D2)) { SetMode(TabViewMode.Screens); return true; }
            if (keyData == (Keys.Control | Keys.D3)) { SetMode(TabViewMode.Recent); return true; }
            if (keyData == (Keys.Control | Keys.Q)) { if (ExitRequested != null) ExitRequested(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        { if (!AllowClose && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } base.OnFormClosing(e); }
        internal void FocusSearch() { search.Input.Focus(); search.Input.SelectionStart = search.Input.TextLength; }
        internal void ClearSearch() { search.Input.Clear(); }
        internal void SetScanning(bool value)
        { scanning = value; refresh.Enabled = !value && !busy; UpdateEmptyState(); }
        internal void SetBusy(bool value) { busy = value; rows.Enabled = search.Enabled = language.Enabled = !value; refresh.Enabled = !value && !scanning; }
        void UpdateHotkeyHint()
        {
            hotkeyHint.Text = Hotkey != null && !Hotkey.Registered ? UiText.T("快捷键未注册，可从托盘修改。") : UiText.F("快捷键：{0} 呼出 / 收起", hotkeyText);
            hotkeyHint.AccessibleName = hotkeyHint.Text;
            tips.SetToolTip(hotkeyHint, hotkeyHint.Text);
        }
        internal void SetStatus(string text, bool error, bool showDetails = false)
        {
            status.Text = String.IsNullOrEmpty(text) ? hotkeyText + UiText.T(" 呼出 · F5 刷新") : text;
            status.ForeColor = error ? Color.FromArgb(173, 71, 34) : UiPaint.Muted;
            details.Visible = error || showDetails;
            tips.SetToolTip(status, status.Text);
        }
        internal void SetData(Snapshot value, List<DesktopTab> items)
        {
            snapshot = value; tabs = items;
            count.Text = UiText.F("{0} 个屏幕 · 读取到 {1} 个标签", value.Monitors.Count, items.Count);
            Filter();
        }
        void SetMode(TabViewMode mode) { Mode = mode; Filter(); FocusSearch(); }
        void Filter()
        {
            if (rows == null || DisplayedTabs == null) return;
            DisplayedTabs = snapshot == null ? new List<DesktopTab>() : DesktopModel.Filter(tabs, Query, Mode, snapshot);
            GroupHeadings = new List<string>();
            var entries = new List<ListEntry>();
            string previousMonitor = null;
            foreach (var tab in DisplayedTabs)
            {
                if (Mode == TabViewMode.Screens && tab.Window.MonitorHandle != previousMonitor)
                {
                    previousMonitor = tab.Window.MonitorHandle;
                    var monitor = snapshot.Monitors.FirstOrDefault(m => m.Handle == previousMonitor);
                    string text = tab.MonitorLabel + (monitor == null ? "" : " · " + monitor.Device.Replace(@"\\.\", "") + " · " + monitor.ScalePercent + "%");
                    GroupHeadings.Add(text); entries.Add(new ListEntry { Heading = text });
                }
                entries.Add(new ListEntry { Tab = tab });
            }
            filtering = true;
            try
            {
                rows.ApplyEntries(entries);
            }
            finally { filtering = false; }
            UpdateEmptyState();
            for (int i = 0; i < modes.Length; i++) modes[i].Chosen = i == (int)Mode;
            if (ViewChanged != null) ViewChanged();
        }
        void UpdateEmptyState()
        {
            if (DisplayedTabs == null) return;
            empty.Visible = DisplayedTabs.Count == 0;
            empty.Text = UiText.T(scanning && tabs.Count == 0 ? "正在读取 Chrome 标签…" : Mode == TabViewMode.Recent && String.IsNullOrWhiteSpace(Query) ?
                "还没有最近使用的标签\n通过 TabPilot 切换后会出现在这里" : String.IsNullOrWhiteSpace(Query) ? "暂时没有读取到 Chrome 标签\n打开 Chrome 后按 F5 刷新" :
                "没有找到匹配的标签\n试试网页标题或屏幕名称");
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) { tips.Dispose(); languages.Dispose(); foreach (var font in ownedFonts) font.Dispose(); ownedFonts.Clear(); fontCache.Clear(); }
        }
    }
}
