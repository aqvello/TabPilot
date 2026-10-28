using System;
using System.Linq;
using System.Windows.Forms;
using TabPilot;

internal static class LanguageChecks
{
    internal static void Run(Action<bool, string> check)
    {
        try
        {
            var snapshot = new Snapshot();
            snapshot.Monitors.Add(new MonitorRecord { Handle = "demo", Primary = true, Bounds = new Box { Left = 0, Top = 0, Right = 1920, Bottom = 1080 } });
            var window = new WindowRecord { Handle = "0xDE00", Pid = 100, ProcessStartUtc = "demo", MonitorHandle = "demo", Eligible = true,
                Title = "示例窗口", Scan = new ScanResult { Status = "Enumerated" } };
            window.Scan.Tabs.Add(new TabRecord { Id = 1, RuntimeId = "demo-tab", Title = "主屏幕 — Example page", Selected = true });
            snapshot.Windows.Add(window);
            var settings = new DesktopSettings();
            string identity = DesktopModel.Build(snapshot, settings)[0].Identity;
            UiText.SetLanguage("en");
            var rows = DesktopModel.Build(snapshot, settings);
            check(rows[0].Title == "主屏幕 — Example page" && rows[0].Identity == identity, "language change preserves original page title and exact identity");
            check(rows[0].MonitorLabel == "Main monitor" && rows[0].Subtitle == "Window 1 · Active tab", "English monitor and window context labels");
            check(DesktopModel.Filter(rows, "main monitor", TabViewMode.All, snapshot).Count == 1, "search matches localized monitor labels");
            using (var form = new PopupForm())
            {
                form.SetData(snapshot, rows);
                check(form.Controls.OfType<Button>().Any(b => b.Text == "All tabs") && form.Controls.OfType<Button>().Any(b => b.Text == "Shortcut"), "English popup navigation and shortcut labels");
                foreach (string language in new[] { "en", "zh-CN" })
                {
                    UiText.SetLanguage(language); form.ApplyLanguage();
                    form.HotkeyText = GlobalHotkey.Describe(3, (int)Keys.Q);
                    string prefix = language == "en" ? "Shortcut:" : "快捷键：";
                    form.SetStatus(ScanFeedback.From(snapshot).Summary, false, true);
                    check(form.HotkeyHintText.StartsWith(prefix) && form.HotkeyHintText.Contains("Ctrl + Alt + Q"), language + " current shortcut survives successful scan status");
                    window.Scan.Groups.Add(new GroupRecord { State = "Collapsed" });
                    var feedback = ScanFeedback.From(snapshot);
                    form.SetStatus(feedback.Summary, feedback.Warning, true);
                    form.SetScanning(true); form.SetScanning(false);
                    check(form.HotkeyHintText.Contains("Ctrl + Alt + Q") && form.StatusText == feedback.Summary, language + " scan and warning preserve shortcut and diagnostic text independently");
                    window.Scan.Groups.Clear();
                    form.SetStatus(UiText.T("读取标签失败，可以按 F5 重试。"), true);
                    string error = form.StatusText;
                    form.HotkeyText = GlobalHotkey.Describe(3, (int)Keys.T);
                    check(form.HotkeyHintText.Contains("Ctrl + Alt + T") && !form.HotkeyHintText.Contains("Ctrl + Alt + Q") && form.StatusText == error, language + " shortcut changes immediately without overwriting an error");
                    form.HotkeyText = GlobalHotkey.Describe(3, 32); form.ApplyLanguage();
                    check(form.HotkeyHintText.StartsWith(prefix) && form.HotkeyHintText.Contains(UiText.T("空格")), language + " current shortcut uses localized Space label");
                    using (var unregistered = new GlobalHotkey(form.Handle))
                    {
                        form.Hotkey = unregistered; form.HotkeyText = GlobalHotkey.Describe(3, 32);
                        check(form.HotkeyHintText == UiText.T("快捷键未注册，可从托盘修改。"), language + " unregistered shortcut is not advertised as active");
                        form.Hotkey = null;
                    }
                }
                UiText.SetLanguage("zh-CN"); form.ApplyLanguage();
                check(form.Controls.OfType<Button>().Any(b => b.Text == "全部") && form.Query == "", "popup can switch back to Chinese without recreation");
            }
            UiText.SetLanguage("en");
            using (var dialog = new HotkeyDialog(3, 32))
                check(dialog.Text == "Change popup shortcut" && dialog.Controls.OfType<Button>().Any(b => b.Text == "Save"), "English shortcut dialog and controls");
            check(GlobalHotkey.Describe(3, 32).EndsWith("Space") && UiText.RelocalizeMessage("设置尚未保存，正在重试：C:\\Window\\Save.txt").EndsWith("C:\\Window\\Save.txt"), "English shortcut label and unchanged diagnostic paths");
            window.Scan.Groups.Add(new GroupRecord { State = "Collapsed" });
            check(ScanFeedback.From(snapshot).Summary.Contains("Collapsed groups") && ScanFeedback.Details(snapshot).Contains("may not include every tab"), "English incomplete-read warning and details");
            settings.Language = "en";
            var saved = Program.Json.Deserialize<DesktopSettings>(Program.Serialize(settings));
            check(saved.Language == "en", "language preference round-trips with existing settings");
            check(Program.Json.Deserialize<DesktopSettings>("{\"HotkeyKey\":81}").Language == "zh-CN", "old settings without language retain Chinese default");
            UiText.SetLanguage("unsupported");
            check(UiText.Language == "zh-CN" && UiText.T("全部") == "全部", "unsupported language safely falls back to Chinese");
        }
        finally { UiText.SetLanguage("zh-CN"); }
    }
}
