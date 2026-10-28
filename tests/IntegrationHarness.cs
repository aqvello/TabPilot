using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using TabPilot;

// Deliberate test-session setup only. Production switching never injects keyboard/mouse input.
internal static class IntegrationHarness
{
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint Type; public INPUTUNION Data; }
    [StructLayout(LayoutKind.Explicit)] struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT Keyboard;
        [FieldOffset(0)] public MOUSEINPUT Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort Vk, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(IntegrationHarness).Assembly.Location), ".."));
    static readonly string Evidence = Path.Combine(Root, "artifacts", "private");
    static readonly string StatePath = Path.Combine(Evidence, "fixture-state.json");
    public class State { public string Handle, OriginalForeground; public List<string> ExpectedTitles = new List<string>(); }

    [STAThread] static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            string command = args.Length == 0 ? "help" : args[0];
            if (command == "prepare") { Prepare(); return 0; }
            var state = Program.Read<State>(StatePath);
            IntPtr h = Native.Hwnd(state.Handle);
            if (!Native.IsWindow(h) || !Native.Title(h).StartsWith("TabPilot Fixture", StringComparison.Ordinal))
                throw new InvalidOperationException("Fixture window missing or title no longer matches; stop without input.");
            if (command == "matrix") { Matrix(state); return 0; }
            if (command == "highlight-here")
            {
                Focus(h); Chord(h, 0x1B);
                var snapshot = Program.Capture(15000);
                var window = snapshot.Windows.Single(w => w.Handle == state.Handle);
                var tab = window.Scan.Tabs.Single(t => t.Title.StartsWith("TabPilot Fixture 19", StringComparison.Ordinal));
                var result = Program.Select(snapshot, tab.Id, Path.Combine(Evidence, "highlight-primary.png"));
                Program.Save(Path.Combine(Evidence, "highlight-idle-retry.json"), result);
                Console.WriteLine(Program.Json.Serialize(result));
                return result.Status == "Passed" ? 0 : 3;
            }
            if (command == "capture") { Capture(state, args[1]); return 0; }
            if (command == "screenshot")
            {
                var b = Native.VisibleFrame(h);
                using (var bitmap = new Bitmap(b.Width, b.Height))
                {
                    using (var g = Graphics.FromImage(bitmap)) g.CopyFromScreen(b.Left, b.Top, 0, 0, bitmap.Size);
                    bitmap.Save(Path.Combine(Evidence, "fixture-desktop.png"), ImageFormat.Png);
                }
                return 0;
            }
            if (command == "group-menu" || command == "ungroup")
            {
                Focus(h);
                Chord(h, 0x1B);
                var root = AutomationElement.FromHandle(h);
                var headers = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ClassNameProperty, "TabGroupHeader"));
                if (headers.Count != 1) throw new InvalidOperationException("Fixture group header not unique");
                var b = headers[0].Current.BoundingRectangle;
                SetCursorPos((int)(b.Left + b.Width / 2), (int)(b.Top + b.Height / 2));
                Send(h, new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = 0x0008 } } },
                    new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = 0x0010 } } });
                Thread.Sleep(300);
                if (command == "ungroup")
                {
                    var buttons = Roots(h).SelectMany(r => r.FindAll(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)).Cast<AutomationElement>())
                        .Where(e => e.Current.Name == "取消分组").GroupBy(BrowserTree.RuntimeId).Select(g => g.First()).ToArray();
                    if (buttons.Length != 1) throw new InvalidOperationException("Ungroup button not unique; no cleanup action taken");
                    ((InvokePattern)buttons[0].GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                    if (!Switcher.Wait(delegate { return BrowserTree.Read(h).Result.Groups.Count == 0; }, 1000))
                    {
                        // Provider may accept Invoke without performing it. Reobserve and verify the test-only fallback.
                        var fresh = Roots(h).SelectMany(r => r.FindAll(TreeScope.Descendants,
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)).Cast<AutomationElement>())
                            .Where(e => e.Current.Name == "取消分组" && !e.Current.IsOffscreen).GroupBy(BrowserTree.RuntimeId).Select(g => g.First()).ToArray();
                        if (fresh.Length != 1) throw new InvalidOperationException("Ungroup invoke unverified; button unavailable for cleanup");
                        var bounds = fresh[0].Current.BoundingRectangle;
                        IntPtr foreground = Native.GetForegroundWindow();
                        if (foreground != h && Native.GetWindow(foreground, 4) != h) throw new InvalidOperationException("Cleanup popup lost fixture ownership");
                        SetCursorPos((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2));
                        Send(foreground, new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = 2 } } },
                            new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = 4 } } });
                    }
                    if (!Switcher.Wait(delegate { return BrowserTree.Read(h).Result.Groups.Count == 0; }, 2000))
                        throw new InvalidOperationException("Ungroup cleanup was not verified");
                }
                Dump(h); return 0;
            }
            if (command == "menu" || command == "create-group")
            {
                Focus(h);
                Chord(h, 0x1B);
                var tree = BrowserTree.Read(h);
                var tab = tree.Tabs.First();
                var b = tab.Record.Bounds;
                if (b == null || b.Width <= 0 || tab.Record.Offscreen) throw new InvalidOperationException("First fixture tab not visible.");
                SetCursorPos(b.Left + b.Width / 2, b.Top + b.Height / 2);
                Send(h, new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = 0x0008 } } },
                    new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = 0x0010 } } });
                Thread.Sleep(300);
                if (command == "create-group")
                {
                    var exact = Roots(h).SelectMany(root => root.FindAll(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)).Cast<AutomationElement>())
                        .Where(e => e.Current.Name == "添加到新标签页组").GroupBy(BrowserTree.RuntimeId).Select(g => g.First()).ToArray();
                    if (exact.Length != 1) throw new InvalidOperationException("Create-group menu item missing or ambiguous");
                    object invoke;
                    if (!exact[0].TryGetCurrentPattern(InvokePattern.Pattern, out invoke)) throw new InvalidOperationException("No menu invoke pattern");
                    ((InvokePattern)invoke).Invoke();
                    Thread.Sleep(300);
                    IntPtr foreground = Native.GetForegroundWindow();
                    if (foreground != h && Native.GetWindow(foreground, 4) != h) throw new InvalidOperationException("Group editor lost fixture ownership");
                    Chord(foreground, 0x1B); // retain unnamed group, dismiss editor; do not change settings.
                    Thread.Sleep(200);
                }
                Dump(h); return 0;
            }
            if (command == "invoke-menu")
            {
                var items = Roots(h).SelectMany(root => root.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)).Cast<AutomationElement>());
                var exact = items.Where(e => e.Current.Name == args[1]).GroupBy(BrowserTree.RuntimeId).Select(g => g.First()).ToArray();
                if (exact.Length != 1) throw new InvalidOperationException("Menu name not uniquely exposed: " + args[1]);
                object p;
                if (!exact[0].TryGetCurrentPattern(InvokePattern.Pattern, out p)) throw new InvalidOperationException("Menu has no InvokePattern");
                ((InvokePattern)p).Invoke(); Thread.Sleep(300); Dump(h); return 0;
            }
            if (command == "dismiss") { Focus(h); Chord(h, 0x1B); Dump(h); return 0; }
            if (command == "collapse")
            {
                Focus(h);
                var root = AutomationElement.FromHandle(h);
                var tabs = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Tab));
                var groups = tabs.Cast<AutomationElement>().Where(e => args.Length > 1 ? e.Current.Name == args[1] : e.Current.ClassName == "TabGroupHeader").ToArray();
                if (groups.Length != 1) throw new InvalidOperationException("Group name is not unique");
                object pattern;
                if (groups[0].TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out pattern)) ((ExpandCollapsePattern)pattern).Collapse();
                else throw new InvalidOperationException("Group has no ExpandCollapsePattern");
                Thread.Sleep(300); Dump(h); return 0;
            }
            if (command == "dump") { Dump(h); return 0; }
            if (command == "close")
            {
                var tabs = BrowserTree.Read(h).Tabs;
                if (tabs.Count != state.ExpectedTitles.Count || tabs.Any(t => !t.Record.Title.StartsWith("TabPilot Fixture ", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Fixture contents changed; refuse to close a window containing unknown tabs");
                if (!PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero) || !Switcher.Wait(delegate { return !Native.IsWindow(h); }, 3000))
                    throw new InvalidOperationException("Fixture window did not close");
                return 0;
            }
            throw new ArgumentException("Commands: prepare, matrix, capture NAME, menu, invoke-menu EXACT_NAME, dismiss, collapse EXACT_GROUP_NAME, dump, close");
        }
        catch (Exception e) { Console.Error.WriteLine(BrowserTree.Error(e)); return 1; }
    }
    static void Prepare()
    {
        if (File.Exists(StatePath))
        {
            var previous = Program.Read<State>(StatePath);
            if (Native.IsWindow(Native.Hwnd(previous.Handle))) throw new InvalidOperationException("Existing fixture still open; use it instead of creating another.");
        }
        var before = Native.Windows().Where(w => w.Eligible).ToArray();
        var source = before.First(w => !w.Minimized);
        IntPtr originalForeground = Native.GetForegroundWindow();
        Focus(Native.Hwnd(source.Handle));
        Chord(Native.Hwnd(source.Handle), 0x11, 0x4E); // Ctrl+N, no Chrome launch command/arguments.
        WindowRecord fixture = null;
        if (!Switcher.Wait(delegate
        {
            var added = Native.Windows().Where(w => w.Eligible && !before.Any(old => old.Handle == w.Handle)).ToArray();
            if (added.Length > 1) throw new InvalidOperationException("Multiple new Chrome windows; ambiguous fixture.");
            fixture = added.SingleOrDefault(); return fixture != null;
        }, 5000)) throw new TimeoutException("Ctrl+N did not create a Chrome window");
        var state = new State { Handle = fixture.Handle, OriginalForeground = Native.Handle(originalForeground) };
        IntPtr h = Native.Hwnd(fixture.Handle);
        string pages = Path.Combine(Evidence, "fixture-pages");
        Directory.CreateDirectory(pages);
        for (int i = 1; i <= 24; i++)
        {
            string title = "TabPilot Fixture " + i.ToString("D2");
            state.ExpectedTitles.Add(title);
            string file = Path.Combine(pages, i.ToString("D2") + ".html");
            File.WriteAllText(file, "<!doctype html><meta charset=utf-8><title>" + title + "</title><h1>" + title +
                "</h1><p>Local PoC fixture. No network.</p><div role=tablist><button role=tab>DOM DECOY TAB</button></div>", Encoding.UTF8);
            Focus(h);
            if (i > 1) Chord(h, 0x11, 0x54);
            Chord(h, 0x11, 0x4C);
            Text(h, new Uri(file).AbsoluteUri);
            Chord(h, 0x0D);
            if (!Switcher.Wait(delegate { return Native.Title(h).StartsWith(title, StringComparison.Ordinal); }, 5000))
                throw new TimeoutException("Fixture title did not load: " + title);
            Console.WriteLine("Prepared " + title);
            Program.Save(StatePath, state);
        }
        Capture(state, "fixture-initial");
    }
    static Snapshot Capture(State state, string name)
    {
        var snapshot = Program.Capture(15000);
        Program.Save(Path.Combine(Evidence, name + ".json"), snapshot);
        var window = snapshot.Windows.Single(w => w.Handle == state.Handle);
        var found = state.ExpectedTitles.Where(t => window.Scan.Tabs.Any(tab => tab.Title == t || tab.Title.StartsWith(t + " -", StringComparison.Ordinal))).ToArray();
        Console.WriteLine("{0}: expected={1}, matched={2}, exposed={3}, groups={4}", name, state.ExpectedTitles.Count, found.Length,
            window.Scan.Tabs.Count, Program.Json.Serialize(window.Scan.Groups));
        Console.WriteLine("Missing: " + String.Join(", ", state.ExpectedTitles.Except(found)));
        if (window.Scan.Tabs.Any(t => t.Title == "DOM DECOY TAB")) throw new InvalidOperationException("Web DOM tab leaked into browser tab inventory.");
        return snapshot;
    }
    static void Matrix(State state)
    {
        var reports = new List<object>();
        IntPtr h = Native.Hwnd(state.Handle);
        int targetOrdinal = 17;
        foreach (var monitor in Native.Monitors())
        {
            Native.ShowWindowAsync(h, 9);
            var b = monitor.WorkArea;
            Native.SetWindowPos(h, IntPtr.Zero, b.Left + 40, b.Top + 40, Math.Min(1500, b.Width - 80), Math.Min(1000, b.Height - 80), 0x0014);
            Thread.Sleep(400);
            var snapshot = Capture(state, "fixture-monitor-" + monitor.Device.Replace("\\", "").Replace(".", ""));
            var window = snapshot.Windows.Single(w => w.Handle == state.Handle);
            if (window.MonitorHandle != monitor.Handle) throw new InvalidOperationException("Monitor mapping mismatch");
            string targetTitle = "TabPilot Fixture " + (targetOrdinal++).ToString("D2");
            int id = window.Scan.Tabs.First(t => t.Title.StartsWith(targetTitle, StringComparison.Ordinal)).Id;
            var selected = Program.Select(snapshot, id, Path.Combine(Evidence, "highlight-" + monitor.Device.Replace("\\", "").Replace(".", "") + ".png"));
            Console.WriteLine(Program.Json.Serialize(selected));
            reports.Add(new { Scenario = "monitor", Monitor = monitor, Window = window, Selection = selected });
            Program.Save(Path.Combine(Evidence, "fixture-matrix.json"), reports);
        }
        Native.ShowWindowAsync(h, 6);
        if (!Switcher.Wait(delegate { return Native.IsIconic(h); }, 2000)) throw new InvalidOperationException("Fixture did not minimize");
        var minimized = Capture(state, "fixture-minimized");
        var minWindow = minimized.Windows.Single(w => w.Handle == state.Handle);
        int minId = minWindow.Scan.Tabs.First(t => t.Title.StartsWith("TabPilot Fixture 24", StringComparison.Ordinal)).Id;
        var restored = Program.Select(minimized, minId);
        Console.WriteLine(Program.Json.Serialize(restored));
        reports.Add(new { Scenario = "minimized", Window = minWindow, Selection = restored });
        Program.Save(Path.Combine(Evidence, "fixture-matrix.json"), reports);
    }
    static void Focus(IntPtr h)
    {
        IntPtr foreground = Native.GetForegroundWindow();
        if (foreground != h && Native.GetWindow(foreground, 4) == h) Chord(foreground, 0x1B);
        Native.SetForegroundWindow(h);
        if (Native.GetForegroundWindow() != h)
        {
            try { AutomationElement.FromHandle(h).SetFocus(); }
            catch (InvalidOperationException) { }
        }
        if (!Switcher.Wait(delegate { return Native.GetForegroundWindow() == h; }, 1500)) throw new InvalidOperationException("Cannot focus fixture; actual foreground=" + Native.Handle(Native.GetForegroundWindow()) + "; no injected input sent.");
    }
    static void Send(IntPtr h, params INPUT[] events)
    {
        if (Native.GetForegroundWindow() != h) throw new InvalidOperationException("Foreground changed; stop input.");
        if (SendInput((uint)events.Length, events, Marshal.SizeOf(typeof(INPUT))) != events.Length)
            throw new InvalidOperationException("SendInput incomplete: " + Marshal.GetLastWin32Error());
    }
    static INPUT Key(ushort vk, bool up) { return new INPUT { Type = 1, Data = new INPUTUNION { Keyboard = new KEYBDINPUT { Vk = vk, Flags = up ? 2u : 0u } } }; }
    static void Chord(IntPtr h, params ushort[] keys)
    {
        var events = keys.Select(k => Key(k, false)).Concat(keys.Reverse().Select(k => Key(k, true))).ToArray();
        Send(h, events); Thread.Sleep(90);
    }
    static void Text(IntPtr h, string text)
    {
        var events = new List<INPUT>();
        foreach (char c in text)
        {
            events.Add(new INPUT { Type = 1, Data = new INPUTUNION { Keyboard = new KEYBDINPUT { Scan = c, Flags = 4 } } });
            events.Add(new INPUT { Type = 1, Data = new INPUTUNION { Keyboard = new KEYBDINPUT { Scan = c, Flags = 6 } } });
        }
        Send(h, events.ToArray()); Thread.Sleep(90);
    }
    static void Mouse(IntPtr h, uint flags) { Send(h, new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = flags } } }); }
    static void Dump(IntPtr h)
    {
        var lines = new List<string>();
        foreach (var root in Roots(h)) DumpNode(root, 0, lines);
        Program.Save(Path.Combine(Evidence, "fixture-tree.json"), lines);
        Console.WriteLine(String.Join(Environment.NewLine, lines));
    }
    static List<AutomationElement> Roots(IntPtr h)
    {
        var roots = new List<AutomationElement> { AutomationElement.FromHandle(h) };
        Native.EnumWindowsProc callback = delegate(IntPtr candidate, IntPtr data)
        {
            uint targetPid, candidatePid;
            Native.GetWindowThreadProcessId(h, out targetPid);
            Native.GetWindowThreadProcessId(candidate, out candidatePid);
            var className = new StringBuilder(256);
            Native.GetClassName(candidate, className, className.Capacity);
            if (candidate != h && Native.IsWindowVisible(candidate) && (Native.GetWindow(candidate, 4) == h ||
                (targetPid == candidatePid && className.ToString() == "#32768")))
                roots.Add(AutomationElement.FromHandle(candidate));
            return true;
        };
        Native.EnumWindows(callback, IntPtr.Zero);
        return roots;
    }
    static void DumpNode(AutomationElement element, int depth, List<string> lines)
    {
        if (element == null || depth > 20 || lines.Count > 1000) return;
        var c = element.Current;
        if (c.ControlType == ControlType.Document) return;
        lines.Add(new String(' ', depth) + c.ControlType.ProgrammaticName + " | " + c.ClassName + " | " + c.Name + " | " + c.BoundingRectangle);
        var walker = TreeWalker.RawViewWalker;
        for (var child = walker.GetFirstChild(element); child != null; child = walker.GetNextSibling(child)) DumpNode(child, depth + 1, lines);
    }
}
