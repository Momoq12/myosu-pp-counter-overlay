// SPDX-License-Identifier: LGPL-3.0-or-later
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

namespace NeonReader {
    public sealed class PpResult {public string checksum;public int mods,hits;public double pp,stars;}
    public sealed class PpCalculator : IDisposable {
        readonly Func<Snapshot> get;volatile bool stopped;Thread thread;Process node;
        public volatile PpResult Latest;
        public PpCalculator(Func<Snapshot> getter){get=getter;}
        public void Start(){thread=new Thread(Run);thread.IsBackground=true;thread.Name="offline PP calculator";thread.Start();}
        public static int Hits(Snapshot s){return s.n300+s.n100+s.n50+s.nMiss+(s.mode==3?s.nGeki+s.nKatu:0);}
        string Resolve(Snapshot s){
            using(Process p=Process.GetProcessById(s.pid)){
                string root=Path.GetDirectoryName(p.MainModule.FileName),songs=Path.Combine(root,"Songs");
                string config=Path.Combine(root,"osu!."+Environment.UserName+".cfg");
                if(File.Exists(config))foreach(string line in File.ReadLines(config))if(line.StartsWith("BeatmapDirectory",StringComparison.OrdinalIgnoreCase)){
                    int at=line.IndexOf('=');if(at>=0){string setting=line.Substring(at+1).Trim();if(setting.Length>0)songs=Path.IsPathRooted(setting)?setting:Path.Combine(root,setting);}break;
                }
                string path=Path.GetFullPath(Path.Combine(songs,s.folder,s.filename));
                if(!path.StartsWith(Path.GetFullPath(songs).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)||!File.Exists(path))throw new IOException("Beatmap file not found");return path;
            }
        }
        void Run(){
            JavaScriptSerializer json=new JavaScriptSerializer();string last="",path="",map="";int pid=0;
            while(!stopped){
                try{
                    Snapshot s=get();
                    if(!s.statsAvailable||s.demo||!s.connected){Latest=null;Thread.Sleep(150);continue;}
                    string key=s.checksum+"|"+s.mods+"|"+s.maxCombo+"|"+s.score+"|"+s.n300+"|"+s.n100+"|"+s.n50+"|"+s.nMiss+"|"+s.nGeki+"|"+s.nKatu;
                    if(key==last){Thread.Sleep(100);continue;}
                    if(map!=s.checksum||pid!=s.pid){path=Resolve(s);map=s.checksum;pid=s.pid;}
                    if(node==null||node.HasExited){
                        node=new Process{StartInfo=new ProcessStartInfo(Path.Combine(Program.Root,"calculator","node.exe"),"\""+Path.Combine(Program.Root,"calculator","worker.js")+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}};
                        node.Start();node.ErrorDataReceived+=(o,e)=>{if(e.Data!=null)Program.Log("PP: "+e.Data);};node.BeginErrorReadLine();
                    }
                    node.StandardInput.WriteLine(json.Serialize(new{path=path,mode=s.mode,mods=s.mods,maxCombo=s.maxCombo,n300=s.n300,n100=s.n100,n50=s.n50,nGeki=s.nGeki,nKatu=s.nKatu,nMiss=s.nMiss,score=s.score,playTime=s.playTime}));
                    string response=node.StandardOutput.ReadLine();if(response==null)throw new IOException("PP calculator exited");
                    var result=json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(response);
                    if(result.ContainsKey("error"))throw new IOException("PP: "+result["error"]);
                    Latest=new PpResult{checksum=s.checksum,mods=s.mods,hits=Hits(s),pp=Convert.ToDouble(result["pp"]),stars=Convert.ToDouble(result["stars"])};
                    last=key;Thread.Sleep(100);
                }catch(Exception ex){Latest=null;Program.Log(ex.Message);Thread.Sleep(1500);}
            }
        }
        public void Dispose(){stopped=true;if(node!=null){try{if(!node.HasExited)node.Kill();}catch{} }if(thread!=null)thread.Join(2000);if(node!=null)node.Dispose();}
    }
}

