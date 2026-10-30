// SPDX-License-Identifier: LGPL-3.0-or-later
// Independent reader; signature/layout reference: tosuapp/tosu StableMemory.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NeonReader {
    public sealed class Snapshot {
        public string app = "Myosu! PP Counter/Overlay", version = "1.0.0", connection = "waiting", message = "Open osu! stable to connect", timestamp;
        public bool demo, connected, statsAvailable;
        public int pid, status = -1, mode, mods, combo, maxCombo, n300, n100, n50, nGeki, nKatu, nMiss, objectCount, playTime;
        public long score;
        public string state = "Waiting for osu!", title = "Your next session awaits", artist = "osu! stable", difficulty = "", player = "", modNames = "NM", checksum = "", filename = "", folder = "";
        public double? accuracy, hp, unstableRate, pp, stars;
        public int ppJudgedObjects;
        public Snapshot Copy(){return (Snapshot)MemberwiseClone();}
        public double ar, cs, od;
        public bool[] keys = new bool[4];
        public Snapshot() { timestamp = DateTime.UtcNow.ToString("o"); }
        public static Snapshot Demo() {
            return new Snapshot { demo=true, connection="preview", message="Sample data - preview only", state="Preview", title="A night in violet", artist="NEON READER", difficulty="After hours", player="Player", statsAvailable=true, accuracy=98.64, pp=247.3, combo=427, maxCombo=512, n300=412, n100=8, n50=1, nMiss=2, score=12846732, mods=72, modNames="HD DT", unstableRate=82.4, objectCount=750, hp=.91, playTime=64000 };
        }
    }

    public interface IMemory { byte[] Read(long address, int count); }
    public static class Data {
        public static uint U32(IMemory m,long p) { return BitConverter.ToUInt32(m.Read(p,4),0); }
        public static int I32(IMemory m,long p) { return BitConverter.ToInt32(m.Read(p,4),0); }
        public static ushort U16(IMemory m,long p) { return BitConverter.ToUInt16(m.Read(p,2),0); }
        public static double F64(IMemory m,long p) { return BitConverter.ToDouble(m.Read(p,8),0); }
        public static float F32(IMemory m,long p) { return BitConverter.ToSingle(m.Read(p,4),0); }
        public static long P(IMemory m,long p) { uint a=U32(m,p); if(a<0x10000 || a>0xfffffff0) throw new IOException("Unavailable game pointer"); return a; }
        public static string Str(IMemory m,long p) {
            if(p==0) return "";
            int n=I32(m,p+4); if(n<0||n>2048) throw new IOException("Invalid game string length");
            return Encoding.Unicode.GetString(m.Read(p+8,n*2)).Replace("\0", "");
        }
        public static string Field(IMemory m,long p,int o) { return Str(m,U32(m,p+o)); }
        public static double Accuracy(int mode,int a,int b,int c,int g,int k,int miss) {
            double total=a+b+c+miss;
            if(mode==1) return a+b+miss==0?100:100.0*(a+.5*b)/(a+b+miss);
            if(mode==2) return total+k==0?100:100.0*(a+b+c)/(total+k);
            if(mode==3) return total+g+k==0?100:100.0*(300.0*(a+g)+200.0*k+100.0*b+50.0*c)/(300*(total+g+k));
            return total==0?100:100.0*(300.0*a+100.0*b+50.0*c)/(300*total);
        }
        public static string Mods(int v) {
            List<string> s=new List<string>();
            int[] bits={1,2,8,16,32,64,128,256,512,1024,2048,4096,8192,16384,32768,65536,131072,262144,524288,1048576,2097152,4194304,8388608,16777216,33554432,67108864,134217728,268435456,536870912,1073741824};
            string[] names={"NF","EZ","HD","HR","SD","DT","RX","HT","NC","FL","AT","SO","AP","PF","4K","5K","6K","7K","8K","FI","RD","CN","TP","9K","CO","1K","3K","2K","V2","MR"};
            for(int i=0;i<bits.Length;i++) if((v&bits[i])!=0 && !(bits[i]==64&&(v&512)!=0) && !(bits[i]==32&&(v&16384)!=0)) s.Add(names[i]);
            return s.Count==0?"NM":String.Join(" ",s);
        }
    }

    public sealed class ProcessMemory : IMemory, IDisposable {
        [StructLayout(LayoutKind.Sequential)] public struct Region { public IntPtr BaseAddress,AllocationBase; public uint AllocationProtect; public UIntPtr RegionSize; public uint State,Protect,Type; }
        [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool ReadProcessMemory(IntPtr process,IntPtr address,byte[] bytes,UIntPtr count,out UIntPtr read);
        [DllImport("kernel32.dll")] static extern UIntPtr VirtualQueryEx(IntPtr p,IntPtr address,out Region r,UIntPtr size);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool IsWow64Process(IntPtr p,out bool wow);
        readonly IntPtr handle;
        public ProcessMemory(int pid,bool require32=true) {
            // Query + read only. No write, injection, debug or all-access rights.
            handle=OpenProcess(0x410,false,pid);
            if(handle==IntPtr.Zero) throw new IOException("Windows denied read access ("+Marshal.GetLastWin32Error()+"). Run at the same privilege level as osu!.");
            bool wow;
            if(require32&&Environment.Is64BitOperatingSystem&&(!IsWow64Process(handle,out wow)||!wow)) { Dispose(); throw new IOException("This process is not 32-bit osu! stable"); }
        }
        public byte[] Read(long p,int count) {
            if(p<0x10000||p>0xffffffffL||count<0||count>4*1024*1024) throw new IOException("Invalid read range");
            if(count==0) return new byte[0];
            byte[] b=new byte[count]; UIntPtr read;
            if(!ReadProcessMemory(handle,new IntPtr(p),b,(UIntPtr)count,out read)||read.ToUInt64()!=(ulong)count) throw new IOException("Game memory changed or is unavailable");
            return b;
        }
        public Dictionary<string,long> Scan(Pattern[] patterns,Func<bool> stop) {
            Dictionary<string,long> found=new Dictionary<string,long>();
            long cursor=0x10000, ceiling=0x100000000L;
            int size=Marshal.SizeOf(typeof(Region));
            while(cursor<ceiling&&!stop()) {
                Region r; if(VirtualQueryEx(handle,new IntPtr(cursor),out r,(UIntPtr)size)==UIntPtr.Zero) break;
                long start=r.BaseAddress.ToInt64(),length=(long)r.RegionSize.ToUInt64();
                long next=start+length; if(length<=0||next<=cursor) break; cursor=next;
                // Stable's JIT code lives in executable private pages, not just osu!.exe.
                uint prot=r.Protect&0xff;
                if(r.State!=0x1000||(r.Protect&0x100)!=0||!(prot==0x10||prot==0x20||prot==0x40||prot==0x80)) continue;
                for(long offset=0;offset<length&&!stop();offset+=1024*1024) {
                    int n=(int)Math.Min(1024*1024+128,length-offset); byte[] b;
                    try { b=Read(start+offset,n); } catch(IOException) { continue; }
                    foreach(Pattern pat in patterns) {
                        if(found.ContainsKey(pat.Name)) continue;
                        int i=pat.Find(b); if(i>=0) found[pat.Name]=start+offset+i+pat.Offset;
                    }
                    if(found.Count==patterns.Length) return found;
                }
            }
            return found;
        }
        public void Dispose() { if(handle!=IntPtr.Zero) CloseHandle(handle); }
    }
    public sealed class Pattern {
        public readonly string Name; public readonly int Offset; readonly int[] bytes;
        public Pattern(string name,string hex,int offset) { Name=name;Offset=offset; string[] s=hex.Split(' ');bytes=new int[s.Length];for(int i=0;i<s.Length;i++) bytes[i]=s[i]=="??"?-1:Convert.ToInt32(s[i],16); }
        public int Find(byte[] b) {
            for(int i=0;i<=b.Length-bytes.Length;i++) {
                if(bytes[0]>=0&&b[i]!=bytes[0]) continue;
                int j=1;for(;j<bytes.Length;j++) if(bytes[j]>=0&&b[i+j]!=bytes[j]) break;
                if(j==bytes.Length) return i;
            } return -1;
        }
    }

    public sealed class StableDecoder {
        // Layout facts/signatures from tosu StableMemory (LGPL-3.0+); source in THIRD-PARTY.md.
        public static readonly Pattern[] Patterns={
            new Pattern("base","F8 01 74 04 83 65",0),
            new Pattern("rules","7D 15 A1 ?? ?? ?? ?? 85 C0",0),
            new Pattern("status","48 83 F8 04 73 1E",-4),
            new Pattern("time","5E 5F 5D C3 A1 ?? ?? ?? ?? 89 ?? 04",0)
        };
        readonly IMemory m; readonly Dictionary<string,long> a;
        public StableDecoder(IMemory memory,Dictionary<string,long> addresses) { m=memory;a=addresses; }
        public Snapshot Read(int pid) {
            Snapshot s=new Snapshot { connected=true,pid=pid,connection="connected",message="Connected to osu! stable" };
            s.status=Data.I32(m,Data.P(m,a["status"]));
            if(s.status<0||s.status>24) throw new IOException("Game status signature no longer matches");
            s.state=StateName(s.status);
            try {
                long map=Data.P(m,Data.P(m,a["base"]-12));
                s.title=Data.Field(m,map,0x24);s.artist=Data.Field(m,map,0x18);s.difficulty=Data.Field(m,map,0xac);
                s.checksum=Data.Field(m,map,0x6c);s.filename=Data.Field(m,map,0x90);s.folder=Data.Field(m,map,0x78);
                s.objectCount=Data.I32(m,map+0xf8); if(s.objectCount<0||s.objectCount>1000000) s.objectCount=0;
                s.ar=Data.F32(m,map+0x2c);s.cs=Data.F32(m,map+0x30);s.od=Data.F32(m,map+0x38);
            } catch(IOException) { s.title="Select a beatmap";s.artist="osu! stable"; }
            if(a.ContainsKey("time")) try { s.playTime=Data.I32(m,Data.P(m,a["time"]+5)); } catch(IOException) { }
            // Do not read stale gameplay structures on menu/transition screens.
            if(s.status!=2&&s.status!=7&&s.status!=14) return s;
            try {
                long rules=Data.P(m,Data.P(m,a["rules"]-11)+4);
                bool result=s.status==7||s.status==14;
                long game=result?0:Data.P(m,rules+0x64);
                long score=result?Data.P(m,rules+0x38):Data.P(m,game+0x38);
                s.mode=Data.I32(m,score+0x64); if(s.mode<0||s.mode>3) throw new IOException("Invalid game mode");
                s.player=Data.Field(m,score,0x28);
                long mod=Data.P(m,score+0x1c);s.mods=Data.I32(m,mod+8)^Data.I32(m,mod+12);
                bool v2=!result&&Data.U32(m,score+0x54)!=0;
                if(v2) s.mods|=536870912;
                s.modNames=Data.Mods(s.mods);
                // A single read keeps the hit counts and combo from different polls together.
                byte[] hit=m.Read(score+0x88,14);
                s.n100=BitConverter.ToUInt16(hit,0);s.n300=BitConverter.ToUInt16(hit,2);s.n50=BitConverter.ToUInt16(hit,4);
                s.nGeki=BitConverter.ToUInt16(hit,6);s.nKatu=BitConverter.ToUInt16(hit,8);s.nMiss=BitConverter.ToUInt16(hit,10);
                s.combo=BitConverter.ToUInt16(hit,12);s.maxCombo=Data.U16(m,score+0x68);
                if(result) s.combo=s.maxCombo;
                s.score=(uint)Data.I32(m,v2?rules+0xf8:score+0x78);
                s.accuracy=Data.Accuracy(s.mode,s.n300,s.n100,s.n50,s.nGeki,s.nKatu,s.nMiss);
                s.statsAvailable=true;
                if(!result) {
                    try { double hp=Data.F64(m,Data.P(m,game+0x40)+0x1c); if(!Double.IsNaN(hp)&&hp>=0&&hp<=1) s.hp=hp; } catch(IOException) { }
                    try { s.unstableRate=ReadUR(score); } catch(IOException) { }
                    if(s.mode==0) try { ReadKeys(s,rules); } catch(IOException) { }
                }
            } catch(IOException) { s.statsAvailable=false;s.message="Waiting for gameplay data"; }
            return s;
        }
        double? ReadUR(long score) {
            long list=Data.P(m,score+0x38);int n=Data.I32(m,list+12);
            if(n<2||n>100000) return null;
            long array=Data.P(m,list+4);int cap=Data.I32(m,array+4);if(n>cap) return null;
            byte[] b=m.Read(array+8,n*4);double sum=0,sq=0;int valid=0;
            for(int i=0;i<n;i++) { int v=BitConverter.ToInt32(b,i*4);if(v < -10000||v>10000) continue;sum+=v;sq+=(double)v*v;valid++; }
            return valid<2?(double?)null:Math.Sqrt(Math.Max(0,sq/valid-Math.Pow(sum/valid,2)))*10;
        }
        void ReadKeys(Snapshot s,long rules) {
            long overlay=Data.P(m,rules+0xac), items=Data.P(m,Data.P(m,overlay+0x10)+4);
            if(Data.I32(m,items+4)<4) return;
            for(int i=0;i<4;i++) s.keys[i]=m.Read(Data.P(m,items+8+i*4)+0x1c,1)[0]!=0;
        }
        public static string StateName(int v) {
            switch(v) {case 0:return "Main menu";case 1:return "Editing";case 2:return "Playing";case 3:return "Exit";case 4:return "Song select";case 5:return "Song select";case 7:return "Results";case 11:return "Multiplayer";case 12:return "Multiplayer select";case 13:return "Multiplayer results";case 14:return "Results";default:return "osu! stable";}
        }
    }
    public sealed class ReaderService : IDisposable {
        volatile bool stopped; Thread worker; volatile Snapshot current=new Snapshot();
        PpCalculator calculator;
        public Snapshot Current { get {Snapshot s=current.Copy();PpResult pp=calculator==null?null:calculator.Latest;if(s.statsAvailable&&pp!=null&&pp.checksum==s.checksum&&pp.mods==s.mods&&pp.hits<=PpCalculator.Hits(s)&&PpCalculator.Hits(s)-pp.hits<32){s.pp=pp.pp;s.stars=pp.stars;s.ppJudgedObjects=pp.hits;}return s;} }
        public void Start() { calculator=new PpCalculator(()=>current);calculator.Start();worker=new Thread(Run);worker.IsBackground=true;worker.Name="osu stable reader";worker.Start(); }
        void Publish(string connection,string message) { current=new Snapshot { connection=connection,message=message,state=connection=="scanning"?"Finding game data":"Waiting for osu!" }; }
        void Run() {
            while(!stopped) {
                Process game=null;
                try {
                    foreach(Process p in Process.GetProcessesByName("osu!")) {
                        bool stable=false;
                        try { stable=p.MainModule.ModuleName.Equals("osu!.exe",StringComparison.OrdinalIgnoreCase); } catch { }
                        if(stable&&game==null) game=p;else p.Dispose();
                    }
                    if(game==null) { Publish("waiting","Open osu! stable to connect");Sleep(1500);continue; }
                    using(game) using(ProcessMemory m=new ProcessMemory(game.Id)) {
                        while(!stopped&&!game.HasExited) {
                            Publish("scanning","Finding osu! stable memory signatures");
                            Dictionary<string,long> addresses=m.Scan(StableDecoder.Patterns,()=>stopped);
                            if(!addresses.ContainsKey("base")||!addresses.ContainsKey("rules")||!addresses.ContainsKey("status")) {
                                Publish("scanning","Waiting for stable to load. Open song select; memory signatures will retry.");Sleep(5000);continue;
                            }
                            StableDecoder decoder=new StableDecoder(m,addresses);int errors=0;
                            while(!stopped&&!game.HasExited) {
                                try { current=decoder.Read(game.Id);errors=0; }
                                catch(IOException) { if(++errors>=10) break; current=new Snapshot { connection="scanning",message="Game data changed; reconnecting",state="Reconnecting" }; }
                                Sleep(33);
                            }
                        }
                    }
                } catch(Exception ex) {
                    if(game!=null) game.Dispose();
                    Publish("error",ex.Message); Program.Log("reader: "+ex.Message);Sleep(4000);
                }
            }
        }
        void Sleep(int ms) { for(int n=0;n<ms&&!stopped;n+=Math.Min(50,ms-n)) Thread.Sleep(Math.Min(50,ms-n)); }
        public void Dispose() { stopped=true;if(calculator!=null)calculator.Dispose();if(worker!=null) worker.Join(2000); }
    }
}

