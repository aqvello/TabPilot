// Independently invented geometry and titles; no captured or scaled hardware/session data.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using TabPilot;

internal static class GuiChecks
{
    static int passed;
    static void Check(bool condition, string name)
    { if (!condition) throw new InvalidOperationException(name); passed++; Console.WriteLine("PASS " + name); }
    static MonitorRecord Monitor(string handle, int left, int top, int right, int bottom, bool primary)
    {
        var bounds = new Box { Left = left, Top = top, Right = right, Bottom = bottom };
        return new MonitorRecord { Handle = handle, Device = handle, Primary = primary, Bounds = bounds, WorkArea = bounds };
    }
    static void Fonts(Control control)
    { if (control.Font.Height <= 0) throw new InvalidOperationException("Disposed/invalid control font"); foreach (Control child in control.Controls) Fonts(child); }
    [STAThread] static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var s = new Snapshot();
            s.Monitors.Add(Monitor("primary", 0, 0, 1728, 1088, true));
            s.Monitors.Add(Monitor("left", -2464, -192, 0, 1248, false));
            s.Monitors.Add(Monitor("above", 224, -1376, 1952, -192, false));
            var a = new WindowRecord { Handle = "0x1111", Pid = 42, ProcessStartUtc = "start", Eligible = true, MonitorHandle = "primary", Title = "Workspace A" };
            a.Scan.Tabs.Add(new TabRecord { Id = 1, Ordinal = 1, RuntimeId = "same", Title = "Example Project - 内存用量 - 37 MB", Selected = true });
            a.Scan.Tabs.Add(new TabRecord { Id = 2, Ordinal = 2, RuntimeId = "two", Title = "Research report" });
            var b = new WindowRecord { Handle = "0x2222", Pid = 42, ProcessStartUtc = "start", Eligible = true, MonitorHandle = "left", Title = "Workspace B" };
            b.Scan.Tabs.Add(new TabRecord { Id = 3, Ordinal = 1, RuntimeId = "same", Title = "Example Project" });
            s.Windows.Add(a); s.Windows.Add(b);
            var settings = new DesktopSettings();
            var rows = DesktopModel.Build(s, settings);
            Check(rows.Count == 3, "all exposed items retained");
            Check(rows[0].Title == "Example Project" && a.Scan.Tabs[0].Title.Contains("内存用量"), "display cleanup preserves raw name");
            Check(rows[0].Identity != rows[2].Identity, "duplicate title/runtime ID across windows remains distinct");
            Check(DesktopModel.Filter(rows, "example project 左侧", TabViewMode.All, s).Single().Tab.Id == 3, "case-insensitive multi-term title/monitor search");
            a.Title = "Example Project";
            var withWindowTitle = DesktopModel.Build(s, settings);
            Check(DesktopModel.Filter(withWindowTitle, "Example Project", TabViewMode.All, s).Count == 2, "active window title cannot match unrelated tabs");
            Check(DesktopModel.Filter(rows, "no-such-tab", TabViewMode.All, s).Count == 0, "empty search result");
            Check(DesktopModel.Filter(rows, "", TabViewMode.Recent, s).Count == 0, "recent has no fabricated entries");
            DesktopModel.Remember(settings, rows[2]);
            settings.Recent.Add(new RecentTab { Identity = "closed-tab", UsedUtc = DateTime.UtcNow });
            rows = DesktopModel.Build(s, settings);
            Check(DesktopModel.Filter(rows, "", TabViewMode.Recent, s).Single().Tab.Id == 3, "closed history entries omitted");
            DesktopModel.Remember(settings, rows[0]);
            Check(settings.Recent[0].Identity == rows[0].Identity && settings.Recent.Count(r => r.Identity == rows[0].Identity) == 1, "recent updates deduplicate exact identity");
            a.Pid = 43; a.ProcessStartUtc = "new-start";
            rows = DesktopModel.Build(s, settings);
            Check(DesktopModel.Filter(rows, "", TabViewMode.Recent, s).Count == 1, "recycled window/process cannot inherit recent identity");
            Check(DesktopModel.MonitorLabel(s, "left") == "左侧屏幕" && DesktopModel.MonitorLabel(s, "above") == "上方屏幕", "negative and vertical monitor layout labels");
            Check(DesktopModel.Filter(rows, "", TabViewMode.Screens, s).First().Window.Handle == b.Handle, "screen order respects negative coordinates");
            // Arbitrary test units: unequal displays, negative origins and a vertical offset.
            var saved = new SavedPopupPlacement { MonitorDevice = "left", Dpi = 168,
                WorkArea = new Box { Left = -2464, Top = -192, Right = 0, Bottom = 1184 },
                Bounds = new Box { Left = -2272, Top = -64, Right = -1120, Bottom = 832 } };
            Check(PopupPlacement.Monitor(saved, s.Monitors, "above").Device == "left", "saved monitor takes priority over the foreground monitor");
            var restored = PopupPlacement.Restore(saved, saved.WorkArea, 168);
            Check(restored == new Rectangle(-2272, -64, 1152, 896), "same-DPI negative coordinates restore exactly");
            var changedScale = PopupPlacement.Restore(saved, new Box { Left = -2016, Top = 64, Right = 0, Bottom = 1600 }, 144);
            Check(changedScale == new Rectangle(-1851, 174, 987, 768), "DPI changes scale saved offsets and size");
            var rearranged = PopupPlacement.Restore(saved, new Box { Left = 2112, Top = 96, Right = 4576, Bottom = 1472 }, 168);
            Check(rearranged.Location == new Point(2304, 224), "rearranged monitor keeps relative position");
            var remaining = s.Monitors.Where(m => m.Device != "left").ToList();
            Check(PopupPlacement.Monitor(saved, remaining, "above").Device == "primary", "disconnected monitor falls back to nearest available display");
            var small = new Box { Left = 0, Top = 32, Right = 992, Bottom = 640 };
            var fitted = PopupPlacement.Restore(saved, small, 168);
            Check(new Rectangle(0, 32, 992, 608).Contains(fitted), "smaller work area cannot leave restored popup offscreen");
            settings.PopupPlacement = saved;
            var persisted = Program.Json.Deserialize<DesktopSettings>(Program.Json.Serialize(settings));
            Check(PopupPlacement.Same(saved, persisted.PopupPlacement), "placement round-trips with settings across process restart");
            Check(PopupPlacement.Monitor(null, s.Monitors, "above").Device == "above", "first launch without saved position uses foreground display");
            using (var form = new PopupForm())
            {
                form.SetData(s, rows);
                form.PlaceOn(Screen.PrimaryScreen);
                var size = form.ClientSize;
                for (int i = 0; i < 20; i++)
                {
                    form.ClientSize = new Size(size.Width - i % 3 * 6, size.Height - i % 3 * 6);
                    Fonts(form);
                }
                Check(true, "repeated resize does not dispose a font still held by WinForms");
            }
            using (var form = new PopupForm())
            {
                Func<int, Snapshot> inventory = offset =>
                {
                    var value = new Snapshot { Monitors = s.Monitors };
                    var window = new WindowRecord { Handle = "0x4444", Pid = 44, ProcessStartUtc = "stable", Eligible = true, MonitorHandle = "primary", Title = "Stable window" };
                    for (int i = 0; i < 100; i++) window.Scan.Tabs.Add(new TabRecord { Id = offset + i + 1, Ordinal = i + 1, RuntimeId = "r" + i, Title = "Page " + i });
                    value.Windows.Add(window); return value;
                };
                var initial = inventory(0);
                form.PlaceOn(Screen.PrimaryScreen);
                form.SetData(initial, DesktopModel.Build(initial, settings));
                var list = form.Controls.OfType<TabRows>().Single();
                IntPtr nativeHandle = list.Handle;
                list.SelectedIndex = 23; list.TopIndex = 20;
                int top = list.TopIndex, revision = list.RebuildCount;
                object nativeItem = list.Items[23];
                form.SetScanning(true); form.SetScanning(false);
                Check(list.RebuildCount == revision && Object.ReferenceEquals(list.Items[23], nativeItem), "scan status does not clear native list items");
                var replacement = inventory(1000);
                form.SetData(replacement, DesktopModel.Build(replacement, settings));
                Check(list.RebuildCount == revision && Object.ReferenceEquals(list.Items[23], nativeItem), "unchanged refresh reuses native items");
                Check(list.TopIndex == top && list.SelectedIndex == 23 && form.SelectedId == 1024, "refresh preserves scroll and selection while rebinding fresh IDs");
                form.ClientSize = new Size(form.ClientSize.Width - 8, form.ClientSize.Height - 8);
                Check(list.RebuildCount == revision && list.SelectedIndex == 23, "layout changes do not rebuild list data");
                replacement = inventory(2000); replacement.Windows[0].Scan.Tabs[23].Title = "Renamed page";
                form.SetData(replacement, DesktopModel.Build(replacement, settings));
                Check(list.RebuildCount == revision && list.CurrentTab.Title == "Renamed page" && form.SelectedId == 2024, "title changes update existing rows without clearing selection");
                var invalidations = new List<Rectangle>();
                list.Invalidated += delegate(object sender, InvalidateEventArgs e) { invalidations.Add(e.InvalidRect); };
                var itemRect = list.GetItemRectangle(list.TopIndex);
                var mouse = typeof(TabRows).GetMethod("OnMouseMove", BindingFlags.NonPublic | BindingFlags.Instance);
                mouse.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.None, 0, 10, itemRect.Top + 3, 0) });
                Check(invalidations.Count > 0 && invalidations.All(rect => rect.Height <= itemRect.Height), "hover invalidation is limited to affected rows");
                int afterHover = invalidations.Count;
                mouse.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.None, 0, 12, itemRect.Top + 4, 0) });
                Check(invalidations.Count == afterHover, "moving within the same row does not repaint the list");
            }
            Check(!GlobalHotkey.Valid(0, (int)Keys.Q) && !GlobalHotkey.Valid(8, (int)Keys.Q) && !GlobalHotkey.Valid(3, (int)Keys.F12), "plain/reserved hotkeys rejected");
            using (var f1 = new Form()) using (var f2 = new Form()) using (var f3 = new Form())
            using (var first = new GlobalHotkey(f1.Handle)) using (var second = new GlobalHotkey(f2.Handle)) using (var third = new GlobalHotkey(f3.Handle))
            {
                string error;
                int one = 0, two = 0;
                foreach (Keys key in new[] { Keys.F6, Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11 })
                    if (first.Set(7, (int)key, out error)) { one = (int)key; break; }
                foreach (Keys key in new[] { Keys.F6, Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11 })
                    if ((int)key != one && second.Set(7, (int)key, out error)) { two = (int)key; break; }
                Check(one != 0 && two != 0, "temporary global hotkeys registered");
                Check(!first.Set(7, two, out error) && first.Registered && first.Key == one, "conflicting hotkey change preserves old binding");
                second.Dispose();
                Check(first.Set(7, two, out error), "released hotkey can be registered");
                Check(third.Set(7, one, out error), "changing hotkey releases old binding");
            }
            ReliabilityChecks.Run(Check);
            LanguageChecks.Run(Check);
            Console.WriteLine("GUI/model checks: " + passed + " passed");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e.ToString()); return 1; }
    }
}
