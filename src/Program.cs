using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace TabPilot
{
    internal static class Program
    {
        internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024, RecursionLimit = 128 };
        static readonly object WorkerLock = new object();
        static readonly HashSet<Process> Workers = new HashSet<Process>();
        static bool workersStopping;
        internal static string Exe { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TabPilot.exe"); } }
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "--worker") return Worker(args);
                bool consoleBuild = Path.GetFileNameWithoutExtension(typeof(Program).Assembly.Location).EndsWith(".Cli", StringComparison.OrdinalIgnoreCase);
                string command = args.Length == 0 ? (consoleBuild ? "interactive" : "gui") : args[0];
                if (command == "gui" || command == "--hidden") return DesktopApp.Run(command == "--hidden" || args.Contains("--hidden"));
                Console.OutputEncoding = new UTF8Encoding(false);
                Console.InputEncoding = new UTF8Encoding(false);
                if (command == "--help" || command == "help") { Help(); return 0; }
                if (command == "self-test") return SelfTest.Run();
                if (command == "list" || command == "interactive")
                {
                    var snapshot = Capture(IntOption(args, "--timeout-ms", 15000));
                    string output = Option(args, "--out", Path.Combine("artifacts", "private", "snapshot.json"));
                    Save(output, snapshot);
                    Print(snapshot);
                    Console.WriteLine("Snapshot: " + Path.GetFullPath(output));
                    return command == "interactive" ? Interactive(snapshot, output) : (snapshot.Windows.Any(w => w.Eligible && w.Scan.Tabs.Count > 0) ? 0 : 2);
                }
                if (command == "select")
                {
                    var snapshot = Read<Snapshot>(Required(args, "--snapshot"));
                    int id = IntOption(args, "--id", -1);
                    SelectionResult result = Select(snapshot, id);
                    string output = Option(args, "--out", Path.Combine("artifacts", "private", "selection.json"));
                    Save(output, result);
                    Print(result);
                    return result.Status == "Passed" ? 0 : 3;
                }
                if (command == "verify")
                {
                    string input = Required(args, "--snapshot");
                    var snapshot = Read<Snapshot>(input);
                    string idsText = Required(args, "--ids");
                    var ids = idsText == "all" ? snapshot.Windows.Where(w => w.Eligible).SelectMany(w => w.Scan.Tabs).Select(t => t.Id).ToArray() :
                        idsText.Split(',').Select(Int32.Parse).ToArray();
                    if (ids.Length == 0) throw new ArgumentException("No target IDs.");
                    var report = new VerificationReport { StartedUtc = DateTime.UtcNow.ToString("o"), SnapshotPath = Path.GetFullPath(input) };
                    var affected = snapshot.Windows.Where(w => w.Eligible && w.Scan.Tabs.Any(t => ids.Contains(t.Id))).ToArray();
                    var originals = affected.SelectMany(w => w.Scan.Tabs).Where(t => t.Selected).Select(t => t.Id).ToArray();
                    string reportPath = Option(args, "--out", Path.Combine("artifacts", "private", "verification.json"));
                    try
                    {
                        foreach (int id in ids)
                        {
                            var result = Select(snapshot, id);
                            report.Results.Add(result);
                            report.Attempted++;
                            if (result.Status == "Passed") report.Passed++;
                            Print(result);
                            Save(reportPath, report);
                        }
                    }
                    finally
                    {
                        // Best effort restores original selected tabs; window activation/minimization are observable side effects.
                        foreach (int id in originals) report.RestoreResults.Add(Select(snapshot, id));
                        foreach (var window in affected.Where(w => w.Minimized))
                            if (Native.SameWindow(window)) Native.ShowWindowAsync(Native.Hwnd(window.Handle), 6);
                        Save(reportPath, report);
                    }
                    Console.WriteLine("Verified selections: {0}/{1} ({2:P1}). Enumeration completeness requires independent expected counts.",
                        report.Passed, report.Attempted, (double)report.Passed / report.Attempted);
                    return report.Passed == report.Attempted ? 0 : 3;
                }
                Help();
                return 1;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(BrowserTree.Error(e));
                if (args.Length == 0 || args[0] == "gui" || args[0] == "--hidden")
                {
                    try { Save(Path.Combine(DesktopApp.DataDirectory, "startup-error.json"), new { Message = e.ToString() }); }
                    catch (Exception) { }
                    if (!Path.GetFileNameWithoutExtension(typeof(Program).Assembly.Location).EndsWith(".Cli", StringComparison.OrdinalIgnoreCase))
                        System.Windows.Forms.MessageBox.Show(UiText.T("TabPilot 无法启动：") + e.Message, "TabPilot", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
                return 1;
            }
        }
        static int Worker(string[] args)
        {
            using (var gate = EventWaitHandle.OpenExisting(args[4]))
                if (!gate.WaitOne(5000)) throw new TimeoutException("Parent did not release worker gate");
            if (args[1] == "scan") Save(args[3], BrowserTree.Read(Native.Hwnd(args[2])).Result);
            else if (args[1] == "select") Save(args[3], Switcher.Select(Read<SelectionRequest>(args[2])));
            else throw new ArgumentException("Unknown worker operation");
            return 0;
        }
        internal static Snapshot Capture(int timeout)
        {
            var snapshot = new Snapshot { CapturedUtc = DateTime.UtcNow.ToString("o"), OsVersion = Environment.OSVersion.ToString(),
                DpiAwareness = Native.DpiContext(), Monitors = Native.Monitors(), Windows = Native.Windows() };
            if (Native.IdentityLookupFailures > 0) snapshot.Diagnostics.Add("PROCESS_IDENTITY_UNAVAILABLE: " + Native.IdentityLookupFailures +
                " process identities could not be queried (access denied or exited). Inaccessible Chrome cannot be ruled out.");
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(Exe))
                snapshot.ExecutableSha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            int id = 1;
            ScanBatch.Run(snapshot.Windows.Where(w => w.Eligible).ToArray(), timeout, delegate(WindowRecord w, int remaining)
            {
                string failure;
                ScanResult result;
                return Child<ScanResult>("scan", w.Handle, remaining, out result, out failure) ? result :
                    new ScanResult { Status = "WorkerFailed", TraversalComplete = false, Diagnostics = new List<string> { failure } };
            });
            foreach (var w in snapshot.Windows.Where(w => w.Eligible))
            {
                foreach (var tab in w.Scan.Tabs) tab.Id = id++;
            }
            if (!snapshot.Windows.Any(w => w.Eligible)) snapshot.Diagnostics.Add("NO_ELIGIBLE_CHROME_WINDOWS: no visible/minimized unowned Google Chrome frame identified.");
            return snapshot;
        }
        internal static SelectionResult Select(Snapshot snapshot, int id, string highlightImagePath = null)
        {
            var matches = snapshot.Windows.SelectMany(w => w.Scan.Tabs.Select(t => new { Window = w, Tab = t })).Where(x => x.Tab.Id == id).ToArray();
            if (matches.Length != 1) return new SelectionResult { Id = id, FailureCode = "INVALID_ID", Detail = "ID not unique or absent from this snapshot." };
            var match = matches[0];
            string request = Path.Combine(Path.GetTempPath(), "tabpilot-request-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                Save(request, new SelectionRequest { Window = match.Window, Tab = match.Tab, HighlightImagePath = highlightImagePath });
                SelectionResult result;
                string failure;
                if (Child<SelectionResult>("select", request, 20000, out result, out failure)) return result;
                return new SelectionResult { Id = id, WindowHandle = match.Window.Handle, Title = match.Tab.Title,
                    FailureCode = "WORKER_FAILED_OR_TIMEOUT", Detail = failure + " Operation may have partially completed; refresh before retrying." };
            }
            finally { File.Delete(request); }
        }
        static bool Child<T>(string operation, string input, int timeout, out T result, out string failure) where T : class
        {
            var watch = Stopwatch.StartNew();
            string output = Path.Combine(Path.GetTempPath(), "tabpilot-result-" + Guid.NewGuid().ToString("N") + ".json");
            result = null;
            failure = null;
            try
            {
                lock (WorkerLock) if (workersStopping) { failure = "APPLICATION_EXITING"; return false; }
                string gateName = @"Local\TabPilot-" + Guid.NewGuid().ToString("N");
                using (var gate = new EventWaitHandle(false, EventResetMode.ManualReset, gateName))
                {
                var info = new ProcessStartInfo(Exe, "--worker " + operation + " " + Quote(input) + " " + Quote(output) + " " + Quote(gateName))
                { UseShellExecute = false, CreateNoWindow = true };
                using (var child = Process.Start(info))
                {
                    lock (WorkerLock)
                    {
                        if (workersStopping) { child.Kill(); failure = "APPLICATION_EXITING"; return false; }
                        Workers.Add(child);
                    }
                    try
                    {
                    // A foreground interactive parent can legitimately grant activation to its worker.
                    if (operation == "select") Native.AllowSetForegroundWindow(child.Id);
                    gate.Set();
                    if (!child.WaitForExit((int)Math.Max(0, timeout - watch.ElapsedMilliseconds)))
                    {
                        child.Kill(); child.WaitForExit(1000);
                        failure = "UIA_WORKER_TIMEOUT after " + timeout + "ms; only TabPilot worker terminated.";
                        return false;
                    }
                    if (child.ExitCode != 0 || !File.Exists(output))
                    { failure = "UIA_WORKER_EXIT=" + child.ExitCode + "; result missing=" + !File.Exists(output); return false; }
                    }
                    finally { lock (WorkerLock) Workers.Remove(child); }
                }
                }
                result = Read<T>(output);
                return result != null;
            }
            catch (Exception e) { failure = BrowserTree.Error(e); return false; }
            finally { File.Delete(output); }
        }
        internal static void StopWorkers()
        {
            lock (WorkerLock)
            {
                workersStopping = true;
                foreach (var child in Workers)
                    try { if (!child.HasExited) child.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
            }
        }
        internal static string Quote(string value)
        {
            // Windows CommandLineToArgvW quoting; no shell is involved.
            var b = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                b.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                b.Append(c);
                slashes = 0;
            }
            b.Append('\\', slashes * 2).Append('"');
            return b.ToString();
        }
        static int Interactive(Snapshot snapshot, string output)
        {
            Console.WriteLine("Enter tab ID; r=refresh; q=quit. Selecting restores minimized windows and activates Chrome.");
            while (true)
            {
                Console.Write("tab> ");
                string line = Console.ReadLine();
                if (line == null || line.Trim().Equals("q", StringComparison.OrdinalIgnoreCase)) return 0;
                if (line.Trim().Equals("r", StringComparison.OrdinalIgnoreCase)) { snapshot = Capture(15000); Save(output, snapshot); Print(snapshot); continue; }
                int id;
                if (!Int32.TryParse(line, out id)) { Console.WriteLine("Use a displayed numeric ID, r or q."); continue; }
                var result = Select(snapshot, id);
                Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)), "selection-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json"), result);
                Print(result);
            }
        }
        static string Clean(string value)
        { return value == null ? "" : new String(value.Select(c => Char.IsControl(c) || Char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format ? ' ' : c).ToArray()); }
        static void Print(Snapshot snapshot)
        {
            Console.WriteLine("TabPilot Windows native PoC | {0} | DPI={1}", snapshot.OsVersion, snapshot.DpiAwareness);
            foreach (var monitor in snapshot.Monitors)
            {
                Console.WriteLine("Monitor {0} {1} scale={2}% primary={3} bounds={4}", monitor.Device, monitor.Handle, monitor.ScalePercent, monitor.Primary, monitor.Bounds);
                foreach (var w in snapshot.Windows.Where(w => w.Eligible && w.MonitorHandle == monitor.Handle))
                {
                    Console.WriteLine("  Window {0} PID={1} minimized={2} DPI={3} frame={4} | {5}", w.Handle, w.Pid, w.Minimized, w.Dpi, w.FrameBounds, Clean(w.Title));
                    Console.WriteLine("    UIA={0} exposed={1} traversalComplete={2}; total count coverage=UNKNOWN", w.Scan.Status, w.Scan.Tabs.Count, w.Scan.TraversalComplete);
                    foreach (var tab in w.Scan.Tabs)
                        Console.WriteLine("    [{0}] {1} selected={2} offscreen={3} select={4} invoke={5}", tab.Id, Clean(tab.Title), tab.Selected, tab.Offscreen, tab.CanSelect, tab.CanInvoke);
                    foreach (var group in w.Scan.Groups) Console.WriteLine("    Group {0}: {1}", Clean(group.Name), group.State);
                    foreach (string d in w.Diagnostics.Concat(w.Scan.Diagnostics)) Console.WriteLine("    ! " + Clean(d));
                }
            }
            foreach (var w in snapshot.Windows.Where(w => !w.Eligible))
                Console.WriteLine("Skipped candidate {0} {1} {2}: {3}", w.Handle, Clean(w.ProductName), w.ClassName, w.SkipReason);
            foreach (string d in snapshot.Diagnostics) Console.WriteLine("! " + d);
        }
        static void Print(SelectionResult result)
        {
            Console.WriteLine("[{0}] {1} HWND={2} method={3} selected={4} foreground={5} border={6}ms | {7}", result.Id,
                result.Status, result.WindowHandle, result.Method, result.SelectionVerified, result.ForegroundVerified, result.HighlightElapsedMs,
                result.FailureCode == null ? result.Detail : result.FailureCode + ": " + result.Detail);
        }
        internal static void Save(string path, object value)
        {
            Storage.Write(path, Serialize(value), false);
        }
        internal static string Serialize(object value) { lock (Json) return Json.Serialize(value); }
        internal static T Read<T>(string path)
        {
            string text;
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(file, Encoding.UTF8)) text = reader.ReadToEnd();
            lock (Json) return Json.Deserialize<T>(text);
        }
        static string Option(string[] args, string name, string fallback)
        {
            int i = Array.IndexOf(args, name);
            if (i < 0) return fallback;
            if (i + 1 >= args.Length) throw new ArgumentException("Missing value for " + name);
            return args[i + 1];
        }
        static string Required(string[] args, string name)
        { string value = Option(args, name, null); if (String.IsNullOrEmpty(value)) throw new ArgumentException("Required: " + name); return value; }
        static int IntOption(string[] args, string name, int fallback) { return Int32.Parse(Option(args, name, fallback.ToString())); }
        static void Help()
        {
            Console.WriteLine(@"TabPilot.exe [gui|--hidden]   Desktop popup; default hotkey Ctrl+Alt+Space.
TabPilot.Cli.exe [interactive|list] [--out snapshot.json] [--timeout-ms 15000]
TabPilot.Cli.exe select --snapshot snapshot.json --id 12 [--out selection.json]
TabPilot.Cli.exe verify --snapshot snapshot.json --ids all|1,2,3 [--out verification.json]
TabPilot.Cli.exe self-test
No extension, Chrome launch flags, DevTools port, profile reads or configuration writes.
Requires Windows 10 1703+ x64, .NET Framework 4.x, interactive desktop at Chrome's integrity level.
UIA enumeration is provider-dependent. Exposed count alone does not prove all tabs were read.
verify activates each requested tab and shows a 2s border; original selected tabs are restored best-effort.");
        }
    }
}
