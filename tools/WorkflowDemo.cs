using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;
using TabPilot;

// Actual production tab selection, against a fresh isolated Chrome profile only.
// Browser media uses PrintWindow + a verified document crop. Never desktop capture.
internal static class WorkflowDemo
{
    const string Prefix = "TabPilot Demo - ";
    static readonly string[] Names = { "Atlas Overview", "Design System", "API Reference", "Team Notes" };
    static readonly Color[] Accents = { Color.FromArgb(37,99,235), Color.FromArgb(124,58,237), Color.FromArgb(13,148,136), Color.FromArgb(217,119,6) };
    static readonly List<IntPtr> Created = new List<IntPtr>();
    static string output;
    static int frame;
    static IntPtr originalForeground;
    static DemoBrowser browser;
    static string chrome;
    static WindowRecord[] unrelatedWindows;
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint Type; public INPUTUNION Data; }
    [StructLayout(LayoutKind.Explicit)] struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT Keyboard; [FieldOffset(0)] public MOUSEINPUT Mouse; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort Vk, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int X,Y; public uint Data,Flags,Time; public UIntPtr Extra; }
    [DllImport("user32.dll", SetLastError=true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [STAThread] static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        originalForeground = Native.GetForegroundWindow();
        int status = 1;
        try
        {
            if(args.Length<1 || args.Length>3) throw new ArgumentException("Usage: WorkflowDemo OUTPUT [en|zh-CN] [CHROME_EXE]");
            output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
            // An old successful proof must never survive a failed attempt in the same directory.
            foreach(var name in new[]{"verification.json","cleanup.json"}) File.Delete(Path.Combine(output,name));
            string language=args.Length>1?args[1]:"en";
            if(language!="en" && language!="zh-CN") throw new ArgumentException("Demo language must be en or zh-CN.");
            chrome=DemoBrowser.FindExecutable(args.Length>2?args[2]:null);
            WorkflowScene.Language=language;
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); UiText.SetLanguage(language);
            Application.AddMessageFilter(new FixtureInputGuard());
            unrelatedWindows=Native.Windows().Where(w=>w.Eligible).ToArray();
            Run(); status = 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e.GetType().Name + ": " + e.Message); }
        finally
        {
            WorkflowScene.Dispose();
            bool exited=false, deleted=false;
            try
            {
                if(browser!=null) { browser.Dispose(); exited=browser.Exited; deleted=browser.ProfileDeleted; }
                bool unchanged=unrelatedWindows!=null && unrelatedWindows.All(w=>Native.SameWindow(w) && Native.Title(Native.Hwnd(w.Handle))==w.Title);
                if(!exited || !deleted || !unchanged || Created.Any(Native.IsWindow)) status=1;
                if(output!=null) Program.Save(Path.Combine(output,"cleanup.json"), new {
                    DemoWindowsClosed=Created.All(h=>!Native.IsWindow(h)), IsolatedBrowserExited=exited,
                    TemporaryProfileDeleted=deleted, ExistingBrowserWindowsUnchanged=unchanged });
            }
            catch(Exception e) { status=1; Console.Error.WriteLine("Demo cleanup failed: "+e.Message); }
            if (Native.IsWindow(originalForeground)) Native.SetForegroundWindow(originalForeground);
        }
        if(status==0) Console.WriteLine("PASS isolated-profile tab selection, verified page change, two-second highlight and profile cleanup.");
        return status;
    }
    sealed class FixtureInputGuard : IMessageFilter
    {
        public bool PreFilterMessage(ref Message message)
        {
            // Only the recorder process is filtered; Chrome setup input is separately scoped.
            int id=message.Msg;
            return (id>=0x0100&&id<=0x010F)||(id>=0x0280&&id<=0x029F)||
                (id>=0x0200&&id<=0x020F)||(id>=0x0300&&id<=0x0304);
        }
    }
    static void Run()
    {
        var monitors=Native.Monitors().OrderBy(m=>m.Bounds.Left).ToArray();
        if(monitors.Length!=3 || !monitors[1].Primary) throw new InvalidOperationException("This recorder requires three actual displays with the primary display in the middle; no simulated test fallback.");
        for (int i=0;i<4;i++) File.WriteAllText(Path.Combine(output,"page-"+i+".html"), Page(i), new UTF8Encoding(false));
        browser=new DemoBrowser(output,chrome,new Uri(Path.Combine(output,"page-0.html")).AbsoluteUri);
        WindowRecord first=null;
        var startup=Stopwatch.StartNew();
        while(startup.ElapsedMilliseconds<15000)
        {
            var windows=browser.Windows();
            if(windows.Length==1 && windows[0].Title.StartsWith(Prefix+Names[0],StringComparison.Ordinal)) { first=windows[0]; break; }
            Pump(80);
        }
        if(first==null) throw new InvalidOperationException("Isolated demo Chrome did not expose the expected local page; existing Chrome will not be used.");
        IntPtr a=Native.Hwnd(first.Handle); Created.Add(a); Place(a,monitors[1]); Focus(a); Pump(350);
        using(var page=DocumentCapture(a,0)) WorkflowScene.SetPage(1,page);
        IntPtr b = NewWindow(a,monitors[2]);
        Navigate(b,1,false); Navigate(b,2,true); // Design is unselected in a different window.
        using(var page=DocumentCapture(b,2)) WorkflowScene.SetPage(2,page);
        IntPtr notes = NewWindow(b,monitors[0]);
        Navigate(notes,3,false);
        using(var page=DocumentCapture(notes,3)) WorkflowScene.SetPage(0,page);
        Focus(a);
        var snapshot = new Snapshot { Monitors = Native.Monitors() };
        snapshot.Windows.Add(Record(a, "Atlas Overview")); snapshot.Windows.Add(Record(b, "API Reference")); snapshot.Windows.Add(Record(notes,"Team Notes"));
        if(snapshot.Windows.Select(w=>w.MonitorHandle).Distinct().Count()!=3) throw new InvalidOperationException("Fixture windows are not on three distinct physical displays.");
        Native.RECT targetBefore; Native.GetWindowRect(b,out targetBefore);
        int id=1;
        foreach(var window in snapshot.Windows)
        {
            window.Scan = BrowserTree.Read(Native.Hwnd(window.Handle)).Result;
            if (window.Scan.Status!="Enumerated" || !window.Scan.TraversalComplete || window.Scan.Tabs.Any(t=>!t.Title.StartsWith(Prefix,StringComparison.Ordinal)) || window.Scan.Groups.Count!=0)
                throw new InvalidOperationException("Fixture inventory is not exclusively known demo tabs; capture stopped.");
            foreach(var tab in window.Scan.Tabs) tab.Id=id++;
        }
        if(snapshot.Windows.Sum(w=>w.Scan.Tabs.Count)!=4) throw new InvalidOperationException("Expected exactly four fixture tabs.");
        var target=snapshot.Windows[1].Scan.Tabs.Single(t=>t.Title.StartsWith(Prefix+Names[1],StringComparison.Ordinal));
        if(target.Selected) throw new InvalidOperationException("Design fixture must initially be a background tab.");
        using(var before=DocumentCapture(a,0))
        using(var popup=new PopupForm())
        {
            popup.SuppressDeactivate=true; popup.HotkeyText="Ctrl + Alt + Space";
            popup.PlaceOn(Screen.PrimaryScreen); popup.SetData(snapshot,DesktopModel.Build(snapshot,new DesktopSettings()));
            popup.SetStatus(WorkflowScene.Localize("Demo windows only · Choose Design System and press Enter"),false);
            Hold(before,null,"Start in a different Chrome window","1 / 4 · WINDOW A / OVERVIEW",15,null);
            Frame(before,null,"before.png","Before: working in window A","WINDOW A / ATLAS OVERVIEW",null);
            popup.Show(); popup.Activate(); Native.SetForegroundWindow(popup.Handle); popup.FocusSearch(); Pump(150);
            Hold(before,popup,"Find the page without hunting through windows","2 / 4 · OPEN TABPILOT",12,null);
            var search=popup.Controls.OfType<SearchField>().Single();
            foreach(char c in "design") { search.Input.Text+=c; Hold(before,popup,"Search for the page you need","2 / 4 · SEARCH: DESIGN",2,null); }
            if(popup.DisplayedTabs.Count!=1 || popup.SelectedId!=target.Id) throw new InvalidOperationException("Exact demo target not selected in popup.");
            Hold(before,popup,"Press Enter on Design System","3 / 4 · ENTER → SWITCH WINDOW + TAB",14,null);
            Task<SelectionResult> selection=null;
            popup.ActivateRequested += delegate(DesktopTab tab)
            {
                if(tab.Tab.Id!=target.Id) throw new InvalidOperationException("Unexpected activation target.");
                popup.Hide(); selection=Task.Run(delegate { return Program.Select(snapshot,tab.Tab.Id); });
            };
            var message=Message.Create(popup.Handle,0x0100,new IntPtr(13),IntPtr.Zero);
            typeof(PopupForm).GetMethod("ProcessCmdKey",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(popup,new object[]{message,Keys.Enter});
            if(selection==null) throw new InvalidOperationException("Enter did not trigger the production selection path.");
            var timer=Stopwatch.StartNew();
            List<IntPtr> edges=null;
            while(timer.ElapsedMilliseconds<12000)
            {
                Pump(20); edges=FindEdges(b);
                if(edges.Count==4 && Native.GetForegroundWindow()==b && Native.Title(b).StartsWith(Prefix+Names[1],StringComparison.Ordinal)) break;
                if(selection.IsCompleted) break;
            }
            if(edges==null || edges.Count!=4 || Native.GetForegroundWindow()!=b) throw new InvalidOperationException("Actual target-window highlight not observed.");
            using(var after=DocumentCapture(b,1))
            {
                WorkflowScene.Selected=true;
                var strips=edges.Select(Print).ToArray();
                try
                {
                    int highlightFrames=0;
                    while(!selection.IsCompleted && edges.All(Native.IsWindowVisible))
                    {
                        if(Native.GetForegroundWindow()!=b) throw new InvalidOperationException("Demo target lost foreground while recording highlight.");
                        WorkflowScene.FocusProgress=Math.Min(1,highlightFrames/4.0);
                        Frame(after,null,"frame-"+(frame++).ToString("D3")+".png","Found on the right screen. Brought into focus.","4 / 4 · SCREEN 3 / DESIGN SYSTEM · HIGHLIGHTED",strips);
                        highlightFrames++; Pump(70);
                    }
                    while(!selection.IsCompleted && timer.ElapsedMilliseconds<22000) Pump(20);
                    if(!selection.IsCompleted) throw new TimeoutException("Production selection did not finish.");
                    var result=selection.Result;
                    if(result.Status!="Passed" || !result.SelectionVerified || !result.ForegroundVerified || !result.HighlightBoundsVerified || result.HighlightElapsedMs<2000 || result.WasSelected)
                        throw new InvalidOperationException("Actual selection/highlight verification failed: "+result.FailureCode);
                    if(highlightFrames<2) throw new InvalidOperationException("Not enough native highlight frames recorded.");
                    Program.Save(Path.Combine(output,"selection-private.json"),result);
                    var check=BrowserTree.Read(b);
                    if(!check.Result.TraversalComplete || !check.Tabs.Any(t=>t.Record.RuntimeId==target.RuntimeId && t.Record.Selected)) throw new InvalidOperationException("Selected page identity did not remain correct.");
                    Native.RECT targetAfter; Native.GetWindowRect(b,out targetAfter);
                    bool stayed=Native.Handle(Native.MonitorFromWindow(b,2))==monitors[2].Handle &&
                        targetBefore.Left==targetAfter.Left && targetBefore.Top==targetAfter.Top && targetBefore.Right==targetAfter.Right && targetBefore.Bottom==targetAfter.Bottom;
                    if(!stayed) throw new InvalidOperationException("Target window unexpectedly moved; focus zoom must be presentation only.");
                    WorkflowScene.FocusProgress=1;
                    Frame(after,null,"after.png","After: the requested tab is active","WINDOW B / DESIGN SYSTEM · ACTUAL RESULT",strips);
                    Hold(after,null,"You're back on the page you wanted","4 / 4 · SWITCHED WINDOW + TAB",24,null);
                    Program.Save(Path.Combine(output,"verification.json"),new { Version=AppVersion.Informational,Language=WorkflowScene.Language,IsolatedBrowserProfile=true,CurrentShortcutHintVerified=true,FixtureWindows=3,FixtureTabs=4,PhysicalMonitors=3,FixtureMonitorsVerified=true,
                        SourceScreen="Center",TargetScreen="Right",CrossMonitorSelection=true,TargetStayedOnOriginalScreen=stayed,TargetBoundsUnchanged=stayed,FocusZoom="Editorial only; no window movement",TargetInitiallySelected=false,
                        ChangedWindow=a!=b,TargetPage=Names[1],Selection=result.Status,result.SelectionVerified,result.ForegroundVerified,
                        result.HighlightBoundsVerified,result.HighlightElapsedMs,NativeHighlightEdgesObserved=edges.Count,RecordedHighlightFrames=highlightFrames,
                        Capture="PrintWindow document crops + native TabPilot controls + captured highlight strips; illustrated three-monitor overview and focus zoom",DesktopCapture=false,PersonalPagesCaptured=false,Frames=frame });
                    Console.WriteLine("Recorded actual highlight frames: "+highlightFrames+"; duration ms: "+result.HighlightElapsedMs);
                }
                finally { foreach(var strip in strips) strip.Dispose(); }
            }
        }
    }
    static IntPtr NewWindow(IntPtr from,MonitorRecord monitor)
    {
        if(!browser.Owns(from)) throw new InvalidOperationException("Demo window belongs to another browser process.");
        var old=browser.Windows().Select(w=>w.Handle).ToHashSetCompat(); Focus(from); Chord(from,0x11,0x4E);
        WindowRecord found=null;
        var watch=Stopwatch.StartNew();
        while(watch.ElapsedMilliseconds<6000)
        {
            found=browser.Windows().SingleOrDefault(w=>!old.Contains(w.Handle)); if(found!=null) break; Pump(80);
        }
        if(found==null) throw new InvalidOperationException("Dedicated Chrome demo window was not created.");
        var h=Native.Hwnd(found.Handle); Created.Add(h); Place(h,monitor); return h;
    }
    static void Place(IntPtr h,MonitorRecord monitor)
    {
        if(!browser.Owns(h)) throw new InvalidOperationException("Refusing to move a browser outside the isolated demo process.");
        var work=Rectangle.FromLTRB(monitor.WorkArea.Left,monitor.WorkArea.Top,monitor.WorkArea.Right,monitor.WorkArea.Bottom);
        Native.SetWindowPos(h,IntPtr.Zero,work.Left+80,work.Top+80,Math.Min(1350,work.Width-160),Math.Min(1000,work.Height-160),0x0014);
        Pump(250);
        if(Native.Handle(Native.MonitorFromWindow(h,2))!=monitor.Handle) throw new InvalidOperationException("Dedicated window did not reach its required display.");
    }
    static HashSet<T> ToHashSetCompat<T>(this IEnumerable<T> values) { return new HashSet<T>(values); }
    static void Navigate(IntPtr h,int page,bool newTab)
    {
        Focus(h); if(newTab) Chord(h,0x11,0x54); Chord(h,0x11,0x4C);
        string url=new Uri(Path.Combine(output,"page-"+page+".html")).AbsoluteUri;
        var inputs=new List<INPUT>();
        foreach(char c in url) { inputs.Add(Key(0,c,4)); inputs.Add(Key(0,c,6)); }
        Send(h,inputs.ToArray()); Chord(h,13);
        if(!Switcher.Wait(delegate{return Native.Title(h).StartsWith(Prefix+Names[page],StringComparison.Ordinal);},6000)) throw new InvalidOperationException("Local demo page did not load.");
        Pump(350);
    }
    static WindowRecord Record(IntPtr h,string title)
    {
        uint pid; Native.GetWindowThreadProcessId(h,out pid);
        using(var process=Process.GetProcessById((int)pid)) return new WindowRecord { Handle=Native.Handle(h),Pid=(int)pid,ProcessStartUtc=process.StartTime.ToUniversalTime().ToString("o"),
            Eligible=true,Visible=true,Title=Prefix+title,MonitorHandle=Native.Handle(Native.MonitorFromWindow(h,2)),Dpi=Native.GetDpiForWindow(h) };
    }
    static Bitmap Print(IntPtr h)
    {
        Native.RECT r; if(!Native.GetWindowRect(h,out r)) throw new InvalidOperationException("Window capture bounds unavailable.");
        var bitmap=new Bitmap(r.Right-r.Left,r.Bottom-r.Top,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(bitmap))
        {
            IntPtr dc=g.GetHdc(); try { if(!PrintWindow(h,dc,2)) throw new InvalidOperationException("Window-owned rendering failed; no desktop fallback."); } finally { g.ReleaseHdc(dc); }
        }
        return bitmap;
    }
    static Bitmap DocumentCapture(IntPtr h,int page)
    {
        if(browser==null || !browser.Owns(h) || !Created.Contains(h) || Native.GetForegroundWindow()!=h || !Native.Title(h).StartsWith(Prefix+Names[page],StringComparison.Ordinal)) throw new InvalidOperationException("Demo document capture identity guard failed.");
        AutomationElement[] docs=new AutomationElement[0];
        var provider=Stopwatch.StartNew();
        do
        {
            docs=AutomationElement.FromHandle(h).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document))
                .Cast<AutomationElement>().Where(d=>!d.Current.IsOffscreen&&d.Current.Name==Prefix+Names[page]).ToArray();
            if(docs.Length==1) break;
            Pump(100); // A fresh Chrome process may expose its renderer tree asynchronously.
        } while(provider.ElapsedMilliseconds<5000);
        if(docs.Length!=1) throw new InvalidOperationException("Unique known demo document unavailable (matching documents: "+docs.Length+"); capture stopped.");
        var box=docs[0].Current.BoundingRectangle;
        Native.RECT r; Native.GetWindowRect(h,out r);
        var crop=Rectangle.FromLTRB((int)Math.Ceiling(box.Left)-r.Left+2,(int)Math.Ceiling(box.Top)-r.Top+2,(int)Math.Floor(box.Right)-r.Left-2,(int)Math.Floor(box.Bottom)-r.Top-2);
        if(crop.Width<400 || crop.Height<300 || crop.X<0 || crop.Y<40 || crop.Right>r.Right-r.Left || crop.Bottom>r.Bottom-r.Top) throw new InvalidOperationException("Demo viewport crop invalid.");
        using(var full=Print(h))
        {
            var image=full.Clone(crop,PixelFormat.Format32bppArgb);
            int matches=0;
            for(int y=0;y<image.Height;y+=12) for(int x=0;x<image.Width;x+=12)
            { var c=image.GetPixel(x,y); var wanted=Accents[page]; if(Math.Abs(c.R-wanted.R)<12&&Math.Abs(c.G-wanted.G)<12&&Math.Abs(c.B-wanted.B)<12) matches++; }
            if(matches<20) { image.Dispose(); throw new InvalidOperationException("Demo document rendering is blank or unexpected; capture stopped."); }
            return image;
        }
    }
    static List<IntPtr> FindEdges(IntPtr target)
    {
        var frame=Native.VisibleFrame(target); var result=new List<IntPtr>();
        Native.EnumWindows(delegate(IntPtr h,IntPtr unused)
        {
            if(!Native.IsWindowVisible(h)||(GetWindowLongPtr(h,-20).ToInt64()&0x08080000)!=0x08080000) return true;
            Native.RECT r; Native.GetWindowRect(h,out r);
            if(r.Left<frame.Left||r.Top<frame.Top||r.Right>frame.Right||r.Bottom>frame.Bottom||(r.Right-r.Left>20&&r.Bottom-r.Top>20)) return true;
            uint pid; Native.GetWindowThreadProcessId(h,out pid);
            try { using(var p=Process.GetProcessById((int)pid)) if(String.Equals(p.MainModule.FileName,Program.Exe,StringComparison.OrdinalIgnoreCase)) result.Add(h); }
            catch(ArgumentException) { } catch(System.ComponentModel.Win32Exception) { }
            return true;
        },IntPtr.Zero);
        return result.OrderBy(h=> { Native.RECT r;Native.GetWindowRect(h,out r);return (r.Right-r.Left>20?0:1)*100000+r.Top+r.Left; }).ToList();
    }
    static void Hold(Bitmap page,PopupForm popup,string title,string step,int copies,Bitmap[] edges)
    {
        string name="frame-"+frame.ToString("D3")+".png"; Frame(page,popup,name,title,step,edges);
        for(int i=1;i<copies;i++) File.Copy(Path.Combine(output,name),Path.Combine(output,"frame-"+(frame+i).ToString("D3")+".png"),true);
        frame+=copies;
    }
    static void Frame(Bitmap page,PopupForm popup,string name,string title,string step,Bitmap[] edges)
    {
        if(popup!=null)
        {
            var hint=popup.Controls.OfType<Label>().Single(l=>l.Text==popup.HotkeyHintText);
            var rows=popup.Controls.OfType<TabRows>().Single();
            if(!popup.HotkeyHintText.Contains(popup.HotkeyText) || !hint.Visible || hint.Top<rows.Bottom || hint.Bottom>popup.ClientSize.Height)
                throw new InvalidOperationException("Current shortcut hint is missing, obscured or overlaps the tab list.");
        }
        using(var canvas=WorkflowScene.Render(page,popup,edges)) canvas.Save(Path.Combine(output,name),ImageFormat.Png);
    }
    static string L(string text) { return WorkflowScene.Localize(text); }
    static string Page(int page)
    {
        string accent=ColorTranslator.ToHtml(Accents[page]);
        string title=Names[page];
        string heading=page==0?L("The design page is in another window."):page==1?"Design System":page==2?"API Reference":"Team Notes";
        string subtitle=page==0?L("You're working in window A. Design System is an unselected tab in window B."):page==1?L("This is the exact tab you selected in TabPilot."):page==2?L("Window B starts here. The Design System tab is open in the background."):L("A separate workspace on the left display. Keep your windows where they belong.");
        return "<!doctype html><html><head><meta charset='utf-8'><title>"+Prefix+title+"</title><style>"+
            "*{box-sizing:border-box}body{margin:0;background:#f4f7fc;color:#182b49;font-family:Segoe UI,Microsoft YaHei UI,Arial,sans-serif;color-scheme:light}header{background:"+accent+";color:white;padding:20px 38px;display:flex;justify-content:space-between;align-items:center}.brand{font-weight:800;letter-spacing:2px}.pill{font-size:12px;border:1px solid #ffffff66;border-radius:20px;padding:7px 13px}main{width:100%;max-width:none;margin:4vh auto;padding:0 6vw}.eyebrow{font-size:12px;font-weight:700;letter-spacing:2px;color:"+accent+"}h1{font-size:3.6vw;line-height:1.15;max-width:none;margin:18px 0}p{color:#5c6e89;font-size:1.65vw;line-height:1.5}.cards{display:flex;gap:18px;margin-top:35px}.card{flex:1;background:white;border:1px solid #dfe7f2;border-radius:16px;padding:2vw}.label{font-size:12px;letter-spacing:1px;color:#70819c}.number{font-size:2.7vw;font-weight:700;margin:14px 0;color:"+accent+"}.swatches{display:flex;gap:7px;margin-top:15px}.swatches i{width:32px;height:32px;border-radius:9px;background:"+accent+"}footer{font-size:1.1vw;color:#7b8da6;margin-top:34px}</style></head><body><header><span class='brand'>ATLAS / "+title.ToUpperInvariant()+"</span><span class='pill'>"+L("FICTIONAL DEMO DATA")+"</span></header><main><div class='eyebrow'>"+(page==0?L("BEFORE · WINDOW A"):page==1?L("TARGET PAGE · WINDOW B"):page==2?L("BACKGROUND · WINDOW B"):L("WINDOW C · LEFT SCREEN"))+"</div><h1>"+heading+"</h1><p>"+subtitle+"</p><div class='cards'><section class='card'><div class='label'>"+(page==1?L("COMPONENTS"):L("WORKSPACE"))+"</div><div class='number'>"+(page==1?L("24 ready"):L("Project Atlas"))+"</div><p>"+(page==1?L("Buttons, cards and navigation"):L("Local pages created for this demonstration"))+"</p></section><section class='card'><div class='label'>"+(page==1?L("COLOR TOKENS"):L("TABPILOT WORKFLOW"))+"</div><div class='number'>"+(page==1?L("Violet / 600"):L("Search → Enter"))+"</div><div class='swatches'><i></i><i style='opacity:.6'></i><i style='opacity:.3'></i></div></section></div><footer>"+L("Demo content only · No accounts, personal webpages or external resources")+"</footer></main></body></html>";
    }
    static void Focus(IntPtr h)
    {
        if(browser==null || !browser.Owns(h)) throw new InvalidOperationException("Refusing to focus a window outside the isolated demo browser.");
        Native.SetForegroundWindow(h);if(Native.GetForegroundWindow()!=h) { try{AutomationElement.FromHandle(h).SetFocus();}catch(InvalidOperationException){} }
        if(!Switcher.Wait(delegate{return Native.GetForegroundWindow()==h;},1500)) throw new InvalidOperationException("Could not focus demo window; no input sent.");
    }
    static INPUT Key(ushort vk,ushort scan,uint flags) { return new INPUT { Type=1,Data=new INPUTUNION { Keyboard=new KEYBDINPUT { Vk=vk,Scan=scan,Flags=flags } } }; }
    static void Chord(IntPtr h,params ushort[] keys) { Send(h,keys.Select(k=>Key(k,0,0)).Concat(keys.Reverse().Select(k=>Key(k,0,2))).ToArray());Pump(100); }
    static void Send(IntPtr h,params INPUT[] inputs) { if(browser==null || !browser.Owns(h) || Native.GetForegroundWindow()!=h) throw new InvalidOperationException("Demo input ownership lost.");if(SendInput((uint)inputs.Length,inputs,Marshal.SizeOf(typeof(INPUT)))!=inputs.Length) throw new InvalidOperationException("Demo input incomplete."); }
    static void Pump(int milliseconds) { var w=Stopwatch.StartNew();do {Application.DoEvents();Thread.Sleep(10);}while(w.ElapsedMilliseconds<milliseconds); }
}
