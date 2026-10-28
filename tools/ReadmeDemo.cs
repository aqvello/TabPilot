using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using TabPilot;

// Documentation fixture only. No DesktopContext, browser scan, settings load,
// screen capture, user input injection, network request or tab activation.
internal static class ReadmeDemo
{
    static string output;
    static int frame;
    [STAThread] static int Main(string[] args)
    {
        try
        {
            output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.AddMessageFilter(new DemoInputGuard());
            foreach (string language in new[] { "zh-CN", "en" }) Generate(language);
            Console.WriteLine("Generated native UI assets from fixed fictional data only."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
    }
    sealed class DemoInputGuard : IMessageFilter
    {
        public bool PreFilterMessage(ref Message message)
        {
            // Ignore human keyboard/IME/mouse/paste input in this fixture process.
            // Programmatic control updates below still execute normally.
            int id = message.Msg;
            return (id >= 0x0100 && id <= 0x010F) || (id >= 0x0280 && id <= 0x029F) ||
                (id >= 0x0200 && id <= 0x020F) || (id >= 0x00A0 && id <= 0x00AD) || (id >= 0x0300 && id <= 0x0304);
        }
    }
    static Snapshot Fixture()
    {
        var s = new Snapshot();
        for (int i = 0; i < 3; i++)
            s.Monitors.Add(new MonitorRecord { Handle = "demo-monitor-" + i, Device = "DEMO " + (i + 1), Primary = i == 1, ScalePercent = 100,
                Bounds = new Box { Left = (i - 1) * 1920, Top = 0, Right = i * 1920, Bottom = 1080 } });
        string[][] titles = {
            new[] { "Project Atlas — Overview", "Design system — Components", "Release checklist — v" + AppVersion.Number },
            new[] { "API reference — Getting started", "Design review — Navigation", "Roadmap — Next milestones" },
            new[] { "Team notes — Weekly planning", "Design tokens — Color & type", "Keyboard shortcuts — Quick guide" }
        };
        for (int w = 0; w < 3; w++)
        {
            var window = new WindowRecord { Handle = "0xDE00" + w, Pid = 100 + w, ProcessStartUtc = "fictional-demo", Eligible = true,
                Title = titles[w][0] + " - Google Chrome", MonitorHandle = s.Monitors[w].Handle, Scan = new ScanResult { Status = "Enumerated" } };
            for (int t = 0; t < 3; t++) window.Scan.Tabs.Add(new TabRecord { Id = w * 3 + t + 1, Ordinal = t + 1, RuntimeId = "demo-" + w + "-" + t,
                Title = titles[w][t], Selected = t == 0 });
            s.Windows.Add(window);
        }
        return s;
    }
    static void Generate(string language)
    {
        UiText.SetLanguage(language);
        var snapshot = Fixture(); var settings = new DesktopSettings { Language = language };
        var tabs = DesktopModel.Build(snapshot, settings);
        DesktopModel.Remember(settings, tabs[1]); DesktopModel.Remember(settings, tabs[4]);
        using (var popup = new PopupForm())
        {
            popup.SuppressDeactivate = true;
            popup.HotkeyText = GlobalHotkey.Describe(3, 32);
            popup.PlaceOn(Screen.PrimaryScreen);
            popup.SetData(snapshot, DesktopModel.Build(snapshot, settings));
            popup.SetStatus(UiText.T("已读取可访问标签，完整性未验证 · 详情"), false, true);
            popup.Show(); popup.Refresh(); Application.DoEvents();
            string suffix = language == "en" ? "en" : "zh";
            Capture(popup, "tabs-" + suffix + ".png", "Your tabs. One shortcut away.", "ALL TABS");
            if (language != "en") return;
            frame = 0;
            Hold(popup, "Open your tab switcher", "1 / 4  ·  OPEN", 12);
            var search = popup.Controls.OfType<SearchField>().Single();
            foreach (char c in "design") { search.Input.Text += c; Hold(popup, "Search across every window", "2 / 4  ·  SEARCH", 2); }
            Capture(popup, "search-en.png", "Search across every window", "SEARCH");
            Hold(popup, "Search across every window", "2 / 4  ·  SEARCH", 12);
            search.Input.Clear(); Click(popup, "By monitor");
            popup.Controls.OfType<TabRows>().Single().SelectedIndex = 1;
            popup.Controls.OfType<TabRows>().Single().TopIndex = 0;
            Capture(popup, "monitors-en.png", "See where each tab belongs", "BY MONITOR");
            Hold(popup, "See where each tab belongs", "3 / 4  ·  MONITORS", 17);
            Click(popup, "Recent");
            Hold(popup, "Return to tabs you switched with TabPilot", "4 / 4  ·  RECENT", 15);
            Click(popup, "All tabs");
            popup.Controls.OfType<TabRows>().Single().MoveSelection(1);
            Hold(popup, "Choose a result with the arrow keys", "↑↓ SELECT  ·  ENTER SWITCH", 12);
        }
    }
    static void Click(PopupForm popup, string text) { popup.Controls.OfType<QuietButton>().Single(b => b.Text == text).PerformClick(); }
    static void Hold(PopupForm popup, string title, string step, int copies)
    {
        string file = "frame-" + frame.ToString("D3") + ".png";
        Capture(popup, file, title, step);
        for (int i = 1; i < copies; i++) File.Copy(Path.Combine(output, file), Path.Combine(output, "frame-" + (frame + i).ToString("D3") + ".png"), true);
        frame += copies;
    }
    static void Capture(PopupForm popup, string filename, string title, string step)
    {
        popup.Refresh(); Application.DoEvents(); Thread.Sleep(25);
        using (var native = new Bitmap(popup.ClientSize.Width, popup.ClientSize.Height, PixelFormat.Format32bppArgb))
        using (var canvas = new Bitmap(1120, 874, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(canvas))
        using (var titleFont = new Font("Segoe UI", 23, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var metaFont = new Font("Segoe UI", 12, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var noteFont = new Font("Segoe UI", 13, FontStyle.Regular, GraphicsUnit.Pixel))
        {
            popup.DrawToBitmap(native, popup.ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            using (var gradient = new LinearGradientBrush(new Rectangle(0, 0, 1120, 874), Color.FromArgb(232, 239, 253), Color.FromArgb(245, 247, 251), 70f)) g.FillRectangle(gradient, 0, 0, 1120, 874);
            using (var ink = new SolidBrush(Color.FromArgb(26, 41, 67))) g.DrawString(title, titleFont, ink, 110, 28);
            using (var muted = new SolidBrush(Color.FromArgb(85, 104, 135)))
            {
                g.DrawString("TABPILOT  /  " + step, metaFont, muted, 110, 65);
                g.DrawString("NATIVE WINDOWS UI  ·  FICTIONAL DEMO DATA  ·  NO PERSONAL BROWSER CONTENT", noteFont, muted, 110, 813);
            }
            for (int i = 14; i >= 1; i--) UiPaint.FillRound(g, Color.FromArgb(3, 26, 41, 67), new RectangleF(110 - i, 124 - i / 2f, 900 + i * 2, 660 + i), 20 + i);
            var saved = g.Save();
            using (var rounded = UiPaint.Rounded(new RectangleF(110, 114, 900, 660), 18))
            { g.SetClip(rounded); g.DrawImage(native, new Rectangle(110, 114, 900, 660)); }
            g.Restore(saved);
            canvas.Save(Path.Combine(output, filename), ImageFormat.Png);
        }
    }
}
