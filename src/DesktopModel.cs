using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace TabPilot
{
    internal enum TabViewMode { All, Screens, Recent }
    public sealed class RecentTab
    {
        public string Identity;
        public DateTime UsedUtc;
    }
    public sealed class DesktopSettings
    {
        public uint HotkeyModifiers = 3;
        public int HotkeyKey = 32;
        public string Language = "zh-CN";
        public SavedPopupPlacement PopupPlacement;
        public List<RecentTab> Recent = new List<RecentTab>();
    }
    internal sealed class DesktopTab
    {
        internal WindowRecord Window;
        internal TabRecord Tab;
        internal string Identity, Title, Subtitle, MonitorLabel;
        internal int WindowNumber;
        internal DateTime LastUsedUtc;
        public override string ToString() { return Title + "，" + MonitorLabel + "，" + Subtitle; }
    }
    internal static class DesktopModel
    {
        internal static string SafeText(string text)
        {
            return text == null ? "" : new String(text.Select(c => Char.IsControl(c) || Char.GetUnicodeCategory(c) == UnicodeCategory.Format ? ' ' : c).ToArray());
        }
        internal static string DisplayTitle(string raw)
        {
            string clean = SafeText(raw);
            clean = Regex.Replace(clean, @"\s+-\s+(?:内存用量(?:高)?|Memory usage(?:\s+high)?)\s*[-:]?.*$", "", RegexOptions.IgnoreCase);
            return String.IsNullOrWhiteSpace(clean) ? UiText.T("未命名标签") : clean.Trim();
        }
        internal static string Identity(WindowRecord w, TabRecord t)
        { return w.Handle + "|" + w.Pid + "|" + w.ProcessStartUtc + "|" + t.RuntimeId; }
        internal static string MonitorLabel(Snapshot snapshot, string handle)
        {
            var monitor = snapshot.Monitors.FirstOrDefault(m => m.Handle == handle);
            if (monitor == null) return UiText.T("未知屏幕");
            if (snapshot.Monitors.Count == 1) return UiText.T("主屏幕");
            var primary = snapshot.Monitors.FirstOrDefault(m => m.Primary) ?? snapshot.Monitors[0];
            double cx = (monitor.Bounds.Left + monitor.Bounds.Right) / 2.0;
            double cy = (monitor.Bounds.Top + monitor.Bounds.Bottom) / 2.0;
            double dx = cx - (primary.Bounds.Left + primary.Bounds.Right) / 2.0;
            double dy = cy - (primary.Bounds.Top + primary.Bounds.Bottom) / 2.0;
            string label = monitor == primary ? "主屏幕" : Math.Abs(dx) >= Math.Abs(dy) ? (dx < 0 ? "左侧屏幕" : "右侧屏幕") : (dy < 0 ? "上方屏幕" : "下方屏幕");
            // Device suffix remains available when multiple monitors share the same relative direction.
            return UiText.T(label);
        }
        internal static List<DesktopTab> Build(Snapshot snapshot, DesktopSettings settings)
        {
            var recent = (settings.Recent ?? new List<RecentTab>()).Where(r => r.Identity != null)
                .GroupBy(r => r.Identity).ToDictionary(g => g.Key, g => g.Max(r => r.UsedUtc));
            var result = new List<DesktopTab>();
            int windowNumber = 0;
            foreach (var w in snapshot.Windows.Where(w => w.Eligible))
            {
                windowNumber++;
                foreach (var tab in w.Scan.Tabs)
                {
                    string identity = Identity(w, tab);
                    DateTime used;
                    recent.TryGetValue(identity, out used);
                    string context = UiText.T("窗口 ") + windowNumber + (w.Minimized ? UiText.T(" · 已最小化") : tab.Selected ? UiText.T(" · 当前标签") : " · " + SafeText(w.Title).Replace(" - Google Chrome", ""));
                    result.Add(new DesktopTab { Window = w, Tab = tab, WindowNumber = windowNumber, Identity = identity,
                        Title = DisplayTitle(tab.Title), Subtitle = context, MonitorLabel = MonitorLabel(snapshot, w.MonitorHandle), LastUsedUtc = used });
                }
            }
            return result;
        }
        internal static List<DesktopTab> Filter(IEnumerable<DesktopTab> tabs, string query, TabViewMode mode, Snapshot snapshot)
        {
            var terms = (query ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            var result = tabs.Where(tab => terms.All(term => (tab.Title + " " + tab.MonitorLabel + " " + tab.Tab.Title + " " + UiText.T("窗口 ") + tab.WindowNumber)
                .IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
            if (mode == TabViewMode.Recent) result = result.Where(t => t.LastUsedUtc != default(DateTime)).OrderByDescending(t => t.LastUsedUtc);
            if (mode == TabViewMode.Screens) result = result.OrderBy(t =>
            {
                var monitor = snapshot.Monitors.FirstOrDefault(m => m.Handle == t.Window.MonitorHandle);
                return monitor == null ? Int32.MaxValue : monitor.Bounds.Left;
            }).ThenBy(t => t.WindowNumber).ThenBy(t => t.Tab.Ordinal);
            return result.ToList();
        }
        internal static void Remember(DesktopSettings settings, DesktopTab tab)
        {
            settings.Recent = (settings.Recent ?? new List<RecentTab>()).Where(r => r.Identity != tab.Identity).ToList();
            settings.Recent.Insert(0, new RecentTab { Identity = tab.Identity, UsedUtc = DateTime.UtcNow });
            settings.Recent = settings.Recent.Take(200).ToList();
        }
    }
}
