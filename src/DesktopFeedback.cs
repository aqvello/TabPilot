using System;
using System.Collections.Generic;
using System.Linq;

namespace TabPilot
{
    internal sealed class ScanFeedback
    {
        internal string Summary;
        internal bool Warning;
        internal static ScanFeedback From(Snapshot snapshot)
        {
            if (snapshot == null) return new ScanFeedback { Summary = "" };
            var windows = snapshot.Windows.Where(w => w.Eligible).ToArray();
            int failed = windows.Count(w => w.Scan.Status != "Enumerated" || !w.Scan.TraversalComplete);
            int collapsed = windows.Count(w => w.Scan.Groups.Any(g => g.State == "Collapsed") ||
                w.Scan.Diagnostics.Any(d => d.StartsWith("COLLAPSED_GROUP_PRESENT", StringComparison.Ordinal)));
            var problems = new List<string>();
            if (failed > 0) problems.Add(UiText.F("{0} 个窗口未完整读取", failed));
            if (collapsed > 0) problems.Add(UiText.T("存在折叠组，可能遗漏标签"));
            if (snapshot.Diagnostics.Any(d => d.StartsWith("PROCESS_IDENTITY_UNAVAILABLE", StringComparison.Ordinal))) problems.Add(UiText.T("部分进程无法访问，可能遗漏窗口"));
            return new ScanFeedback { Warning = problems.Count > 0, Summary = problems.Count > 0 ?
                String.Join(UiText.T("；"), problems) + UiText.T("，可查看详情或按 F5 重试。") :
                windows.Length == 0 ? UiText.T("没有识别到 Chrome 窗口；可查看详情或按 F5 重试。") : UiText.T("已读取可访问标签，完整性未验证 · 详情") };
        }
        internal static string Details(Snapshot snapshot)
        {
            if (snapshot == null) return UiText.T("尚无扫描结果。");
            var lines = new List<string> { UiText.T("可访问标签数量不等于 Chrome 内部全部标签数量。") };
            lines.AddRange(snapshot.Diagnostics);
            foreach (var w in snapshot.Windows.Where(w => w.Eligible))
            {
                lines.Add(UiText.F("{0}: {1}，读取到 {2} 个标签", w.Handle, w.Scan.Status, w.Scan.Tabs.Count));
                lines.AddRange(w.Diagnostics.Concat(w.Scan.Diagnostics));
            }
            return String.Join(Environment.NewLine, lines);
        }
    }
    internal static class SelectionFeedback
    {
        internal static bool ReopenAfterFailure(SelectionResult result)
        { return result.Status != "Passed" && !result.SelectionVerified; }
        internal static bool CheckHighlight(SelectionResult result)
        {
            result.FocusChangedAfterSelection = !result.HighlightForegroundPreserved;
            return result.HighlightCreated && result.HighlightElapsedMs >= 2000 && result.HighlightBoundsVerified;
        }
    }
}
