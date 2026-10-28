using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TabPilot
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct WINDOWPLACEMENT
        {
            public int Length, Flags, ShowCmd;
            public POINT Min, Max;
            public RECT Normal;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct MONITORINFOEX
        {
            public int Size;
            public RECT Monitor, Work;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
        }
        internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr data);
        internal delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr data);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);
        [DllImport("user32.dll")] internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);
        [DllImport("user32.dll")] internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr hwnd, StringBuilder text, int max);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] internal static extern bool GetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT placement);
        [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool AllowSetForegroundWindow(int pid);
        [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern IntPtr GetThreadDpiAwarenessContext();
        [DllImport("user32.dll")] internal static extern bool AreDpiAwarenessContextsEqual(IntPtr a, IntPtr b);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT value, int size);
        [DllImport("shcore.dll")] internal static extern int GetScaleFactorForMonitor(IntPtr monitor, out int scale);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref int size);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        internal static int IdentityLookupFailures;
        internal static string Handle(IntPtr value) { return "0x" + value.ToInt64().ToString("X"); }
        internal static IntPtr Hwnd(string value) { return new IntPtr(Convert.ToInt64(value.Substring(2), 16)); }
        internal static Box Box(RECT r) { return new Box { Left = r.Left, Top = r.Top, Right = r.Right, Bottom = r.Bottom }; }
        internal static string Title(IntPtr hwnd) { var s = new StringBuilder(4096); GetWindowText(hwnd, s, s.Capacity); return s.ToString(); }
        internal static Box VisibleFrame(IntPtr hwnd)
        {
            RECT r;
            if (DwmGetWindowAttribute(hwnd, 9, out r, Marshal.SizeOf(typeof(RECT))) == 0 && r.Right > r.Left) return Box(r);
            if (!GetWindowRect(hwnd, out r)) throw new InvalidOperationException("GetWindowRect failed");
            return Box(r);
        }
        internal static string DpiContext()
        {
            return AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), new IntPtr(-4)) ? "PerMonitorV2" : "UnexpectedContext";
        }
        internal static List<MonitorRecord> Monitors()
        {
            var list = new List<MonitorRecord>();
            MonitorEnumProc callback = delegate(IntPtr h, IntPtr dc, ref RECT rect, IntPtr data)
            {
                var info = new MONITORINFOEX { Size = Marshal.SizeOf(typeof(MONITORINFOEX)) };
                if (!GetMonitorInfo(h, ref info)) return true;
                int scale;
                int hr = GetScaleFactorForMonitor(h, out scale);
                list.Add(new MonitorRecord { Handle = Handle(h), Device = info.Device, Bounds = Box(info.Monitor),
                    WorkArea = Box(info.Work), Primary = (info.Flags & 1) != 0, ScalePercent = hr == 0 ? scale : 0,
                    ScaleError = hr == 0 ? null : "GetScaleFactorForMonitor HRESULT=" + hr.ToString("X8") });
                return true;
            };
            if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero)) throw new InvalidOperationException("EnumDisplayMonitors failed");
            return list;
        }
        internal static List<WindowRecord> Windows()
        {
            var list = new List<WindowRecord>();
            var paths = new Dictionary<uint, string>();
            IdentityLookupFailures = 0;
            // EnumWindows (rather than Process.MainWindowHandle) includes every window of a browser process.
            EnumWindowsProc callback = delegate(IntPtr h, IntPtr data)
            {
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                string path;
                if (!paths.TryGetValue(pid, out path))
                {
                    IntPtr processHandle = OpenProcess(0x1000, false, pid);
                    if (processHandle != IntPtr.Zero)
                    {
                        try
                        {
                            var text = new StringBuilder(32768);
                            int size = text.Capacity;
                            if (QueryFullProcessImageName(processHandle, 0, text, ref size)) path = text.ToString();
                        }
                        finally { CloseHandle(processHandle); }
                    }
                    if (path == null) IdentityLookupFailures++;
                    paths[pid] = path;
                }
                if (path == null || !String.Equals(Path.GetFileName(path), "chrome.exe", StringComparison.OrdinalIgnoreCase)) return true;
                try
                {
                    using (var process = Process.GetProcessById((int)pid))
                    {
                        var c = new StringBuilder(256);
                        GetClassName(h, c, c.Capacity);
                        var w = new WindowRecord { Handle = Handle(h), Pid = (int)pid, Title = Title(h), ClassName = c.ToString(),
                            Visible = IsWindowVisible(h), Minimized = IsIconic(h) };
                        try
                        {
                            w.ProcessPath = path;
                            var version = FileVersionInfo.GetVersionInfo(w.ProcessPath);
                            w.ProductName = version.ProductName;
                            w.Version = version.FileVersion;
                            w.ProcessStartUtc = process.StartTime.ToUniversalTime().ToString("o");
                        }
                        catch (Exception e) { w.Diagnostics.Add("PROCESS_METADATA: " + e.Message); }
                        // Exclude other vendors that also name their executable chrome.exe.
                        bool google = String.Equals(w.ProductName, "Google Chrome", StringComparison.OrdinalIgnoreCase) ||
                            (w.ProcessPath != null && w.ProcessPath.IndexOf(@"\Google\Chrome", StringComparison.OrdinalIgnoreCase) >= 0);
                        w.Eligible = google && w.ClassName == "Chrome_WidgetWin_1" && (w.Visible || w.Minimized) && GetWindow(h, 4) == IntPtr.Zero;
                        if (!w.Eligible) w.SkipReason = !google ? "NOT_GOOGLE_CHROME_OR_UNVERIFIED_VENDOR" :
                            (!w.Visible && !w.Minimized ? "HIDDEN_HELPER_WINDOW" : "NON_BROWSER_OR_OWNED_WINDOW");
                        RECT r;
                        if (GetWindowRect(h, out r)) w.WindowBounds = Box(r);
                        try { w.FrameBounds = VisibleFrame(h); } catch (Exception e) { w.Diagnostics.Add("FRAME: " + e.Message); }
                        var placement = new WINDOWPLACEMENT { Length = Marshal.SizeOf(typeof(WINDOWPLACEMENT)) };
                        if (GetWindowPlacement(h, ref placement)) w.NormalBounds = Box(placement.Normal);
                        w.CoordinateMeaning = w.Minimized ? "WindowBounds=iconic; NormalBounds=restore workspace coordinates; monitor=pre-minimize monitor" :
                            "WindowBounds/FrameBounds=physical screen pixels; NormalBounds=restore workspace coordinates";
                        w.MonitorHandle = Handle(MonitorFromWindow(h, 2));
                        w.Dpi = GetDpiForWindow(h);
                        list.Add(w);
                    }
                }
                catch (ArgumentException) { /* process exited during enumeration */ }
                catch (System.ComponentModel.Win32Exception) { /* inaccessible process identity */ }
                return true;
            };
            if (!EnumWindows(callback, IntPtr.Zero)) throw new InvalidOperationException("EnumWindows failed");
            return list;
        }
        internal static bool SameWindow(WindowRecord expected)
        {
            IntPtr h = Hwnd(expected.Handle);
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            if (!IsWindow(h) || pid != expected.Pid || expected.ProcessStartUtc == null) return false;
            using (var p = Process.GetProcessById((int)pid))
                return p.StartTime.ToUniversalTime().ToString("o") == expected.ProcessStartUtc;
        }
    }
}
