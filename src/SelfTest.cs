using System;
using System.Linq;

namespace TabPilot
{
    internal static class SelfTest
    {
        internal static int Run()
        {
            // These guard identity/safety behavior; they do not replace Chrome integration tests.
            var snapshot = new Snapshot();
            Assert(Program.Select(snapshot, 1).FailureCode == "INVALID_ID", "missing ID rejected");
            var w = new WindowRecord { Handle = "0x0", Pid = -1, ProcessStartUtc = "invalid" };
            w.Scan.Tabs.Add(new TabRecord { Id = 1, RuntimeId = "test", Title = "same title" });
            snapshot.Windows.Add(w);
            Assert(Program.Select(snapshot, 1).FailureCode == "STALE_WINDOW", "stale HWND cannot select a same-title tab");
            snapshot.Windows.Add(w);
            Assert(Program.Select(snapshot, 1).FailureCode == "INVALID_ID", "ambiguous ID rejected");
            Assert(Program.Quote("a b") == "\"a b\"", "space quoting");
            Assert(Program.Quote("a\"b") == "\"a\\\"b\"", "quote escaping");
            Assert(Program.Quote(@"C:\space path\") == "\"C:\\space path\\\\\"", "trailing slash escaping");
            Assert(Native.DpiContext() == "PerMonitorV2", "manifest DPI context");
            Assert(Native.Monitors().Any(), "monitor enumeration");
            Console.WriteLine("Self-test: 8/8 passed. Chrome and hardware matrix are separate integration tests.");
            return 0;
        }
        static void Assert(bool passed, string message)
        { if (!passed) throw new InvalidOperationException("SELF_TEST_FAILED: " + message); }
    }
}
