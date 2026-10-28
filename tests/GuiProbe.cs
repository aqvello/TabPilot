using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// Local integration probe, restricted to the workspace's TabPilot processes.
internal static class GuiProbe
{
    delegate bool Callback(IntPtr h, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback, IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h, Callback callback, IntPtr data);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder b, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder b, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT rect);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int index);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint thread, ref GUITHREADINFO info);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [StructLayout(LayoutKind.Sequential)] struct GUITHREADINFO
    { public int Size, Flags; public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret; public RECT CaretRect; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint Type; public INPUTUNION Data; }
    [StructLayout(LayoutKind.Explicit)] struct INPUTUNION
    { [FieldOffset(0)] public KEYBDINPUT Keyboard; [FieldOffset(0)] public MOUSEINPUT Mouse; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort Vk, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    static INPUT Key(ushort vk, bool up) { return new INPUT { Type = 1, Data = new INPUTUNION { Keyboard = new KEYBDINPUT { Vk = vk, Flags = up ? 2u : 0u } } }; }
    static void Send(params INPUT[] inputs)
    {
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT))) != inputs.Length)
            throw new InvalidOperationException("SendInput incomplete: " + Marshal.GetLastWin32Error());
    }
    static void Chord(params ushort[] keys) { Send(keys.Select(k => Key(k, false)).Concat(keys.Reverse().Select(k => Key(k, true))).ToArray()); }
    static Dictionary<string, object> State(string bin)
    {
        for (int i = 0; i < 100; i++)
        {
            try
            {
                string json;
                using (var file = new FileStream(Path.Combine(bin, "data", "ui-state.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(file, Encoding.UTF8)) json = reader.ReadToEnd();
                var result = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                if (result != null && result.ContainsKey("Visible")) return result;
            }
            catch (IOException) { }
            catch (ArgumentException) { }
            Thread.Sleep(20);
        }
        throw new IOException("Could not read a complete UI diagnostic snapshot");
    }
    static string Text(IntPtr h) { var b = new StringBuilder(8192); GetWindowText(h, b, b.Capacity); return b.ToString(); }
    static string Class(IntPtr h) { var b = new StringBuilder(256); GetClassName(h, b, b.Capacity); return b.ToString(); }
    static bool PopupTitle(string text) { return text == "TabPilot — 标签切换" || text == "TabPilot — Tab switcher"; }
    [STAThread] static int Main(string[] args)
    {
        try { return Run(args); }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.GetType().FullName + " HRESULT=" + e.HResult.ToString("X8"));
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine(e.StackTrace);
            return 1;
        }
    }
    static int Run(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        string bin = Path.GetDirectoryName(typeof(GuiProbe).Assembly.Location);
        if (args.Length > 0 && args[0] == "focus-after-selection")
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var wait = Stopwatch.StartNew();
            bool highlighting = false;
            while (!highlighting && wait.ElapsedMilliseconds < 15000)
            {
                Callback findEdge = delegate(IntPtr h, IntPtr data)
                {
                    if (!IsWindowVisible(h) || (GetWindowLongPtr(h, -20).ToInt64() & 0x08080000) != 0x08080000) return true;
                    uint pid; GetWindowThreadProcessId(h, out pid);
                    try
                    {
                        using (var owner = Process.GetProcessById((int)pid))
                            if (String.Equals(owner.MainModule.FileName, Path.Combine(bin, "TabPilot.exe"), StringComparison.OrdinalIgnoreCase))
                            { highlighting = true; return false; }
                    }
                    catch (ArgumentException) { }
                    catch (System.ComponentModel.Win32Exception) { }
                    return true;
                };
                EnumWindows(findEdge, IntPtr.Zero); Thread.Sleep(10);
            }
            if (!highlighting) throw new InvalidOperationException("No verified-selection highlight appeared");
            using (var stub = new Form { Text = "TabPilot focus test", StartPosition = FormStartPosition.Manual, ShowInTaskbar = false, TopMost = true })
            {
                stub.SetBounds(400, 180, 300, 150); stub.Show(); Application.DoEvents();
                RECT r; GetWindowRect(stub.Handle, out r);
                var point = new POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 };
                SetCursorPos(point.X, point.Y);
                if (GetAncestor(WindowFromPoint(point), 2) != stub.Handle) throw new InvalidOperationException("Focus test window is not under pointer");
                Send(new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = 2 } } },
                    new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = 4 } } });
                SetForegroundWindow(stub.Handle);
                wait.Restart();
                var state = State(bin);
                while ((bool)state["Selecting"] && wait.ElapsedMilliseconds < 10000)
                { Application.DoEvents(); Thread.Sleep(20); state = State(bin); }
                if ((bool)state["Selecting"] || (bool)state["Visible"] || GetForegroundWindow() != stub.Handle)
                    throw new InvalidOperationException("Completed selection reopened popup or took foreground back");
                var result = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(bin, "data", "last-selection.json"), Encoding.UTF8));
                if ((string)result["Status"] != "Passed" || !(bool)result["FocusChangedAfterSelection"] || !(bool)result["SelectionVerified"])
                    throw new InvalidOperationException("Focus handoff was not recorded as a verified successful selection");
                Console.WriteLine("PASS focus moved during highlight, selection passed, popup stayed hidden, test window kept foreground");
                return 0;
            }
        }
        if (args.Length > 0 && args[0] == "position")
        {
            var ui = State(bin);
            IntPtr h = new IntPtr(Convert.ToInt64(((string)ui["WindowHandle"]).Substring(2), 16));
            uint pid; GetWindowThreadProcessId(h, out pid);
            using (var owner = Process.GetProcessById((int)pid))
                if (!String.Equals(owner.MainModule.FileName, Path.Combine(bin, "TabPilot.exe"), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Position target does not belong to workspace TabPilot");
            RECT bounds;
            if (!GetWindowRect(h, out bounds)) throw new InvalidOperationException("Cannot read popup position");
            var screen = Screen.FromHandle(h);
            var work = screen.WorkingArea;
            string result = new JavaScriptSerializer().Serialize(new { MonitorDevice = screen.DeviceName, Dpi = GetDpiForWindow(h),
                Bounds = new { bounds.Left, bounds.Top, bounds.Right, bounds.Bottom },
                WorkArea = new { work.Left, work.Top, work.Right, work.Bottom } });
            File.WriteAllText(Path.Combine(bin, "data", "position-probe.json"), result, Encoding.UTF8);
            Console.WriteLine(result); return 0;
        }
        if (args.Length > 0 && args[0] == "recall-from")
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var ui = State(bin);
            if (!(bool)ui["HotkeyRegistered"] || (bool)ui["Visible"]) throw new InvalidOperationException("Hide the popup before recall-from test");
            using (var stub = new Form { Text = "TabPilot position test", StartPosition = FormStartPosition.Manual, ShowInTaskbar = false, TopMost = true, AutoScaleMode = AutoScaleMode.None })
            {
                stub.SetBounds(Int32.Parse(args[1]), Int32.Parse(args[2]), 260, 120);
                stub.Show(); Application.DoEvents();
                RECT r; GetWindowRect(stub.Handle, out r);
                var point = new POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 };
                SetCursorPos(point.X, point.Y);
                if (GetAncestor(WindowFromPoint(point), 2) != stub.Handle) throw new InvalidOperationException("Test window is not under the pointer");
                Send(new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = 2 } } },
                    new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = 4 } } });
                var watch = Stopwatch.StartNew();
                while (GetForegroundWindow() != stub.Handle && watch.ElapsedMilliseconds < 500) { Application.DoEvents(); Thread.Sleep(10); }
                if (GetForegroundWindow() != stub.Handle) SetForegroundWindow(stub.Handle);
                while (GetForegroundWindow() != stub.Handle && watch.ElapsedMilliseconds < 2000) { Application.DoEvents(); Thread.Sleep(10); }
                if (GetForegroundWindow() != stub.Handle) throw new InvalidOperationException("Temporary test window did not get foreground; actual=" + Text(GetForegroundWindow()) + "; dpi=" + GetDpiForWindow(stub.Handle));
                var foreground = Screen.FromHandle(stub.Handle).DeviceName;
                var keys = new List<ushort>();
                int mods = Convert.ToInt32(ui["HotkeyModifiers"]);
                if ((mods & 2) != 0) keys.Add(0x11);
                if ((mods & 1) != 0) keys.Add(0x12);
                if ((mods & 4) != 0) keys.Add(0x10);
                keys.Add(Convert.ToUInt16(ui["HotkeyKey"]));
                Chord(keys.ToArray());
                watch.Restart();
                while (watch.ElapsedMilliseconds < 5000)
                {
                    Application.DoEvents(); Thread.Sleep(50); ui = State(bin);
                    if ((bool)ui["Visible"] && GetForegroundWindow() != stub.Handle) break;
                }
                if (!(bool)ui["Visible"]) throw new InvalidOperationException("Popup was not recalled");
                Console.WriteLine("Foreground before recall: " + foreground);
                return 0;
            }
        }
        if (args.Length > 0 && (args[0] == "hotkey" || args[0] == "paint-sweep" || args[0] == "cross-dpi-render" || args[0] == "cross-dpi-drag"))
        {
            var state = State(bin);
            if (!(bool)state["HotkeyRegistered"]) throw new InvalidOperationException("Application hotkey is not registered; do not inject an occupied key");
            var keys = new List<ushort>();
            int mods = Convert.ToInt32(state["HotkeyModifiers"]);
            if ((mods & 2) != 0) keys.Add(0x11);
            if ((mods & 1) != 0) keys.Add(0x12);
            if ((mods & 4) != 0) keys.Add(0x10);
            keys.Add(Convert.ToUInt16(state["HotkeyKey"]));
            if (args[0] == "hotkey") { Chord(keys.ToArray()); return 0; }
            if ((bool)state["Visible"]) { Chord(keys.ToArray()); Thread.Sleep(150); }
            Chord(keys.ToArray());
            var wait = Stopwatch.StartNew();
            do
            {
                Thread.Sleep(100);
                state = State(bin);
            } while ((!(bool)state["Visible"] || (bool)state["Scanning"]) && wait.ElapsedMilliseconds < 15000);
        }
        bool acted = false;
        Exception actionError = null;
        Callback callback = delegate(IntPtr h, IntPtr data)
        {
            if (!IsWindowVisible(h)) return true;
            uint pid; GetWindowThreadProcessId(h, out pid);
            try
            {
                using (var p = Process.GetProcessById((int)pid))
                {
                    string path = p.MainModule.FileName;
                    if (String.Equals(path, Path.Combine(bin, "TabPilot.exe"), StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(path, Path.Combine(bin, "TabPilot.Cli.exe"), StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("WINDOW " + h + " PID=" + pid + " " + Class(h) + " " + Text(h));
                        if (args.Length > 1 && args[0] == "key" && args[1] == "escape" && (Text(h) == "修改呼出快捷键" || Text(h) == "Change popup shortcut"))
                        {
                            if (GetForegroundWindow() != h) throw new InvalidOperationException("Hotkey dialog does not own foreground");
                            Chord(0x1B); acted = true; return true;
                        }
                        if (args.Length > 0 && !acted && PopupTitle(Text(h)) && args[0] != "dump" && args[0] != "screenshot" && args[0] != "details")
                        {
                            if (acted) throw new InvalidOperationException("Multiple TabPilot popup windows");
                            acted = true;
                            IntPtr foreground = GetForegroundWindow();
                            if (foreground != h && GetAncestor(foreground, 3) != h) throw new InvalidOperationException("TabPilot does not own foreground; no input sent");
                            if (args[0] == "verify-shortcut")
                            {
                                var ui = State(bin);
                                string expected = Convert.ToString(ui["HotkeyHint"]);
                                string shortcut = Convert.ToString(ui["Hotkey"]);
                                if (!(bool)ui["HotkeyRegistered"] || String.IsNullOrEmpty(expected) || !expected.Contains(shortcut))
                                    throw new InvalidOperationException("Current registered shortcut is absent from the footer state");
                                RECT parent; GetWindowRect(h, out parent);
                                int matches = 0;
                                EnumChildWindows(h, delegate(IntPtr hintWindow, IntPtr unused)
                                {
                                    if (!IsWindowVisible(hintWindow) || Text(hintWindow) != expected) return true;
                                    RECT rect; GetWindowRect(hintWindow, out rect);
                                    if (rect.Left < parent.Left || rect.Top < parent.Top || rect.Right > parent.Right || rect.Bottom > parent.Bottom)
                                        throw new InvalidOperationException("Shortcut footer is outside the popup bounds");
                                    matches++; return true;
                                }, IntPtr.Zero);
                                if (matches != 1) throw new InvalidOperationException("Expected one visible native shortcut footer");
                                Console.WriteLine("PASS visible native shortcut footer matches the registered combination");
                            }
                            else if (args[0] == "verify-language")
                            {
                                bool english = args[1] == "en";
                                var ui = State(bin);
                                var labels = new HashSet<string>();
                                Callback find = delegate(IntPtr c, IntPtr d) { labels.Add(Text(c)); return true; };
                                EnumChildWindows(h, find, IntPtr.Zero);
                                if ((string)ui["Language"] != args[1] || !labels.Contains(english ? "All tabs" : "全部") ||
                                    !labels.Contains(english ? "Shortcut" : "快捷键") || Text(h) != (english ? "TabPilot — Tab switcher" : "TabPilot — 标签切换"))
                                    throw new InvalidOperationException("Popup language did not match expected labels and caption");
                                Console.WriteLine("PASS visible UI language: " + args[1]);
                            }
                            else if (args[0] == "pick-language")
                            {
                                IntPtr picker = IntPtr.Zero;
                                Callback find = delegate(IntPtr c, IntPtr d) { if (Text(c) == "中文 / EN" && Class(c).Contains("BUTTON")) picker = c; return true; };
                                EnumChildWindows(h, find, IntPtr.Zero);
                                if (picker == IntPtr.Zero) throw new InvalidOperationException("Language picker not found");
                                SendMessage(picker, 0x00F5, IntPtr.Zero, IntPtr.Zero); Thread.Sleep(80);
                                if (GetForegroundWindow() != h) throw new InvalidOperationException("Language picker lost foreground; no input sent");
                                Chord(0x24);
                                if (args[1] == "en") Chord(0x28);
                                Chord(0x0D);
                            }
                            else if (args[0] == "query" || args[0] == "clear")
                            {
                                var info = new GUITHREADINFO { Size = Marshal.SizeOf(typeof(GUITHREADINFO)) };
                                if (!GetGUIThreadInfo(0, ref info) || GetAncestor(info.Focus, 2) != h || !Class(info.Focus).Contains("EDIT"))
                                    throw new InvalidOperationException("Search focus not verified");
                                Chord(0x11, 0x41);
                                Thread.Sleep(50);
                                if (GetForegroundWindow() != h) throw new InvalidOperationException("Search lost foreground; no text sent");
                                var events = new List<INPUT>();
                                foreach (char c in args[0] == "clear" ? "" : args[1])
                                {
                                    events.Add(new INPUT { Type = 1, Data = new INPUTUNION { Keyboard = new KEYBDINPUT { Scan = c, Flags = 4 } } });
                                    events.Add(new INPUT { Type = 1, Data = new INPUTUNION { Keyboard = new KEYBDINPUT { Scan = c, Flags = 6 } } });
                                }
                                if (events.Count == 0) Chord(0x08); else Send(events.ToArray());
                            }
                            else if (args[0] == "key")
                            {
                                switch (args[1])
                                {
                                    case "escape": Chord(0x1B); break;
                                    case "enter": Chord(0x0D); break;
                                    case "down": Chord(0x28); break;
                                    case "up": Chord(0x26); break;
                                    case "all": Chord(0x11, 0x31); break;
                                    case "screens": Chord(0x11, 0x32); break;
                                    case "recent": Chord(0x11, 0x33); break;
                                    case "refresh": Chord(0x74); break;
                                    case "quit": Chord(0x11, 0x51); break;
                                    default: throw new ArgumentException("Unknown test key");
                                }
                            }
                            else if (args[0] == "button")
                            {
                                IntPtr button = IntPtr.Zero;
                                Callback find = delegate(IntPtr c, IntPtr d) { if (Text(c) == args[1] && Class(c).Contains("BUTTON")) button = c; return true; };
                                EnumChildWindows(h, find, IntPtr.Zero);
                                if (button == IntPtr.Zero) throw new InvalidOperationException("Button missing");
                                SendMessage(button, 0x00F5, IntPtr.Zero, IntPtr.Zero);
                            }
                            else if (args[0] == "click-first")
                            {
                                IntPtr list = IntPtr.Zero;
                                Callback find = delegate(IntPtr c, IntPtr d) { if (Class(c).Contains("LISTBOX")) list = c; return true; };
                                EnumChildWindows(h, find, IntPtr.Zero);
                                RECT r; GetWindowRect(list, out r);
                                if (list == IntPtr.Zero) throw new InvalidOperationException("Tab list missing");
                                SetCursorPos(r.Left + 100, r.Top + 35);
                                Send(new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = 2 } } },
                                    new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = 4 } } });
                            }
                            else if (args[0] == "move")
                            {
                                if (!SetWindowPos(h, IntPtr.Zero, Int32.Parse(args[1]), Int32.Parse(args[2]), 0, 0, 0x0015))
                                    throw new InvalidOperationException("Move failed");
                            }
                            else if (args[0] == "bounds")
                            {
                                if (!SetWindowPos(h, IntPtr.Zero, Int32.Parse(args[1]), Int32.Parse(args[2]), 0, 0, 0x0015))
                                    throw new InvalidOperationException("Move failed");
                                Thread.Sleep(120);
                                if (!SetWindowPos(h, IntPtr.Zero, Int32.Parse(args[1]), Int32.Parse(args[2]), Int32.Parse(args[3]), Int32.Parse(args[4]), 0x0014))
                                    throw new InvalidOperationException("Bounds restore failed");
                            }
                            else if (args[0] == "paint-sweep")
                            {
                                Chord(0x11, 0x31); // Keep the sampled "All" option selected while hover changes elsewhere.
                                Thread.Sleep(100);
                                var buttons = new Dictionary<string, RECT>();
                                Callback find = delegate(IntPtr c, IntPtr d)
                                {
                                    string text = Text(c);
                                    if (Class(c).Contains("BUTTON") && (text == "全部" || text == "按屏幕" || text == "最近使用"))
                                    { RECT rect; GetWindowRect(c, out rect); buttons[text] = rect; }
                                    return true;
                                };
                                EnumChildWindows(h, find, IntPtr.Zero);
                                if (buttons.Count != 3) throw new InvalidOperationException("Mode controls missing");
                                var area = buttons["全部"];
                                var positions = buttons.Values.ToArray();
                                var hashes = new List<string>();
                                using (var bitmap = new Bitmap(area.Right - area.Left, area.Bottom - area.Top, PixelFormat.Format32bppArgb))
                                using (var g = Graphics.FromImage(bitmap))
                                {
                                    for (int i = 0; i < 120; i++)
                                    {
                                        if (GetForegroundWindow() != h || !IsWindowVisible(h)) throw new InvalidOperationException("Popup lost foreground; sweep stopped");
                                        var position = positions[i % positions.Length];
                                        SetCursorPos((position.Left + position.Right) / 2, (position.Top + position.Bottom) / 2);
                                        Thread.Sleep(12);
                                        g.CopyFromScreen(area.Left, area.Top, 0, 0, bitmap.Size);
                                        var bits = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                                        try
                                        {
                                            var bytes = new byte[Math.Abs(bits.Stride) * bits.Height];
                                            Marshal.Copy(bits.Scan0, bytes, 0, bytes.Length);
                                            ulong hash = 14695981039346656037UL;
                                            unchecked { foreach (byte b in bytes) { hash ^= b; hash *= 1099511628211UL; } }
                                            hashes.Add(hash.ToString("X16"));
                                        }
                                        finally { bitmap.UnlockBits(bits); }
                                    }
                                    bitmap.Save(Path.Combine(bin, "data", args[1] + ".png"), ImageFormat.Png);
                                }
                                string result = new JavaScriptSerializer().Serialize(new { Samples = hashes.Count,
                                    UniqueFrames = hashes.Distinct().Count(), Transitions = hashes.Skip(1).Where((v, i) => v != hashes[i]).Count(), Hashes = hashes });
                                File.WriteAllText(Path.Combine(bin, "data", args[1] + ".json"), result, Encoding.UTF8);
                                Console.WriteLine(result);
                            }
                            else if (args[0] == "cross-dpi-render" || args[0] == "cross-dpi-drag")
                            {
                                RenderSweep(h, bin, args[0] == "cross-dpi-drag");
                            }
                            return true;
                        }
                        Callback child = delegate(IntPtr c, IntPtr d)
                        {
                            string text = Text(c);
                            Console.WriteLine("  " + Class(c) + " | " + text);
                            if (args.Length > 0 && args[0] == "details" && text.StartsWith("详细信息", StringComparison.Ordinal))
                                SendMessage(c, 0x00F5, IntPtr.Zero, IntPtr.Zero);
                            return true;
                        };
                        EnumChildWindows(h, child, IntPtr.Zero);
                        if (args.Length > 0 && args[0] == "screenshot" && Text(h).StartsWith("TabPilot", StringComparison.Ordinal))
                        {
                            RECT r; GetWindowRect(h, out r);
                            using (var bitmap = new Bitmap(r.Right - r.Left, r.Bottom - r.Top))
                            {
                                using (var g = Graphics.FromImage(bitmap)) g.CopyFromScreen(r.Left, r.Top, 0, 0, bitmap.Size);
                                bitmap.Save(Path.Combine(bin, "data", "gui-screenshot.png"), ImageFormat.Png);
                            }
                        }
                    }
                }
            }
            catch (ArgumentException) { }
            catch (System.ComponentModel.Win32Exception) { }
            catch (Exception e) { actionError = e; return false; }
            return true;
        };
        EnumWindows(callback, IntPtr.Zero);
        if (actionError != null) { Console.Error.WriteLine(actionError.ToString()); return 1; }
        if (args.Length > 0 && args[0] != "dump" && args[0] != "screenshot" && args[0] != "details" && !acted)
            throw new InvalidOperationException("Visible TabPilot window not found");
        return 0;
    }
    sealed class PaintBackdrop : Form
    {
        internal PaintBackdrop()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Magenta;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        { get { var p = base.CreateParams; p.ExStyle |= 0x08000080; return p; } }
    }
    static void RenderSweep(IntPtr popup, string bin, bool drag)
    {
        RECT original; GetWindowRect(popup, out original);
        var samples = new List<object>();
        int failures = 0, dragLoops = 0, movingFrames = 0;
        bool mouseHeld = false;
        string name = drag ? "cross-dpi-drag" : "cross-dpi-render";
        var dpis = new HashSet<uint>();
        var captured = new HashSet<uint>();
        long styles = GetWindowLongPtr(popup, -20).ToInt64();
        using (var backdrop = new PaintBackdrop())
        using (var pixel = new Bitmap(1, 1))
        using (var graphics = Graphics.FromImage(pixel))
        {
            backdrop.SetBounds(original.Left - 3, original.Top - 3, original.Right - original.Left + 6, original.Bottom - original.Top + 6);
            backdrop.Show(); Application.DoEvents();
            try
            {
                var points = Screen.AllScreens.OrderBy(screen => screen.WorkingArea.Left).Select(screen => new Point(screen.WorkingArea.Left + Math.Min(100, screen.WorkingArea.Width / 10), screen.WorkingArea.Top + Math.Min(100, screen.WorkingArea.Height / 10))).ToArray();
                for (int move = 0; move < 32; move++)
                {
                    if (GetForegroundWindow() != popup || !IsWindowVisible(popup)) throw new InvalidOperationException("Popup lost foreground; render test stopped");
                    var target = points[move % points.Length];
                    if (drag)
                    {
                        RECT source; GetWindowRect(popup, out source);
                        float startScale = GetDpiForWindow(popup) / 96f;
                        int offsetX = (int)(50 * startScale), offsetY = (int)(28 * startScale);
                        var start = new POINT { X = source.Left + offsetX, Y = source.Top + offsetY };
                        SetCursorPos(start.X, start.Y);
                        if (GetAncestor(WindowFromPoint(start), 2) != popup) throw new InvalidOperationException("Popup heading is not under the pointer");
                        Send(new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = 2 } } });
                        mouseHeld = true;
                        Thread.Sleep(40);
                        for (int step = 1; step <= 8; step++)
                        {
                            SetCursorPos(start.X + (target.X + offsetX - start.X) * step / 8,
                                start.Y + (target.Y + offsetY - start.Y) * step / 8);
                            Application.DoEvents(); Thread.Sleep(20);
                        }
                        var info = new GUITHREADINFO { Size = Marshal.SizeOf(typeof(GUITHREADINFO)) };
                        if (!GetGUIThreadInfo(0, ref info) || info.MoveSize != popup) throw new InvalidOperationException("Native window drag loop was not entered");
                        dragLoops++;
                    }
                    else SetWindowPos(popup, IntPtr.Zero, target.X, target.Y, 0, 0, 0x0015);
                    int attempts = 0;
                    for (int frame = 0; frame < 3; frame++)
                    {
                        if (++attempts > 30) throw new InvalidOperationException("Window did not stabilize enough to sample its content");
                        if (mouseHeld && frame == 2)
                        {
                            Send(new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = 4 } } });
                            mouseHeld = false;
                        }
                        Application.DoEvents(); Thread.Sleep(30);
                        RECT r; GetWindowRect(popup, out r);
                        uint dpi = GetDpiForWindow(popup); dpis.Add(dpi);
                        SetWindowPos(backdrop.Handle, popup, r.Left - 3, r.Top - 3, r.Right - r.Left + 6, r.Bottom - r.Top + 6, 0x0050);
                        Application.DoEvents(); Thread.Sleep(15);
                        float scale = dpi / 96f;
                        var probes = new[] { new Point(r.Left + (int)(10 * scale), (r.Top + r.Bottom) / 2),
                            new Point(r.Left + (r.Right - r.Left) / 3, r.Top + (int)(28 * scale)),
                            new Point(r.Left + (r.Right - r.Left) / 2, r.Top + (int)(115 * scale)) };
                        var colors = new List<string>();
                        int unexpected = 0;
                        foreach (var point in probes)
                        {
                            graphics.CopyFromScreen(point.X, point.Y, 0, 0, pixel.Size);
                            var color = pixel.GetPixel(0, 0);
                            colors.Add(ColorTranslator.ToHtml(color));
                            if (color.R < 248 || color.G < 248 || color.B < 248) unexpected++;
                        }
                        RECT after; GetWindowRect(popup, out after);
                        if (r.Left != after.Left || r.Top != after.Top || r.Right != after.Right || r.Bottom != after.Bottom || dpi != GetDpiForWindow(popup))
                        {
                            // Queued native drag messages can move the window after GetWindowRect.
                            // Retry instead of sampling the screen coordinates it has already left.
                            movingFrames++; frame--; continue;
                        }
                        failures += unexpected;
                        samples.Add(new { Move = move, Frame = frame, Dpi = dpi, Colors = colors.ToArray(), Bounds = new { r.Left, r.Top, r.Right, r.Bottom } });
                        if (captured.Add(dpi))
                            using (var bitmap = new Bitmap(r.Right - r.Left, r.Bottom - r.Top))
                            {
                                using (var g = Graphics.FromImage(bitmap)) g.CopyFromScreen(r.Left, r.Top, 0, 0, bitmap.Size);
                                bitmap.Save(Path.Combine(bin, "data", name + "-" + dpi + ".png"), ImageFormat.Png);
                            }
                    }
                }
            }
            finally
            {
                if (mouseHeld) Send(new INPUT { Type = 0, Data = new INPUTUNION { Mouse = new MOUSEINPUT { Flags = 4 } } });
                backdrop.Hide();
                SetWindowPos(popup, IntPtr.Zero, original.Left, original.Top, 0, 0, 0x0015);
                Thread.Sleep(150);
                SetWindowPos(popup, IntPtr.Zero, original.Left, original.Top, original.Right - original.Left, original.Bottom - original.Top, 0x0014);
                string result = new JavaScriptSerializer().Serialize(new { Moves = 32, DragLoops = dragLoops, Frames = samples.Count, RetriedMovingFrames = movingFrames, CheckedPixels = samples.Count * 3,
                    NonOpaqueSamples = failures, Dpis = dpis.ToArray(), LayeredWindow = (styles & 0x00080000) != 0,
                    WholeWindowComposition = (styles & 0x02000000) != 0, Samples = samples });
                File.WriteAllText(Path.Combine(bin, "data", name + ".json"), result, Encoding.UTF8);
                Console.WriteLine("Render samples=" + samples.Count + ", unexpected background pixels=" + failures);
            }
        }
        if (failures != 0 || dpis.Count < 2) throw new InvalidOperationException("Cross-DPI opaque rendering check failed");
    }
}
