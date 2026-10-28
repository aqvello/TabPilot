// Independently invented persistence fixture; unrelated to captured hardware or placement.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using TabPilot;

internal static class ReliabilityChecks
{
    internal static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reliability-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string portable = Path.Combine(root, "portable"), fallback = Path.Combine(root, "user");
            check(Storage.SelectDirectory(portable, fallback) == portable, "writable portable data directory retained");
            string blocked = Path.Combine(root, "blocked"); File.WriteAllText(blocked, "not a writable directory");
            check(Storage.SelectDirectory(blocked, fallback) == fallback, "unavailable portable directory falls back to user data");
            bool rejected = false;
            try { Storage.SelectDirectory(blocked, blocked); } catch (IOException) { rejected = true; }
            check(rejected, "no writable storage produces an explicit error");

            string path = Path.Combine(portable, "settings.json");
            var settings = new DesktopSettings { HotkeyKey = 81, PopupPlacement = new SavedPopupPlacement { MonitorDevice = "fictional-storage-screen", Dpi = 216,
                Bounds = new Box { Left = 144, Top = -1696, Right = 1360, Bottom = -768 },
                WorkArea = new Box { Left = -96, Top = -1984, Right = 1936, Bottom = -448 } } };
            Storage.Write(path, Program.Serialize(settings), true);
            string first = File.ReadAllText(path);
            settings.HotkeyKey = 84;
            Storage.Write(path, Program.Serialize(settings), true);
            check(File.ReadAllText(path + ".bak") == first && Program.Read<DesktopSettings>(path).HotkeyKey == 84,
                "atomic settings replacement preserves previous successful backup");
            var pending = new PendingSettings(delegate { Storage.Write(path, Program.Serialize(settings), true); });
            pending.Changed();
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                check(!pending.Flush() && pending.Dirty && pending.Error != null, "locked settings retain dirty state and expose retry error");
            check(Program.Read<DesktopSettings>(path).HotkeyKey == 84 && Directory.GetFiles(portable, "*.tmp").Length == 0,
                "failed atomic replacement leaves old settings intact and removes temporary file");
            settings.HotkeyKey = 81;
            check(pending.Flush() && !pending.Dirty && pending.Error == null && Program.Read<DesktopSettings>(path).HotkeyKey == 81,
                "retry after lock release persists latest settings and clears error");
            File.WriteAllText(path, "{broken");
            string notice;
            var recovered = Storage.ReadSettings(path, out notice);
            check(recovered.HotkeyKey == 84 && PopupPlacement.Same(recovered.PopupPlacement, settings.PopupPlacement) && notice != null,
                "corrupt primary settings recover hotkey and position from backup");
            File.Delete(path);
            check(Storage.ReadSettings(path, out notice).HotkeyKey == 84 && notice != null, "missing primary settings recover backup");
            File.WriteAllText(path + ".bak", "broken");
            check(Storage.ReadSettings(path, out notice).HotkeyKey == 32 && notice != null, "unrecoverable settings show notice and use defaults");
        }
        finally { Directory.Delete(root, true); }

        var windows = Enumerable.Range(0, 20).Select(i => new WindowRecord { Handle = i.ToString(), Eligible = true }).ToArray();
        int active = 0, peak = 0;
        var watch = Stopwatch.StartNew();
        ScanBatch.Run(windows, 400, delegate(WindowRecord w, int remaining)
        {
            int count = Interlocked.Increment(ref active);
            int old;
            do { old = peak; if (old >= count) break; } while (Interlocked.CompareExchange(ref peak, count, old) != old);
            try
            {
                if (w.Handle == "0") Thread.Sleep(remaining);
                else Thread.Sleep(Math.Min(remaining, 30));
                return new ScanResult { Status = "Enumerated" };
            }
            finally { Interlocked.Decrement(ref active); }
        });
        check(peak > 1 && peak <= 4, "scan concurrency is bounded to four workers");
        check(watch.ElapsedMilliseconds < 2000 && windows.Skip(1).Any(w => w.Scan.Status == "Enumerated"),
            "one slow scan does not serialize healthy windows or multiply the deadline");
        windows = Enumerable.Range(0, 20).Select(i => new WindowRecord { Handle = i.ToString(), Eligible = true }).ToArray();
        watch.Restart();
        ScanBatch.Run(windows, 250, delegate(WindowRecord w, int remaining) { Thread.Sleep(remaining + 5); return new ScanResult { Status = "WorkerFailed", TraversalComplete = false }; });
        check(watch.ElapsedMilliseconds < 1500 && windows.Any(w => w.Scan.Status == "DeadlineExceeded"),
            "queued windows receive explicit deadline failures instead of another full timeout");
        ScanBatch.Run(windows.Take(2).ToArray(), 1000, delegate(WindowRecord w, int remaining)
        { if (w.Handle == "0") throw new IOException("test worker error"); return new ScanResult { Status = "Enumerated" }; });
        check(windows[0].Scan.Status == "WorkerFailed" && windows[1].Scan.Status == "Enumerated", "one failed scan preserves other window results");

        var snapshot = new Snapshot();
        var window = new WindowRecord { Handle = "test", Eligible = true, Scan = new ScanResult { Status = "Enumerated" } };
        snapshot.Windows.Add(window);
        window.Scan.Groups.Add(new GroupRecord { State = "Collapsed" });
        window.Scan.Diagnostics.Add("COMPLETENESS_UNKNOWN: test");
        window.Scan.Diagnostics.Add("COLLAPSED_GROUP_PRESENT: test");
        check(ScanFeedback.From(snapshot).Warning && ScanFeedback.From(snapshot).Summary.Contains("折叠"),
            "enumerated windows still display a collapsed-group omission warning");
        string details = ScanFeedback.Details(snapshot);
        check(details.Contains("COLLAPSED_GROUP_PRESENT") && details.Contains("COMPLETENESS_UNKNOWN"),
            "details retain diagnostics from successfully enumerated windows");
        window.Scan.Groups.Clear(); window.Scan.Diagnostics.RemoveAt(1);
        check(!ScanFeedback.From(snapshot).Warning && ScanFeedback.From(snapshot).Summary.Contains("完整性未验证"),
            "normal scan reports unknown completeness without claiming all tabs");
        snapshot.Diagnostics.Add("PROCESS_IDENTITY_UNAVAILABLE: test");
        check(ScanFeedback.From(snapshot).Warning && ScanFeedback.Details(snapshot).Contains("PROCESS_IDENTITY_UNAVAILABLE"),
            "inaccessible process warning reaches summary and details");
        snapshot.Diagnostics.Clear(); window.Scan.Status = "DeadlineExceeded"; window.Scan.TraversalComplete = false;
        check(ScanFeedback.From(snapshot).Warning && ScanFeedback.From(snapshot).Summary.Contains("未完整读取"), "batch timeout is visibly reported as incomplete");

        var selection = new SelectionResult { SelectionVerified = true, ForegroundVerified = true, HighlightCreated = true,
            HighlightElapsedMs = 2001, HighlightBoundsVerified = true, HighlightForegroundPreserved = false };
        check(SelectionFeedback.CheckHighlight(selection) && selection.FocusChangedAfterSelection,
            "focus moving after verified selection does not invalidate a valid highlight");
        check(!SelectionFeedback.ReopenAfterFailure(selection), "verified selection cannot reopen popup and steal focus");
        selection.HighlightBoundsVerified = false;
        check(!SelectionFeedback.CheckHighlight(selection) && !SelectionFeedback.ReopenAfterFailure(selection),
            "real highlight failures remain reported without reopening after completed selection");
        check(SelectionFeedback.ReopenAfterFailure(new SelectionResult { FailureCode = "FOREGROUND_DENIED" }),
            "failure before selection still returns to popup for explanation");
    }
}
