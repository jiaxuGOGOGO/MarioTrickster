// MarioTrickster 纯逻辑快速体检（不需要 Unity）：样板/默认房间可玩性、寻路、连招路线、说明书一致性、文字对比度、监狱塔。
// 用法：bash verify.sh 会自动编译运行。新增样板后在 Samples() 里加一行。
using System; using System.IO; using System.Linq; using System.Collections.Generic;
static class CHECK {
 static LevelPathPlanner.Cell F(string[] g,char c){ for(int r=0;r<g.Length;r++){int x=g[r].IndexOf(c); if(x>=0) return new LevelPathPlanner.Cell(x,g.Length-1-r);} return new LevelPathPlanner.Cell(-1,-1);}
 static IEnumerable<(string,string[])> Samples(){
   yield return ("默认恶作剧房间", File.ReadAllText("room_template.txt").Replace("\r","").TrimEnd('\n').Split('\n'));
   // 自动发现 LevelWorkshopModel 里所有 public static string[] XxxSample（新增样板不用改这里）
   foreach(var f in typeof(LevelWorkshopModel).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static))
     if(f.FieldType==typeof(string[]) && f.Name.EndsWith("Sample")) yield return (f.Name,(string[])f.GetValue(null));
 }
 static void Main(){
  int fail=0;
  Func<char,bool> solid=AsciiElementRegistry.GetDefault().IsSolid;
  foreach(var (n,g) in Samples()){
   var c=LevelWorkshopModel.Check(g,true,solid);
   var m=F(g,'M'); var o=F(g,'o'); var e=F(g,'G');
   Console.WriteLine($"[{(c.Playable?"OK":"FAIL")}] {n}: {c.Headline} | M→宝 {LevelPathPlanner.Path(g,m,o)?.Count} 宝→出口 {LevelPathPlanner.Path(g,o,e)?.Count} | {ComboRouteAnalyzer.Analyze(g,10f).Summary}");
   if(!c.Playable) fail++;
   foreach(var q in c.cells.Take(6)) Console.WriteLine($"     ({q.x},{q.y}) {q.text}"); foreach(var q in c.general.Take(3)) Console.WriteLine("     "+q);
  }
  foreach(var info in ElementCatalog.All){ if(info.ch=='.'||info.ch==' ') continue; var bg=ElementCatalog.EditorColor(info.ch); bg.a=1f; float r=ElementCatalog.ContrastRatio(bg,ElementCatalog.TextColorOn(bg)); if(r<3f){ Console.WriteLine($"[FAIL] 工坊格子文字对比度低 {info.ch} {r}"); fail++; } }
  var reg=AsciiElementRegistry.GetDefault(); foreach(char ch in reg.GetAllRegisteredChars()){ var i=ElementCatalog.Get(ch); if(i==null){ Console.WriteLine("[FAIL] 已登记但说明书没有: "+ch); fail++; } else if(reg.GetEntry(ch).elementName!=i.themeKey){ Console.WriteLine("[FAIL] 登记名与说明书 key 不一致: "+ch); fail++; } }
  foreach(var i in ElementCatalog.All) if(reg.GetEntry(i.ch)==null){ Console.WriteLine("[FAIL] 说明书有但没登记: "+i.ch); fail++; }
  int bad=0; for(int f=2;f<=FloorStacker.MaxFloors;f++) for(int s=0;s<3;s++){ var gg=FloorStacker.Build(f,s); if(!LevelWorkshopModel.Check(gg,true,solid).Playable) bad++; }
  Console.WriteLine($"[{(bad==0?"OK":"FAIL")}] 监狱塔 2..{FloorStacker.MaxFloors} 层 × 3 种子：不可玩 {bad}"); fail+=bad;
  Console.WriteLine(fail==0?"SIM ALL OK":"SIM FAILURES: "+fail);
  Environment.Exit(fail==0?0:1);
 }}
