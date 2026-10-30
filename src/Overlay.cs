// SPDX-License-Identifier: LGPL-3.0-or-later
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NeonReader {
    public sealed class Overlay : Form {
        readonly ReaderService reader;readonly Preferences prefs;readonly Timer ticker;readonly Image gif;readonly NotifyIcon tray;
        readonly ContextMenuStrip menu;Icon appIcon; bool demo,clickThrough,animate=true;readonly bool renderOnly;
        [StructLayout(LayoutKind.Sequential)] struct POINT {public int x,y;public POINT(int a,int b){x=a;y=b;}}
        [StructLayout(LayoutKind.Sequential)] struct SIZE {public int cx,cy;public SIZE(int a,int b){cx=a;cy=b;}}
        [StructLayout(LayoutKind.Sequential,Pack=1)] struct BLEND {public byte op,flags,alpha,format;}
        [StructLayout(LayoutKind.Sequential)] struct RECT {public int left,top,right,bottom;}
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h,IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll",SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr h,IntPtr dst,ref POINT pos,ref SIZE size,IntPtr src,ref POINT origin,uint key,ref BLEND blend,uint flags);
        public LocalFeed Feed;
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,IntPtr l);
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
        [DllImport("user32.dll",EntryPoint="GetWindowLong")] static extern int GetWindowLong(IntPtr h,int i);
        [DllImport("user32.dll",EntryPoint="SetWindowLong")] static extern int SetWindowLong(IntPtr h,int i,int v);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
        readonly Color ink=Color.FromArgb(244,234,255),muted=Color.FromArgb(181,155,204),pink=Color.FromArgb(249,130,230),cyan=Color.FromArgb(125,231,235);
        public Overlay(ReaderService service,Preferences p,bool preview) {
            reader=service;prefs=p;demo=preview;renderOnly=service==null;animate=p.animate;
            Text="Myosu! PP Counter/Overlay";FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;
            ShowInTaskbar=false;TopMost=p.topmost;DoubleBuffered=true;BackColor=Color.FromArgb(15,9,25);
            SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);
            p.compact=true;
            ClientSize=new Size(Math.Max(400,Math.Min(1200,p.width)),HeightFor(Math.Max(400,Math.Min(1200,p.width))));
            MinimumSize=new Size(400,119);MaximumSize=new Size(1200,357);
            Rectangle desktop=Screen.PrimaryScreen.WorkingArea;
            Point target=new Point(p.x,p.y);
            if((p.x==-1&&p.y==-1)||!VisibleOnScreen(new Rectangle(target,Size))) target=new Point(desktop.Right-Width-28,desktop.Bottom-Height-28);
            Location=target;
            string asset=Path.Combine(Program.Root,"background.gif");
            gif=Image.FromFile(asset);
            appIcon=new Icon(Path.Combine(Program.Root,"App.ico"),64,64);
            Icon=appIcon;
            menu=new ContextMenuStrip { BackColor=Color.FromArgb(31,19,45),ForeColor=ink,ShowImageMargin=false };
            Add("Show / hide overlay   Ctrl+Alt+N",()=>ToggleVisible());
            Add("Click-through   Ctrl+Alt+C",()=>SetClickThrough(!clickThrough));
            Add("Always on top",()=>{prefs.topmost=!prefs.topmost;TopMost=prefs.topmost;UpdateMenu();});
            Add("Animate background",()=>{animate=!animate;prefs.animate=animate;UpdateMenu();});
            ToolStripMenuItem transparency=new ToolStripMenuItem("Transparency");
            transparency.DropDownItems.Add(new ToolStripLabel("Background visibility"));
            TrackBar scene=new TrackBar{Minimum=0,Maximum=100,Value=(int)(Math.Max(0,Math.Min(1,prefs.backgroundOpacity))*100),Width=190,TickStyle=TickStyle.None,AutoSize=false,Height=28};
            scene.ValueChanged+=(s,e)=>{prefs.backgroundOpacity=scene.Value/100.0;Present();};transparency.DropDownItems.Add(new ToolStripControlHost(scene));
            transparency.DropDownItems.Add(new ToolStripLabel("Whole overlay visibility"));
            TrackBar whole=new TrackBar{Minimum=25,Maximum=100,Value=(int)(Math.Max(.25,Math.Min(1,prefs.opacity))*100),Width=190,TickStyle=TickStyle.None,AutoSize=false,Height=28};
            whole.ValueChanged+=(s,e)=>{prefs.opacity=whole.Value/100.0;Present();};transparency.DropDownItems.Add(new ToolStripControlHost(whole));
            menu.Items.Add(transparency);
            menu.Items.Add(new ToolStripSeparator());
            Add("Preview with sample data",()=>{demo=!demo;UpdateMenu();Invalidate();});
            Add("Open local data feed",()=>Process.Start("http://127.0.0.1:"+(Feed==null?prefs.port:Feed.Port)+"/"));
            Add("Help",()=>Process.Start(Path.Combine(Program.Root,"README.txt")));
            menu.Items.Add(new ToolStripSeparator());
            Add("Reset size and position",()=>{ClientSize=new Size(640,190);Location=new Point(desktop.Right-Width-28,desktop.Bottom-Height-28);SetClickThrough(false);prefs.opacity=1;prefs.backgroundOpacity=.6;whole.Value=100;scene.Value=60;UpdateMenu();});
            Add("Quit Myosu! PP Counter/Overlay",()=>Close());
            ContextMenuStrip=menu;
            tray=new NotifyIcon {Icon=appIcon,Text="Myosu! PP Counter/Overlay - osu! stable",ContextMenuStrip=menu,Visible=!renderOnly};
            tray.DoubleClick+=(s,e)=>{Show();SetClickThrough(false);Activate();};
            ticker=new Timer {Interval=33};ticker.Tick+=(s,e)=>{if(Visible)Present();};
            if(!renderOnly)ticker.Start();
            if(ImageAnimator.CanAnimate(gif)) ImageAnimator.Animate(gif,(s,e)=>{});
            MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xA1,(IntPtr)2,IntPtr.Zero);}};
            MouseWheel+=(s,e)=>{
                if((ModifierKeys&Keys.Control)!=0) {int w=Math.Max(400,Math.Min(1200,Width+(e.Delta>0?40:-40)));ClientSize=new Size(w,HeightFor(w));}
                else if((ModifierKeys&Keys.Shift)!=0){prefs.backgroundOpacity=Math.Max(0,Math.Min(1,prefs.backgroundOpacity+(e.Delta>0?.05:-.05)));scene.Value=(int)Math.Round(prefs.backgroundOpacity*100);}
            };
            UpdateMenu();
        }
        static bool VisibleOnScreen(Rectangle r) {foreach(Screen s in Screen.AllScreens)if(Rectangle.Intersect(s.WorkingArea,r).Width>=80&&Rectangle.Intersect(s.WorkingArea,r).Height>=80)return true;return false;}
        int HeightFor(int width){return (int)Math.Round(width*190.0/640);}
        void Add(string label,Action action){menu.Items.Add(label,null,(s,e)=>action());}
        void UpdateMenu(){foreach(ToolStripItem item in menu.Items){ToolStripMenuItem m=item as ToolStripMenuItem;if(m==null)continue;if(m.Text.StartsWith("Click-through"))m.Checked=clickThrough;if(m.Text=="Always on top")m.Checked=prefs.topmost;if(m.Text=="Animate background")m.Checked=animate;if(m.Text=="Compact layout")m.Checked=prefs.compact;if(m.Text=="Preview with sample data")m.Checked=demo;}}
        void ToggleVisible(){if(Visible)Hide();else Show();}
        void SetClickThrough(bool enabled){clickThrough=enabled;int flags=GetWindowLong(Handle,-20);SetWindowLong(Handle,-20,enabled?flags|0x20|0x80000:flags&~0x20);UpdateMenu();}
        protected override bool ShowWithoutActivation{get{return true;}}
        protected override CreateParams CreateParams{get{CreateParams p=base.CreateParams;p.ExStyle|=0x80|0x80000;return p;}}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);if(!renderOnly){if(!RegisterHotKey(Handle,1,0x4003,(uint)Keys.N))Program.Log("Ctrl+Alt+N is already in use");if(!RegisterHotKey(Handle,2,0x4003,(uint)Keys.C))Program.Log("Ctrl+Alt+C is already in use");}}
        protected override void WndProc(ref Message m){
            if(m.Msg==0x312){if(m.WParam.ToInt32()==1)ToggleVisible();if(m.WParam.ToInt32()==2)SetClickThrough(!clickThrough);}
            if(m.Msg==0x84&&!clickThrough){long lp=m.LParam.ToInt64();Point p=PointToClient(new Point((short)(lp&65535),(short)((lp>>16)&65535)));int edge=9;bool left=p.X<edge,right=p.X>=Width-edge,top=p.Y<edge,bottom=p.Y>=Height-edge;int hit=top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:right?11:0;if(hit!=0){m.Result=(IntPtr)hit;return;}}
            if(m.Msg==0x214){RECT r=(RECT)Marshal.PtrToStructure(m.LParam,typeof(RECT));int zone=m.WParam.ToInt32();int w=(zone==3||zone==6)?(int)((r.bottom-r.top)*640.0/190):r.right-r.left;w=Math.Max(400,Math.Min(1200,w));int h=HeightFor(w);if(zone==1||zone==4||zone==7)r.left=r.right-w;else r.right=r.left+w;if(zone==3||zone==4||zone==5)r.top=r.bottom-h;else r.bottom=r.top+h;Marshal.StructureToPtr(r,m.LParam,false);m.Result=(IntPtr)1;return;}
            base.WndProc(ref m);
        }
        protected override void OnPaint(PaintEventArgs e){}
        void Present(){
            if(renderOnly||!IsHandleCreated||IsDisposed||Width<1||Height<1)return;
            using(Bitmap frame=new Bitmap(Width,Height,PixelFormat.Format32bppArgb)){
                using(Graphics g=Graphics.FromImage(frame)){g.Clear(Color.Transparent);Draw(g,ClientSize,demo?Snapshot.Demo():reader.Current);}
                IntPtr screen=GetDC(IntPtr.Zero),dc=CreateCompatibleDC(screen),bmp=frame.GetHbitmap(Color.FromArgb(0)),old=SelectObject(dc,bmp);
                try{POINT pos=new POINT(Left,Top),origin=new POINT(0,0);SIZE size=new SIZE(Width,Height);BLEND blend=new BLEND{op=0,flags=0,alpha=(byte)(255*Math.Max(.25,Math.Min(1,prefs.opacity))),format=1};UpdateLayeredWindow(Handle,screen,ref pos,ref size,dc,ref origin,0,ref blend,2);}
                finally{SelectObject(dc,old);DeleteObject(bmp);DeleteDC(dc);ReleaseDC(IntPtr.Zero,screen);}
            }
        }
        static GraphicsPath Round(RectangleF r,float radius){GraphicsPath p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
        void FillRound(Graphics g,Color c,RectangleF r,float radius){using(GraphicsPath p=Round(r,radius))using(Brush b=new SolidBrush(c))g.FillPath(b,p);}
        void TextAt(Graphics g,string text,float size,Color color,RectangleF r,FontStyle style=FontStyle.Regular,StringAlignment align=StringAlignment.Near){using(Font f=new Font("Segoe UI",size,style,GraphicsUnit.Pixel))using(Brush b=new SolidBrush(color))using(StringFormat sf=new StringFormat()){sf.Trimming=StringTrimming.EllipsisCharacter;sf.FormatFlags=StringFormatFlags.NoWrap;sf.Alignment=align;sf.LineAlignment=StringAlignment.Center;g.DrawString(text??"",f,b,r,sf);}}
        void Draw(Graphics g,Size size,Snapshot s){
            g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            float scale=size.Width/640f;g.ScaleTransform(scale,scale);float h=190;
            using(GraphicsPath clip=Round(new RectangleF(0,0,640,h),19))g.SetClip(clip);
            if(animate)ImageAnimator.UpdateFrames(gif);
            float imageHeight=640f*gif.Height/gif.Width;
            float visibility=(float)Math.Max(0,Math.Min(1,prefs.backgroundOpacity));
            using(ImageAttributes attr=new ImageAttributes()){ColorMatrix matrix=new ColorMatrix();matrix.Matrix33=visibility;attr.SetColorMatrix(matrix);g.DrawImage(gif,new Rectangle(0,(int)(h-imageHeight),640,(int)imageHeight),0,0,gif.Width,gif.Height,GraphicsUnit.Pixel,attr);}
            using(LinearGradientBrush shade=new LinearGradientBrush(new RectangleF(0,0,640,h),Color.FromArgb((int)(80*visibility),12,5,25),Color.FromArgb((int)(235*visibility),12,7,23),90))g.FillRectangle(shade,0,0,640,h);
            float y=13;
            TextAt(g,s.title,23,ink,new RectangleF(25,y,586,32),FontStyle.Bold);
            string detail=s.connected||s.demo?s.artist+(String.IsNullOrEmpty(s.difficulty)?"":"  /  "+s.difficulty):s.message;
            TextAt(g,detail,12,muted,new RectangleF(26,y+34,585,21));
            float metrics=y+66;
            TextAt(g,"ACCURACY",9,muted,new RectangleF(26,metrics,170,16),FontStyle.Bold);
            TextAt(g,s.statsAvailable&&s.accuracy.HasValue?s.accuracy.Value.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"%":"—",29,ink,new RectangleF(24,metrics+15,165,40),FontStyle.Bold);
            TextAt(g,"COMBO",9,muted,new RectangleF(207,metrics,120,16),FontStyle.Bold);
            TextAt(g,s.statsAvailable?s.combo.ToString("N0")+"x":"—",29,pink,new RectangleF(204,metrics+15,125,40),FontStyle.Bold);
            TextAt(g,"PP",9,muted,new RectangleF(347,metrics,100,16),FontStyle.Bold);
            TextAt(g,s.statsAvailable&&s.pp.HasValue?s.pp.Value.ToString("0.0"):"—",29,cyan,new RectangleF(344,metrics+15,116,40),FontStyle.Bold);
            TextAt(g,"SCORE",9,muted,new RectangleF(440,metrics,171,16),FontStyle.Bold,StringAlignment.Far);
            TextAt(g,s.statsAvailable?s.score.ToString("N0"):"—",25,ink,new RectangleF(463,metrics+17,150,36),FontStyle.Bold,StringAlignment.Far);
            float bottom=y+128;
            string[] labels={"300","100","50","MISS"};int[] counts={s.n300,s.n100,s.n50,s.nMiss};Color[] colors={cyan,Color.FromArgb(168,231,156),Color.FromArgb(239,201,135),Color.FromArgb(255,130,169)};
            for(int i=0;i<4;i++){float x=26+i*80;FillRound(g,Color.FromArgb(85,72,42,92),new RectangleF(x,bottom,72,25),7);TextAt(g,labels[i],8,colors[i],new RectangleF(x+7,bottom,31,25),FontStyle.Bold);TextAt(g,s.statsAvailable?counts[i].ToString():"—",11,ink,new RectangleF(x+32,bottom,32,25),FontStyle.Bold,StringAlignment.Far);}
            string tail=s.demo?"SAMPLE DATA":s.statsAvailable?(s.unstableRate.HasValue?s.unstableRate.Value.ToString("0.0")+" UR  ·  ":"")+s.modNames:s.connected?s.state:"osu! stable";
            TextAt(g,tail,10,s.demo?pink:muted,new RectangleF(358,bottom,252,25),FontStyle.Regular,StringAlignment.Far);
            float lineY=h-8;using(Pen line=new Pen(Color.FromArgb(65,166,107,193),2))g.DrawLine(line,26,lineY,614,lineY);
            if(s.statsAvailable&&s.objectCount>0){double hits=s.n300+s.n100+s.n50+s.nMiss+(s.mode==3?s.nGeki+s.nKatu:0);using(Pen line=new Pen(pink,2))g.DrawLine(line,26,lineY,26+(float)(588*Math.Min(1,hits/s.objectCount)),lineY);}
            g.ResetClip();using(GraphicsPath frame=Round(new RectangleF(.5f,.5f,639,h-1),19))using(Pen edge=new Pen(Color.FromArgb(120,134,70,165),1))g.DrawPath(edge,frame);
        }
        public void RenderPreview(string path){using(Bitmap b=new Bitmap(640,190))using(Graphics g=Graphics.FromImage(b)){g.Clear(Color.Transparent);Draw(g,new Size(640,190),Snapshot.Demo());b.Save(path,ImageFormat.Png);}}
        protected override void OnFormClosed(FormClosedEventArgs e){
            ticker.Stop();ticker.Dispose();
            if(!renderOnly){UnregisterHotKey(Handle,1);UnregisterHotKey(Handle,2);prefs.x=Left;prefs.y=Top;prefs.width=Width;prefs.Save();}
            tray.Visible=false;tray.Dispose();ImageAnimator.StopAnimate(gif,(s,a)=>{});gif.Dispose();appIcon.Dispose();menu.Dispose();base.OnFormClosed(e);
        }
    }
}

