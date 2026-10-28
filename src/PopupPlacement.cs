using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace TabPilot
{
    public sealed class SavedPopupPlacement
    {
        public string MonitorDevice;
        public Box Bounds, WorkArea;
        public uint Dpi;
    }
    internal static class PopupPlacement
    {
        internal static bool Valid(SavedPopupPlacement placement)
        {
            return placement != null && placement.Bounds != null && placement.WorkArea != null &&
                placement.Bounds.Width > 0 && placement.Bounds.Height > 0 && placement.WorkArea.Width > 0 && placement.WorkArea.Height > 0 &&
                placement.Dpi >= 96 && placement.Dpi <= 960;
        }
        internal static MonitorRecord Monitor(SavedPopupPlacement saved, IList<MonitorRecord> monitors, string firstUseDevice)
        {
            if (monitors.Count == 0) throw new InvalidOperationException("No display is available");
            if (!Valid(saved)) return monitors.FirstOrDefault(m => String.Equals(m.Device, firstUseDevice, StringComparison.OrdinalIgnoreCase)) ??
                monitors.FirstOrDefault(m => m.Primary) ?? monitors[0];
            var original = monitors.FirstOrDefault(m => String.Equals(m.Device, saved.MonitorDevice, StringComparison.OrdinalIgnoreCase));
            if (original != null) return original;
            double x = (saved.Bounds.Left + (double)saved.Bounds.Right) / 2;
            double y = (saved.Bounds.Top + (double)saved.Bounds.Bottom) / 2;
            return monitors.OrderBy(m =>
            {
                double dx = Math.Max(m.WorkArea.Left - x, Math.Max(0, x - m.WorkArea.Right));
                double dy = Math.Max(m.WorkArea.Top - y, Math.Max(0, y - m.WorkArea.Bottom));
                return dx * dx + dy * dy;
            }).ThenByDescending(m => m.Primary).First();
        }
        internal static Rectangle Restore(SavedPopupPlacement saved, Box work, uint dpi)
        {
            double scale = Valid(saved) ? Math.Max(96, dpi) / (double)saved.Dpi : Math.Max(96, dpi) / 96.0;
            int width = (int)Math.Max(1, Math.Min(work.Width, Math.Round((Valid(saved) ? saved.Bounds.Width : 900) * scale)));
            int height = (int)Math.Max(1, Math.Min(work.Height, Math.Round((Valid(saved) ? saved.Bounds.Height : 660) * scale)));
            double x = Valid(saved) ? work.Left + Math.Round((saved.Bounds.Left - (double)saved.WorkArea.Left) * scale) : work.Left + (work.Width - width) / 2;
            double y = Valid(saved) ? work.Top + Math.Round((saved.Bounds.Top - (double)saved.WorkArea.Top) * scale) : work.Top + (work.Height - height) / 2;
            return new Rectangle((int)Math.Max(work.Left, Math.Min(work.Right - width, x)),
                (int)Math.Max(work.Top, Math.Min(work.Bottom - height, y)), width, height);
        }
        static bool SameBox(Box a, Box b)
        { return a != null && b != null && a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom; }
        internal static bool Same(SavedPopupPlacement a, SavedPopupPlacement b)
        {
            return a != null && b != null && String.Equals(a.MonitorDevice, b.MonitorDevice, StringComparison.OrdinalIgnoreCase) &&
                a.Dpi == b.Dpi && SameBox(a.Bounds, b.Bounds) && SameBox(a.WorkArea, b.WorkArea);
        }
    }
}
