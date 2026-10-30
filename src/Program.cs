// SPDX-License-Identifier: LGPL-3.0-or-later
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace NeonReader {
    public sealed class Preferences {
        public int x=-1,y=-1,width=640,port=24060;
        public bool topmost=true,animate=true,compact=true;
        public double opacity=1,backgroundOpacity=.6;
        public static Preferences Load() {
            try { return new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(Path.Combine(Program.Root,"settings.json")))??new Preferences(); } catch {return new Preferences();}
        }
        public void Save() {
            try { File.WriteAllText(Path.Combine(Program.Root,"settings.json"),new JavaScriptSerializer().Serialize(this)); } catch(Exception ex) {Program.Log("settings: "+ex.Message);}
        }
    }
    static class Program {
        public static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
        public static void Log(string text) { try { string p=Path.Combine(Root,"reader.log");if(File.Exists(p)&&new FileInfo(p).Length>512*1024) File.Delete(p);File.AppendAllText(p,DateTime.UtcNow.ToString("o")+" "+text+Environment.NewLine); } catch {} }
        [STAThread] static int Main(string[] args) {
            try {
                if(Array.IndexOf(args,"--fixture")>=0) return Checks.Fixture();
                if(Array.IndexOf(args,"--self-test")>=0) return Checks.Run();
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                if(args.Length==2&&args[0]=="--render") {
                    using(Overlay window=new Overlay(null,Preferences.Load(),true)) window.RenderPreview(args[1]);return 0;
                }
                bool created;
                using(Mutex singleton=new Mutex(true,"Local\\Myosu-PP-Counter-Overlay-1",out created)) {
                    if(!created) {MessageBox.Show("Myosu! PP Counter/Overlay is already running. Look for its purple tray icon.\nCtrl+Alt+N shows or hides the overlay.","Myosu! PP Counter/Overlay");return 0;}
                    Preferences settings=Preferences.Load();
                    using(ReaderService reader=new ReaderService()) using(LocalFeed feed=new LocalFeed(reader,settings.port)) using(Overlay window=new Overlay(reader,settings,Array.IndexOf(args,"--demo")>=0)) {
                        reader.Start();feed.Start();window.Feed=feed;Application.Run(window);
                    }
                }
                return 0;
            } catch(Exception ex) { Log(ex.ToString()); MessageBox.Show(ex.Message,"Myosu! PP Counter/Overlay",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1; }
        }
    }
    public sealed class LocalFeed : IDisposable {
        readonly ReaderService reader;readonly int port; TcpListener listener; volatile bool stopped;int clients;
        public string Error="";
        public int Port {get{return port;}}
        public LocalFeed(ReaderService r,int p) {reader=r;port=p>=1024&&p<=65535?p:24060;}
        public void Start() {
            try {listener=new TcpListener(IPAddress.Loopback,port);listener.Start(8);Thread t=new Thread(Accept);t.IsBackground=true;t.Start();}
            catch(Exception ex) {Error="Local feed unavailable: "+ex.Message;Program.Log(Error);}
        }
        void Accept() {
            while(!stopped) try {
                TcpClient c=listener.AcceptTcpClient();
                if(Interlocked.Increment(ref clients)>8) {Interlocked.Decrement(ref clients);c.Close();continue;}
                ThreadPool.QueueUserWorkItem(o=>{try {Serve(c);}catch(Exception ex){Program.Log("feed: "+ex.Message);}finally{c.Close();Interlocked.Decrement(ref clients);}});
            } catch(SocketException) {if(!stopped) Thread.Sleep(100);} catch(ObjectDisposedException){break;}
        }
        void Serve(TcpClient c) {
            using(c) {
                c.ReceiveTimeout=3000;c.SendTimeout=3000;NetworkStream stream=c.GetStream();
                StringBuilder headers=new StringBuilder();int v;
                while(headers.Length<8192&&(v=stream.ReadByte())!=-1) {headers.Append((char)v);if(headers.Length>=4&&headers.ToString(headers.Length-4,4)=="\r\n\r\n")break;}
                string request=headers.ToString();if(!request.EndsWith("\r\n\r\n"))return;
                string[] lines=request.Split(new[]{"\r\n"},StringSplitOptions.None);string[] first=lines[0].Split(' ');
                if(first.Length!=3||first[0]!="GET") {Reply(stream,405,"application/json","{\"error\":\"GET required\"}","");return;}
                string cors="";
                foreach(string line in lines) if(line.StartsWith("Origin:",StringComparison.OrdinalIgnoreCase)) {
                    string origin=line.Substring(7).Trim();Uri uri;
                    if(origin!="null"&&(!Uri.TryCreate(origin,UriKind.Absolute,out uri)||!(uri.Host=="127.0.0.1"||uri.Host=="localhost"))) {Reply(stream,403,"application/json","{\"error\":\"Local origins only\"}","");return;}
                    cors="Access-Control-Allow-Origin: "+origin+"\r\n";
                }
                string path=first[1].Split('?')[0];
                if(path=="/state"||path=="/json"||path=="/api/state") Reply(stream,200,"application/json; charset=utf-8",new JavaScriptSerializer().Serialize(reader.Current),cors);
                else if(path=="/health") Reply(stream,200,"application/json","{\"app\":\"Myosu\",\"ok\":true}",cors);
                else if(path=="/") Reply(stream,200,"text/html; charset=utf-8","<!doctype html><meta charset=utf-8><title>Myosu! PP Counter/Overlay</title><style>body{background:#130c21;color:#efdefb;font:16px Segoe UI;padding:6vw}a{color:#f58eea}pre{white-space:pre-wrap;max-width:900px}</style><h1>Myosu! PP Counter/Overlay</h1><p>Your local osu! stable memory feed. <a href=/state>View JSON</a></p><pre id=s></pre><script>async function tick(){try{s.textContent=JSON.stringify(await(await fetch('/state')).json(),null,2)}catch(e){s.textContent='Reader disconnected'}}tick();setInterval(tick,250)</script>",cors);
                else Reply(stream,404,"application/json","{\"error\":\"Not found\"}",cors);
            }
        }
        static void Reply(NetworkStream s,int code,string type,string text,string cors) {
            byte[] body=Encoding.UTF8.GetBytes(text);byte[] h=Encoding.ASCII.GetBytes("HTTP/1.1 "+code+" "+(code==200?"OK":"Error")+"\r\nContent-Type: "+type+"\r\nContent-Length: "+body.Length+"\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\n"+cors+"Connection: close\r\n\r\n");s.Write(h,0,h.Length);s.Write(body,0,body.Length);
        }
        public void Dispose(){stopped=true;if(listener!=null)listener.Stop();}
    }
}


