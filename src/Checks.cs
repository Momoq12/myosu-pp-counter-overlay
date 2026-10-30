// SPDX-License-Identifier: LGPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace NeonReader {
    static class Checks {
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr VirtualAlloc(IntPtr a,UIntPtr n,uint type,uint protect);
        [DllImport("kernel32.dll")] static extern bool VirtualFree(IntPtr a,UIntPtr n,uint type);
        static readonly List<string> passed=new List<string>();
        static void Assert(bool condition,string label){if(!condition)throw new Exception(label);passed.Add(label);}
        public static int Run(){
            Process child=null;
            try {
                Assert(new Pattern("p","AA ?? CC",0).Find(new byte[]{0,170,31,204})==1,"wildcard pattern at final possible position");
                Assert(new Pattern("p","AA BB",0).Find(new byte[]{170})==-1,"short pattern buffer rejected");
                Assert(Math.Abs(Data.Accuracy(0,90,5,3,0,0,2)-92.1666666667)<.000001,"osu standard accuracy");
                Assert(Data.Accuracy(1,8,2,0,0,0,0)==90,"taiko accuracy");
                Assert(Data.Accuracy(2,8,1,1,0,1,1)==(1000.0/12),"catch accuracy");
                Assert(Math.Abs(Data.Accuracy(3,3,1,1,3,1,1)-71.6666666667)<.000001,"mania accuracy");
                Assert(Data.Accuracy(0,0,0,0,0,0,0)==100,"no-hit accuracy defined");
                Assert(Data.Mods(64|512|8)=="HD NC","NC suppresses duplicate DT");
                Assert(Data.Mods(32|16384)=="PF","PF suppresses duplicate SD");
                child=new Process {StartInfo=new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--fixture"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true}};
                child.Start();
                string ready=child.StandardOutput.ReadLine();Assert(ready=="READY","controlled child process started");
                using(ProcessMemory memory=new ProcessMemory(child.Id,false)) {
                    Dictionary<string,long> addresses=memory.Scan(StableDecoder.Patterns,()=>false);
                    Assert(addresses.Count==4,"VirtualQueryEx and cross-process signature scan");
                    StableDecoder decoder=new StableDecoder(memory,addresses);Snapshot s=decoder.Read(child.Id);
                    Assert(s.connected&&s.statsAvailable&&s.status==2,"gameplay pointer chains resolved through ReadProcessMemory");
                    Assert(s.title=="Fixture song"&&s.artist=="Memory test"&&s.difficulty=="Hard","UTF-16 beatmap metadata");
                    Assert(s.player=="Fixture player"&&s.mods==72&&s.modNames=="HD DT","player and XOR-obfuscated mods");
                    Assert(s.n300==410&&s.n100==8&&s.n50==1&&s.nMiss==2&&s.combo==427&&s.maxCombo==512&&s.score==12846732,"score, combo and hit counts decoded");
                    Assert(s.playTime==64000&&s.objectCount==750,"play time and object count");
                    Assert(s.unstableRate.HasValue&&Math.Abs(s.unstableRate.Value-81.64965809)<.00001,"hit-error standard deviation and UR");
                    Command(child,"results");s=decoder.Read(child.Id);
                    Assert(s.statsAvailable&&s.status==7&&s.combo==512,"result screen uses its own score pointer");
                    Command(child,"menu");s=decoder.Read(child.Id);
                    Assert(!s.statsAvailable&&!s.accuracy.HasValue&&s.combo==0,"menu clears stale gameplay values");
                    Command(child,"broken");s=decoder.Read(child.Id);
                    Assert(!s.statsAvailable,"invalid gameplay pointer never becomes fake live data");
                    bool rejected=false;try{memory.Read(4,16);}catch(IOException){rejected=true;}Assert(rejected,"invalid memory range rejected");
                    string json=new JavaScriptSerializer().Serialize(s);Assert(json.Contains("\"statsAvailable\":false"),"local feed snapshot serializes unavailable state");
                }
                child.StandardInput.WriteLine("quit");child.WaitForExit(3000);
                File.WriteAllText(Path.Combine(Program.Root,"test-results.json"),new JavaScriptSerializer().Serialize(new{ok=true,checks=passed.Count,passed=passed,liveOsuVerified=false}));return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(Program.Root,"test-results.json"),new JavaScriptSerializer().Serialize(new{ok=false,error=ex.ToString(),passed=passed}));return 1;}
            finally{if(child!=null){try{if(!child.HasExited)child.Kill();}catch{}child.Dispose();}}
        }
        static void Command(Process p,string command){p.StandardInput.WriteLine(command);if(p.StandardOutput.ReadLine()!="OK")throw new Exception("Fixture command failed");}
        public static int Fixture(){
            long start=0x22000000;IntPtr block=VirtualAlloc(new IntPtr(start),(UIntPtr)65536,0x3000,0x40);
            if(block==IntPtr.Zero)return 2;
            try {
                byte[] b=new byte[65536];
                Action<int,int> i32=(o,v)=>Buffer.BlockCopy(BitConverter.GetBytes(v),0,b,o,4);
                Action<int,int> ptr=(o,v)=>i32(o,(int)(start+v));
                Action<int,ushort> u16=(o,v)=>Buffer.BlockCopy(BitConverter.GetBytes(v),0,b,o,2);
                Action<int,double> f64=(o,v)=>Buffer.BlockCopy(BitConverter.GetBytes(v),0,b,o,8);
                Action<int,string> str=(o,v)=>{i32(o+4,v.Length);byte[] t=Encoding.Unicode.GetBytes(v);Buffer.BlockCopy(t,0,b,o+8,t.Length);};
                byte[][] sig={new byte[]{0xF8,1,0x74,4,0x83,0x65},new byte[]{0x7d,0x15,0xa1,0,0,0,0,0x85,0xc0},new byte[]{0x48,0x83,0xf8,4,0x73,0x1e},new byte[]{0x5e,0x5f,0x5d,0xc3,0xa1,0,0,0,0,0x89,0,4}};
                int[] places={0x100,0x300,0x500,0x700};for(int j=0;j<4;j++)Buffer.BlockCopy(sig[j],0,b,places[j],sig[j].Length);
                ptr(0x100-12,0x904);ptr(0x904,0x1000);ptr(0x500-4,0x900);i32(0x900,2);
                ptr(0x300-11,0x908);ptr(0x90c,0x1600);ptr(0x705,0x920);i32(0x920,64000);
                ptr(0x1600+0x64,0x1800);ptr(0x1800+0x38,0x2000);ptr(0x1600+0x38,0x2000);
                ptr(0x1800+0x40,0x2500);f64(0x2500+0x1c,.91);
                ptr(0x2000+0x1c,0x2400);i32(0x2408,100);i32(0x240c,100^72);
                ptr(0x2000+0x28,0x3300);str(0x3300,"Fixture player");
                i32(0x2064,0);u16(0x2068,512);i32(0x2078,12846732);
                u16(0x2088,8);u16(0x208a,410);u16(0x208c,1);u16(0x2092,2);u16(0x2094,427);
                ptr(0x2038,0x2600);ptr(0x2604,0x2800);i32(0x260c,3);i32(0x2804,3);i32(0x2808,-10);i32(0x280c,0);i32(0x2810,10);
                ptr(0x1024,0x3000);str(0x3000,"Fixture song");ptr(0x1018,0x3100);str(0x3100,"Memory test");ptr(0x10ac,0x3200);str(0x3200,"Hard");
                ptr(0x106c,0x3400);str(0x3400,"fixture-hash");ptr(0x1090,0x3500);str(0x3500,"fixture.osu");ptr(0x1078,0x3600);str(0x3600,"Fixture folder");i32(0x10f8,750);
                Marshal.Copy(b,0,block,b.Length);Console.WriteLine("READY");Console.Out.Flush();string command;
                while((command=Console.ReadLine())!=null&&command!="quit"){
                    Marshal.WriteInt32(new IntPtr(start+0x900),command=="results"?7:command=="menu"?5:2);
                    if(command=="broken")Marshal.WriteInt32(new IntPtr(start+0x1838),0);
                    Console.WriteLine("OK");Console.Out.Flush();
                }
                return 0;
            }finally{VirtualFree(block,UIntPtr.Zero,0x8000);}
        }
    }
}

