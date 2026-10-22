using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text.Json;
class Program {
 static object State(WorldParkPack.Session s)=>new {venue=s.venueId,node=s.nodeId,found=s.found.OrderBy(x=>x,StringComparer.Ordinal).ToArray(),revealed=s.revealed.OrderBy(x=>x,StringComparer.Ordinal).ToArray(),results=s.results.OrderBy(x=>x.Key,StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.Value),rooms=s.roomResults.Select(r=>new{venue=r.venue,node=r.node,won=r.won}).ToArray(),activities=s.activityResults.Select(r=>new{venue=r.venue,node=r.node,kind=r.kind,won=r.won}).ToArray(),log=s.log.ToArray()};
 static void Main(string[] args){
  if(args[0]=="map"){File.WriteAllText(args[1],FeatureMap.Markdown());Console.WriteLine("功能地图 "+FeatureMap.All.Length);return;}
  if(args[0]=="demo"){Demo(args[1]);return;}
  if(args[0]=="games"){Games(args[1],args[2]);return;}
  if(args[0]=="explore"){Explore(args[1],args[2]);return;}
  if(args[0]=="views"){Views(args[1],args[2]);return;}
  using var doc=JsonDocument.Parse(File.ReadAllText(args[0]));var output=new List<object>();
  foreach(var t in doc.RootElement.EnumerateArray()){
   var name=t.GetProperty("name").GetString();var p=WorldParkPack.Parse(t.GetProperty("json").GetString(),out string error);
   if(p==null){output.Add(new{name,parsed=false});continue;}
   var issues=WorldParkPack.Validate(p).Select(i=>i.severity+"|"+i.code+"|"+i.venue+"|"+i.node).OrderBy(i=>i,StringComparer.Ordinal).ToArray();
   var s=WorldParkPack.Start(p,out error);var states=new List<object>();
   if(s!=null){states.Add(State(s));foreach(var a in t.GetProperty("actions").EnumerateArray()){
    string action=a.GetString();bool ok;
    if(action=="next")ok=s.Continue(out error);else if(action=="win"||action=="lose")ok=s.ResolveRoom(action=="win",out error);else if(action=="explore-win"||action=="explore-lose")ok=s.ResolveExplore(action=="explore-win",out error);else if(action.StartsWith("observe:"))ok=s.ObserveClue(action.Substring(8),out error);else if(action.StartsWith("game:"))ok=s.ResolveGame(action.Substring(5).Split('|'),out error);else ok=s.Travel(action.Substring(7),out error);states.Add(new{ok,state=State(s)});
   }}output.Add(new{name,parsed=true,issues,started=s!=null,states});
  }File.WriteAllText(args[1],JsonSerializer.Serialize(output));Console.WriteLine("C#跨端用例 "+output.Count);
 }
 // S251：博弈内核逐轮对比。用例的 json 是只含一个 game 节点的世界包，借此同时比较 game 字段解析。
 static void Games(string input,string outputPath){
  using var doc=JsonDocument.Parse(File.ReadAllText(input));var output=new List<object>();
  foreach(var t in doc.RootElement.EnumerateArray()){
   var name=t.GetProperty("name").GetString();var p=WorldParkPack.Parse(t.GetProperty("json").GetString(),out string error);
   if(p==null){output.Add(new{name,parsed=false});continue;}
   var def=p.venues[0].nodes.First(n=>n.kind=="game").game;var choices=t.GetProperty("choices").EnumerateArray().Select(c=>c.GetString()).ToList();
   var issues=BorderlandGame.Check(def).Select(i=>i.severity+"|"+i.code).OrderBy(i=>i,StringComparer.Ordinal).ToArray();
   var st=BorderlandGame.Run(def,choices);
   output.Add(new{name,parsed=true,issues,finished=st.finished,won=st.won,error=st.error,round=st.round,player=st.player,lives=st.lives,points=st.points,alive=st.alive,prompt=st.prompt,transcript=st.transcript.ToArray(),pending=st.pending.ToArray(),records=st.records.Select(r=>new{round=r.round,choice=r.choice,livesBefore=r.livesBefore,livesAfter=r.livesAfter,pointsAfter=r.pointsAfter,aliveAfter=r.aliveAfter,correct=r.correct,informedAnswer=r.informedAnswer,firstClaim=r.firstClaim,eliminated=r.eliminated.ToArray(),picks=r.picks,marks=r.marks,target=r.target,opponent=r.opponent,winners=r.winners.ToArray()}).ToArray()});
  }
  File.WriteAllText(outputPath,JsonSerializer.Serialize(output,new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));Console.WriteLine("C#博弈用例 "+output.Count);
 }
 // S253：现场探索内核逐步对比（网页试玩 explore.js ↔ C# WorldParkExplore）。pre = 走到 explore 节点的流程动作；steps = [dt,h,v] 或 ["act",点位id]。
 static double Num(JsonElement e)=>e.ValueKind==JsonValueKind.String?double.Parse(e.GetString(),System.Globalization.CultureInfo.InvariantCulture):e.GetDouble();
 static bool Apply(WorldParkPack.Session s,string action,out string error){
  if(action=="next")return s.Continue(out error);if(action=="win"||action=="lose")return s.ResolveRoom(action=="win",out error);
  if(action=="explore-win"||action=="explore-lose")return s.ResolveExplore(action=="explore-win",out error);
  if(action.StartsWith("observe:"))return s.ObserveClue(action.Substring(8),out error);if(action.StartsWith("game:"))return s.ResolveGame(action.Substring(5).Split('|'),out error);
  return s.Travel(action.Substring(7),out error);
 }
 static object ExploreState(WorldParkExplore.Run r){var near=r.NearbyPoint;return new{x=r.x,y=r.y,elapsed=r.Elapsed,remaining=r.Remaining,ended=r.Ended,won=r.Won,caughtBy=r.CaughtBy,msg=r.LastMessage,hidden=r.PlayerHidden,hunted=r.Hunted,nearby=near==null?"":near.id,
  chasers=r.Chasers.Select(c=>new{id=c.point.id,x=c.x,y=c.y,fx=c.fx,fy=c.fy,mode=c.mode,sees=c.SeesPlayer,lastX=c.lastX,lastY=c.lastY,playerSees=r.PlayerSees(c)}).ToArray(),ev=r.Events.Count,found=r.session.found.OrderBy(x=>x,StringComparer.Ordinal).ToArray()};}
 static void Explore(string input,string outputPath){
  using var doc=JsonDocument.Parse(File.ReadAllText(input));var output=new List<object>();
  foreach(var t in doc.RootElement.EnumerateArray()){
   var name=t.GetProperty("name").GetString();var p=WorldParkPack.Parse(t.GetProperty("json").GetString(),out string error);
   if(p==null){output.Add(new{name,error="parse:"+error});continue;}
   var s=WorldParkPack.Start(p,out error);if(s==null){output.Add(new{name,error="start:"+error});continue;}
   string preError="";foreach(var a in t.GetProperty("pre").EnumerateArray()){if(!Apply(s,a.GetString(),out error)){preError=a.GetString()+":"+error;break;}}
   if(preError!=""){output.Add(new{name,error="pre:"+preError});continue;}
   var run=WorldParkExplore.Create(s,out error);if(run==null){output.Add(new{name,error="create:"+error});continue;}
   var states=new List<object>{ExploreState(run)};var acts=new List<object>();
   foreach(var st in t.GetProperty("steps").EnumerateArray()){
    if(st[0].ValueKind==JsonValueKind.String&&st[0].GetString()=="act"){bool ok=run.Interact(st[1].GetString(),out string m);acts.Add(new{ok,m});}
    else run.Tick(Num(st[0]),Num(st[1]),Num(st[2]));
    states.Add(ExploreState(run));
   }
   output.Add(new{name,error="",states,acts,events=run.Events.Select(e=>new{time=e.time,chaser=e.chaser,name=e.name,type=e.type}).ToArray()});
  }
  File.WriteAllText(outputPath,JsonSerializer.Serialize(output,new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));Console.WriteLine("C#探索用例 "+output.Count);
 } // S253：网页试玩画面（play-model.js）↔ C# WorldParkSceneModel / WorldParkTableModel / WorldParkStageCues 逐项对比。
 // 每个动作之前：剧情类阶段输出场景卡；博弈动作逐轮输出各时刻的博弈桌；探索/博弈输出 Activity；会场入口输出 Entrance。
 static void Views(string input,string outputPath){
  using var doc=JsonDocument.Parse(File.ReadAllText(input));var output=new List<object>();
  foreach(var t in doc.RootElement.EnumerateArray()){
   var name=t.GetProperty("name").GetString();var p=WorldParkPack.Parse(t.GetProperty("json").GetString(),out string error);
   if(p==null){output.Add(new{name,error="parse:"+error});continue;}
   var s=WorldParkPack.Start(p,out error);if(s==null){output.Add(new{name,error="start:"+error});continue;}
   var times=t.GetProperty("times").EnumerateArray().Select(x=>x.GetDouble()).ToArray();var frames=new List<object>();string fail="";
   foreach(var a in t.GetProperty("actions").EnumerateArray()){
    string action=a.GetString();var v=s.Venue;var n=s.Current;
    var f=new Dictionary<string,object>{["action"]=action,["venue"]=s.venueId,["node"]=s.nodeId,["entrance"]=WorldParkStageCues.Entrance(v)};
    if(n.kind=="story"||n.kind=="reveal"||n.kind=="ending"||n.kind=="room"){var sc=WorldParkSceneModel.Build(s,null);f["scene"]=sc;f["ready"]=WorldParkSceneModel.ReadyAt(sc);f["visible"]=new[]{0.0,0.4,1.0,3.3}.Select(x=>WorldParkSceneModel.Visible(sc.text,x)).ToArray();f["ending"]=WorldParkStageCues.Ending(v,n);}
    if(action.StartsWith("game:")){var ch=action.Substring(5).Split('|').ToList();var tables=new List<object>();var elim=new List<object>();
     tables.Add(WorldParkTableModel.Build(n.game,null,BorderlandGame.Run(n.game,new List<string>()),-1));
     for(int k=1;k<=ch.Count;k++){var before=BorderlandGame.Run(n.game,ch.Take(k-1).ToList());var st=BorderlandGame.Run(n.game,ch.Take(k).ToList());
      foreach(var x in times)tables.Add(WorldParkTableModel.Build(n.game,before,st,x));elim.Add(WorldParkStageCues.NewlyEliminated(n.game,before,st));
      if(st.finished){f["activity"]=WorldParkStageCues.Activity(v,st.won,st.won?"博弈通过":"你输掉了这场博弈");}}
     f["tables"]=tables;f["eliminated"]=elim;}
    if(action=="explore-win"||action=="explore-lose")f["activity"]=WorldParkStageCues.Activity(v,action=="explore-win","现场结果");
    frames.Add(f);if(action=="end")break;
    if(!Apply(s,action,out error)){fail=action+":"+error;break;}
   }
   var beeps=new List<int>();for(double r=12.0;r>-0.5;r-=0.37)beeps.Add(WorldParkStageCues.CountdownBeep(r,r-0.37));
   output.Add(new{name,error=fail,frames,beeps});
  }
  File.WriteAllText(outputPath,JsonSerializer.Serialize(output,new JsonSerializerOptions{IncludeFields=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));Console.WriteLine("C#画面用例 "+output.Count);
 } static void Demo(string file){
  var p=WorldParkPack.Parse(File.ReadAllText(file),out string error);if(p==null)throw new Exception(error);
  foreach(bool investigate in new[]{true,false})foreach(string exit in new[]{"west","east"}){
   var s=WorldParkPack.Start(p,out error);if(s==null)throw new Exception(error);s.Continue(out error);
   var run=WorldParkExplore.Create(s,out error);if(run==null)throw new Exception(error);
   if(run.Interact("plate",out error))throw new Exception("远程调查被接受");
   if(investigate)foreach(var point in run.node.points.Where(q=>q.kind=="clue")){Walk(run,point.x,point.y);if(!run.Interact(point.id,out error))throw new Exception(error);}
   var dest=run.node.points.Single(q=>q.id==exit);Walk(run,dest.x,dest.y);if(!run.Interact(exit,out error))throw new Exception(error);
   if(!run.Ended||run.Won!=(exit=="west"))throw new Exception("出口结果错误");
   if(run.Interact(exit,out error))throw new Exception("出口可重复结算");
   if(!s.ResolveExplore(run.Won,out error)||!s.Continue(out error))throw new Exception(error);
   if(s.roomResults.Count!=0||s.activityResults.Count!=1)throw new Exception("活动事实污染房间结果");
   Console.WriteLine((investigate?"调查":"不调查")+"→"+exit+": "+s.nodeId+" found="+s.found.Count+" revealed="+s.revealed.Count+" activity="+s.activityResults.Count+" rooms="+s.roomResults.Count);
  }
 }
 static void Walk(WorldParkExplore.Run r,int x,int y){
  var from=new OverworldMap.Cell((int)Math.Floor(r.x),(int)Math.Floor(r.y));var to=new OverworldMap.Cell(x,y);
  var queue=new Queue<OverworldMap.Cell>();var prev=new Dictionary<OverworldMap.Cell,OverworldMap.Cell>();queue.Enqueue(from);prev[from]=from;int[] dx={1,0,-1,0},dy={0,1,0,-1};
  while(queue.Count>0&&!prev.ContainsKey(to)){var c=queue.Dequeue();for(int i=0;i<4;i++){var n=new OverworldMap.Cell(c.x+dx[i],c.y+dy[i]);if(OverworldMap.Walkable(r.map,n.x,n.y)&&!prev.ContainsKey(n)){prev[n]=c;queue.Enqueue(n);}}}
  if(!prev.ContainsKey(to))throw new Exception("出口/线索不可达");var path=new List<OverworldMap.Cell>();for(var c=to;!c.Equals(from);c=prev[c])path.Add(c);path.Reverse();
  foreach(var c in path){int budget=200;while(Math.Abs(r.x-c.x-0.5)>0.025||Math.Abs(r.y-c.y-0.5)>0.025){if(--budget==0)throw new Exception("真实碰撞卡住 "+c);double h=c.x+0.5-r.x,v=c.y+0.5-r.y,dist=Math.Sqrt(h*h+v*v);r.Tick(Math.Min(0.025,dist/4),h/dist,v/dist);}}
 }
}
