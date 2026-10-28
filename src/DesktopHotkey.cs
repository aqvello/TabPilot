using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TabPilot
{
    internal sealed class GlobalHotkey : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        readonly IntPtr hwnd;
        int id = 0x3151;
        internal bool Registered;
        internal uint Modifiers;
        internal int Key;
        internal GlobalHotkey(IntPtr hwnd) { this.hwnd = hwnd; }
        internal bool Set(uint modifiers, int key, out string error)
        {
            error = null;
            if (!Valid(modifiers, key)) { error = UiText.T("请选择包含 Ctrl 或 Alt 的组合键，并避开系统保留键。"); return false; }
            if (Registered && Modifiers == modifiers && Key == key) return true;
            int candidate = id == 0x3151 ? 0x3152 : 0x3151;
            if (!RegisterHotKey(hwnd, candidate, modifiers | 0x4000, (uint)key))
            { error = UiText.T("快捷键已被其他程序占用，或 Windows 不允许注册。请点击“快捷键”换一个组合键。"); return false; }
            if (Registered) UnregisterHotKey(hwnd, id);
            id = candidate; Modifiers = modifiers; Key = key; Registered = true;
            return true;
        }
        internal bool Matches(IntPtr value) { return Registered && value.ToInt32() == id; }
        internal static bool Valid(uint modifiers, int key)
        {
            return (modifiers & 3) != 0 && (modifiers & ~7u) == 0 && key >= 8 && key <= 254 &&
                key != (int)Keys.F12 && key != (int)Keys.ControlKey && key != (int)Keys.ShiftKey && key != (int)Keys.Menu &&
                key != (int)Keys.LWin && key != (int)Keys.RWin && key != (int)Keys.Tab && key != (int)Keys.Escape &&
                !(modifiers == 1 && key == (int)Keys.F4);
        }
        internal static string Describe(uint modifiers, int key)
        {
            return ((modifiers & 2) != 0 ? "Ctrl + " : "") + ((modifiers & 1) != 0 ? "Alt + " : "") +
                ((modifiers & 4) != 0 ? "Shift + " : "") + (key == 32 ? UiText.T("空格") : ((Keys)key).ToString());
        }
        public void Dispose() { if (Registered) UnregisterHotKey(hwnd, id); Registered = false; }
    }
    internal sealed class HotkeyDialog : Form
    {
        internal uint Modifiers;
        internal int Key;
        readonly Label value = new Label();
        internal HotkeyDialog(uint modifiers, int key)
        {
            Modifiers = modifiers; Key = key;
            Text = UiText.T("修改呼出快捷键");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new System.Drawing.Size(440, 180);
            Font = new System.Drawing.Font("Microsoft YaHei UI", 10);
            KeyPreview = true;
            var prompt = new Label { Text = UiText.T("按下新的组合键，例如 Ctrl + Alt + Q"), AutoSize = false, Left = 20, Top = 20, Width = 400, Height = 30 };
            value.SetBounds(20, 60, 400, 40);
            value.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            value.BackColor = System.Drawing.Color.FromArgb(245, 245, 247);
            value.Text = GlobalHotkey.Describe(modifiers, key);
            var save = new Button { Text = UiText.T("保存"), DialogResult = DialogResult.OK, Left = 240, Top = 125, Width = 80, Height = 32 };
            var cancel = new Button { Text = UiText.T("取消"), DialogResult = DialogResult.Cancel, Left = 330, Top = 125, Width = 80, Height = 32 };
            Controls.AddRange(new Control[] { prompt, value, save, cancel });
            CancelButton = cancel;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                uint mods = (e.Control ? 2u : 0u) | (e.Alt ? 1u : 0u) | (e.Shift ? 4u : 0u);
                if (GlobalHotkey.Valid(mods, (int)e.KeyCode))
                { Modifiers = mods; Key = (int)e.KeyCode; value.Text = GlobalHotkey.Describe(mods, Key); e.Handled = e.SuppressKeyPress = true; }
            };
        }
    }
}
