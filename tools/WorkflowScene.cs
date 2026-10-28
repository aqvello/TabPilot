using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using TabPilot;

// Editorial framing, never a desktop capture. The pages and popup are native
// captures from the dedicated three-display fixture. Zoom does not move a window.
internal static class WorkflowScene
{
    static readonly Bitmap[] Pages=new Bitmap[3];
    static readonly Color Ink=Color.FromArgb(225,233,249), Muted=Color.FromArgb(154,172,202);
    static readonly Color Violet=Color.FromArgb(154,111,255), Orange=Color.FromArgb(255,155,55);
    static readonly RectangleF Hero=new RectangleF(250,247,940,570);
    internal static bool Selected;
    internal static double FocusProgress;
    internal static string Language="en";
    static readonly Dictionary<string,string> Chinese=new Dictionary<string,string>
    {
        {"Your tabs. Across every screen.","所有屏幕的标签，一搜即达。"},
        {"NO CHROME EXTENSION","无需 Chrome 扩展"},
        {"There it is. The right tab, brought forward.","找到了！目标标签已切到前台。"},
        {"Search once. Find it on any screen.","搜一次，找到任意屏幕上的标签。"},
        {"Three screens. Where did that tab go?","三台显示器，目标标签在哪儿？"},
        {"Screen 3 activates Design System. Your other windows stay in place.","右屏激活 Design System，其他窗口保持原位。"},
        {"Design System is a background tab in the Chrome window on the right.","Design System 藏在右屏 Chrome 窗口的后台标签中。"},
        {"01  LEFT SCREEN","01  左侧屏幕"}, {"02  CENTER SCREEN","02  中间屏幕"}, {"03  RIGHT SCREEN","03  右侧屏幕"},
        {"WINDOW C / TEAM NOTES","窗口 C / Team Notes"}, {"WINDOW A / OVERVIEW","窗口 A / Overview"},
        {"WINDOW B / DESIGN SYSTEM","窗口 B / Design System"}, {"WINDOW B / API REFERENCE","窗口 B / API Reference"},
        {"Ctrl  +  Alt  +  Space","Ctrl  +  Alt  +  空格"},
        {"Open TabPilot. Type a title. Press Enter.","呼出 TabPilot，输入标题，按 Enter 切换。"},
        {"No hunting through windows or crowded tab bars.","不用逐个翻找窗口和拥挤的标签栏。"},
        {"Design System is here, but not selected","Design System 在这里，尚未选中"},
        {"ENTER  →  DESIGN SYSTEM ON THE RIGHT SCREEN","Enter  →  切到右屏的 Design System"},
        {"TYPE A TITLE TO SEARCH ACROSS SCREENS","输入标题，跨屏搜索标签"},
        {"SCREEN 3  →  WINDOW B  →  DESIGN","右屏  →  窗口 B  →  Design"},
        {"Selected. Activated. Highlighted for 2 seconds.","标签已选中，窗口已激活，高亮持续 2 秒。"},
        {"Your selected page is ready.","你选择的页面，已经准备好了。"},
        {"Focus zoom for clarity · The window remains on the right display","放大用于演示强调 · 实际窗口仍留在右屏"},
        {"REAL TAB SELECTION / FICTIONAL LOCAL PAGES","真实标签切换 / 专用虚构本地页面"},
        {"Illustrated monitor overview + focus zoom · Browser toolbars and desktop omitted","三屏示意与放大效果 · 已隐藏浏览器工具栏和桌面"},
        {"Demo windows only · Choose Design System and press Enter","仅演示窗口 · 选择 Design System 并按 Enter"},
        {"The design page is in another window.","设计页面在另一个窗口里。"},
        {"You're working in window A. Design System is an unselected tab in window B.","你正在窗口 A 中工作。Design System 是窗口 B 中尚未选中的后台标签。"},
        {"This is the exact tab you selected in TabPilot.","这就是你刚刚在 TabPilot 中选中的标签。"},
        {"Window B starts here. The Design System tab is open in the background.","窗口 B 当前显示此页，Design System 标签已在后台打开。"},
        {"A separate workspace on the left display. Keep your windows where they belong.","左屏上的独立工作区，各个窗口留在原来的位置。"},
        {"FICTIONAL DEMO DATA","虚构演示数据"}, {"BEFORE · WINDOW A","切换前 · 窗口 A"},
        {"TARGET PAGE · WINDOW B","目标页面 · 窗口 B"}, {"BACKGROUND · WINDOW B","后台窗口 · 窗口 B"},
        {"WINDOW C · LEFT SCREEN","窗口 C · 左侧屏幕"}, {"COMPONENTS","组件"}, {"WORKSPACE","工作区"},
        {"24 ready","24 个可用组件"}, {"Project Atlas","Atlas 项目"},
        {"Buttons, cards and navigation","按钮、卡片与导航"},
        {"Local pages created for this demonstration","专为此次演示创建的本地页面"},
        {"COLOR TOKENS","颜色变量"}, {"TABPILOT WORKFLOW","TABPILOT 操作流程"},
        {"Violet / 600","紫色 / 600"}, {"Search → Enter","搜索 → Enter"},
        {"Demo content only · No accounts, personal webpages or external resources","仅演示内容 · 不含账号、个人网页或外部资源"}
    };
    internal static string Localize(string text)
    {
        if(Language!="zh-CN") return text;
        string translated;
        if(Chinese.TryGetValue(text,out translated)) return translated;
        if(new[]{"TabPilot","Team Notes","Atlas Overview","Design System","API Reference"}.Contains(text)) return text;
        throw new InvalidOperationException("Untranslated workflow caption: "+text);
    }
    static string FontFamily { get { return Language=="zh-CN"?"Microsoft YaHei UI":"Segoe UI"; } }
    internal static void SetPage(int screen,Bitmap page) { if(Pages[screen]!=null) Pages[screen].Dispose(); Pages[screen]=(Bitmap)page.Clone(); }
    internal static void Dispose() { foreach(var page in Pages) if(page!=null) page.Dispose(); }
    static void Text(Graphics g,string text,float size,Color color,float x,float y,bool bold)
    {
        text=Localize(text);
        using(var font=new Font(FontFamily,size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))
        using(var brush=new SolidBrush(color)) g.DrawString(text,font,brush,x,y);
    }
    static void Center(Graphics g,string text,float size,Color color,float y,bool bold)
    {
        text=Localize(text);
        using(var font=new Font(FontFamily,size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))
        using(var brush=new SolidBrush(color))
        using(var format=new StringFormat { Alignment=StringAlignment.Center })
            g.DrawString(text,font,brush,new RectangleF(20,y,1400,size*2),format);
    }
    static void Round(Graphics g,Color color,RectangleF box,float radius) { UiPaint.FillRound(g,color,box,radius); }
    static RectangleF Window(int index) { return new RectangleF(63+index*450,204,414,246); }
    internal static Bitmap Render(Bitmap page,PopupForm popup,Bitmap[] edges)
    {
        var canvas=new Bitmap(1440,960);
        using(var g=Graphics.FromImage(canvas))
        {
            g.SmoothingMode=SmoothingMode.AntiAlias; g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            using(var gradient=new LinearGradientBrush(new Rectangle(0,0,1440,960),Color.FromArgb(13,21,39),Color.FromArgb(26,35,60),90)) g.FillRectangle(gradient,0,0,1440,960);
            Text(g,"TabPilot",32,Color.White,48,24,true);
            Text(g,"Your tabs. Across every screen.",20,Muted,216,35,false);
            Round(g,Color.FromArgb(40,55,84),new RectangleF(1134,30,258,34),17);
            Text(g,"NO CHROME EXTENSION",13,Ink,1154,38,true);
            string headline=Selected?"There it is. The right tab, brought forward.":popup!=null?"Search once. Find it on any screen.":"Three screens. Where did that tab go?";
            Center(g,headline,32,Color.White,92,true);
            Center(g,Selected?"Screen 3 activates Design System. Your other windows stay in place.":"Design System is a background tab in the Chrome window on the right.",18,Muted,140,false);

            for(int i=0;i<3;i++)
            {
                int x=48+i*450;
                Round(g,Color.FromArgb(3,8,18),new RectangleF(x+5,198,444,282),15);
                Round(g,Color.FromArgb(58,72,98),new RectangleF(x,190,444,282),14);
                Round(g,Color.FromArgb(26,46,76),new RectangleF(x+7,197,430,266),9);
                // Two small, neutral desktop tiles suggest independent workspaces.
                Round(g,Color.FromArgb(38,59,89),new RectangleF(x+17,212,28,28),6);
                Round(g,Color.FromArgb(38,59,89),new RectangleF(x+17,247,28,28),6);
                Round(g,Color.FromArgb(70,84,109),new RectangleF(x+208,472,28,28),3);
                Round(g,Color.FromArgb(70,84,109),new RectangleF(x+149,498,146,9),4);
                string label=i==0?"01  LEFT SCREEN":i==1?"02  CENTER SCREEN":"03  RIGHT SCREEN";
                Text(g,label,15,i==2?Violet:Muted,x+12,529,true);
                string state=i==0?"WINDOW C / TEAM NOTES":i==1?"WINDOW A / OVERVIEW":Selected?"WINDOW B / DESIGN SYSTEM":"WINDOW B / API REFERENCE";
                Text(g,state,12,Muted,x+12,555,false);
                Browser(g,Window(i),i==2&&Selected?page:Pages[i],i,i==2&&Selected,i==2&&Selected?edges:null);
            }
            using(var line=new Pen(Color.FromArgb(50,67,94),1)) g.DrawLine(line,48,590,1392,590);
            if(!Selected && popup==null)
            {
                Round(g,Color.FromArgb(40,55,84),new RectangleF(480,644,480,68),18);
                Center(g,"Ctrl  +  Alt  +  Space",28,Color.White,658,true);
                Center(g,"Open TabPilot. Type a title. Press Enter.",25,Ink,754,true);
                Center(g,"No hunting through windows or crowded tab bars.",18,Muted,796,false);
                Round(g,Color.FromArgb(57,42,89),new RectangleF(978,594,366,35),12);
                Text(g,"Design System is here, but not selected",14,Violet,993,602,true);
            }
            if(popup!=null)
            {
                // Keep the three monitor outlines visible around the real app UI.
                Round(g,Color.FromArgb(8,13,25),new RectangleF(299,237,846,620),17);
                popup.Refresh(); Application.DoEvents();
                using(var native=new Bitmap(popup.Width,popup.Height))
                {
                    popup.DrawToBitmap(native,popup.ClientRectangle);
                    g.DrawImage(native,new Rectangle(300,225,840,616));
                }
                bool found=popup.DisplayedTabs.Count==1;
                Round(g,found?Color.FromArgb(98,63,170):Color.FromArgb(41,57,85),new RectangleF(441,854,558,42),14);
                Center(g,found?"ENTER  →  DESIGN SYSTEM ON THE RIGHT SCREEN":"TYPE A TITLE TO SEARCH ACROSS SCREENS",16,Color.White,865,true);
                if(found)
                {
                    // An editorial connector ties the selected result to its screen.
                    using(var pen=new Pen(Violet,3))
                    {
                        pen.DashStyle=DashStyle.Dash;
                        g.DrawLine(pen,1144,610,1376,610);g.DrawLine(pen,1376,610,1376,436);
                        pen.DashStyle=DashStyle.Solid;
                        g.DrawLine(pen,1376,436,1367,451);g.DrawLine(pen,1376,436,1385,451);
                    }
                }
            }
            if(Selected)
            {
                // Editorial focus pull: the underlying real window stays on screen 3.
                var origin=Window(2); double t=FocusProgress; t=t*t*(3-2*t);
                var zoom=new RectangleF((float)(origin.X+(Hero.X-origin.X)*t),(float)(origin.Y+(Hero.Y-origin.Y)*t),
                    (float)(origin.Width+(Hero.Width-origin.Width)*t),(float)(origin.Height+(Hero.Height-origin.Height)*t));
                if(t>0)
                {
                    using(var shade=new SolidBrush(Color.FromArgb((int)(t*95),6,12,25))) g.FillRectangle(shade,40,180,1360,680);
                    Round(g,Color.FromArgb(5,9,20),new RectangleF(zoom.X+8,zoom.Y+15,zoom.Width,zoom.Height),14);
                    Browser(g,zoom,page,2,true,edges);
                }
                Round(g,Color.FromArgb(55,40,88),new RectangleF(1000,187,346,37),13);
                Text(g,"SCREEN 3  →  WINDOW B  →  DESIGN",15,Violet,1016,197,true);
                if(t>.85)
                {
                    Center(g,edges!=null?"Selected. Activated. Highlighted for 2 seconds.":"Your selected page is ready.",22,edges!=null?Orange:Ink,844,true);
                    Center(g,"Focus zoom for clarity · The window remains on the right display",14,Muted,879,false);
                }
            }
            Text(g,"REAL TAB SELECTION / FICTIONAL LOCAL PAGES",12,Muted,48,926,true);
            Text(g,"Illustrated monitor overview + focus zoom · Browser toolbars and desktop omitted",12,Muted,833,926,false);
        }
        return canvas;
    }
    static void Browser(Graphics g,RectangleF box,Bitmap page,int screen,bool selected,Bitmap[] edges)
    {
        float scale=box.Width/940f, bar=Math.Max(27,40*scale), inset=Math.Max(2,4*scale);
        Round(g,Color.FromArgb(230,235,247),box,Math.Max(5,12*scale));
        string[] tabs=screen==0?new[]{"Team Notes"}:screen==1?new[]{"Atlas Overview"}:new[]{"Design System","API Reference"};
        float tx=box.X+10*scale;
        for(int i=0;i<tabs.Length;i++)
        {
            bool active=screen!=2 || (selected?i==0:i==1);
            float width=(screen==2?255:300)*scale;
            Round(g,active?Color.White:Color.FromArgb(211,218,234),new RectangleF(tx,box.Y+5,width,bar-8),5);
            Text(g,tabs[i],Math.Max(10,17*scale),active?Color.FromArgb(45,50,75):Color.FromArgb(101,111,135),tx+9*scale,box.Y+7,true);
            tx+=width+5*scale;
        }
        var viewport=new RectangleF(box.X+inset,box.Y+bar,box.Width-2*inset,box.Height-bar-inset);
        using(var clip=UiPaint.Rounded(viewport,Math.Max(3,8*scale)))
        {
            var save=g.Save();g.SetClip(clip);using(var white=new SolidBrush(Color.White)) g.FillRectangle(white,viewport);
            double ratio=Math.Min(viewport.Width/page.Width,viewport.Height/page.Height);
            float w=(float)(page.Width*ratio),h=(float)(page.Height*ratio);
            g.DrawImage(page,new RectangleF(viewport.X+(viewport.Width-w)/2,viewport.Y+(viewport.Height-h)/2,w,h));
            g.Restore(save);
        }
        if(edges!=null)
        {
            var horizontal=edges.Where(e=>e.Width>e.Height).ToArray();var vertical=edges.Where(e=>e.Height>=e.Width).ToArray();
            if(horizontal.Length!=2||vertical.Length!=2) throw new InvalidOperationException("Four native highlight strips required.");
            int thick=Math.Max(3,(int)(5*scale));
            g.DrawImage(horizontal[0],new RectangleF(box.X,box.Y,box.Width,thick));g.DrawImage(horizontal[1],new RectangleF(box.X,box.Bottom-thick,box.Width,thick));
            g.DrawImage(vertical[0],new RectangleF(box.X,box.Y,thick,box.Height));g.DrawImage(vertical[1],new RectangleF(box.Right-thick,box.Y,thick,box.Height));
        }
    }
}
