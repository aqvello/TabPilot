using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace TabPilot
{
    internal sealed class EdgeForm : Form
    {
        internal EdgeForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(255, 100, 20);
            TopMost = true;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x00000080 | 0x00000020 | 0x00080000; return p; }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0084) { m.Result = new IntPtr(-1); return; } // HTTRANSPARENT
            if (m.Msg == 0x0021) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE
            base.WndProc(ref m);
        }
    }
    internal static class Highlight
    {
        internal static void Show(IntPtr hwnd, SelectionResult result, string imagePath)
        {
            var edges = new[] { new EdgeForm(), new EdgeForm(), new EdgeForm(), new EdgeForm() };
            var timer = new Timer { Interval = 16 };
            var watch = new Stopwatch();
            bool stayedForeground = true;
            bool boundsVerified = true;
            bool captured = false;
            Action update = delegate
            {
                var b = Native.VisibleFrame(hwnd);
                if (result.HighlightFrameBounds == null) result.HighlightFrameBounds = b;
                int width = Math.Max(3, (int)Math.Round(Native.GetDpiForWindow(hwnd) / 96.0 * 4));
                var rects = new[] { new Rectangle(b.Left, b.Top, b.Width, width),
                    new Rectangle(b.Left, b.Bottom - width, b.Width, width),
                    new Rectangle(b.Left, b.Top + width, width, Math.Max(1, b.Height - 2 * width)),
                    new Rectangle(b.Right - width, b.Top + width, width, Math.Max(1, b.Height - 2 * width)) };
                for (int i = 0; i < edges.Length; i++)
                {
                    var r = rects[i];
                    bool positioned = Native.SetWindowPos(edges[i].Handle, new IntPtr(-1), r.X, r.Y, r.Width, r.Height, 0x0010 | 0x0040);
                    Native.RECT actual;
                    bool read = Native.GetWindowRect(edges[i].Handle, out actual);
                    boundsVerified &= positioned && read && actual.Left == r.Left && actual.Top == r.Top && actual.Right == r.Right && actual.Bottom == r.Bottom;
                    if (result.HighlightEdgeBounds.Count < 4) result.HighlightEdgeBounds.Add(Native.Box(actual));
                }
            };
            try
            {
                foreach (var edge in edges) { edge.Opacity = 0.95; edge.Show(); }
                update();
                Application.DoEvents();
                result.HighlightCreated = Array.TrueForAll(edges, e => Native.IsWindowVisible(e.Handle));
                watch.Start();
                timer.Tick += delegate
                {
                    stayedForeground &= Native.GetForegroundWindow() == hwnd;
                    if (!captured && imagePath != null && watch.ElapsedMilliseconds > 200)
                    {
                        captured = true;
                        try
                        {
                            var b = Native.VisibleFrame(hwnd);
                            using (var bitmap = new Bitmap(b.Width, b.Height))
                            {
                                using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(b.Left, b.Top, 0, 0, bitmap.Size);
                                string full = Path.GetFullPath(imagePath);
                                Directory.CreateDirectory(Path.GetDirectoryName(full));
                                bitmap.Save(full, ImageFormat.Png);
                                result.HighlightImagePath = full;
                            }
                        }
                        catch (Exception e) { result.HighlightImageError = e.Message; }
                    }
                    if (watch.ElapsedMilliseconds >= 2000 || !Native.IsWindow(hwnd) || Native.IsIconic(hwnd))
                    { timer.Stop(); Application.ExitThread(); return; }
                    try { update(); } catch { timer.Stop(); Application.ExitThread(); }
                };
                timer.Start();
                Application.Run();
                result.HighlightElapsedMs = watch.ElapsedMilliseconds;
                result.HighlightForegroundPreserved = stayedForeground && Native.GetForegroundWindow() == hwnd;
                result.HighlightBoundsVerified = boundsVerified;
            }
            finally { timer.Dispose(); foreach (var edge in edges) edge.Dispose(); }
        }
    }
}
