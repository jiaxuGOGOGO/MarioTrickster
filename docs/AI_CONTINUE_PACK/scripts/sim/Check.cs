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
  Console.WriteLine(fail==0?"SIM ALL OK":"SIM FAILURES: "+fail);
  Environment.Exit(fail==0?0:1);
  static float KnockbackHelperLift(float up,float min)=>Math.Max(up,min);
  static string[] Step1PrankRoomBuilderRoom()=>File.ReadAllText("room_template.txt").Replace("\r","").Split('\n').Where(l=>l.Length>0).ToArray();
 }}
