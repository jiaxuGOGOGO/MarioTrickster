using System; using System.Linq; using System.Collections.Generic;
public static class Stats {
  public static (double lo,double hi) Wilson(int k,int n,double z=1.96){ if(n==0) return (0,1); double p=(double)k/n, d=1+z*z/n, c=p+z*z/(2*n), h=z*Math.Sqrt(p*(1-p)/n+z*z/(4.0*n*n)); return ((c-h)/d,(c+h)/d); }
  public static void Run(){
    var t=MarioMindTuningSO.LoadOrDefault();
    var maps=new (string n,string s)[]{("Sample",OverworldPack.SampleText),("Big",OverworldPack.BigSampleText),("Mountain",OverworldPack.MountainSampleText),("Storm",OverworldPack.StormSampleText)};
    int N=60;
    foreach(var (n,s) in maps) foreach(var k in new[]{OverworldBots.Kind.Hider,OverworldBots.Kind.Prankster}){
      var sig=new HashSet<string>(); var tim=new HashSet<double>(); int full=0,am=0,doors=0,ca=0,ended=0; var sw=System.Diagnostics.Stopwatch.StartNew();
      for(int seed=1;seed<=N;seed++){ var r=OverworldBots.PlayDay(OverworldPack.Parse(s)[0],t,k,true,seed,1+(seed-1)%4,true); sig.Add($"{r.ambush}/{r.caught}"); tim.Add(Math.Round(r.realSeconds,1)); am+=r.ambush; doors+=r.doors; ca+=r.caught; if(r.ambush==r.doors) full++; if(r.dayEnded) ended++; }
      OverworldSession.ResetStatics(); var w=Wilson(full,N); var wa=Wilson(am,doors);
      Console.WriteLine($"{n,-8} {k,-9} distinct={sig.Count,2} times={tim.Count,2} fullDays {full}/{N} [{Math.Max(0,w.lo):0.00},{w.hi:0.00}] doors {am}/{doors} [{wa.lo:0.00},{wa.hi:0.00}] caught/day {ca/(double)N:0.00} ended {ended}/{N} {sw.ElapsedMilliseconds/N}ms");
    }
  }
}
