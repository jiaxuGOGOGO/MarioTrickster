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
  // S214：网页 ↔ Unity 同步往返：网页关卡包（含还没实现的新机制字符）→ Unity 关卡库 .txt → 网页读回（sync_back.json 由 verify.sh 用网页的 syLevelFromTxt 解析）必须和原来一模一样；Unity 再存一次也不丢新机制
  { int sb=0; var reg2=AsciiElementRegistry.GetDefault();
    var grid=new List<string>(LevelWorkshopModel.LureSample); int r0=grid.Count-3; var ch=grid[r0].ToCharArray(); int px=Array.IndexOf(ch,'.'); ch[px]='Ж'; grid[r0]=new string(ch);
    string json="{\"type\":\"mariotrickster-levelpack\",\"levels\":[{\"id\":\"L1\",\"name\":\"同步测试\",\"goal\":\"目标\",\"grid\":["+string.Join(",",grid.Select(g=>"\""+g.Replace("\\","\\\\").Replace("\"","\\\"")+"\""))+"],\"notes\":[{\"x\":2,\"y\":3,\"text\":\"批注\"}]}],\"proposals\":[{\"c\":\"Ж\",\"zh\":\"磁铁\"}]}";
    var lv=LevelPack.Parse(json,cc=>reg2.GetEntry(cc)!=null||Step1Layout.Slots.ContainsKey(cc),out string er); string txt=lv!=null&&lv.Count==1?LevelPack.ToText(lv[0]):"";
    if(!txt.Contains("# Pending: Ж=磁铁")){ sb++; Console.WriteLine("     [FAIL] 新机制没写进 # Pending"); }
    File.WriteAllText("sync_level.txt",txt); File.WriteAllText("sync_grid.json","["+string.Join(",",grid.Select(g=>"\""+g.Replace("\\","\\\\").Replace("\"","\\\"")+"\""))+"]");
    // Unity 里再改一格、再存：新机制那格没动 → 仍保留
    var again=new LevelPack.Level{ name="同步测试", rows=lv[0].rows.ToArray() }; LevelWorkshopModel.CarryPending(txt,again); if(!LevelPack.ToText(again).Contains("# Pending: Ж=磁铁")){ sb++; Console.WriteLine("     [FAIL] Unity 再存一次把网页新机制弄丢了"); }
    var rows2=lv[0].rows.ToArray(); int rr=rows2.Length-1-(lv[0].pending['Ж'][0].Item2); var c2=rows2[rr].ToCharArray(); c2[lv[0].pending['Ж'][0].Item1]='#'; rows2[rr]=new string(c2);
    var over=new LevelPack.Level{ name="同步测试", rows=rows2 }; LevelWorkshopModel.CarryPending(txt,over); if(over.pending.ContainsKey('Ж')){ sb++; Console.WriteLine("     [FAIL] 那格在 Unity 里画了别的东西，新机制却还留着"); }
    if(!LevelWorkshopModel.SameGrid(txt,string.Join("\n",lv[0].rows)+"\n# Name: x")){ sb++; Console.WriteLine("     [FAIL] SameGrid 应忽略元数据行"); }
    string web=File.Exists("sync_back.json")?File.ReadAllText("sync_back.json").Trim():null; bool same=web!=null&&web=="ok";
    if(web!=null&&!same){ sb++; Console.WriteLine("     [FAIL] 网页读回 Unity 关卡库：" + web); }
    Console.WriteLine($"[{(sb==0?"OK":"FAIL")}] S214 网页↔Unity 同步往返：新机制字符、批注、目标都保留{(web==null?"（网页读回对照：下次 verify）":same?"，网页读回一模一样":"")}"); fail+=sb; }
  // S215：一天总览（CampaignLedger）与网页 owLedger 逐字一致（ow_ledger.json 由 verify.sh 生成）；样板小镇该提醒的要提醒
  { int lb=0; var m=OverworldPack.Parse(OverworldPack.SampleText)[0];
    string[] Res(string n){ if(n==LevelWorkshopModel.DefaultRoomName) return Step1PrankRoomBuilderRoom(); foreach(var s in LevelWorkshopModel.SampleRooms) if(s.name==n) return s.rows; return null; }
    var rep=CampaignLedger.Build(m,n=>Res(n),3,3); var mine=CampaignLedger.Lines(rep);
    if(rep.rooms.Count!=4||rep.rooms.Any(r=>r.missing||r.total==0)){ lb++; Console.WriteLine("     [FAIL] 总览：房间没找全"); }
    if(!rep.warnings.Any(w=>w.Contains("一次教太多"))){ lb++; Console.WriteLine("     [FAIL] 总览：第一扇门 8 种新机关应提醒"); }
    var web=File.Exists("ow_ledger.json")?(MiniJson.Parse(File.ReadAllText("ow_ledger.json"),out _) as List<object>)?.Select(x=>(string)x).ToList():null;
    bool same=web!=null&&web.SequenceEqual(mine); if(web!=null&&!same){ lb++; foreach(var x in mine.Except(web).Take(3)) Console.WriteLine("       Unity: "+x); foreach(var x in web.Except(mine).Take(3)) Console.WriteLine("       网页: "+x); }
    Console.WriteLine($"[{(lb==0?"OK":"FAIL")}] S215 一天总览：{string.Join("｜",rep.rooms.Select(r=>$"门{r.door} 主角{r.star} 新{r.firstTime.Count}"))}｜提醒 {rep.warnings.Count} 条{(web==null?"（网页对照跳过）":same?"，网页一致":"，网页不一致")}"); fail+=lb; }
  // S216：被弹飞/打飞的抛物线（用游戏同一份 Step1Feel.StunStep 模拟）——高度要装得进房间、距离要读得懂
  { int fb=0; var t=MarioMindTuningSO.LoadOrDefault(); float g=t.launchGravity, dr=t.launchAirDrag, gf=t.launchGroundFriction;
    var cases=new (string zh, UnityEngine.Vector2 v, float minApex, float maxApex, float maxRange)[]{
      ("弹簧板", new UnityEngine.Vector2(t.springForwardPush, t.springLaunchSpeed), 2f, ElementCatalog.SpringHeadroomCells-1f, 3f),
      ("炸弹(中心)", new UnityEngine.Vector2(t.bombKnockback, Math.Max(t.bombKnockback*0.6f, t.blastLift)), 0.4f, 2f, 4f),
      ("人肉大炮", new UnityEngine.Vector2(18.5f*0.766f, 18.5f*0.643f), 1f, 3.5f, 12f),
      ("炮弹命中", new UnityEngine.Vector2(7f, KnockbackHelperLift(3f,t.hurtLift)), 0.2f, 1f, 3f),
      ("火/刺受伤", new UnityEngine.Vector2(5f, KnockbackHelperLift(2f,t.hurtLift)), 0.2f, 1f, 3f) };
    var parts=new List<string>();
    foreach(var cs in cases){ var arc=Step1Feel.Simulate(cs.v,g,40f,dr,gf); float total=arc.range+arc.slideAfter;
      bool ok=arc.apex>=cs.minApex&&arc.apex<=cs.maxApex&&total<=cs.maxRange; if(!ok){fb++; Console.WriteLine($"     [FAIL] {cs.zh}: 高 {arc.apex:0.0} 格（要 {cs.minApex}–{cs.maxApex}），远 {total:0.0} 格（≤{cs.maxRange}）");}
      parts.Add($"{cs.zh} 高{arc.apex:0.0}远{total:0.0}"); }
    // 旧 bug 的对照：没有重力往上飞（只为报告）
    float oldSpring=t.springLaunchSpeed*t.springAirStunSeconds;
    if(Step1Feel.StunOver(0.1f,true,false,1.5f)||!Step1Feel.StunOver(-0.1f,true,true,1.5f)||!Step1Feel.StunOver(-1.6f,true,false,1.5f)){fb++;Console.WriteLine("     [FAIL] 落地才恢复控制 / 最多多等 1.5 秒");}
    if(Step1Feel.TelegraphRate(8f,1f)<=Step1Feel.TelegraphRate(8f,0f)){fb++;Console.WriteLine("     [FAIL] 预警越来越急");}
    Console.WriteLine($"[{(fb==0?"OK":"FAIL")}] S216 抛物线：{string.Join("｜",parts)}（以前弹簧硬直期没重力：0.6 秒匀速上飘 {oldSpring:0.0} 格）"); fail+=fb; }
  // S217：大世界扩展（OverworldMap.Resize）——扩展后老镇原样、外圈是树、仍可玩且一天走得完；各方向 + 裁剪 + 上限；与网页 owResize 逐字一致（ow_resize.json 由 verify.sh 生成）
  { int rb=0; var parts=new List<string>(); var r=OverworldMap.Rules.Default;
    var cases=new (string k,int l,int rr,int t,int b)[]{("all8",8,8,8,8),("east16",0,16,0,0),("west16",16,0,0,0),("north16",0,0,16,0),("south16",0,0,0,16),("x2",0,44,0,32),("crop4",-4,-4,-4,-4),("crop1",-1,-1,-1,-1)};
    var mine=new Dictionary<string,string>();
    foreach(var cs in cases){ var m=OverworldPack.Parse(OverworldPack.SampleText)[0]; int w0=m.W,h0=m.H; var old=m.rows.ToArray();
      var res=OverworldMap.Resize(m,cs.l,cs.rr,cs.t,cs.b); mine[cs.k]=res.ok?string.Join("\n",m.rows)+"|"+res.lost+"|"+string.Join(";",m.notes.Select(n=>n.x+","+n.y)):"x "+res.why;
      if(!res.ok){ rb++; Console.WriteLine($"     [FAIL] 扩展 {cs.k}：{res.why}"); continue; }
      if(m.W!=w0+cs.l+cs.rr||m.H!=h0+cs.t+cs.b){ rb++; Console.WriteLine($"     [FAIL] 扩展 {cs.k}：尺寸不对 {m.W}×{m.H}"); }
      bool frame=true; for(int x=0;x<m.W;x++) if(!OverworldCatalog.Solid(m.rows[0][x])||!OverworldCatalog.Solid(m.rows[m.H-1][x])) frame=false; for(int y=0;y<m.H;y++) if(!OverworldCatalog.Solid(m.rows[y][0])||!OverworldCatalog.Solid(m.rows[y][m.W-1])) frame=false;
      if(!frame){ rb++; Console.WriteLine($"     [FAIL] 扩展 {cs.k}：外圈有缺口（会走出地图，H1）"); }
      if(cs.l>=0&&cs.t>=0){ int diff=0; for(int y=1;y<h0-1;y++) for(int x=1;x<w0-1;x++) if(old[y][x]!=m.rows[y+cs.t][x+cs.l]) diff++; if(diff>0){ rb++; Console.WriteLine($"     [FAIL] 扩展 {cs.k}：老镇里面变了 {diff} 格"); } }
      if(res.lost>0){ var cr=OverworldMap.Check(m,r); parts.Add($"{cs.k} {m.W}×{m.H} 裁掉{res.lost}格→{(cr.Playable?"仍可玩":"检查报红")}"); continue; } // 裁掉东西：编辑器会先问，检查会报红——不要求可玩
      var rep=OverworldMap.Check(m,r); if(!rep.Playable){ rb++; Console.WriteLine($"     [FAIL] 扩展 {cs.k} 后不可玩：{string.Join(" / ",rep.issues.Where(i=>i.sev==OverworldMap.Sev.Error).Take(2))}"); continue; }
      var day=OverworldWalker.SimulateDay(m,r); if(!day.ok){ rb++; Console.WriteLine($"     [FAIL] 扩展 {cs.k} 后一天走不完：{day.summary}"); }
      if(cs.l>0){ var mapx=OverworldMap.Parse(OverworldMap.ToText(m)); bool open=OverworldMap.Walkable(mapx,cs.l,mapx.H/2)||!OverworldCatalog.Solid(mapx.rows[mapx.H/2][cs.l]); if(!open&&old[old.Length/2][0]=='t'){ rb++; Console.WriteLine($"     [FAIL] 扩展 {cs.k}：老围栏没拆，新地和老镇不连通"); } }
      parts.Add($"{cs.k} {m.W}×{m.H}"); }
    // 上限 / 下限 / 大世界：拼一张 192×128 仍能检查、机器人一天走完
    { var m=OverworldPack.Parse(OverworldPack.SampleText)[0]; var big=OverworldMap.Resize(m,0,OverworldMap.MaxW-m.W,0,OverworldMap.MaxH-m.H); var sw=System.Diagnostics.Stopwatch.StartNew(); var rep=OverworldMap.Check(m,r); var bot=OverworldBots.PlayDay(m,MarioMindTuningSO.LoadOrDefault(),OverworldBots.Kind.Hider,true,1); OverworldSession.ResetStatics(); sw.Stop();
      if(!big.ok||!rep.Playable||!bot.dayEnded){ rb++; Console.WriteLine($"     [FAIL] 最大 {OverworldMap.MaxW}×{OverworldMap.MaxH}：{big.why} 可玩={rep.Playable} 一天结束={bot.dayEnded}"); }
      if(OverworldMap.Resize(m,1,0,0,0).ok){ rb++; Console.WriteLine("     [FAIL] 超过上限应拒绝"); }
      var sm=OverworldPack.Parse(OverworldPack.SampleText)[0]; if(OverworldMap.Resize(sm,-20,-20,0,0).ok){ rb++; Console.WriteLine("     [FAIL] 小于下限应拒绝"); }
      parts.Add($"最大 {m.W}×{m.H} 检查+机器人一天 {sw.ElapsedMilliseconds}ms"); }
    // 裁掉门 → 报 lost，并且检查会报"缺门/家"
    { var m=OverworldPack.Parse(OverworldPack.SampleText)[0]; var res=OverworldMap.Resize(m,-12,0,0,0); if(!res.ok||res.lost==0){ rb++; Console.WriteLine("     [FAIL] 裁掉有东西的地方应报告 lost"); } else parts.Add($"裁掉左 12 列丢 {res.lost} 格"); }
    var web=File.Exists("ow_resize.json")?(MiniJson.Parse(File.ReadAllText("ow_resize.json"),out _) as Dictionary<string,object>):null; int wd=0;
    if(web!=null) foreach(var kv in mine){ if(!web.TryGetValue(kv.Key,out var o)||(string)o!=kv.Value){ wd++; Console.WriteLine($"     [FAIL] 扩展 {kv.Key}：网页≠Unity"); } }
    rb+=wd;
    Console.WriteLine($"[{(rb==0?"OK":"FAIL")}] S217 小镇扩展：{string.Join("｜",parts)}｜网页对照{(web==null?"跳过":wd==0?"一致":"不一致")}"); fail+=rb; }
  // S218：小镇大机关（巨炮 K + 靶心 X / 滚石 O / 水塔 U）+ 天气 + 连锁 + 马里奥吃亏会躲 + 网页逐字对照（ow_props.json 由 verify.sh 生成）
  { int rb=0; var parts=new List<string>(); var r=OverworldMap.Rules.Default; var t=MarioMindTuningSO.LoadOrDefault();
    var big=OverworldPack.Parse(OverworldPack.BigSampleText)[0];
    var rep=OverworldMap.Check(big,r); if(!rep.Playable){ rb++; Console.WriteLine("     [FAIL] 星露大镇不可玩："+string.Join(" / ",rep.issues.Where(i=>i.sev==OverworldMap.Sev.Error).Take(3))); }
    var day=OverworldWalker.SimulateDay(big,r); if(!day.ok){ rb++; Console.WriteLine("     [FAIL] 星露大镇一天走不完："+day.summary); }
    var chain=OverworldProps.LongestChain(big); if(chain.Count<3){ rb++; Console.WriteLine($"     [FAIL] 星露大镇最长连锁只有 {chain.Count}"); }
    parts.Add($"星露大镇 {big.W}×{big.H} {rep.Headline} 最长连锁 {chain.Count}");
    // 反例：巨炮没有靶心 / 炮口堵死 / 靶心在封闭围栏里（落点走不回家）/ 大机关太多
    { var m=OverworldPack.Parse(OverworldPack.SampleText)[0]; OverworldMap.Set(m,26,16,'K'); var c=OverworldMap.Check(m,r); if(!c.issues.Any(i=>i.sev==OverworldMap.Sev.Warn&&i.text.Contains("找不到靶心"))){ rb++; Console.WriteLine("     [FAIL] 没靶心的巨炮应提醒（S219 起能自己瞄，只是黄色提醒）"); } }
    { var m=OverworldPack.Parse(OverworldPack.SampleText)[0]; OverworldMap.Set(m,26,16,'K'); OverworldMap.Set(m,26,15,'c'); OverworldMap.Set(m,26,3,'X'); var c=OverworldMap.Check(m,r); if(!c.issues.Any(i=>i.text.Contains("第一格就被挡住"))){ rb++; Console.WriteLine("     [FAIL] 炮口堵死应报错"); } }
    { var m=OverworldPack.Parse(OverworldPack.SampleText)[0]; OverworldMap.Set(m,2,20,'K'); OverworldMap.Set(m,2,1,'X'); for(int x=1;x<=6;x++) for(int y=1;y<=6;y++) if(x==1||y==6||x==6) if(m.At(x,y)=='.'||m.At(x,y)=='"') OverworldMap.Set(m,x,y,'f');
      var c=OverworldMap.Check(m,r); if(!c.issues.Any(i=>i.text.Contains("走不回马里奥的家"))){ rb++; Console.WriteLine("     [FAIL] 靶心在围栏里（被轰过去就困住）应报错 H1："+string.Join(" / ",c.issues.Take(3))); } }
    { var m=OverworldPack.Parse(OverworldPack.SampleText)[0]; int n=0; for(int x=2;x<42&&n<13;x+=3){ if(m.At(x,1)=='.'){ OverworldMap.Set(m,x,1,'U'); n++; } } var c=OverworldMap.Check(m,r); if(!c.issues.Any(i=>i.text.Contains("最多 12 个"))){ rb++; Console.WriteLine("     [FAIL] 13 个大机关应报错"); } }
    // 滚石：撞碎木箱栅栏、不碰最外圈；水塔只淹草/路/高草
    { var m=OverworldPack.Parse(OverworldPack.SampleText)[0]; var sm=new List<OverworldMap.Cell>(); var lane=OverworldProps.Lane(m,new OverworldMap.Cell(1,20),0,sm); if(lane.Any(c=>c.x<=0||c.y<=0||c.x>=m.W-1||c.y>=m.H-1)){ rb++; Console.WriteLine("     [FAIL] 滚石滚进了最外圈"); } }
    // 所有地形改变都只会"打开"：发动全部大机关后，检查依然可玩、马里奥一天仍走得完（H1）
    { var m=OverworldPack.Parse(OverworldPack.BigSampleText)[0];
      foreach(var c in OverworldProps.All(m)){ char k=m.At(c.x,c.y); if(k=='O'){ for(int d=0;d<4;d++) foreach(var q in OverworldProps.Lane(m,c,d)) if(OverworldProps.Smashable(m.At(q.x,q.y))) OverworldMap.Set(m,q.x,q.y,'.'); OverworldMap.Set(m,c.x,c.y,'.'); } else if(k=='U') foreach(var q in OverworldProps.Flood(m,c,OverworldProps.FloodRadius+1)) OverworldMap.Set(m,q.x,q.y,'g'); }
      var c2=OverworldMap.Check(m,r); var d2=OverworldWalker.SimulateDay(m,r); if(!c2.Playable||!d2.ok){ rb++; Console.WriteLine($"     [FAIL] 最坏情况（所有大机关都发动）后不可玩：{c2.Headline} {d2.summary}"); } else parts.Add("全部发动后仍可玩+一天走完"); }
    // 小镇里真的连锁：强制马里奥站在巨炮炮口 → 轰飞 → 落地晕 → 冲击震响滚石 → 滚石撞到水塔 → 淹地；他记住 'K'
    { OverworldSession.ResetStatics(); OverworldSession.NewDay("星露大镇","Town"); OverworldSession.Active=true;
      var town=new OverworldTown(OverworldPack.Parse(OverworldPack.BigSampleText)[0],t); var k=new OverworldMap.Cell(26,16);
      OverworldProps.Aim(town.map,k,out _,out int dir,out _); var mz=OverworldProps.MuzzleCells(town.map,k,dir)[1];
      town.mario.x=mz.x+0.5; town.mario.y=mz.y+0.5; town.mario.Clear(); town.tx=k.x-1.5; town.ty=k.y+0.5;
      bool armed=town.Arm(k,1,town.tx,town.ty); bool flew=false,dizzy=false; int maxDepth=0; float tt=0; var inp=new OverworldTown.Input();
      OverworldSession.Minute=OverworldMap.DayStart; // 06:00 他在等出门（站着不动）——测"站在炮口里会怎样"，不测时机
      while(tt<12f){ town.Tick(1f/30,inp); tt+=1f/30; if(town.marioFlying) flew=true; if(town.lastOrder.state==OverworldMarioState.Dizzy) dizzy=true; foreach(var b in town.active) maxDepth=Math.Max(maxDepth,b.depth); if(tt>0.1f&&!town.BigBusy&&flew) break; }
      int mud=OverworldSession.Changed.Count(kv=>kv.Value=='g'), smashed=OverworldSession.Changed.Count(kv=>kv.Value=='.');
      if(!armed||!flew||!dizzy||maxDepth<3||mud==0||!OverworldSession.MarioWary.Contains('K')){ rb++; Console.WriteLine($"     [FAIL] 小镇连锁：发动={armed} 飞={flew} 晕={dizzy} 连锁深度={maxDepth} 淹={mud} 撞碎={smashed} 记住炮={OverworldSession.MarioWary.Contains('K')}"); }
      else parts.Add($"实跑连锁 {maxDepth} 连（轰飞→晕→滚石撞碎 {smashed - 1}→淹 {mud} 格）");
      // 他已经吃过亏：再站到炮口、看得见炮 → 预警期间躲出炮口（最多 4 步）
      var town2=new OverworldTown(OverworldPack.Parse(OverworldPack.BigSampleText)[0],t); var k2=new OverworldMap.Cell(63,28);
      OverworldProps.Aim(town2.map,k2,out _,out int d2r,out _); var mz2=OverworldProps.MuzzleCells(town2.map,k2,d2r)[2];
      town2.mario.x=mz2.x+0.5; town2.mario.y=mz2.y+0.5; town2.mario.fx=0; town2.mario.fy=1; town2.mario.Clear(); town2.tx=k2.x+2.5; town2.ty=k2.y+0.5;
      town2.Arm(k2,1,town2.tx,town2.ty); bool hit2=false; tt=0; while(tt<t.overworldBigFuseSeconds+0.2f){ town2.Tick(1f/30,inp); tt+=1f/30; if(town2.marioFlying) hit2=true; }
      if(hit2){ rb++; Console.WriteLine("     [FAIL] 吃过亏的马里奥看见炮口在闪还站着被轰（学不会）"); } else parts.Add("吃过亏会躲");
      // 没吃过亏的他（新 Play）不会躲 → H4：他不知道机关的规则，只凭经历
      OverworldSession.MarioWary.Clear(); OverworldSession.UsedCells.Clear(); var town3=new OverworldTown(OverworldPack.Parse(OverworldPack.BigSampleText)[0],t);
      town3.mario.x=mz2.x+0.5; town3.mario.y=mz2.y+0.5; town3.mario.fx=0; town3.mario.fy=1; town3.mario.Clear(); town3.Arm(k2,1,k2.x+2.5,k2.y+0.5); bool hit3=false; tt=0; while(tt<t.overworldBigFuseSeconds+0.3f){ town3.Tick(1f/30,inp); tt+=1f/30; if(town3.marioFlying) hit3=true; }
      if(!hit3){ rb++; Console.WriteLine("     [FAIL] 第一次见巨炮的马里奥不该会躲（他没有经历）"); }
      OverworldSession.ResetStatics(); }
    // 天气：第 1 天必晴；同一天永远一样；5 种都出现；赶集日门的时间仍有序、≤20:00、每扇差 ≥15 分钟
    { var kinds=new HashSet<OverworldEvents.Kind>(); bool same=true, order=true;
      for(int d=1;d<=60;d++){ var a=OverworldEvents.Of("星露大镇",d); var b=OverworldEvents.Of("星露大镇",d); if(a.kind!=b.kind||a.wind!=b.wind) same=false; kinds.Add(a.kind);
        if(a.kind==OverworldEvents.Kind.Market){ var m=OverworldPack.Parse(OverworldPack.BigSampleText)[0]; OverworldEvents.ApplyTo(m,a); int prev=-99; foreach(var x in m.doors.OrderBy2()){ if(x.minute<prev+15||x.minute>OverworldMap.LatestDoor) order=false; prev=x.minute; } if(!OverworldMap.Check(m,r).Playable) order=false; } }
      if(OverworldEvents.Of("星露大镇",1).kind!=OverworldEvents.Kind.Clear||!same||kinds.Count<5||!order){ rb++; Console.WriteLine($"     [FAIL] 天气：第1天={OverworldEvents.Of("星露大镇",1).kind} 可复现={same} 种类={kinds.Count} 赶集日时间={order}"); }
      else parts.Add($"天气 60 天 {kinds.Count} 种、可复现、赶集日仍可玩"); }
    // 机器人玩家：星露大镇 4 种玩家 × 3 天（天气不同），一天都能结束、会躲的每扇门都埋伏上
    { var sw=System.Diagnostics.Stopwatch.StartNew(); int hideAm=0, doors=0; bool ended=true;
      for(int d=1;d<=3;d++) foreach(var kd in new[]{OverworldBots.Kind.Hider,OverworldBots.Kind.Prankster,OverworldBots.Kind.Idle,OverworldBots.Kind.Chaos}){
        var m=OverworldPack.Parse(OverworldPack.BigSampleText)[0]; var br=OverworldBots.PlayDay(m,t,kd,true,d,d); if(!br.dayEnded) ended=false; if(kd==OverworldBots.Kind.Hider){ hideAm+=br.ambush; doors+=br.doors; } }
      OverworldSession.ResetStatics(); sw.Stop();
      if(!ended||hideAm<doors){ rb++; Console.WriteLine($"     [FAIL] 星露大镇机器人：一天都结束={ended} 会躲的埋伏 {hideAm}/{doors}"); } else parts.Add($"机器人 3 天×4 种 {sw.ElapsedMilliseconds}ms 会躲的埋伏 {hideAm}/{doors}"); }
    // 网页对照：owCheck 星露大镇 + 总览文字 + 天气 20 天
    var mine=new List<string>(); foreach(var i in rep.issues) mine.Add(i.sev+" "+i); foreach(var l in OverworldProps.Describe(big)) mine.Add("D "+l.text); for(int d=1;d<=20;d++){ var w=OverworldEvents.Of(big.name,d); mine.Add($"W {d} {(int)w.kind} {w.wind}"); }
    mine.AddRange(OverworldEvents.Preview(big,1,8));
    int wd=0; bool haveWeb=File.Exists("ow_props.json");
    if(haveWeb){ var web=(MiniJson.Parse(File.ReadAllText("ow_props.json"),out _) as List<object>)?.Select(o=>(string)o).ToList()??new List<string>();
      for(int i=0;i<Math.Max(web.Count,mine.Count);i++){ string a=i<mine.Count?mine[i]:"(无)", b=i<web.Count?web[i]:"(无)"; if(a!=b){ wd++; if(wd<=3) Console.WriteLine($"     [FAIL] 大机关 网页≠Unity 第{i}行：\n        Unity {a}\n        网页  {b}"); } } }
    rb+=wd;
    Console.WriteLine($"[{(rb==0?"OK":"FAIL")}] S218 小镇大机关：{string.Join("｜",parts)}｜网页对照{(!haveWeb?"跳过":wd==0?$"一致 {mine.Count} 行":"不一致")}"); fail+=rb; }
  // S219：巨炮自由瞄准（你 / 马里奥都能坐）+ 山地（山丘挡视线 / 山洞隧道）+ 雷雨闪电 / 酸雨 / 泥石流 + 网页逐字对照（ow_mtn.json）
  { int rb=0; var parts=new List<string>(); var r=OverworldMap.Rules.Default; var t=MarioMindTuningSO.LoadOrDefault(); var inp=new OverworldTown.Input();
    var mtn=OverworldPack.Parse(OverworldPack.MountainSampleText)[0];
    var rep=OverworldMap.Check(mtn,r); if(!rep.Playable){ rb++; Console.WriteLine("     [FAIL] 星露山镇不可玩："+string.Join(" / ",rep.issues.Where(i=>i.sev==OverworldMap.Sev.Error).Take(3))); }
    var day=OverworldWalker.SimulateDay(mtn,r); if(!day.ok){ rb++; Console.WriteLine("     [FAIL] 星露山镇一天走不完："+day.summary); }
    parts.Add($"星露山镇 {rep.Headline}");
    // 旧图不受影响：星露大镇 S218 的检查文字 / 默认落点不变（靶心仍是默认瞄准）
    { var big=OverworldPack.Parse(OverworldPack.BigSampleText)[0]; OverworldProps.DefaultAim(big,new OverworldMap.Cell(26,16),out int dd,out int ds); OverworldProps.Aim(big,new OverworldMap.Cell(26,16),out var tg,out _,out _);
      var l1=OverworldProps.AimLanding(big,new OverworldMap.Cell(26,16),dd,ds,-1); var l2=OverworldProps.Landing(big,tg,-1); if(!l1.Equals(l2)){ rb++; Console.WriteLine($"     [FAIL] 默认瞄准落点 {l1} ≠ S218 靶心落点 {l2}"); } }
    // 视线：山丘挡住两个站在平地的人；站上山丘就看得过去；山 A 永远挡
    { var m=OverworldMap.Parse("# Overworld: los\n"+string.Join("\n",OverworldMap.NewMap(16,12))); OverworldMap.Set(m,7,5,'^');
      bool flat=OverworldMap.LineOfSight(m,4.5,5.5,10.5,5.5); OverworldMap.Set(m,4,5,'^'); bool up=OverworldMap.LineOfSight(m,4.5,5.5,10.5,5.5); OverworldMap.Set(m,7,5,'A'); bool mt=OverworldMap.LineOfSight(m,4.5,5.5,10.5,5.5);
      if(flat||!up||mt){ rb++; Console.WriteLine($"     [FAIL] 视线：平地隔山丘={flat}（应 false） 站上山丘={up}（应 true） 隔山={mt}（应 false）"); } else parts.Add("山丘挡平地视线、站上去看得远"); }
    // 瞄准：顺着远 / 反着近到头调头 / 横着转；落点走不回家不许打
    { var k=new OverworldMap.Cell(26,16); OverworldProps.DefaultAim(mtn,k,out int dir,out int dist); int d0=dir,s0=dist;
      OverworldProps.AimStep(mtn,k,ref dir,ref dist,dir); bool far=dist==s0+1; for(int i=0;i<40;i++) OverworldProps.AimStep(mtn,k,ref dir,ref dist,d0^1); bool flip=dir==(d0^1);
      int dir2=0,dist2=5; OverworldProps.AimStep(mtn,k,ref dir2,ref dist2,2); bool turn=dir2==2&&dist2==5;
      if(!far||!flip||!turn){ rb++; Console.WriteLine($"     [FAIL] 瞄准：远={far} 调头={flip} 转向={turn}"); } else parts.Add("瞄准 远/近/调头/转向"); }
    // 你坐炮：E 坐进去 → 方向键瞄 → L 发射 → 飞到瞄的落点；落点走不回家 → 不许打（H1）
    { OverworldSession.ResetStatics(); OverworldSession.NewDay(mtn.name,"Town"); OverworldSession.Active=true;
      var town=new OverworldTown(OverworldPack.Parse(OverworldPack.MountainSampleText)[0],t); var k=new OverworldMap.Cell(26,16);
      town.tx=k.x-0.5; town.ty=k.y+0.5; OverworldSession.Minute=OverworldMap.DayStart; town.Tick(1f/30,new OverworldTown.Input{door=true});
      bool sat=town.Seated; int want=town.seat!=null?town.seat.dist:0; for(int i=0;i<3&&sat;i++) town.Tick(1f/30,new OverworldTown.Input{aim=town.seat.dir+1}); 
      var land=town.seat!=null?town.AimLandingOf(k,town.seat.dir,town.seat.dist):k; town.Tick(1f/30,new OverworldTown.Input{peel=true});
      float tt=0; bool flew=false; while(tt<5f){ town.Tick(1f/30,inp); tt+=1f/30; if(town.youFlying) flew=true; if(flew&&!town.youFlying) break; }
      bool atLand=OverworldTown.Dist(town.tx,town.ty,land.x+0.5,land.y+0.5)<0.6;
      if(!sat||!flew||!atLand||OverworldSession.CannonRides!=1){ rb++; Console.WriteLine($"     [FAIL] 你坐炮：坐进去={sat} 飞={flew} 落在瞄的地方={atLand} ({town.tx:0.0},{town.ty:0.0}) vs {land}"); } else parts.Add($"你坐炮瞄 {want+3} 格飞到 {land}");
      // 被围起来的落点：不许打
      var m2=OverworldPack.Parse(OverworldPack.MountainSampleText)[0]; for(int x=5;x<=9;x++) for(int y=30;y<=34;y++) if(x==5||x==9||y==30||y==34) OverworldMap.Set(m2,x,y,'f');
      OverworldMap.Set(m2,20,32,'K'); bool okIn=OverworldProps.AimOk(m2,new OverworldMap.Cell(20,32),1,OverworldProps.AimLanding(m2,new OverworldMap.Cell(20,32),1,13,-1),OverworldMap.Find(m2,'M')[0]);
      if(okIn){ rb++; Console.WriteLine("     [FAIL] 瞄进围栏里（走不回家）应不许发射"); } else parts.Add("瞄进死地不许打"); }
    // 马里奥坐炮：星露山镇 8:00 他去门 1……找一个"坐炮能省 ≥10 格"的场景：门 4 在东南，炮在 (63,28) 北边
    { OverworldSession.ResetStatics(); OverworldSession.NewDay(mtn.name,"Town"); OverworldSession.Active=true;
      var m=OverworldPack.Parse(OverworldPack.MountainSampleText)[0]; var town=new OverworldTown(m,t);
      town.mario.x=62.5; town.mario.y=30.5; town.mario.Clear(); town.tx=3.5; town.ty=2.5; OverworldSession.NextStop=3; OverworldSession.Minute=16*60+31;
      bool rode=false,flew=false,dizzy=false; float tt=0; while(tt<25f&&OverworldSession.NextStop==3){ town.Tick(1f/30,inp); tt+=1f/30; if(town.MarioSeated) rode=true; if(town.marioFlying) flew=true; if(town.lastOrder.state==OverworldMarioState.Dizzy) dizzy=true; }
      if(!rode||!flew||dizzy){ rb++; Console.WriteLine($"     [FAIL] 马里奥坐炮抄近路：坐={rode} 飞={flew} 晕={dizzy}（自己坐不该晕） 门={OverworldSession.NextStop}"); } else parts.Add($"马里奥自己坐炮抄近路（{tt:0} 秒进门）");
      // 你拨歪：他坐着瞄的时候你站旁边按 L → 他飞歪、落地晕、以后不坐
      OverworldSession.ResetStatics(); OverworldSession.NewDay(mtn.name,"Town"); OverworldSession.Active=true;
      var t2=new OverworldTown(OverworldPack.Parse(OverworldPack.MountainSampleText)[0],t); t2.mario.x=62.5; t2.mario.y=30.5; t2.mario.Clear(); t2.tx=65.5; t2.ty=27.5; OverworldSession.NextStop=3; OverworldSession.Minute=16*60+31;
      bool tam=false,dz=false; tt=0; while(tt<20f){ var ii=new OverworldTown.Input{peel=t2.MarioSeated&&!tam}; t2.Tick(1f/30,ii); if(t2.hint==OverworldTown.Note.CannonTamper) tam=true; if(t2.lastOrder.state==OverworldMarioState.Dizzy) dz=true; tt+=1f/30; if(dz) break; }
      if(!tam||!dz||!OverworldSession.MarioWary.Contains('K')){ rb++; Console.WriteLine($"     [FAIL] 拨歪他的炮：拨到={tam} 他落地晕={dz} 记住了={OverworldSession.MarioWary.Contains('K')}"); } else parts.Add("拨歪他的炮 → 他晕 + 以后不坐");
      OverworldSession.ResetStatics(); }
    // 雷雨：路灯 L 召唤闪电 → 旁边的人晕、震响山丘 → 泥石流（湿的天）；晴天路灯 L 没用
    { OverworldSession.ResetStatics(); int stormDay=-1; for(int d=2;d<80&&stormDay<0;d++) if(OverworldEvents.Of(mtn,d).kind==OverworldEvents.Kind.Storm) stormDay=d;
      if(stormDay<0){ rb++; Console.WriteLine("     [FAIL] 星露山镇 80 天里没有雷雨"); }
      else { OverworldSession.NewDay(mtn.name,"Town",stormDay); OverworldSession.Active=true; var town=new OverworldTown(OverworldPack.Parse(OverworldPack.MountainSampleText)[0],t);
        var lamp=new OverworldMap.Cell(45,31); town.tx=lamp.x+2.5; town.ty=lamp.y+0.5; town.mario.x=lamp.x-0.5; town.mario.y=lamp.y+0.5; town.mario.Clear(); OverworldSession.Minute=OverworldMap.DayStart;
        town.Tick(1f/30,new OverworldTown.Input{peel=true}); bool dz=false; float tt=0; while(tt<4f){ town.Tick(1f/30,inp); tt+=1f/30; if(town.lastOrder.state==OverworldMarioState.Dizzy) dz=true; }
        if(OverworldSession.Lightnings!=1||!dz||OverworldSession.Mudslides<1){ rb++; Console.WriteLine($"     [FAIL] 雷雨闪电：劈={OverworldSession.Lightnings} 他晕={dz} 泥石流={OverworldSession.Mudslides}"); } else parts.Add($"第 {stormDay} 天雷雨：闪电 → 他晕 → 泥石流 {OverworldSession.Mudslides} 道"); }
      OverworldSession.ResetStatics(); OverworldSession.NewDay(mtn.name,"Town",1); OverworldSession.Active=true; var sunny=new OverworldTown(OverworldPack.Parse(OverworldPack.MountainSampleText)[0],t);
      sunny.tx=47.5; sunny.ty=31.5; sunny.Tick(1f/30,new OverworldTown.Input{peel=true}); for(int i=0;i<90;i++) sunny.Tick(1f/30,inp);
      if(OverworldSession.Lightnings!=0){ rb++; Console.WriteLine("     [FAIL] 晴天路灯不该召唤闪电"); } OverworldSession.ResetStatics(); }
    // 山洞：钻进去按 E 从另一头出来；马里奥寻路不走隧道（H4：他不知道）
    { OverworldSession.ResetStatics(); OverworldSession.NewDay(mtn.name,"Town"); OverworldSession.Active=true; var town=new OverworldTown(OverworldPack.Parse(OverworldPack.MountainSampleText)[0],t);
      var cv=OverworldMap.Find(town.map,'h'); town.tx=cv[0].x+0.5; town.ty=cv[0].y+0.5; town.Tick(1f/30,new OverworldTown.Input{door=true});
      bool hop=OverworldTown.Dist(town.tx,town.ty,cv[1].x+0.5,cv[1].y+0.5)<0.2;
      if(!hop||cv.Count!=2){ rb++; Console.WriteLine($"     [FAIL] 山洞：{cv.Count} 个，钻过去={hop}"); } else parts.Add($"山洞 {cv[0]}⇄{cv[1]}"); OverworldSession.ResetStatics(); }
    // 最坏情况：所有泥石流都冲 + 酸雨高草全枯 → 仍可玩、一天走得完（地形只会"打开"）
    { var m=OverworldPack.Parse(OverworldPack.MountainSampleText)[0];
      foreach(var c in OverworldMap.Find(m,'^')){ if(OverworldProps.MudDir(m,c)<0) continue; foreach(var q in OverworldProps.MudLane(m,c)) if(OverworldProps.Muddable(m.At(q.x,q.y))) OverworldMap.Set(m,q.x,q.y,'g'); OverworldMap.Set(m,c.x,c.y,'g'); }
      OverworldEvents.ApplyTo(m,new OverworldEvents.Day{kind=OverworldEvents.Kind.Acid});
      var c2=OverworldMap.Check(m,r); var d2=OverworldWalker.SimulateDay(m,r); if(!c2.Playable||!d2.ok){ rb++; Console.WriteLine($"     [FAIL] 泥石流全冲 + 酸雨后不可玩：{c2.Headline} {d2.summary}"); } else parts.Add("泥石流全冲+酸雨后仍可玩"); }
    // 天气池看格局：星露山镇 7 种都会出现；星露小镇（有路灯没山洞）没有酸雨；没路灯的图没有雷雨
    { var kinds=new HashSet<OverworldEvents.Kind>(); for(int d=1;d<=120;d++) kinds.Add(OverworldEvents.Of(mtn,d).kind);
      var small=OverworldPack.Parse(OverworldPack.SampleText)[0]; bool acid=false; for(int d=1;d<=120;d++) if(OverworldEvents.Of(small,d).kind==OverworldEvents.Kind.Acid) acid=true;
      var bare=OverworldMap.Parse("# Overworld: bare\n"+string.Join("\n",OverworldMap.NewMap(16,12))); bool storm=false; for(int d=1;d<=120;d++) if(OverworldEvents.Of(bare,d).kind==OverworldEvents.Kind.Storm) storm=true;
      if(kinds.Count<7||acid||storm){ rb++; Console.WriteLine($"     [FAIL] 天气池：山镇 {kinds.Count} 种 小镇出现酸雨={acid} 空地出现雷雨={storm}"); } else parts.Add("天气池 7 种（按格局：路灯→雷雨、山洞→酸雨）"); }
    // 机器人：星露山镇 4 种玩家 × 4 天，一天都能结束，会躲的每扇门都埋伏上（H10）
    { var sw=System.Diagnostics.Stopwatch.StartNew(); int am=0,doors=0; bool ended=true;
      for(int d=1;d<=4;d++) foreach(var kd in new[]{OverworldBots.Kind.Hider,OverworldBots.Kind.Prankster,OverworldBots.Kind.Idle,OverworldBots.Kind.Chaos}){
        var br=OverworldBots.PlayDay(OverworldPack.Parse(OverworldPack.MountainSampleText)[0],t,kd,true,d,d); if(!br.dayEnded) ended=false; if(kd==OverworldBots.Kind.Hider){ am+=br.ambush; doors+=br.doors; } }
      OverworldSession.ResetStatics(); sw.Stop();
      if(!ended||am<doors){ rb++; Console.WriteLine($"     [FAIL] 星露山镇机器人：都结束={ended} 会躲的埋伏 {am}/{doors}"); } else parts.Add($"机器人 4 天×4 种 {sw.ElapsedMilliseconds}ms 埋伏 {am}/{doors}"); }
    // 网页对照
    var mine=new List<string>(); foreach(var i in rep.issues) mine.Add(i.sev+" "+i); foreach(var l in OverworldProps.Describe(mtn)) mine.Add("D "+l.text);
    for(int d=1;d<=30;d++){ var w=OverworldEvents.Of(mtn,d); mine.Add($"W {d} {(int)w.kind} {w.wind}"); } mine.AddRange(OverworldEvents.Preview(mtn,1,14));
    { var k=new OverworldMap.Cell(26,16); OverworldProps.DefaultAim(mtn,k,out int dir,out int dist); foreach(int key in new[]{0,0,1,1,1,2,3,3,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}){ OverworldProps.AimStep(mtn,k,ref dir,ref dist,key); var l=OverworldProps.AimLanding(mtn,k,dir,dist,2); mine.Add($"A {dir} {dist} {l.x},{l.y} {OverworldProps.AimOk(mtn,k,dir,l,OverworldMap.Find(mtn,'M')[0])}"); } }
    foreach(var (ax,ay,bx,by) in new[]{(35.5,20.5,35.5,24.5),(33.5,22.5,38.5,22.5),(36.5,22.5,30.5,22.5),(50.5,31.5,50.5,38.5),(40.5,30.5,60.5,30.5)}) mine.Add($"L {OverworldMap.LineOfSight(mtn,ax,ay,bx,by)}");
    int wd=0; bool haveWeb=File.Exists("ow_mtn.json");
    if(haveWeb){ var web=(MiniJson.Parse(File.ReadAllText("ow_mtn.json"),out _) as List<object>)?.Select(o=>(string)o).ToList()??new List<string>();
      for(int i=0;i<Math.Max(web.Count,mine.Count);i++){ string a2=i<mine.Count?mine[i]:"(无)", b=i<web.Count?web[i]:"(无)"; if(a2!=b){ wd++; if(wd<=3) Console.WriteLine($"     [FAIL] 山镇 网页≠Unity 第{i}行：\n        Unity {a2}\n        网页  {b}"); } } }
    rb+=wd;
    Console.WriteLine($"[{(rb==0?"OK":"FAIL")}] S219 巨炮瞄准 + 山地 + 雷雨泥石流：{string.Join("｜",parts)}｜网页对照{(!haveWeb?"跳过":wd==0?$"一致 {mine.Count} 行":"不一致")}"); fail+=rb; }
  // S220：心 / 伤害表 / 雷区（设计师画范围、每次劈 min..max 道、可复现）/ 补心能量 / Q 雷云（会劈到自己）/ 带心进房间 + 网页逐字对照（ow_storm.json）
  { int rb=0; var parts=new List<string>(); var r=OverworldMap.Rules.Default; var t=MarioMindTuningSO.LoadOrDefault(); var inp=new OverworldTown.Input(); const float dt=1f/30;
    var sm=OverworldPack.Parse(OverworldPack.StormSampleText)[0];
    var rep=OverworldMap.Check(sm,r); if(!rep.Playable){ rb++; Console.WriteLine("     [FAIL] 星露雷镇不可玩："+string.Join(" / ",rep.issues.Where(i=>i.sev==OverworldMap.Sev.Error).Take(3))); }
    if(sm.storms.Count!=2){ rb++; Console.WriteLine($"     [FAIL] 星露雷镇雷区 {sm.storms.Count} 个（应 2）"); }
    parts.Add($"星露雷镇 {rep.Headline} 雷区 {sm.storms.Count}");
    // 旧图文本 / JSON 一个字不变（没有雷区就不写 Storm 行）
    { bool same=true; foreach(var txt in new[]{OverworldPack.SampleText,OverworldPack.BigSampleText,OverworldPack.MountainSampleText}){ var m=OverworldMap.Parse(txt); if(OverworldMap.ToText(m)!=OverworldMap.ToText(OverworldMap.Parse(OverworldMap.ToText(m)))||OverworldMap.ToJson(m).Contains("storms")) same=false; }
      var rt=OverworldMap.Parse(OverworldMap.ToText(sm)); bool rtOk=rt.storms.Count==2&&OverworldMap.StormText(rt.storms[0])==OverworldMap.StormText(sm.storms[0])&&rt.storms[0].always&&!rt.storms[1].always;
      var js=OverworldMap.FromJson(MiniJson.Parse(OverworldMap.ToJson(sm),out _) as Dictionary<string,object>); bool jsOk=js.storms.Count==2&&OverworldMap.StormText(js.storms[1])==OverworldMap.StormText(sm.storms[1]);
      if(!same||!rtOk||!jsOk){ rb++; Console.WriteLine($"     [FAIL] 往返：旧图不变={same} 文本往返={rtOk} JSON往返={jsOk}"); } else parts.Add("旧图文本/JSON 不变、雷区往返一致"); }
    // 落点：可复现、数量在 min..max、不重复、都在框里、门/家/出生点 1 格外
    { bool ok=true; int lo=99,hi=0; for(int z=0;z<sm.storms.Count;z++) for(int v=0;v<60;v++){ var a=OverworldStorm.Volley(sm,z,3,v); var b=OverworldStorm.Volley(sm,z,3,v); var st=sm.storms[z];
        if(string.Join(";",a)!=string.Join(";",b)) ok=false; if(a.Count<st.min||a.Count>st.max) ok=false; if(a.Distinct().Count()!=a.Count) ok=false; lo=Math.Min(lo,a.Count); hi=Math.Max(hi,a.Count);
        foreach(var c in a){ if(c.x<st.x0||c.x>st.x1||c.y<st.y0||c.y>st.y1||!OverworldMap.Walkable(sm,c.x,c.y)) ok=false; for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++){ char q=sm.At(c.x+dx,c.y+dy); if(q=='M'||q=='T'||OverworldCatalog.IsDoor(q)) ok=false; } } }
      if(!ok){ rb++; Console.WriteLine("     [FAIL] 雷区落点：不可复现 / 道数越界 / 重复 / 出框 / 挨着门"); } else parts.Add($"落点可复现、每次 {lo}–{hi} 道"); }
    // 检查：坐标出界 / min>max / 太多 / 劈不到 → 红；盖住门 → 黄
    { var m=OverworldMap.Parse(OverworldPack.StormSampleText); m.storms.Add(new OverworldMap.Storm{x0=0,y0=0,x1=5,y1=5,min=1,max=2}); bool e1=!OverworldMap.Check(m,r).Playable;
      m=OverworldMap.Parse(OverworldPack.StormSampleText); m.storms[0].min=5; m.storms[0].max=3; bool e2=!OverworldMap.Check(m,r).Playable;
      m=OverworldMap.Parse(OverworldPack.StormSampleText); for(int i=0;i<3;i++) m.storms.Add(new OverworldMap.Storm{x0=2,y0=2,x1=4,y1=3,min=1,max=1}); bool e3=!OverworldMap.Check(m,r).Playable;
      m=OverworldMap.Parse(OverworldPack.StormSampleText); m.storms.Add(new OverworldMap.Storm{x0=21,y0=20,x1=22,y1=26,min=1,max=1}); bool e4=!OverworldMap.Check(m,r).Playable; // 河里
      m=OverworldMap.Parse(OverworldPack.StormSampleText); m.storms.Add(new OverworldMap.Storm{x0=10,y0=17,x1=18,y1=19,min=1,max=2}); bool w5=OverworldMap.Check(m,r).issues.Any(i=>i.sev==OverworldMap.Sev.Warn&&i.text.Contains("挨着门"));
      m=OverworldMap.Parse(OverworldPack.StormSampleText); for(int i=0;i<4;i++){ var cs=OverworldMap.Find(m,'.'); OverworldMap.Set(m,cs[i*7].x,cs[i*7].y,'+'); } bool e6=!OverworldMap.Check(m,r).Playable;
      if(!e1||!e2||!e3||!e4||!w5||!e6){ rb++; Console.WriteLine($"     [FAIL] 雷区检查：出界={e1} min>max={e2} 太多={e3} 劈不到={e4} 挨门提醒={w5} 补心太多={e6}"); } else parts.Add("检查：出界/道数/太多/劈不到/挨门/补心太多"); }
    // 扩展地图：雷区跟着平移；裁掉 → 放不下的丢掉
    { var m=OverworldMap.Parse(OverworldPack.StormSampleText); var rr=OverworldMap.Resize(m,4,0,0,2); bool mv=rr.ok&&m.storms[0].x0==20&&m.storms[0].y0==15;
      var m2=OverworldMap.Parse(OverworldPack.StormSampleText); OverworldMap.Resize(m2,-20,0,0,0); bool drop=m2.storms.Count==1;
      if(!mv||!drop){ rb++; Console.WriteLine($"     [FAIL] 扩展：平移={mv} 裁掉丢弃={drop}（剩 {m2.storms.Count}）"); } else parts.Add("扩展平移/裁掉丢弃"); }
    OverworldTown Fresh(int day){ OverworldSession.ResetStatics(); OverworldSession.NewDay(sm.name,"Town",day); OverworldSession.Active=true; return new OverworldTown(OverworldPack.Parse(OverworldPack.StormSampleText)[0],t); }
    // 雷区实跑（晴天也劈 always 的那个）：站在落点上 → 预警 1.2 秒 → 掉 1 颗心 + 晕；进场景当下不劈；预警期间不许快进
    { var town=Fresh(1); int v0=OverworldStorm.VolleyIndex(OverworldSession.Minute,t.overworldStormVolleySeconds,t.overworldMinutesPerSecond,0);
      for(int i=0;i<3;i++) town.Tick(dt,inp); bool noInstant=town.strikes.Count==0;
      double next=OverworldMap.DayStart; while(OverworldStorm.VolleyIndex(next,t.overworldStormVolleySeconds,t.overworldMinutesPerSecond,0)<=v0) next+=0.5;
      int vol=OverworldStorm.VolleyIndex(next,t.overworldStormVolleySeconds,t.overworldMinutesPerSecond,0); var cells=OverworldStorm.Volley(sm,0,1,vol);
      OverworldSession.Minute=next-0.01; town.tx=cells[0].x+0.5; town.ty=cells[0].y+0.5; town.mario.x=3.5; town.mario.y=2.5; town.mario.Clear(); OverworldSession.NextStop=0;
      town.Tick(dt,inp); bool warn=town.strikes.Count==cells.Count; bool noFF=!town.CanFastForward; float tt=0; int h0=OverworldSession.YouHearts; bool hitEarly=false;
      while(tt<t.overworldBoltTelegraphSeconds-0.1f){ town.Tick(dt,inp); tt+=dt; if(OverworldSession.YouHearts<h0) hitEarly=true; }
      while(tt<2f){ town.Tick(dt,inp); tt+=dt; }
      bool hit=OverworldSession.YouHearts==h0-1&&town.frozen>0f; bool noEnergy=OverworldSession.Energy==0;
      if(!noInstant||!warn||!noFF||hitEarly||!hit||!noEnergy){ rb++; Console.WriteLine($"     [FAIL] 雷区实跑：进场不劈={noInstant} 预警{town.strikes.Count}/{cells.Count}={warn} 不许快进={noFF} 预警没到就掉心={hitEarly} 掉心+晕={hit} 天灾不给能量={noEnergy}"); } else parts.Add($"雷区：预警 {t.overworldBoltTelegraphSeconds} 秒后劈 {cells.Count} 道 → 你 -1❤ 晕"); }
    // 只在雷雨天的雷区：晴天一整天不劈
    { var town=Fresh(1); town.tx=3.5; town.ty=2.5; int bolts=0; for(int i=0;i<30*60;i++){ town.Tick(dt,new OverworldTown.Input{fastForward=true}); foreach(var s in town.strikes) if(s.zone==1) bolts++; }
      if(bolts>0){ rb++; Console.WriteLine($"     [FAIL] 晴天不该劈雷区 2（劈了 {bolts}）"); } else parts.Add("雷雨雷区晴天不劈"); }
    // 伤害表：i 帧 2.5 秒挡掉心不挡晕；心掉光 = 晕 3 秒剩 1 颗；香蕉皮/水淹不掉心
    { var town=Fresh(1); var hurt=typeof(OverworldTown).GetMethod("HurtYou",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
      hurt.Invoke(town,null); hurt.Invoke(town,null); bool grace=OverworldSession.YouHearts==2;
      for(int i=0;i<120;i++) town.Tick(dt,inp); hurt.Invoke(town,null); for(int i=0;i<120;i++) town.Tick(dt,inp); hurt.Invoke(town,null); // S221：保护期 = 晕 2 + 1.5 秒
      bool ko=OverworldSession.YouHearts==1&&town.frozen>=t.overworldKoSeconds-0.01f&&OverworldSession.Kos==1&&!OverworldSession.DayOver;
      var hm=typeof(OverworldTown).GetMethod("HitMario",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
      hm.Invoke(town,new object[]{'O',true}); bool mHurt=OverworldSession.MarioHearts==2&&OverworldSession.Energy==1; hm.Invoke(town,new object[]{'i',false}); bool mGrace=OverworldSession.MarioHearts==2;
      if(!grace||!ko||!mHurt||!mGrace){ rb++; Console.WriteLine($"     [FAIL] 伤害：无敌时间={grace} 心掉光晕倒剩 1={ko} 他被你砸掉心+能量={mHurt} 他无敌时间={mGrace}"); } else parts.Add("保护期 晕+1.5 秒、掉光晕 3 秒剩 1 颗、你砸他 +能量"); }
    // 补心 / 能量：少了心才捡，每个一次；他路过顺手捡；能量满 3
    { var town=Fresh(1); var hp=OverworldMap.Find(town.map,'+')[0]; var ep=OverworldMap.Find(town.map,'*');
      town.tx=hp.x+0.5; town.ty=hp.y+0.5; town.Tick(dt,inp); bool notFull=!OverworldSession.UsedCells.Contains(hp.y*town.map.W+hp.x);
      OverworldSession.YouHearts=1; town.Tick(dt,inp); bool healed=OverworldSession.YouHearts==2; town.Tick(dt,inp); bool once=OverworldSession.YouHearts==2;
      foreach(var e in ep){ town.tx=e.x+0.5; town.ty=e.y+0.5; town.Tick(dt,inp); } bool full=OverworldSession.Energy==3;
      if(!notFull||!healed||!once||!full){ rb++; Console.WriteLine($"     [FAIL] 拾取：满心不捡={notFull} 补心={healed} 只一次={once} 能量满={full}（{OverworldSession.Energy}）"); } else parts.Add("补心/能量拾取"); }
    // Q 雷云：能量不够 → 提示；满 → 停在原地劈；他在云里会被劈；你站着不走也会被劈到（副作用）；能量清零
    { var town=Fresh(1); town.tx=50.5; town.ty=19.5; town.Tick(dt,new OverworldTown.Input{weather=true}); bool low=town.hint==OverworldTown.Note.CloudLow&&town.cloud==null;
      OverworldSession.Energy=3; town.mario.x=51.5; town.mario.y=19.5; town.mario.Clear(); OverworldSession.NextStop=0; OverworldSession.Minute=7*60;
      town.Tick(dt,new OverworldTown.Input{weather=true}); bool made=town.cloud!=null&&OverworldSession.Energy==0; double cx=town.cloud?.x??0;
      int mh=OverworldSession.MarioHearts; float tt=0; int selfHits=0; int yh=OverworldSession.YouHearts; bool still=true;
      while(tt<t.overworldCloudSeconds+2f){ town.Tick(dt,inp); tt+=dt; if(town.cloud!=null&&town.cloud.x!=cx) still=false; if(OverworldSession.YouHearts<yh){ selfHits++; yh=OverworldSession.YouHearts; } }
      bool mHit=OverworldSession.MarioHeartsLost>=1; bool gone=town.cloud==null;
      // 副作用：一百个召唤点里你原地不动，至少有一些会劈到你自己
      int selfAny=0; for(int k=0;k<12;k++){ var t3=Fresh(1); OverworldSession.Energy=3; OverworldSession.Clouds=k; var cs=OverworldMap.Find(t3.map,'.'); var c=cs[(k*97)%cs.Count]; t3.tx=c.x+0.5; t3.ty=c.y+0.5; t3.mario.x=3.5; t3.mario.y=2.5; t3.mario.Clear(); OverworldSession.Minute=7*60;
        t3.Tick(dt,new OverworldTown.Input{weather=true}); for(int i=0;i<30*12;i++) t3.Tick(dt,inp); if(OverworldSession.YouHeartsLost>0) selfAny++; }
      if(!low||!made||!still||!mHit||!gone||selfAny==0){ rb++; Console.WriteLine($"     [FAIL] 雷云：能量不够提示={low} 召唤={made} 停在原地={still} 劈到他={mHit} 结束={gone} 原地不动被劈 {selfAny}/12"); } else parts.Add($"Q 雷云：劈到他、原地不动的你 {selfAny}/12 次也挨劈"); }
    // 带心进房间：他至少 2 颗、你的心 = 房间命数；房间打完回满
    { OverworldSession.ResetStatics(); OverworldSession.NewDay(sm.name,"Town"); OverworldSession.MarioHearts=1; OverworldSession.YouHearts=2;
      int mc=OverworldRoomCarry.MarioRoomHearts(t,3), yc=OverworldRoomCarry.TricksterRoomLives(3); OverworldSession.RecordRoom(1,true); bool full=OverworldSession.MarioHearts==3&&OverworldSession.YouHearts==3;
      OverworldSession.MarioHearts=3; int m3=OverworldRoomCarry.MarioRoomHearts(t,3);
      if(mc!=2||yc!=2||!full||m3!=3){ rb++; Console.WriteLine($"     [FAIL] 带心进房间：他 {mc}（应 2） 你 {yc}（应 2） 打完回满={full} 满心 {m3}"); } else parts.Add("带心进房间（他至少 2）、打完回满"); OverworldSession.ResetStatics(); }
    // 机器人：星露雷镇 4 天 × 5 种，一天都能结束；会躲的每扇门都埋伏上（H10）；Prankster/Chaos 会按 Q
    { var sw=System.Diagnostics.Stopwatch.StartNew(); int am=0,doors=0,clouds=0; bool ended=true;
      for(int d=1;d<=4;d++) foreach(var kd in new[]{OverworldBots.Kind.Hider,OverworldBots.Kind.Prankster,OverworldBots.Kind.Idle,OverworldBots.Kind.Chaos,OverworldBots.Kind.Follower}){
        var br=OverworldBots.PlayDay(OverworldPack.Parse(OverworldPack.StormSampleText)[0],t,kd,true,d,d); clouds+=OverworldSession.Clouds; if(!br.dayEnded) ended=false; if(kd==OverworldBots.Kind.Hider){ am+=br.ambush; doors+=br.doors; } }
      OverworldSession.ResetStatics(); sw.Stop();
      if(!ended||am*100<doors*95){ rb++; Console.WriteLine($"     [FAIL] 星露雷镇机器人：都结束={ended} 会躲的埋伏 {am}/{doors}"); } else parts.Add($"机器人 4 天×5 种 {sw.ElapsedMilliseconds}ms 埋伏 {am}/{doors} 雷云 {clouds} 次"); }
    // 网页对照
    var mine=new List<string>(); foreach(var i in rep.issues) mine.Add(i.sev+" "+i);
    for(int z=0;z<sm.storms.Count;z++) for(int v=0;v<12;v++) mine.Add($"V {z} {v} "+string.Join(";",OverworldStorm.Volley(sm,z,5,v).Select(c=>c.x+","+c.y)));
    foreach(double mm in new[]{360.0,399.9,400.0,455.5,800.0}) for(int z=0;z<2;z++) mine.Add($"I {mm} {z} {OverworldStorm.VolleyIndex(mm,t.overworldStormVolleySeconds,t.overworldMinutesPerSecond,z)}");
    mine.Add("T "+OverworldMap.ToText(sm).Replace("\n","/")); for(int d=1;d<=20;d++){ var w=OverworldEvents.Of(sm,d); mine.Add($"W {d} {(int)w.kind}"); }
    int wd=0; bool haveWeb=File.Exists("ow_storm.json");
    if(haveWeb){ var web=(MiniJson.Parse(File.ReadAllText("ow_storm.json"),out _) as List<object>)?.Select(o=>(string)o).ToList()??new List<string>();
      for(int i=0;i<Math.Max(web.Count,mine.Count);i++){ string a2=i<mine.Count?mine[i]:"(无)", b2=i<web.Count?web[i]:"(无)"; if(a2!=b2){ wd++; if(wd<=3) Console.WriteLine($"     [FAIL] 雷镇 网页≠Unity 第{i}行：\n        Unity {a2}\n        网页  {b2}"); } } }
    rb+=wd;
    Console.WriteLine($"[{(rb==0?"OK":"FAIL")}] S220 心 + 雷区 + 雷云：{string.Join("｜",parts)}｜网页对照{(!haveWeb?"跳过":wd==0?$"一致 {mine.Count} 行":"不一致")}"); fail+=rb; }
  // S221：全流程模拟找出的三处问题 —— ① 连控（保护期在晕的时候就过完了 → 雷云里能被连续击倒、定身 7 秒）② 吃过亏的他闪一步就穿过雷云（Q 对他没用）③ 机器人从不按 Q（没人测过这条路）
  { int rb=0; var parts=new List<string>(); var t=MarioMindTuningSO.LoadOrDefault(); const float dt=1f/30;
    OverworldTown Open(){ OverworldSession.ResetStatics(); var m=OverworldPack.Parse(OverworldPack.StormSampleText)[0]; m.storms.Clear(); OverworldSession.NewDay(m.name,"Town",1); OverworldSession.Active=true; return new OverworldTown(m,t); }
    List<OverworldMap.Cell> Open7(OverworldTown tw)=>OverworldMap.Find(tw.map,'.').Where(q=>{for(int dy=-3;dy<=3;dy++)for(int dx=-3;dx<=3;dx++) if(!OverworldMap.Walkable(tw.map,q.x+dx,q.y+dy)) return false; return true;}).ToList();
    // ① 你只剩 1 颗心、站在雷云中心不动：最长连续定身 ≤ 掉光晕倒秒数（不能被连着击倒）
    { double worst=0, minFree=99; for(int k=0;k<40;k++){ var town=Open(); var cs=Open7(town); var c=cs[(k*37)%cs.Count]; OverworldSession.Minute=7*60; OverworldSession.Energy=3; OverworldSession.Clouds=k; OverworldSession.YouHearts=1;
        town.tx=c.x+0.5; town.ty=c.y+0.5; town.mario.x=3.5; town.mario.y=2.5; town.mario.Clear(); town.Tick(dt,new OverworldTown.Input{weather=true});
        double cur=0, free=0; bool was=false, seen=false; for(int i=0;i<30*12;i++){ town.Tick(dt,new OverworldTown.Input()); bool f=town.frozen>0;
          if(f){ if(!was&&seen) minFree=Math.Min(minFree,free); cur+=dt; worst=Math.Max(worst,cur); free=0; seen=true; } else { cur=0; free+=dt; } was=f; } }
      // 站着不动可以再挨一下（自己选的），但：一次最多定身 = 掉光晕倒秒数；两次之间至少有"站起来后的保护期"那么久能跑（1 格只要 0.2 秒）
      if(worst>t.overworldKoSeconds+0.05||minFree<t.overworldHurtGraceSeconds-0.05){ rb++; Console.WriteLine($"     [FAIL] 连控：最长连续定身 {worst:0.0}s（应 ≤ {t.overworldKoSeconds}） 两次定身之间最短能动 {minFree:0.00}s（应 ≥ {t.overworldHurtGraceSeconds}）"); }
      else parts.Add($"不连控：站云心不动最长定身 {worst:0.0}s、两次之间至少能跑 {(minFree>90?0:minFree):0.0}s（修前 7.1s 连续定身）"); }
    // 他 1 颗心被雷云罩：不会被连劈
    { int loops=0; for(int k=0;k<20;k++){ var town=Open(); var cs=Open7(town); var c=cs[(k*53)%cs.Count]; OverworldSession.Minute=7*60; OverworldSession.Energy=3; OverworldSession.Clouds=k; OverworldSession.MarioHearts=1;
        town.mario.x=c.x+0.5; town.mario.y=c.y+0.5; town.mario.Clear(); town.tx=c.x+0.5; town.ty=c.y+0.5; town.Tick(dt,new OverworldTown.Input{weather=true});
        for(int i=0;i<30*12;i++) town.Tick(dt,new OverworldTown.Input{h=1f}); if(OverworldSession.MarioHeartsLost>=2) loops++; }
      if(loops>0){ rb++; Console.WriteLine($"     [FAIL] 他被雷云连劈 {loops}/20"); } else parts.Add("他在云里最多挨 1 下"); }
    // ② 雷云挡在他去门 1 的路上：没吃过亏 / 吃过亏 都要被拖住 ≥ 3 秒，而且照样进门（H10）
    { var gain=new List<double>(); foreach(int wary in new[]{0,1}){
        double Arrive(bool cast){ var town=Open(); if(wary==1) OverworldSession.MarioWary.Add('i'); OverworldSession.Minute=town.stops[0].minute-1; OverworldSession.Energy=3; bool did=false; double tt=0;
          for(int i=0;i<30*120;i++){ var inp=new OverworldTown.Input();
            if(!did&&cast&&OverworldSession.Minute>=town.stops[0].minute+2){ var route=OverworldMap.Path(town.map,OverworldGuide.Near(town.map,town.mario.x,town.mario.y),town.doorCells[town.stops[0].n]); if(route!=null&&route.Count>8){ var c=route[7]; town.tx=c.x+.5; town.ty=c.y+.5; inp.weather=true; did=true; } }
            if(did&&town.cloud!=null){ town.tx=3.5; town.ty=2.5; }
            town.Tick(dt,inp); tt+=dt; if(town.marioInside) return tt; } return -1; }
        double b=Arrive(false), c=Arrive(true); gain.Add(c<0?-1:c-b); }
      if(gain.Any(g=>g<3)){ rb++; Console.WriteLine($"     [FAIL] 雷云拖住他：没吃过亏 +{gain[0]:0.0}s 吃过亏 +{gain[1]:0.0}s（应都 ≥ 3 秒且能进门）"); } else parts.Add($"雷云挡路拖住他 +{gain[0]:0.0}s（吃过亏的在云外等 +{gain[1]:0.0}s）"); }
    // ③ 捣蛋型机器人 4 天会按 Q、仍然每门埋伏（H10 / 不卡）
    { int clouds=0,am=0,doors=0; bool ended=true; for(int d=1;d<=4;d++){ var br=OverworldBots.PlayDay(OverworldPack.Parse(OverworldPack.StormSampleText)[0],t,OverworldBots.Kind.Prankster,true,d,d); clouds+=OverworldSession.Clouds; am+=br.ambush; doors+=br.doors; if(!br.dayEnded) ended=false; }
      if(clouds<4||!ended||am<doors){ rb++; Console.WriteLine($"     [FAIL] 捣蛋型：雷云 {clouds} 次（应每天 1 次） 结束={ended} 埋伏 {am}/{doors}"); } else parts.Add($"捣蛋型 4 天按 Q {clouds} 次、埋伏 {am}/{doors}"); }
    OverworldSession.ResetStatics();
    Console.WriteLine($"[{(rb==0?"OK":"FAIL")}] S221 全流程模拟修正：{string.Join("｜",parts)}"); fail+=rb; }
  // S222：统计门槛——以前每种机器人 3–12 天是"同一天复制 N 遍"（除乱按/反应慢外完全确定）。现在开手抖模式（humanNoise），
  // 每个样板镇 60 天（规则 of three：60 次全对 → 95% 把握失败率 < 5%）+ Wilson 95% 区间（NIST 推荐）。
  { int sb=0; var t=MarioMindTuningSO.LoadOrDefault(); var parts=new List<string>(); const int N=60;
    (double lo,double hi) W(int k,int n){ double z=1.96,p=(double)k/n,d=1+z*z/n,c=p+z*z/(2*n),h=z*Math.Sqrt(p*(1-p)/n+z*z/(4.0*n*n)); return (Math.Max(0,(c-h)/d),Math.Min(1,(c+h)/d)); }
    foreach(var (nm,txt) in new[]{("小镇",OverworldPack.SampleText),("大镇",OverworldPack.BigSampleText),("山镇",OverworldPack.MountainSampleText),("雷镇",OverworldPack.StormSampleText)}){
      int full=0,ended=0; var times=new HashSet<double>();
      for(int s=1;s<=N;s++){ var r=OverworldBots.PlayDay(OverworldPack.Parse(txt)[0],t,OverworldBots.Kind.Hider,true,s,1+(s-1)%4,true); if(r.ambush==r.doors) full++; if(r.dayEnded) ended++; times.Add(Math.Round(r.realSeconds,1)); }
      var w=W(full,N); if(w.lo<0.935||ended<N||times.Count<5){ sb++; Console.WriteLine($"     [FAIL] {nm} 会躲的玩家 {full}/{N} 天全埋伏 Wilson 下限 {w.lo:0.000}（应 ≥ 0.935：60 天全对时 = 0.940） 结束 {ended}/{N} 不同用时 {times.Count}（应 ≥ 5，否则样本不独立）"); }
      else parts.Add($"{nm} 会躲 {full}/{N}（下限 {w.lo:0.000}，{times.Count} 种用时）"); }
    { int full=0; for(int s=1;s<=N;s++){ var r=OverworldBots.PlayDay(OverworldPack.Parse(OverworldPack.SampleText)[0],t,OverworldBots.Kind.Follower,true,s,1,true); if(r.ambush==r.doors) full++; }
      var w=W(full,N); if(w.hi>0.5){ sb++; Console.WriteLine($"     [FAIL] 不躲也能全胜 {full}/{N}（上限 {w.hi:0.00}，应 ≤ 0.5）：躲藏没意义"); } else parts.Add($"不躲 {full}/{N} 天全胜（上限 {w.hi:0.00}）"); }
    OverworldSession.ResetStatics();
    // 被抓说明原因：空地正面 = 视线里；伪装还在动 = 木箱动了；路灯下 = 路灯
    { var m=OverworldPack.Parse(OverworldPack.SampleText)[0]; var r=new OverworldMap.SightRules{range=8,nightRange=3,halfAngleDeg=60,nearRadius=1.2,grassRadius=1,lampRadius=2.5,night=true};
      var lamps=new List<OverworldMap.Cell>{new OverworldMap.Cell(10,5)};
      var a=OverworldMap.WhySeen(m,lamps,4.5,5.5,10.5,5.5,r,false); var b=OverworldMap.WhySeen(m,lamps,4.5,5.5,6.5,5.5,r,true); var c=OverworldMap.WhySeen(m,lamps,4.5,5.5,5.2,5.5,r,false);
      if(a!=OverworldMap.SeenWhy.Lamp||b!=OverworldMap.SeenWhy.DisguiseMoved||c!=OverworldMap.SeenWhy.Near){ sb++; Console.WriteLine($"     [FAIL] 被抓原因：路灯={a} 伪装={b} 贴身={c}"); }
      else parts.Add("被抓说原因（路灯/伪装在动/贴身）");
      int nowhy=0; for(int s=1;s<=20;s++){ OverworldSession.ResetStatics(); OverworldSession.NewDay(m.name,"Town",1); OverworldSession.Active=true; var town=new OverworldTown(m,t); int c0=0;
        for(int i=0;i<30*90&&!OverworldSession.DayOver;i++){ var inp=new OverworldTown.Input{h=(float)Math.Sign(town.mario.x-town.tx),v=(float)Math.Sign(town.mario.y-town.ty),fastForward=true}; town.Tick(1f/30f,inp); if(OverworldSession.Caught>c0){ c0=OverworldSession.Caught; if(town.caughtWhy==OverworldMap.SeenWhy.None) nowhy++; } }
        if(c0==0&&s==1){ sb++; Console.WriteLine("     [FAIL] 冲向他的玩家一次都没被抓（测试无效）"); } }
      if(nowhy>0){ sb++; Console.WriteLine($"     [FAIL] 被抓却没有原因 {nowhy} 次"); } else parts.Add("冲向他被抓每次都有原因"); }
    OverworldSession.ResetStatics();
    Console.WriteLine($"[{(sb==0?"OK":"FAIL")}] S222 统计门槛 + 被抓原因：{string.Join("｜",parts)}"); fail+=sb; }
  // S223（总方案阶段 B）：马里奥中招反应 = 纯画面。① 每种连招事件都有一种固定反应 ② 演戏时间 ≤ 这种坑本来就晕的秒数（H9 不延长）
  // ③ 三段都连续（逐帧不跳变）、最后回到原样 ④ 连锁跳过"愣住" ⑤ 数据文件 = 默认表、写坏能回退 ⑥ 画面组件不碰晕眩/速度/捣蛋者
  { int rb=0; var t=MarioMindTuningSO.LoadOrDefault(); var parts=new List<string>(); var D=MarioReaction.Default;
    var stun=new Dictionary<string,float>{{"hurt",t.hurtStunSeconds},{"trip",t.tripStunSeconds},{"slip",t.bananaSlipSeconds},{"launch",t.springAirStunSeconds},{"cage",t.cageSeconds},{"snare",t.snareSeconds}};
    foreach(var k in new[]{"hurt","trip","slip","launch","cage","snare","drop","pit","stop"}){
      if(!MarioReaction.TryGet(D,k,out var b)){ rb++; Console.WriteLine($"     [FAIL] {k} 没有反应"); continue; }
      if(stun.TryGetValue(k,out float st)){ if(b.Held>st+1e-4f){ rb++; Console.WriteLine($"     [FAIL] {k} 演 {b.Held:0.00} 秒 > 本来晕 {st:0.00} 秒（会让玩家觉得他被多控了）"); } }
      else if(b.Total>MarioReaction.MaxUnstunnedTotal+1e-4f){ rb++; Console.WriteLine($"     [FAIL] {k} 不晕的坑演了 {b.Total:0.00} 秒（> {MarioReaction.MaxUnstunnedTotal}）"); }
      foreach(float face in new[]{1f,-1f}){ var prev=MarioReaction.Sample(b,0f,face); double jump=0; bool oob=false;
        for(float x=1f/60f;x<=b.Total+0.05f;x+=1f/60f){ var f=MarioReaction.Sample(b,x,face); if(float.IsNaN(f.sx)||f.sx<0.6f||f.sx>1.5f||f.sy<0.6f||f.sy>1.5f||Math.Abs(f.dx)>0.5f||Math.Abs(f.dy)>0.5f) oob=true;
          double dr=Math.Abs(MarioReaction.Wrap(f.rotDeg-prev.rotDeg)); jump=Math.Max(jump,Math.Max(dr/30.0,Math.Max(Math.Abs(f.sx-prev.sx),Math.Abs(f.sy-prev.sy))/0.1)); jump=Math.Max(jump,Math.Max(Math.Abs(f.dx-prev.dx),Math.Abs(f.dy-prev.dy))/0.1); prev=f; }
        var end=MarioReaction.Sample(b,b.Total+0.01f,face);
        if(oob||jump>1.0||end.sx!=1f||end.sy!=1f||end.rotDeg!=0f||end.dx!=0f||end.dy!=0f){ rb++; Console.WriteLine($"     [FAIL] {k} 朝向{face}：越界={oob} 最大单帧跳变={jump:0.00}（应 ≤ 1：转 30° / 缩放 0.1 / 位移 0.1） 回原样={(end.sx==1f&&end.rotDeg==0f)}"); } }
      if(MarioReaction.StartTime(b,true)!=b.freeze||MarioReaction.StartTime(b,false)!=0f){ rb++; Console.WriteLine($"     [FAIL] {k} 连锁没跳过愣住"); } }
    parts.Add($"{D.Length} 种坑各一种固定反应，演戏 ≤ 原本晕眩，逐帧连续、回原样");
    var file=File.Exists(WsRepo("Assets/Resources/MarioReactions.json"))?File.ReadAllText(WsRepo("Assets/Resources/MarioReactions.json")):null;
    if(file==null){ rb++; Console.WriteLine("     [FAIL] 找不到 Assets/Resources/MarioReactions.json"); }
    else { var ft=MarioReaction.Parse(file,out string err); if(err!=""||MarioReaction.ToJson(ft)!=MarioReaction.ToJson(D)){ rb++; Console.WriteLine($"     [FAIL] 数据文件 ≠ 默认表 {err}"); } else parts.Add("数据文件 = 默认表"); }
    var bad2=MarioReaction.Parse("{oops",out string e2); var part=MarioReaction.Parse("{\"reactions\":[{\"kind\":\"hurt\",\"act\":9,\"pose\":\"Nope\"}]}",out string e3); MarioReaction.TryGet(part,"hurt",out var hh);
    if(e2==""||bad2.Length!=D.Length||part.Length!=D.Length||hh.act>4f||e3==""){ rb++; Console.WriteLine("     [FAIL] 写坏的数据文件没有安全回退"); } else parts.Add("写坏/缺项/超范围都安全回退");
    var view=File.ReadAllText(WsRepo("Assets/Scripts/Gameplay/Step1/MarioReactionView.cs"));
    foreach(var w in new[]{"ApplyKnockbackStun","ExtendStun","velocity","MarioSpeedScale","TricksterController","Rigidbody"}) if(view.Contains(w)){ rb++; Console.WriteLine("     [FAIL] 反应画面组件出现 "+w+"（只能动外观）"); }
    Console.WriteLine($"[{(rb==0?"OK":"FAIL")}] S223 马里奥中招反应：{string.Join("｜",parts)}"); fail+=rb; }
  // S224：总方案阶段 C + 少等待（用户："有些我都没耐心试玩下去"）。宪法 P4：死区（10 秒没事可做）< 15%。
  //  ① 挂机/干等的人：以前一天 81% 是死区 → 自动快进后必须 < 15%；② 会躲的玩家照样 60/60 全埋伏（Wilson 下限 ≥ 0.935），自动快进不让人错过门；
  //  ③ 门口按一次 E = 预约，他走近自动进门；④ 视锥灌注 / 声音圈 = 判定用的同一个数；⑤ 差点被发现的时刻：分段、被抓不算、最险排前。
  { int cb=0; var parts=new List<string>(); var t=MarioMindTuningSO.LoadOrDefault(); const int N=60;
    (double lo,double hi) W(int k,int n){ double z=1.96,p=(double)k/n,d=1+z*z/n,c=p+z*z/(2*n),h=z*Math.Sqrt(p*(1-p)/n+z*z/(4.0*n*n)); return (Math.Max(0,(c-h)/d),Math.Min(1,(c+h)/d)); }
    foreach(var (nm,txt) in new[]{("小镇",OverworldPack.SampleText),("大镇",OverworldPack.BigSampleText),("山镇",OverworldPack.MountainSampleText),("雷镇",OverworldPack.StormSampleText)}){
      double rOld=0,dOld=0,rNew=0,dNew=0; for(int s=1;s<=10;s++){ var a=OverworldBots.PlayDay(OverworldPack.Parse(txt)[0],t,OverworldBots.Kind.Idle,false,s,1+(s-1)%4,true,false); rOld+=a.realSeconds; dOld+=a.deadZoneSeconds;
        var b=OverworldBots.PlayDay(OverworldPack.Parse(txt)[0],t,OverworldBots.Kind.Idle,false,s,1+(s-1)%4,true,true); rNew+=b.realSeconds; dNew+=b.deadZoneSeconds; if(!b.dayEnded){ cb++; Console.WriteLine($"     [FAIL] {nm} 自动快进后挂机一天没结束"); } }
      int full=0; var times=new HashSet<double>(); double dz=0,rr=0;
      for(int s=1;s<=N;s++){ var r=OverworldBots.PlayDay(OverworldPack.Parse(txt)[0],t,OverworldBots.Kind.Hider,false,s,1+(s-1)%4,true,true); if(r.ambush==r.doors) full++; times.Add(Math.Round(r.realSeconds,1)); dz+=r.deadZoneSeconds; rr+=r.realSeconds; }
      var w=W(full,N);
      if(dNew/rNew>=0.15||w.lo<0.935||times.Count<5||dz/rr>=0.15){ cb++; Console.WriteLine($"     [FAIL] {nm}：挂机死区 {dOld/rOld:P0}→{dNew/rNew:P0}（应 <15%）｜会躲+自动快进 {full}/{N} 下限 {w.lo:0.000}（应 ≥0.935） 不同用时 {times.Count} 死区 {dz/rr:P0}"); }
      else parts.Add($"{nm} 干等死区 {dOld/rOld:P0}→{dNew/rNew:P0}，会躲 {full}/{N}（下限 {w.lo:0.000}）");
      OverworldSession.ResetStatics(); }
    // ③ 预约埋伏：走到门口、伪装、只按一次 E → 他走近自动进门（埋伏）
    { int ok=0; foreach(var txt in new[]{OverworldPack.SampleText,OverworldPack.StormSampleText}){ var m=OverworldPack.Parse(txt)[0]; OverworldSession.ResetStatics(); OverworldSession.NewDay(m.name,"Town",1); OverworldSession.Active=true;
        var town=new OverworldTown(m,t){autoFastIdleSeconds=t.overworldAutoFastIdleSeconds}; var dc=town.doorCells[town.NextStop.n]; bool pressed=false;
        for(int i=0;i<30*400&&!OverworldSession.DayOver;i++){ var inp=new OverworldTown.Input();
          if(!town.NearDoor(town.NextStop.n)){ var path=OverworldMap.Path(town.map,OverworldGuide.Near(town.map,town.tx,town.ty),dc); if(path!=null&&path.Count>1){ var c=path[1]; double dx=c.x+.5-town.tx,dy=c.y+.5-town.ty,d=Math.Sqrt(dx*dx+dy*dy); inp.h=(float)(dx/d); inp.v=(float)(dy/d);} }
          else if(!town.disguised) inp.disguise=true; else if(!pressed){ inp.door=true; pressed=true; }
          town.Tick(1f/30f,inp); if(town.wantsEnter){ if(town.enterOutcome==OverworldMind.DoorOutcome.Ambush) ok++; break; } } }
      OverworldSession.ResetStatics();
      if(ok!=2){ cb++; Console.WriteLine($"     [FAIL] 门口只按一次 E 没有自动埋伏：{ok}/2"); } else parts.Add("按一次 E 预约 → 他走近自动埋伏"); }
    // ④ 圈 / 灌注 = 判定
    { bool a=Math.Abs(Step1Readability.FillReach(0.5f,9,9)-4.5f)<1e-4&&Step1Readability.FillReach(1,3,9)==3&&Step1Readability.FillReach(0,9,9)==0;
      bool b=Step1Readability.SoundRadius(Step1Readability.Sound.Taunt,t)==t.hearingRange&&Math.Abs(Step1Readability.SoundRadius(Step1Readability.Sound.Vent,t)-t.hearingRange/3f)<1e-4&&Step1Readability.TownTauntRadius(t)==t.overworldVisionRange*1.5f;
      var town=File.ReadAllText(WsRepo("Assets/Scripts/Overworld/OverworldTown.cs")); var eyes=File.ReadAllText(WsRepo("Assets/Scripts/Gameplay/Step1/MarioEyes.cs")); var rings=File.ReadAllText(WsRepo("Assets/Scripts/Gameplay/Step1/Step1SoundRings.cs"));
      bool c=town.Contains("<= Step1Readability.TownTauntRadius(tuning)")&&town.Contains("<= Step1Readability.TownNoiseRadius(tuning)")&&eyes.Contains("t.hearingRange / 3f")&&!rings.Contains("MarioEyes")&&!rings.Contains("Meter.")&&!rings.Contains("RustleOnPass");
      bool d=t.overworldAutoFastIdleSeconds>0&&t.visionConeFill&&t.soundRings&&MarioMindTuningSO.CurrentDataVersion>=23;
      if(!(a&&b&&c&&d)){ cb++; Console.WriteLine($"     [FAIL] 灌注={a} 声音圈半径={b} 圈和判定同一个数/只是画面={c} 默认开={d}"); } else parts.Add("视锥灌注 + 声音圈 = 判定同一个数"); }
    // ⑤ 差点被发现
    { var log=new NearMissLog(); log.Feed(true,0.4f,"07:00",""); log.Feed(true,0.7f,"07:01","Rustle"); log.Feed(false,0,"",""); log.Feed(true,0.5f,"09:00","Open"); log.Caught("09:01","Open"); log.Feed(true,0.9f,"11:00","Lamp"); log.Feed(false,0,"","");
      var top=log.Closest(3); bool ok=log.NearMisses==2&&top.Count==2&&top[0].clock=="11:00"&&top[1].why=="Rustle"&&Step1Text.NearMissLines(top,2).Contains("草晃了");
      int days=0,withLine=0; for(int s=1;s<=20;s++){ OverworldSession.ResetStatics(); var r=OverworldBots.PlayDay(OverworldPack.Parse(OverworldPack.SampleText)[0],t,OverworldBots.Kind.Follower,true,s,1,true,true); days++; if(OverworldSession.Summary(r.doors).Contains("他差点发现你")||OverworldSession.Summary(r.doors).Contains("没怀疑过")) withLine++; }
      OverworldSession.ResetStatics();
      if(!ok||withLine!=days){ cb++; Console.WriteLine($"     [FAIL] 差点被发现：分段={ok} 结算里有这一行 {withLine}/{days}"); } else parts.Add("一天结束列出最险的 3 次"); }
    Console.WriteLine($"[{(cb==0?"OK":"FAIL")}] S224 少等待 + 阶段 C 可读性：{string.Join("｜",parts)}"); fail+=cb; }
  // S225：① 房间死区（宪法 P4，S224 只量了小镇）：每个样板 / 监狱塔 / 向导房间，开局等待后"连续 ≥10 秒马里奥不经过任何机关"的时间占比 < 15%
  //  ② 按 L / P 没成功一定有中文原因（樱井"消灭无反应"）：TricksterController 里每一条失败原因都翻译到，不落到兜底句；显示组件只听不改
  //  ③ 起疑台词分层（Splinter Cell Blacklist）：每种原因有具体台词，第 3 次起换短句；原因只来自感知（H4）④ 卡住救援头顶一句话
  { int eb=0; var parts=new List<string>(); var t=MarioMindTuningSO.LoadOrDefault();
    var rooms=new List<(string,string[])>(); rooms.Add(("默认房间",Step1PrankRoomBuilderRoom())); foreach(var x in Samples()) rooms.Add(x);
    for(int f=2;f<=6;f+=2) rooms.Add(($"监狱塔{f}层",FloorStacker.Build(f,0))); foreach(char star in LevelBlueprint.WizardStars) rooms.Add(($"向导{star}",LevelBlueprint.Wizard(star,30,"").grid));
    double worstShare=0; float worstIdle=0; string worstName=""; int measured=0;
    foreach(var (n,g) in rooms){ float speed=StrategySim.RunSpeed(9f,t.marioSpeedScale); var r=StrategySim.Analyze(g,speed,t.startDelaySeconds,3,1.6f,1.5f,3f,1.8f); if(r.route==null) continue; measured++;
      var route=r.route.Select(c=>(c.x,c.y)).ToList(); var times=new List<float>{t.startDelaySeconds}; float len=0;
      for(int i=1;i<route.Count;i++){ len+=(float)Math.Sqrt((route[i].x-route[i-1].x)*(route[i].x-route[i-1].x)+(route[i].y-route[i-1].y)*(route[i].y-route[i-1].y)); times.Add(t.startDelaySeconds+len/speed); }
      var passes=LevelBlueprint.Passes(route,times,r.onRoute.Select(s=>(s.x,s.y))); var (segs,_)=LevelBlueprint.Rhythm(passes,r.routeSeconds);
      float dead=0,idle=0; foreach(var sg in segs){ if(sg.busy) continue; float a=Math.Max(sg.a,LevelBlueprint.StartGrace), L=sg.b-a; if(L<=0) continue; idle=Math.Max(idle,L); if(L>=LevelBlueprint.MaxIdle) dead+=L; }
      double share=dead/Math.Max(0.1f,r.routeSeconds); if(share>worstShare||idle>worstIdle){ worstName=n; } worstShare=Math.Max(worstShare,share); worstIdle=Math.Max(worstIdle,idle);
      if(share>=0.15){ eb++; Console.WriteLine($"     [FAIL] {n}：死区 {share:P0}（应 <15%），最长 {idle:0.0} 秒没机关 → 路上加一个机关或缩短这段"); } }
    if(measured<8){ eb++; Console.WriteLine($"     [FAIL] 只量到 {measured} 个房间（路线算不出来？）"); } else parts.Add($"{measured} 个房间死区最高 {worstShare:P0}、最长空档 {worstIdle:0.0} 秒（开局等待不算）");
    // ②
    var ctl=File.ReadAllText(WsRepo("Assets/Scripts/Enemy/TricksterController.cs")); int a0=ctl.IndexOf("private string GetAbilityFailReason()"); var body=ctl.Substring(a0,ctl.IndexOf("#endregion",a0)-a0);
    var reasons=System.Text.RegularExpressions.Regex.Matches(body,"return \\$?\"([^\"]+)\"").Select(m=>m.Groups[1].Value.Replace("{abilitySystem.PossessionState}","Blending")).ToList();
    reasons.Add("Not enough energy to disguise!"); string fb=Step1Text.AbilityFailZh("???"); int un=0;
    foreach(var rs in reasons){ var z=Step1Text.AbilityFailZh(rs); if(z==fb||!System.Text.RegularExpressions.Regex.IsMatch(z,"[\u4e00-\u9fff]")){ un++; Console.WriteLine("     [FAIL] 按 L 失败原因没翻译："+rs); } }
    var ff=File.ReadAllText(WsRepo("Assets/Scripts/Gameplay/Step1/Step1FailFeedback.cs")); var combo=File.ReadAllText(WsRepo("Assets/Scripts/Gameplay/Step1/Step1Combo.cs"));
    bool wired=ff.Contains("OnAbilityFailed +=")&&ff.Contains("OnDisguiseFailed +=")&&ff.Contains("Step1Hint.Show")&&combo.Contains("AddComponent<Step1FailFeedback>()");
    bool passive=!System.Text.RegularExpressions.Regex.IsMatch(ff,"OnAbilityPressed\\(|OnDisguisePressed\\(|ToggleDisguise|Disguise\\(\\)|cooldownTimer|TryConsume|velocity");
    bool pReasons=Step1Text.DisguiseFailZh(false,false,2.2f,false).Contains("3 秒")&&Step1Text.DisguiseFailZh(true,false,0,false)!=null&&Step1Text.DisguiseFailZh(false,true,0,false)!=null&&Step1Text.DisguiseFailZh(false,false,0,false)==null;
    if(un>0||reasons.Count<12||!wired||!passive||!pReasons){ eb++; Console.WriteLine($"     [FAIL] 失败原因：{reasons.Count} 条 未翻译 {un} 接线={wired} 只显示不改={passive} P 原因={pReasons}"); } else parts.Add($"按 L 的 {reasons.Count} 种失败 + 按 P 的 3 种静默失败都有中文原因");
    // ③④
    var causes=new[]{SuspicionCause.SawYou,SuspicionCause.OddProp,SuspicionCause.SawTrap,SuspicionCause.Rustle,SuspicionCause.Taunt,SuspicionCause.Hurt};
    var first=new HashSet<string>(); bool tier=true; foreach(var c in causes){ var a=Step1Text.CauseIntent(MarioMindState.Curious,c,1); var b=Step1Text.CauseIntent(MarioMindState.Curious,c,3); first.Add(a); if(a==null||b==null||a==b||!a.Contains("\n")||b.Length>=a.Length+4) tier=false; }
    tier&=first.Count==causes.Length&&Step1Text.CauseIntent(MarioMindState.Curious,SuspicionCause.None,1)==null&&Step1Text.CauseIntent(MarioMindState.Chasing,SuspicionCause.SawYou,1)==null;
    var mind=File.ReadAllText(WsRepo("Assets/Scripts/Gameplay/Step1/RushMarioMind.cs")); int sc=mind.IndexOf("public static SuspicionCause StrongestCause"); var scBody=mind.Substring(sc,mind.IndexOf("}",mind.IndexOf("return SuspicionCause.None",sc))-sc);
    bool h4=!System.Text.RegularExpressions.Regex.IsMatch(scBody,"Trickster(Controller|Possession)|Disguise|IsFullyBlended")&&mind.Contains("StrongestCause(seesTrickster, seesOddProp, p.witnessedActivation, p.sawRustle, p.heardTaunt, p.hurt)");
    var lbl=File.ReadAllText(WsRepo("Assets/Scripts/Gameplay/Step1/MarioMindLabel.cs")); var resc=File.ReadAllText(WsRepo("Assets/Scripts/Gameplay/Step1/Step1StuckRescue.cs"));
    bool head=lbl.Contains("Step1Text.CauseIntent(order.state, order.cause, order.causeTimes)")&&lbl.Contains("Step1Text.StuckRescueHead")&&resc.Contains("MarioMindLabel.RaiseRescued()")&&Step1Text.HeadIntent(MarioMindState.Running,"CAREFUL").Contains("坑过");
    if(!tier||!h4||!head){ eb++; Console.WriteLine($"     [FAIL] 分层台词={tier} 原因只来自感知={h4} 头顶接线/卡住一句话/小心说原因={head}"); } else parts.Add("6 种起疑原因各有台词、第 3 次换短句，原因只来自感知；卡住救援头顶一句话");
    Console.WriteLine($"[{(eb==0?"OK":"FAIL")}] S225 房间死区 + 按了就有反应 + 他说为什么：{string.Join("｜",parts)}"); fail+=eb; }
  Console.WriteLine(fail==0?"SIM ALL OK":"SIM FAILURES: "+fail);
  Environment.Exit(fail==0?0:1);
  static float KnockbackHelperLift(float up,float min)=>Math.Max(up,min);
  static string WsRepo(string rel)=>System.IO.Path.Combine("/home/user/workspace/repo",rel);
  static string[] Step1PrankRoomBuilderRoom()=>File.ReadAllText("room_template.txt").Replace("\r","").Split('\n').Where(l=>l.Length>0).ToArray();
 }}
