using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TabPilot
{
    internal static class ScanBatch
    {
        // One deadline for the entire UIA batch, with a bounded number of isolated workers.
        internal static void Run(IList<WindowRecord> windows, int budgetMs, Func<WindowRecord, int, ScanResult> scan)
        {
            if (budgetMs <= 0) throw new ArgumentOutOfRangeException("budgetMs");
            var watch = Stopwatch.StartNew();
            Parallel.ForEach(windows, new ParallelOptions { MaxDegreeOfParallelism = 4 }, window =>
            {
                int remaining = (int)Math.Max(0, budgetMs - watch.ElapsedMilliseconds);
                if (remaining == 0)
                {
                    window.Scan = new ScanResult { Status = "DeadlineExceeded", TraversalComplete = false };
                    window.Scan.Diagnostics.Add("SCAN_DEADLINE_EXCEEDED: batch budget exhausted before this window could be scanned; retry with F5.");
                    return;
                }
                try { window.Scan = scan(window, remaining); }
                catch (Exception e)
                {
                    window.Scan = new ScanResult { Status = "WorkerFailed", TraversalComplete = false };
                    window.Scan.Diagnostics.Add(BrowserTree.Error(e));
                }
            });
        }
    }
}
