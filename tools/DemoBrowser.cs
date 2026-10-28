using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using TabPilot;

// Documentation recorder only. The shipped application never launches Chrome.
internal sealed class DemoBrowser : IDisposable
{
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
    Process process;
    DateTime started;
    readonly string profile;
    internal bool Exited { get; private set; }
    internal bool ProfileDeleted { get; private set; }

    internal DemoBrowser(string output, string executable, string page)
    {
        profile = Path.Combine(output, "isolated-profile-" + Guid.NewGuid().ToString("N"));
        // A fresh directory is mandatory; no caller-supplied profile or existing session fallback.
        Directory.CreateDirectory(profile);
        try
        {
            process = Process.Start(new ProcessStartInfo {
                FileName = executable, UseShellExecute = false,
                Arguments = "--user-data-dir=\"" + profile + "\" --no-first-run --no-default-browser-check --disable-extensions --disable-sync --new-window \"" + page + "\""
            });
            if (process == null) throw new InvalidOperationException("Isolated demo browser did not start.");
            started = process.StartTime.ToUniversalTime();
        }
        catch { Dispose(); throw; }
    }
    internal static string FindExecutable(string requested)
    {
        if (!String.IsNullOrEmpty(requested))
        {
            string path = Path.GetFullPath(requested);
            if (!File.Exists(path) || !String.Equals(Path.GetFileName(path), "chrome.exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Expected an installed chrome.exe path.");
            return path;
        }
        foreach (var folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData })
        {
            string path = Path.Combine(Environment.GetFolderPath(folder), @"Google\Chrome\Application\chrome.exe");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Chrome was not found. Supply its executable path as the third argument.");
    }
    internal bool Owns(IntPtr window)
    {
        if (process == null || process.HasExited || !Native.IsWindow(window)) return false;
        uint pid; Native.GetWindowThreadProcessId(window, out pid);
        return pid == process.Id && process.StartTime.ToUniversalTime() == started;
    }
    internal WindowRecord[] Windows()
    {
        return Native.Windows().Where(w => w.Eligible && Owns(Native.Hwnd(w.Handle))).ToArray();
    }
    public void Dispose()
    {
        if (process != null)
        {
            if (!process.HasExited)
            {
                foreach (var window in Windows()) PostMessage(Native.Hwnd(window.Handle), 0x0010, IntPtr.Zero, IntPtr.Zero);
                if (!process.WaitForExit(5000))
                {
                    // Kill only this launched browser process, never a name-based Chrome process tree.
                    process.Kill();
                    if (!process.WaitForExit(5000)) throw new IOException("Isolated demo browser did not exit.");
                }
            }
            Exited = process.HasExited;
            process.Dispose(); process = null;
        }
        else Exited = true;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            try { if (Directory.Exists(profile)) Directory.Delete(profile, true); ProfileDeleted = true; return; }
            catch (IOException) { if (attempt == 29) throw; }
            catch (UnauthorizedAccessException) { if (attempt == 29) throw; }
            Thread.Sleep(100);
        }
    }
}
