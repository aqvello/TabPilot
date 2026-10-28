using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TabPilot
{
    internal static class UiPaint
    {
        internal static readonly Color Ink = Color.FromArgb(24, 25, 27);
        internal static readonly Color Muted = Color.FromArgb(112, 114, 118);
        internal static readonly Color Line = Color.FromArgb(226, 227, 230);
        internal static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }
        internal static void FillRound(Graphics g, Color color, RectangleF r, float radius)
        { using (var p = Rounded(r, radius)) using (var b = new SolidBrush(color)) g.FillPath(b, p); }
        internal static void Text(Graphics g, string text, Font font, Color color, Rectangle r, bool right = false)
        {
            TextRenderer.DrawText(g, text, font, r, color, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine |
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | (right ? TextFormatFlags.Right : TextFormatFlags.Left));
        }
        internal static void Globe(Graphics g, float x, float y, float radius, float scale)
        {
            using (var pen = new Pen(Color.FromArgb(115, 117, 120), Math.Max(1, 1.35f * scale)))
            {
                g.DrawEllipse(pen, x - radius, y - radius, radius * 2, radius * 2);
                g.DrawEllipse(pen, x - radius * .46f, y - radius, radius * .92f, radius * 2);
                g.DrawLine(pen, x - radius, y, x + radius, y);
            }
        }
    }
    internal sealed class QuietButton : Button
    {
        bool chosen;
        internal bool Chosen
        {
            get { return chosen; }
            set { if (chosen == value) return; chosen = value; Invalidate(); }
        }
        internal float UiScale = 1;
        bool hover;
        internal QuietButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            BackColor = Color.White;
            ForeColor = UiPaint.Ink;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);
            if (Chosen || hover || Focused) UiPaint.FillRound(e.Graphics, Chosen ? Color.FromArgb(241, 241, 243) : Color.FromArgb(248, 248, 249),
                new RectangleF(0, 0, Width - 1, Height - 1), Height / 2f);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? (Chosen ? UiPaint.Ink : ForeColor) : UiPaint.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }
    internal sealed class SearchField : Panel
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, uint message, IntPtr w, string text);
        internal readonly TextBox Input = new TextBox();
        internal float UiScale = 1;
        internal SearchField()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.White;
            Input.BorderStyle = BorderStyle.None;
            Input.BackColor = Color.White;
            Input.ForeColor = UiPaint.Ink;
            Input.AccessibleName = UiText.T("搜索标签");
            Input.AutoSize = false;
            Input.HandleCreated += delegate { ApplyLanguage(); };
            Input.GotFocus += delegate { Invalidate(); };
            Input.LostFocus += delegate { Invalidate(); };
            Controls.Add(Input);
            MouseDown += delegate { Input.Focus(); };
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int inset = (int)(20 * UiScale);
            int h = Math.Min(Height - 8, (int)Math.Ceiling(Input.Font.Height * 1.18));
            Input.SetBounds(inset, (Height - h) / 2, Math.Max(1, Width - inset * 2), h);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = UiPaint.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), Height / 2f))
            using (var pen = new Pen(Input.Focused ? Color.FromArgb(169, 174, 182) : Color.FromArgb(214, 216, 220), Math.Max(1, UiScale)))
                e.Graphics.DrawPath(pen, p);
        }
        internal void Relayout() { OnResize(EventArgs.Empty); Invalidate(); }
        internal void ApplyLanguage()
        {
            Input.AccessibleName = UiText.T("搜索标签");
            if (Input.IsHandleCreated) SendMessage(Input.Handle, 0x1501, new IntPtr(1), UiText.T("搜索所有已打开的网页…"));
        }
    }
    internal sealed class ListEntry
    {
        internal DesktopTab Tab;
        internal string Heading;
        internal string Key { get { return Tab == null ? "heading|" + Heading : "tab|" + Tab.Identity; } }
        public override string ToString() { return Tab == null ? Heading : Tab.ToString(); }
    }
    internal sealed class TabRows : ListBox
    {
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint message, IntPtr w, IntPtr l);
        internal float UiScale = 1;
        internal Font TitleFont, SmallFont;
        internal event Action<DesktopTab> ActivatedTab;
        int hovered = -1;
        internal int RebuildCount { get; private set; }
        internal TabRows()
        {
            BorderStyle = BorderStyle.None;
            BackColor = Color.White;
            DrawMode = DrawMode.OwnerDrawVariable;
            IntegralHeight = false;
            HorizontalScrollbar = false;
            AccessibleName = "Chrome 标签列表";
            Cursor = Cursors.Hand;
            MeasureItem += delegate(object sender, MeasureItemEventArgs e)
            {
                var item = e.Index >= 0 && e.Index < Items.Count ? Items[e.Index] as ListEntry : null;
                e.ItemHeight = (int)Math.Round((item != null && item.Tab == null ? 36 : 82) * UiScale);
            };
            SelectedIndexChanged += delegate
            {
                if (SelectedIndex >= 0 && ((ListEntry)SelectedItem).Tab == null) MoveSelection(1);
            };
        }
        internal DesktopTab CurrentTab { get { var row = SelectedItem as ListEntry; return row == null ? null : row.Tab; } }
        internal void SetMetrics(float scale, Font titleFont, Font smallFont)
        {
            bool changed = UiScale != scale || TitleFont != titleFont || SmallFont != smallFont;
            UiScale = scale; TitleFont = titleFont; SmallFont = smallFont;
            if (Font != smallFont) Font = smallFont;
            if (!changed) return;
            if (IsHandleCreated)
                for (int i = 0; i < Items.Count; i++)
                    SendMessage(Handle, 0x01A0, new IntPtr(i), new IntPtr((int)Math.Round((((ListEntry)Items[i]).Tab == null ? 36 : 82) * scale)));
            Invalidate();
        }
        internal void ApplyEntries(IList<ListEntry> entries)
        {
            bool sameOrder = entries.Count == Items.Count;
            if (sameOrder)
                for (int i = 0; i < entries.Count; i++)
                    if (((ListEntry)Items[i]).Key != entries[i].Key) { sameOrder = false; break; }
            if (sameOrder)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var current = (ListEntry)Items[i];
                    var next = entries[i];
                    bool changed = current.Tab != null && (current.Tab.Title != next.Tab.Title ||
                        current.Tab.Subtitle != next.Tab.Subtitle || current.Tab.MonitorLabel != next.Tab.MonitorLabel);
                    // Keep native items/selection/scroll position, but always bind actions to the fresh snapshot.
                    current.Tab = next.Tab;
                    if (changed) InvalidateRow(i);
                }
                return;
            }
            string selected = CurrentTab == null ? null : CurrentTab.Identity;
            string topKey = Items.Count > 0 && TopIndex >= 0 && TopIndex < Items.Count ? ((ListEntry)Items[TopIndex]).Key : null;
            int top = -1, selection = -1;
            BeginUpdate();
            try
            {
                hovered = -1;
                Items.Clear(); RebuildCount++;
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i]; Items.Add(entry);
                    if (entry.Tab != null && entry.Tab.Identity == selected) selection = i;
                    if (entry.Key == topKey) top = i;
                }
                if (selection >= 0) SelectedIndex = selection;
                else MoveSelection(1);
                if (top >= 0) TopIndex = top;
            }
            finally { EndUpdate(); }
        }
        void InvalidateRow(int index)
        {
            if (!IsHandleCreated || index < 0 || index >= Items.Count) return;
            var bounds = Rectangle.Intersect(ClientRectangle, GetItemRectangle(index));
            if (bounds.Width > 0 && bounds.Height > 0) Invalidate(bounds);
        }
        internal void MoveSelection(int delta)
        {
            int index = SelectedIndex;
            if (index < 0) index = delta > 0 ? -1 : Items.Count;
            for (int i = index + delta; i >= 0 && i < Items.Count; i += delta)
                if (((ListEntry)Items[i]).Tab != null) { SelectedIndex = i; return; }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int next = IndexFromPoint(e.Location);
            if (hovered != next) { int previous = hovered; hovered = next; InvalidateRow(previous); InvalidateRow(next); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e)
        { int previous = hovered; hovered = -1; InvalidateRow(previous); base.OnMouseLeave(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int index = IndexFromPoint(e.Location);
            if (e.Button == MouseButtons.Left && index >= 0 && index < Items.Count)
            {
                var row = (ListEntry)Items[index];
                if (row.Tab != null && ActivatedTab != null) ActivatedTab(row.Tab);
            }
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count || TitleFont == null || SmallFont == null) return;
            var row = (ListEntry)Items[e.Index];
            var g = e.Graphics;
            float s = UiScale;
            var b = e.Bounds;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(Color.White)) g.FillRectangle(brush, b);
            if (row.Tab == null)
            {
                UiPaint.Text(g, row.Heading, SmallFont, UiPaint.Muted, new Rectangle(b.X + (int)(10 * s), b.Y, b.Width - (int)(20 * s), b.Height));
                return;
            }
            bool selected = (e.State & DrawItemState.Selected) != 0;
            if (selected || hovered == e.Index)
                UiPaint.FillRound(g, selected ? Color.FromArgb(244, 245, 247) : Color.FromArgb(249, 249, 250),
                    new RectangleF(b.X, b.Y + 3 * s, b.Width - 4 * s, b.Height - 6 * s), 12 * s);
            UiPaint.Globe(g, b.X + 19 * s, b.Y + 36 * s, 7 * s, s);
            string label = row.Tab.MonitorLabel;
            Size badge = TextRenderer.MeasureText(g, label, SmallFont, Size.Empty, TextFormatFlags.NoPadding);
            int badgeWidth = badge.Width + (int)(17 * s);
            int badgeX = b.Right - badgeWidth - (int)(15 * s);
            int badgeY = b.Y + (int)(27 * s);
            UiPaint.FillRound(g, Color.FromArgb(238, 239, 241), new RectangleF(badgeX, badgeY, badgeWidth, 24 * s), 12 * s);
            TextRenderer.DrawText(g, label, SmallFont, new Rectangle(badgeX, badgeY, badgeWidth, (int)(24 * s)), UiPaint.Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            int x = b.X + (int)(40 * s);
            int textWidth = Math.Max(10, badgeX - x - (int)(15 * s));
            UiPaint.Text(g, row.Tab.Title, TitleFont, UiPaint.Ink, new Rectangle(x, b.Y + (int)(10 * s), textWidth, (int)(29 * s)));
            UiPaint.Text(g, row.Tab.Subtitle, SmallFont, UiPaint.Muted, new Rectangle(x, b.Y + (int)(42 * s), textWidth, (int)(23 * s)));
        }
    }
}
