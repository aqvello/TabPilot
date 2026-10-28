using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Automation;

namespace TabPilot
{
    internal sealed class LiveTab
    {
        internal AutomationElement Element;
        internal TabRecord Record;
    }
    internal sealed class BrowserTree
    {
        internal ScanResult Result = new ScanResult();
        internal List<LiveTab> Tabs = new List<LiveTab>();
        const int MaxNodes = 5000, MaxDepth = 24;
        readonly HashSet<string> seen = new HashSet<string>();
        internal static string RuntimeId(AutomationElement element) { return String.Join(".", element.GetRuntimeId()); }
        internal static BrowserTree Read(IntPtr hwnd)
        {
            var tree = new BrowserTree();
            try
            {
                tree.Visit(AutomationElement.FromHandle(hwnd), 0, false, Native.Handle(hwnd));
                tree.Result.Tabs = tree.Tabs.Select(t => t.Record).ToList();
                tree.Result.Status = tree.Result.TraversalComplete ? (tree.Tabs.Count == 0 ? "NoTabsExposed" : "Enumerated") : "Partial";
                if (tree.Tabs.Count == 0) tree.Result.Diagnostics.Add("NO_TAB_ITEMS: UIA did not expose browser TabItem elements; minimized/provider/fullscreen/app window may be responsible. Cause not proven.");
                tree.Result.Diagnostics.Add("COMPLETENESS_UNKNOWN: exposed UIA count is not an independent browser tab count; hidden/collapsed tabs may be absent.");
                if (tree.Result.Groups.Any(g => g.State == "Collapsed"))
                    tree.Result.Diagnostics.Add("COLLAPSED_GROUP_PRESENT: child tabs may be absent from UIA. No automatic expansion is performed.");
            }
            catch (Exception e) { tree.Result.Status = "UiaError"; tree.Result.TraversalComplete = false; tree.Result.Diagnostics.Add(Error(e)); }
            return tree;
        }
        void Visit(AutomationElement element, int depth, bool inTabStrip, string hwnd)
        {
            if (element == null) return;
            if (++Result.NodesVisited > MaxNodes || depth > MaxDepth)
            {
                Result.TraversalComplete = false;
                if (!Result.Diagnostics.Contains("TRAVERSAL_LIMIT")) Result.Diagnostics.Add("TRAVERSAL_LIMIT");
                return;
            }
            try
            {
                var current = element.Current;
                // Never enumerate page DOM: a web ARIA tablist must not become a browser tab.
                if (current.ControlType == ControlType.Document || current.ClassName == "Chrome_RenderWidgetHostHWND") return;
                string runtime = RuntimeId(element);
                if (!seen.Add(runtime)) return;
                bool tabStrip = inTabStrip || current.ClassName.IndexOf("TabStrip", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.ClassName.IndexOf("TabContainer", StringComparison.OrdinalIgnoreCase) >= 0;
                if (current.ControlType == ControlType.TabItem && inTabStrip)
                {
                    object pattern;
                    bool canSelect = element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out pattern);
                    bool selected = canSelect && ((SelectionItemPattern)pattern).Current.IsSelected;
                    bool canInvoke = element.TryGetCurrentPattern(InvokePattern.Pattern, out pattern);
                    var b = current.BoundingRectangle;
                    var record = new TabRecord { Ordinal = Tabs.Count + 1, WindowHandle = hwnd, RuntimeId = runtime,
                        Title = current.Name, AutomationId = current.AutomationId, Selected = selected,
                        Offscreen = current.IsOffscreen, Enabled = current.IsEnabled, CanSelect = canSelect, CanInvoke = canInvoke,
                        Bounds = b.IsEmpty ? null : new Box { Left = (int)b.Left, Top = (int)b.Top, Right = (int)b.Right, Bottom = (int)b.Bottom } };
                    Tabs.Add(new LiveTab { Element = element, Record = record });
                    return; // close buttons and descendants are not tabs
                }
                object expand;
                if (tabStrip && (current.ControlType == ControlType.Tab || current.ControlType == ControlType.Group ||
                    current.ClassName.IndexOf("TabGroupHeader", StringComparison.OrdinalIgnoreCase) >= 0) &&
                    element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out expand))
                    Result.Groups.Add(new GroupRecord { RuntimeId = runtime, Name = current.Name,
                        State = ((ExpandCollapsePattern)expand).Current.ExpandCollapseState.ToString() });
                var walker = TreeWalker.RawViewWalker;
                var child = walker.GetFirstChild(element);
                while (child != null && Result.NodesVisited <= MaxNodes)
                {
                    Visit(child, depth + 1, tabStrip, hwnd);
                    child = walker.GetNextSibling(child);
                }
            }
            catch (Exception e) { Result.TraversalComplete = false; Result.Diagnostics.Add(Error(e)); }
        }
        internal static string Error(Exception e) { return e.GetType().Name + " HRESULT=0x" + e.HResult.ToString("X8") + ": " + e.Message; }
    }
    internal static class Switcher
    {
        internal static SelectionResult Select(SelectionRequest request)
        {
            var result = new SelectionResult { Id = request.Tab.Id, WindowHandle = request.Window.Handle, Title = request.Tab.Title,
                WasSelected = request.Tab.Selected };
            try
            {
                if (!Native.SameWindow(request.Window)) return Fail(result, "STALE_WINDOW", "HWND/PID/process start time no longer match; refresh snapshot.");
                IntPtr hwnd = Native.Hwnd(request.Window.Handle);
                var initial = BrowserTree.Read(hwnd);
                var tab = initial.Tabs.SingleOrDefault(t => t.Record.RuntimeId == request.Tab.RuntimeId);
                if (tab == null) return Fail(result, "STALE_OR_UNEXPOSED_TAB", "Exact runtime ID absent. No title/index fallback is used; refresh snapshot.");
                result.WasSelected = tab.Record.Selected;
                result.WasMinimized = Native.IsIconic(hwnd);
                if (result.WasMinimized)
                {
                    Native.ShowWindowAsync(hwnd, 9);
                    if (!Wait(delegate { return !Native.IsIconic(hwnd); }, 2000)) return Fail(result, "RESTORE_FAILED", "Window remained minimized.");
                }
                // Foreground lock is respected; do not inject Alt or attach input queues to bypass it.
                bool foregroundCall = Native.SetForegroundWindow(hwnd);
                result.Attempts.Add("SetForegroundWindow=" + foregroundCall);
                if (Native.GetForegroundWindow() != hwnd)
                {
                    try { AutomationElement.FromHandle(hwnd).SetFocus(); result.Attempts.Add("UIA window SetFocus returned"); }
                    catch (Exception e) { result.Attempts.Add("UIA window SetFocus: " + BrowserTree.Error(e)); }
                }
                if (!Wait(delegate { return Native.GetForegroundWindow() == hwnd; }, 1500))
                    return Fail(result, "FOREGROUND_DENIED", "Windows foreground policy denied activation, another window intercepted focus, or a modal is blocking. Run from an interactive foreground terminal.");
                // Restoration may rebuild UIA. Resolve identity again; never use an ordinal as identity.
                var refreshed = BrowserTree.Read(hwnd);
                tab = refreshed.Tabs.SingleOrDefault(t => t.Record.RuntimeId == request.Tab.RuntimeId);
                if (tab == null) return Fail(result, "STALE_AFTER_RESTORE", "Restoration rebuilt the accessibility node. Refresh and select again.");
                object pattern;
                if (tab.Element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out pattern))
                {
                    try { ((SelectionItemPattern)pattern).Select(); result.Method = "SelectionItem.Select"; }
                    catch (Exception e) { result.Attempts.Add(BrowserTree.Error(e)); }
                }
                if (result.Method == null && tab.Element.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
                {
                    try { ((InvokePattern)pattern).Invoke(); result.Method = "Invoke.Invoke"; }
                    catch (Exception e) { result.Attempts.Add(BrowserTree.Error(e)); }
                }
                if (result.Method == null) return Fail(result, "NO_USABLE_PATTERN", "SelectionItem/Invoke absent or failed; no coordinate or Ctrl+number fallback.");
                result.SelectionVerified = Wait(delegate
                {
                    var check = BrowserTree.Read(hwnd);
                    return check.Result.TraversalComplete && check.Tabs.Count(t => t.Record.Selected) == 1 &&
                        check.Tabs.Any(t => t.Record.RuntimeId == request.Tab.RuntimeId && t.Record.Selected);
                }, 2500);
                result.ForegroundVerified = Native.GetForegroundWindow() == hwnd;
                if (!result.SelectionVerified) return Fail(result, "SELECTION_UNVERIFIED", "Exact runtime ID was not the sole selected tab after the operation.");
                if (!result.ForegroundVerified) return Fail(result, "FOREGROUND_LOST", "Tab selected but target is no longer foreground.");
                if (request.Highlight)
                {
                    Highlight.Show(hwnd, result, request.HighlightImagePath);
                    if (!SelectionFeedback.CheckHighlight(result))
                        return Fail(result, "HIGHLIGHT_UNVERIFIED", "Tab was selected; border creation, duration, or bounds check failed.");
                }
                result.Status = "Passed";
                result.Detail = "Exact tab runtime ID was exclusively selected and target HWND was foreground at selection verification." +
                    (result.FocusChangedAfterSelection ? " Focus moved afterwards during the highlight; no reactivation was attempted." : "");
                return result;
            }
            catch (Exception e) { return Fail(result, "UIA_OR_WIN32_ERROR", BrowserTree.Error(e)); }
        }
        internal static bool Wait(Func<bool> predicate, int timeout)
        {
            var watch = Stopwatch.StartNew();
            do { if (predicate()) return true; Thread.Sleep(80); } while (watch.ElapsedMilliseconds < timeout);
            return false;
        }
        internal static SelectionResult Fail(SelectionResult r, string code, string message)
        { r.FailureCode = code; r.Detail = message; return r; }
    }
}
