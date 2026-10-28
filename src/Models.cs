using System;
using System.Collections.Generic;

namespace TabPilot
{
    public class Box
    {
        public int Left, Top, Right, Bottom;
        public int Width { get { return Right - Left; } }
        public int Height { get { return Bottom - Top; } }
        public override string ToString() { return String.Format("({0},{1}) {2}x{3}", Left, Top, Width, Height); }
    }
    public class MonitorRecord
    {
        public string Handle, Device;
        public Box Bounds, WorkArea;
        public bool Primary;
        public int ScalePercent;
        public string ScaleError;
    }
    public class TabRecord
    {
        public int Id, Ordinal;
        public string WindowHandle, RuntimeId, Title, AutomationId;
        public bool Selected, Offscreen, Enabled, CanSelect, CanInvoke;
        public Box Bounds;
    }
    public class GroupRecord
    {
        public string Name, RuntimeId, State;
    }
    public class ScanResult
    {
        public List<TabRecord> Tabs = new List<TabRecord>();
        public List<GroupRecord> Groups = new List<GroupRecord>();
        public List<string> Diagnostics = new List<string>();
        public int NodesVisited;
        public bool TraversalComplete = true;
        public string Status = "Unknown";
    }
    public class WindowRecord
    {
        public string Handle, ClassName, Title, ProcessPath, ProductName, Version, ProcessStartUtc;
        public int Pid;
        public bool Visible, Minimized, Eligible;
        public string SkipReason, MonitorHandle, CoordinateMeaning;
        public Box WindowBounds, FrameBounds, NormalBounds;
        public uint Dpi;
        public List<string> Diagnostics = new List<string>();
        public ScanResult Scan = new ScanResult();
    }
    public class Snapshot
    {
        public string CapturedUtc, OsVersion, DpiAwareness, ExecutableSha256;
        public List<MonitorRecord> Monitors = new List<MonitorRecord>();
        public List<WindowRecord> Windows = new List<WindowRecord>();
        public List<string> Diagnostics = new List<string>();
    }
    public class SelectionRequest
    {
        public WindowRecord Window;
        public TabRecord Tab;
        public bool Highlight = true;
        public string HighlightImagePath;
    }
    public class SelectionResult
    {
        public int Id;
        public string WindowHandle, Title, Status = "Failed", Method, FailureCode, Detail;
        public bool WasMinimized, WasSelected, ForegroundVerified, SelectionVerified;
        public bool HighlightCreated, HighlightForegroundPreserved;
        public bool FocusChangedAfterSelection;
        public bool HighlightBoundsVerified;
        public Box HighlightFrameBounds;
        public List<Box> HighlightEdgeBounds = new List<Box>();
        public string HighlightImagePath, HighlightImageError;
        public long HighlightElapsedMs;
        public List<string> Attempts = new List<string>();
    }
    public class VerificationReport
    {
        public string StartedUtc, SnapshotPath;
        public int Attempted, Passed;
        public List<SelectionResult> Results = new List<SelectionResult>();
        public List<SelectionResult> RestoreResults = new List<SelectionResult>();
    }
}
