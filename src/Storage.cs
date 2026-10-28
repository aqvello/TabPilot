using System;
using System.IO;
using System.Text;

namespace TabPilot
{
    internal static class Storage
    {
        internal static string SelectDirectory(string portable, string userDirectory)
        {
            if (Writable(portable)) return portable;
            if (Writable(userDirectory)) return userDirectory;
            throw new IOException(UiText.T("无法创建可写的数据目录。请检查程序目录或当前用户的数据目录权限。"));
        }
        static bool Writable(string directory)
        {
            string probe = null;
            try
            {
                Directory.CreateDirectory(directory);
                probe = Path.Combine(directory, ".write-test-" + Guid.NewGuid().ToString("N"));
                using (var file = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) file.WriteByte(0);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            finally { if (probe != null) TryDelete(probe); }
        }
        internal static void Write(string path, string contents, bool backup)
        {
            string full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            string temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(contents);
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    file.Write(bytes, 0, bytes.Length);
                    file.Flush(backup);
                }
                // The old file stays intact if writing/flushing/replacing fails.
                if (File.Exists(full)) File.Replace(temporary, full, backup ? full + ".bak" : null);
                else File.Move(temporary, full);
            }
            finally { TryDelete(temporary); }
        }
        static void TryDelete(string path)
        { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        internal static DesktopSettings ReadSettings(string path, out string notice)
        {
            notice = null;
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    var value = Program.Read<DesktopSettings>(candidate);
                    if (value == null || !GlobalHotkey.Valid(value.HotkeyModifiers, value.HotkeyKey)) throw new InvalidDataException("Invalid settings");
                    if (candidate != path) notice = UiText.T("设置文件损坏，已恢复上一次成功保存的备份。");
                    return value;
                }
                catch (Exception e) { notice = UiText.T("设置读取失败，已使用默认设置。") + e.Message; }
            }
            return new DesktopSettings();
        }
    }
    internal sealed class PendingSettings
    {
        readonly Action write;
        internal bool Dirty { get; private set; }
        internal string Error { get; private set; }
        internal PendingSettings(Action write) { this.write = write; }
        internal void Changed() { Dirty = true; }
        internal bool Flush()
        {
            if (!Dirty) return true;
            try { write(); Dirty = false; Error = null; return true; }
            catch (Exception e) { Error = UiText.T("设置尚未保存，正在重试：") + e.Message; return false; }
        }
    }
}
