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
   var st=StrategySim.Analyze(g,5f,4f,3,1.6f,1.5f,3f,1.8f); Console.WriteLine("     策略模拟："+st.Summary()); if(st.trapAfterReinforce||st.route==null){ fail++; Console.WriteLine("     [FAIL] 炸弹仍能困住马里奥 / 寻路走不通"); }
   foreach(var q in c.cells.Take(6)) Console.WriteLine($"     ({q.x},{q.y}) {q.text}"); foreach(var q in c.general.Take(3)) Console.WriteLine("     "+q);
  }
  foreach(var info in ElementCatalog.All){ if(info.ch=='.'||info.ch==' ') continue; var bg=ElementCatalog.EditorColor(info.ch); bg.a=1f; float r=ElementCatalog.ContrastRatio(bg,ElementCatalog.TextColorOn(bg)); if(r<3f){ Console.WriteLine($"[FAIL] 工坊格子文字对比度低 {info.ch} {r}"); fail++; } }
  var reg=AsciiElementRegistry.GetDefault(); foreach(char ch in reg.GetAllRegisteredChars()){ var i=ElementCatalog.Get(ch); if(i==null){ Console.WriteLine("[FAIL] 已登记但说明书没有: "+ch); fail++; } else if(reg.GetEntry(ch).elementName!=i.themeKey){ Console.WriteLine("[FAIL] 登记名与说明书 key 不一致: "+ch); fail++; } }
  foreach(var i in ElementCatalog.All) if(reg.GetEntry(i.ch)==null){ Console.WriteLine("[FAIL] 说明书有但没登记: "+i.ch); fail++; }
  int bad=0; for(int f=2;f<=FloorStacker.MaxFloors;f++) for(int s=0;s<3;s++){ var gg=FloorStacker.Build(f,s); if(!LevelWorkshopModel.Check(gg,true,solid).Playable) bad++; }
  Console.WriteLine($"[{(bad==0?"OK":"FAIL")}] 监狱塔 2..{FloorStacker.MaxFloors} 层 × 3 种子：不可玩 {bad}"); fail+=bad;
  // S208：新建关卡向导（8 个主角机关 × 3 种时长）全部可玩 + 炸弹困不住；8 个模式印章单独盖在空房间里都可玩；与网页结果逐字一致（wiz_web.json 由 verify.sh 用 node 生成）
  var webWiz=File.Exists("wiz_web.json")? (MiniJson.Parse(File.ReadAllText("wiz_web.json"),out _) as Dictionary<string,object>) : null;
  int wbad=0, wdiff=0;
  foreach(char star in LevelBlueprint.WizardStars) foreach(int sec in new[]{20,30,40}){
   var d=LevelBlueprint.Wizard(star,sec,""); var c=LevelWorkshopModel.Check(d.grid,true,solid); var st=StrategySim.Analyze(d.grid,5f,4f,3,1.6f,1.5f,3f,1.8f);
   if(!c.Playable||st.trapAfterReinforce||st.route==null){ wbad++; Console.WriteLine($"     [FAIL] 向导 {star} {sec}s：{c.Headline} / {st.Summary()}"); foreach(var q in c.cells.Take(3)) Console.WriteLine($"       ({q.x},{q.y}) {q.text}"); }
   if(webWiz!=null){ var k=$"wiz_{star}_{sec}"; var wg=webWiz.TryGetValue(k,out var o)? ((List<object>)o).Select(x=>(string)x).ToArray():null; if(wg==null||!wg.SequenceEqual(d.grid)){ wdiff++; Console.WriteLine($"     [FAIL] 向导 {k}：网页与 Unity 生成的不一样"); } }
  }
  Console.WriteLine($"[{(wbad==0&&wdiff==0?"OK":"FAIL")}] S208 新建关卡向导 {LevelBlueprint.WizardStars.Length}×3 关：不可玩 {wbad}，网页≠Unity {wdiff}{(webWiz==null?"（没找到 wiz_web.json，跳过对照）":"")}"); fail+=wbad+wdiff;
  { var b=LevelBlueprint.Wizard('~',20,"").grid.Select(r=>r).ToArray(); var row=b.Length-1-3; b[row]=new string(b[row].Select((ch,i)=> i>5&&i<b[row].Length-5&&"MGoT".IndexOf(ch)<0 ? '.' : ch).ToArray()); for(int r=b.Length-3;r<b.Length-1;r++) b[r]="W"+new string('#',b[r].Length-2)+"W";
    int pbad=0; foreach(var p in LevelBlueprint.Patterns){ var g=LevelBlueprint.Stamp(b,p,12,3); if(!LevelWorkshopModel.Check(g,true,solid).Playable){ pbad++; Console.WriteLine("     [FAIL] 印章 "+p.zh+" 盖在空房间里不可玩"); } }
    Console.WriteLine($"[{(pbad==0?"OK":"FAIL")}] S208 模式印章 {LevelBlueprint.Patterns.Length} 个单独盖章：不可玩 {pbad}"); fail+=pbad; }
  // S209：按马里奥 AI 的走法（身体宽 0.8、同一条转向规则 LevelPathPlanner.SteerX）走一遍：所有样板 + 监狱塔 + 向导关卡都要走得完
  { int rbad=0, rt=0; var all=Samples().ToList();
    for(int f=2;f<=FloorStacker.MaxFloors;f++) for(int s=0;s<3;s++) all.Add(($"监狱塔{f}层#{s}",FloorStacker.Build(f,s)));
    foreach(char star in LevelBlueprint.WizardStars) foreach(int sec in new[]{20,30,40}) all.Add(($"向导{star}{sec}s",LevelBlueprint.Wizard(star,sec,"").grid));
    foreach(var (n,g) in all){ rt++; var w=LevelRouteFollower.Run(g); if(!w.ok){ rbad++; Console.WriteLine($"     [FAIL] {n}：{w.Summary}"); } }
    bool repro=!LevelRouteFollower.Run(LevelWorkshopModel.HakoniwaSample,true).ok;
    Console.WriteLine($"[{(rbad==0&&repro?"OK":"FAIL")}] S209 按马里奥走法走一遍 {rt} 关：走不完 {rbad}（旧规则复现截图卡住：{(repro?"是":"否")}）"); fail+=rbad+(repro?0:1); }
  // S210：小镇大地图。样板能玩 + 无人捣乱时一天走得完（按真实身体/速度走）+ .txt/JSON 往返一致 + 一组调参都走得完 + 反例能被检查出来 + 与网页 owCheck 逐字一致（ow_web.json 由 verify.sh 用 node 生成）
  { int obad=0; var samp=OverworldPack.Parse(OverworldPack.SampleText); var m=samp.Count==1?samp[0]:null;
    if(m==null){ obad++; Console.WriteLine("     [FAIL] 样板小镇解析失败"); }
    else {
      var r=OverworldMap.Rules.Default; var rep=OverworldMap.Check(m,r);
      if(!rep.Playable){ obad++; foreach(var i in rep.issues) Console.WriteLine("     [FAIL] 样板小镇："+i); }
      var day=OverworldWalker.SimulateDay(m,r); if(!day.ok){ obad++; Console.WriteLine("     [FAIL] 样板小镇一天没走完："+day.summary); }
      for(int k=0;k<rep.schedule.stops.Count;k++) if(OverworldMap.AmbushLead(rep.schedule,k,r.minutesPerSecond)<5){ obad++; Console.WriteLine($"     [FAIL] 样板门 {rep.schedule.stops[k].door.n} 你来不及埋伏"); }
      if(OverworldMap.ToText(OverworldMap.Parse(OverworldMap.ToText(m)))!=OverworldMap.ToText(m)){ obad++; Console.WriteLine("     [FAIL] 小镇 .txt 往返不一致"); }
      var pk=OverworldPack.Parse("{\"levels\":[],\"overworlds\":["+OverworldMap.ToJson(m)+"]}"); if(pk.Count!=1||OverworldMap.ToText(pk[0])!=OverworldMap.ToText(m)){ obad++; Console.WriteLine("     [FAIL] 小镇 JSON 往返不一致"); }
      if(LevelPack.Parse("{\"levels\":["+OverworldMap.ToJson(m)+"]}",c=>true,out _)?.Count>0){ obad++; Console.WriteLine("     [FAIL] 横版关卡包误把小镇当成房间"); }
      foreach(var ms in new[]{2.8,3.4,4.0}) foreach(var mp in new[]{3.0,4.0,6.0}){ var rr=r; rr.marioSpeed=ms; rr.minutesPerSecond=mp; var dd=OverworldWalker.SimulateDay(m,rr); if(!dd.ok){ obad++; Console.WriteLine($"     [FAIL] 调参 速度{ms} 每秒{mp}分钟：{dd.summary}"); } }
      // 反例：把桥变成水 → 右半边走不到，必须报错
      var broken=OverworldMap.Parse(OverworldMap.ToText(m)); int row=broken.H-1-14; broken.rows[row]=broken.rows[row].Substring(0,21)+"ww"+broken.rows[row].Substring(23); int row2=broken.H-1-19; broken.rows[row2]=broken.rows[row2].Substring(0,21)+"ww"+broken.rows[row2].Substring(23);
      if(OverworldMap.Check(broken,r).Playable){ obad++; Console.WriteLine("     [FAIL] 拆了桥检查却没报错"); }
      // 网页对照
      var web=File.Exists("ow_web.json")? (MiniJson.Parse(File.ReadAllText("ow_web.json"),out _) as Dictionary<string,object>) : null; int wd=0;
      if(web!=null){
        var cases=new List<(string,OverworldMap.Map)>{("sample",m),("broken",broken)};
        foreach(var (k,mm) in cases){ var cr=OverworldMap.Check(mm,r); var mine=cr.issues.Select(i=>i.sev+" "+i).ToList(); if(cr.schedule!=null){ mine.AddRange(cr.schedule.stops.Select(s=>$"stop {s.door.n} {OverworldMap.Clock(s.arrive)} {OverworldMap.Clock(s.leave)} {string.Join(";",s.path)}")); mine.Add("home "+OverworldMap.Clock(cr.schedule.homeArrive)); }
          var theirs=web.TryGetValue(k,out var o)? ((List<object>)o).Select(x=>(string)x).ToList():null;
          if(theirs==null||!theirs.SequenceEqual(mine)){ wd++; Console.WriteLine($"     [FAIL] 小镇检查 {k}：网页≠Unity"); if(theirs!=null) foreach(var x in mine.Except(theirs).Take(3)) Console.WriteLine("       Unity: "+x); if(theirs!=null) foreach(var x in theirs.Except(mine).Take(3)) Console.WriteLine("       网页: "+x); } }
      }
      obad+=wd;
      Console.WriteLine($"[{(obad==0?"OK":"FAIL")}] S210 小镇 {m.name}：{rep.Headline}｜{day.summary}｜网页对照{(web==null?"跳过（没找到 ow_web.json）":wd==0?"一致":"不一致")}");
    }
    fail+=obad; }
  // S211：小镇↔房间切换节奏：黑屏里加载、加载好才激活且只激活一次、加载卡住也不会永远黑屏、重复按不叠加
  { int tb=0; var p=new SceneTransitPlan(); if(!p.Begin("a")||p.Begin("b")) tb++;
    float tt=0; int acts=0; bool readyAt(float x)=>x>=1.5f; while(p.Busy&&tt<30){ float dt=1f/60; tt+=dt; if(p.Tick(dt,readyAt(tt))){ acts++; if(tt<1.5f) tb++; p.Activated(); } }
    if(acts!=1||p.Busy) tb++;
    var q=new SceneTransitPlan(); q.Begin("c"); float t2=0; bool a2=false; while(!a2&&t2<30){ t2+=0.05f; a2=q.Tick(0.05f,false);} if(!a2||t2>q.fadeOutSeconds+q.maxLoadSeconds+0.2f) tb++;
    var r=new SceneTransitPlan(); r.Begin("d"); float t3=0; bool a3=false; while(!a3){ t3+=1f/60; a3=r.Tick(1f/60,true);} r.Activated(); while(r.Busy){ t3+=1f/60; r.Tick(1f/60,true);} 
    Console.WriteLine($"[{(tb==0?"OK":"FAIL")}] S211 场景切换节奏：加载瞬间完成时一次切换 {t3:0.00} 秒；加载卡住 {t2:0.0} 秒后仍会揭幕"); fail+=tb; }
  // S212：转场缓入缓出 + 卡顿帧不跳；地图指引（边缘箭头、赛跑提示、时间滑条）按日程走；时间滑条与网页 owMarioAt 逐字一致（ow_scrub.json 由 verify.sh 用 node 生成）
  { int gb=0; var p=new SceneTransitPlan(); p.Begin("x"); p.Tick(1f,true); p.Tick(0.5f,true); p.Activated(); p.Tick(p.Step(0.8f),true); if(p.Alpha<0.5f){ gb++; Console.WriteLine("     [FAIL] 激活卡顿帧让淡入跳了一大截"); }
    if(SceneTransitPlan.Ease(0.1f)>=0.1f||SceneTransitPlan.Ease(0.9f)<=0.9f) gb++;
    var ar=OverworldGuide.EdgeArrow(5f,5f); if(ar.onScreen||Math.Abs(ar.x-0.94f)>1e-3||Math.Abs(ar.y-0.94f)>1e-3) { gb++; Console.WriteLine("     [FAIL] 边缘箭头没贴边"); }
    var m=OverworldPack.Parse(OverworldPack.SampleText)[0]; var r=OverworldMap.Rules.Default; var sc=OverworldMap.Check(m,r).schedule;
    var T=OverworldMap.Find(m,'T')[0]; var M=OverworldMap.Find(m,'M')[0];
    var ra=OverworldGuide.RaceTo(m,r,sc.stops[0].cell,sc.stops[0].door.minute,OverworldMap.DayStart,T.x+0.5,T.y+0.5,M.x+0.5,M.y+0.5,false);
    double lead=OverworldMap.AmbushLead(sc,0,r.minutesPerSecond); if(ra.verdict!=OverworldGuide.Verdict.Ahead||Math.Abs(ra.margin-lead)>0.01){ gb++; Console.WriteLine($"     [FAIL] 赛跑提示 {ra.margin:0.00} 秒 ≠ 检查里的提前量 {lead:0.00} 秒"); }
    // 时间滑条：每一分钟他都在能走的格子上 / 门里 / 家里，且走路时一步不超过 1 格
    OverworldMap.Cell prev=M; var lines=new List<string>();
    for(int t=OverworldMap.DayStart;t<=OverworldMap.DayEnd;t+=1){ var w=OverworldGuide.MarioAt(m,sc,t,r.marioSpeed,r.minutesPerSecond); if(t%20==0) lines.Add(OverworldMap.Clock(t)+" "+w.cell.x+","+w.cell.y+" "+w.what);
      if(!OverworldMap.Walkable(m,w.cell.x,w.cell.y)){ gb++; Console.WriteLine($"     [FAIL] {OverworldMap.Clock(t)} 他在挡路格 {w.cell}"); break; }
      if(w.insideDoor==0 && Math.Abs(w.cell.x-prev.x)+Math.Abs(w.cell.y-prev.y)>2 && !w.cell.Equals(M)){ gb++; Console.WriteLine($"     [FAIL] {OverworldMap.Clock(t)} 他瞬移 {prev}→{w.cell}"); break; }
      prev=w.cell; }
    var web=File.Exists("ow_scrub.json")? (MiniJson.Parse(File.ReadAllText("ow_scrub.json"),out _) as List<object>)?.Select(x=>(string)x).ToList() : null;
    bool same=web!=null&&web.SequenceEqual(lines); if(web!=null&&!same){ gb++; foreach(var x in lines.Except(web).Take(3)) Console.WriteLine("       Unity: "+x); foreach(var x in web.Except(lines).Take(3)) Console.WriteLine("       网页: "+x); }
    Console.WriteLine($"[{(gb==0?"OK":"FAIL")}] S212 转场+地图指引：第一扇门你比他早 {ra.margin:0.0} 秒；时间滑条 {lines.Count} 个时刻{(web==null?"（网页对照跳过）":same?"，网页一致":"，网页不一致")}"); fail+=gb; }
  // S213：玩家视角模拟——6 种机器人玩家（跟箭头站着按 E / 会躲 / 反应慢 / 贪道具 / 捣蛋 / 挂机）+ 乱按 100 天，用和游戏同一份 OverworldTown 规则玩一天
  { int pb=0; var m=OverworldPack.Parse(OverworldPack.SampleText)[0]; var t=MarioMindTuningSO.LoadOrDefault(); var sum=new List<string>();
    foreach(OverworldBots.Kind k in Enum.GetValues(typeof(OverworldBots.Kind))){ if(k==OverworldBots.Kind.Chaos) continue;
      int am=0,mi=0,ca=0; double lg=0,rs=0; for(int s=1;s<=3;s++){ var r=OverworldBots.PlayDay(m,t,k,true,s); am+=r.ambush; mi+=r.missed; ca+=r.caught; lg=Math.Max(lg,r.longestIdle); rs+=r.realSeconds; if(!r.dayEnded){ pb++; Console.WriteLine($"     [FAIL] {k} 一天没结束"); } }
      sum.Add($"{k} 埋伏{am}/{3*m.doors.Count} 被抓{ca} {rs/3:0}秒");
      if(lg>6){ pb++; Console.WriteLine($"     [FAIL] {k}：用了快进还干等 {lg:0.0} 秒"); }
      if((k==OverworldBots.Kind.Hider||k==OverworldBots.Kind.Prankster) && am<3*m.doors.Count){ pb++; Console.WriteLine($"     [FAIL] {k}（会躲的玩家）没能全部埋伏：{am}"); }
      if(k==OverworldBots.Kind.Slow && ca>3){ pb++; Console.WriteLine($"     [FAIL] 反应慢的玩家 3 天被抓 {ca} 次（出门缓冲不够）"); }
      if(k==OverworldBots.Kind.Follower && am>=3*m.doors.Count){ pb++; Console.WriteLine("     [FAIL] 站在门口不躲也能全胜：躲藏没意义"); }
      if(k==OverworldBots.Kind.Idle && (am>0||mi<3*m.doors.Count)){ pb++; Console.WriteLine("     [FAIL] 挂机结果不对"); } }
    int ne=0,mc=0; for(int s=1;s<=100;s++){ var r=OverworldBots.PlayDay(m,t,OverworldBots.Kind.Chaos,s%2==0,s); if(!r.dayEnded) ne++; mc=Math.Max(mc,r.caught); } if(ne>0||mc>4){ pb++; Console.WriteLine($"     [FAIL] 乱按 100 天：没结束 {ne}，最多被抓 {mc}"); }
    var noFF=OverworldBots.PlayDay(m,t,OverworldBots.Kind.Hider,false,1); var ff=OverworldBots.PlayDay(m,t,OverworldBots.Kind.Hider,true,1);
    Console.WriteLine($"[{(pb==0?"OK":"FAIL")}] S213 玩家视角模拟：{string.Join("｜",sum)}｜乱按 100 天全部结束、最多被抓 {mc}｜一天 {noFF.realSeconds:0} 秒 → 快进 {ff.realSeconds:0} 秒"); fail+=pb; }
  Console.WriteLine(fail==0?"SIM ALL OK":"SIM FAILURES: "+fail);
  Environment.Exit(fail==0?0:1);
 }}
