using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text.Json;
namespace UnityEngine { public static class Application { public static string persistentDataPath = Path.GetFullPath(".work/worldpark-verify/save-fixture"); } }
class Program
{
 static int checks;static List<string> routes=new List<string>();
 static void Check(bool ok,string error){checks++;if(!ok)throw new Exception(error);}
 static void Main(string[] args){
  if(args.Length>0&&args[0]=="feature"){File.WriteAllText("docs/FEATURE_MAP.md",FeatureMap.Markdown());Console.WriteLine("FeatureMap "+FeatureMap.All.Length);return;}
  if(args.Length>0&&args[0]=="parity"){Parity(args[1],args[2]);return;}
  var p=WorldParkPack.Parse(File.ReadAllText("Assets/Resources/WorldPark/Demo.worldpark.json"),out string e);Check(p!=null,e);Check(!WorldParkPack.Validate(p).Any(i=>i.severity=="error"),"默认包结构不合法");
  foreach(bool firstWin in new[]{true,false}) foreach(bool secondWin in firstWin?new[]{true,false}:new[]{false})Journey(p,firstWin,secondWin);
  var s=WorldParkPack.Start(p,out e);string saved=WorldParkProgress.Save(s,out e);Check(saved!=null,e);Check(!saved.Contains("truth")&&!saved.Contains("hint")&&!saved.Contains("维修单："),"存档泄漏作者内容");
  var different=WorldParkPack.Parse(WorldParkPack.ToJson(p),out e);different.premise+="修改";Check(WorldParkProgress.Restore(different,saved,out e)==null,"修改包却接受旧检查点");
  Check(WorldParkProgress.Restore(p,saved.Replace("\"kind\":\"continue\"","\"kind\":\"invalid\""),out e)!=null,"初始空事件存档不应受无匹配替换影响");
  Check(WorldParkSaveStore.TrySave(s,out e),e);string path=WorldParkSaveStore.PathFor(p);byte[] good=File.ReadAllBytes(path);Check(WorldParkSaveStore.TryLoad(p,out var loaded,out e),e);Check(State(s)==State(loaded),"磁盘恢复不一致");
  Check(s.Continue(out e),e);Check(!WorldParkSaveStore.TrySave(s,out e),"活动中途不应写存档");Check(good.SequenceEqual(File.ReadAllBytes(path)),"坏保存覆盖旧检查点");
  File.WriteAllText(path,"{broken");Check(!WorldParkSaveStore.TryLoad(p,out loaded,out e),"坏文件不应回退新旅程");Check(loaded==null,"坏文件产生会话");File.WriteAllBytes(path,good);
  File.WriteAllText(".work/worldpark-verify/journey.json",JsonSerializer.Serialize(new{checks,routes,pass=true}));Console.WriteLine("综合纯逻辑验收通过 "+checks+" 断言；"+routes.Count+"条真实移动旅程");
 }
 static void Journey(WorldParkPack.Pack p,bool firstWin,bool secondWin){
  var s=WorldParkPack.Start(p,out string e);Checkpoint(s);Check(s.Continue(out e),e);Explore(s,firstWin?"west":"east",true);Check(s.Continue(out e),e);Checkpoint(s);
  Check(s.CanTravel("venue_gallery")==firstWin,"第一场结果门槛错误");Check(s.CanTravel("venue_archive")==!firstWin,"失败分支条件错误");
  if(firstWin){Check(s.Travel("venue_gallery",out e),e);Checkpoint(s);Check(s.Continue(out e),e);
   if(secondWin){Explore(s,"return_gate",true);}else{var run=WorldParkExplore.Create(s,out e);Check(run!=null,e);for(int i=0;i<90;i++)run.Tick(1,0,0);Check(run.Ended&&!run.Won&&run.Remaining==0,"计时未锁存失败");Check(s.ResolveExplore(false,out e),e);Checkpoint(s);}
   Check(s.Continue(out e),e);Checkpoint(s);Check(s.Travel("venue_archive",out e),e);
  }else Check(s.Travel("venue_archive",out e),e);
  Checkpoint(s);Check(s.Continue(out e),e);Explore(s,"leave",true);Check(s.Continue(out e),e);Checkpoint(s);
  Check(s.results["venue_town"]==(firstWin?"clear":"fail"),"上游结果被抹掉");Check(s.results["venue_archive"]=="recorded","自定义结局未保存");Check(s.roomResults.Count==0,"探索冒充房间结果");Check(s.activityResults.Count==(firstWin?3:2),"活动事实数错误");
  routes.Add((firstWin?"第一场通过":"第一场失败")+(firstWin?(secondWin?"→检修通过":"→检修超时"):"")+"→记录层recorded");Console.WriteLine(routes.Last());
 }
 static void Explore(WorldParkPack.Session s,string exit,bool investigate){
  var run=WorldParkExplore.Create(s,out string e);Check(run!=null,e);Check(WorldParkProgress.Save(s,out e)==null,"探索阶段不能作为检查点");
  var point=run.node.points.Single(q=>q.id==exit);
  if(exit=="return_gate")Check(!run.Interact(exit,out e),"没拿检修单却可验收");
  if(investigate)foreach(var clue in run.node.points.Where(q=>q.kind=="clue")){Walk(run,clue.x,clue.y);Check(run.Interact(clue.id,out e),e);}
  Walk(run,point.x,point.y);Check(run.Interact(point.id,out e),e);bool won=run.Won;Check(run.Ended,"出口没有锁存");Check(!run.Interact(point.id,out e),"出口重复结算");Check(s.ResolveExplore(won,out e),e);Checkpoint(s);
 }
 static void Checkpoint(WorldParkPack.Session s){string json=WorldParkProgress.Save(s,out string e);Check(json!=null,e);var restored=WorldParkProgress.Restore(s.pack,json,out e);Check(restored!=null,e);Check(State(s)==State(restored),"检查点恢复状态/结果/动作不一致");}
 static string State(WorldParkPack.Session s)=>JsonSerializer.Serialize(new{s.venueId,s.nodeId,found=s.found.OrderBy(x=>x).ToArray(),revealed=s.revealed.OrderBy(x=>x).ToArray(),results=s.results.OrderBy(x=>x.Key).ToArray(),rooms=s.roomResults.Select(x=>new{x.venue,x.node,x.won}),activities=s.activityResults.Select(x=>new{x.venue,x.node,x.kind,x.won}),actions=s.actions.Select(x=>new{x.kind,x.value})});
 static void Walk(WorldParkExplore.Run r,int x,int y){var from=new OverworldMap.Cell((int)Math.Floor(r.x),(int)Math.Floor(r.y));var to=new OverworldMap.Cell(x,y);var q=new Queue<OverworldMap.Cell>();var prev=new Dictionary<OverworldMap.Cell,OverworldMap.Cell>();q.Enqueue(from);prev[from]=from;int[] dx={1,0,-1,0},dy={0,1,0,-1};while(q.Count>0&&!prev.ContainsKey(to)){var c=q.Dequeue();for(int i=0;i<4;i++){var n=new OverworldMap.Cell(c.x+dx[i],c.y+dy[i]);if(OverworldMap.Walkable(r.map,n.x,n.y)&&!prev.ContainsKey(n)){prev[n]=c;q.Enqueue(n);}}}Check(prev.ContainsKey(to),"点不可达 "+to);var path=new List<OverworldMap.Cell>();for(var c=to;!c.Equals(from);c=prev[c])path.Add(c);path.Reverse();foreach(var c in path){int budget=200;while(Math.Abs(r.x-c.x-0.5)>0.025||Math.Abs(r.y-c.y-0.5)>0.025){if(--budget<=0||r.Ended)throw new Exception("移动卡住/超时 "+c);double h=c.x+0.5-r.x,v=c.y+0.5-r.y,d=Math.Sqrt(h*h+v*v);r.Tick(Math.Min(0.025,d/4),h/d,v/d);}}}
 static void Parity(string input,string output){
  using var d=JsonDocument.Parse(File.ReadAllText(input));var results=new List<object>();foreach(var t in d.RootElement.EnumerateArray()){string name=t.GetProperty("name").GetString();var p=WorldParkPack.Parse(t.GetProperty("json").GetString(),out string e);if(p==null){results.Add(new{name,parsed=false});continue;}var issues=WorldParkPack.Validate(p).Select(i=>i.severity+"|"+i.code+"|"+i.venue+"|"+i.node).OrderBy(x=>x,StringComparer.Ordinal).ToArray();var s=WorldParkPack.Start(p,out e);var states=new List<string>();if(s!=null){states.Add(ParityState(s));foreach(var act in t.GetProperty("actions").EnumerateArray()){string a=act.GetString();bool ok=a=="next"?s.Continue(out e):a.StartsWith("observe:")?s.ObserveClue(a.Substring(8),out e):a.StartsWith("travel:")?s.Travel(a.Substring(7),out e):s.ResolveExplore(a=="explore-win",out e);states.Add((ok?"true":"false")+ParityState(s));}}results.Add(new{name,parsed=true,issues,started=s!=null,states});}File.WriteAllText(output,JsonSerializer.Serialize(results));Console.WriteLine("跨端C# "+results.Count);
 }
 static string ParityState(WorldParkPack.Session s)=>JsonSerializer.Serialize(new{venue=s.venueId,node=s.nodeId,found=s.found.OrderBy(x=>x,StringComparer.Ordinal),revealed=s.revealed.OrderBy(x=>x,StringComparer.Ordinal),results=s.results.OrderBy(x=>x.Key,StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.Value),rooms=s.roomResults.Select(x=>new{x.venue,x.node,x.won}),activities=s.activityResults.Select(x=>new{x.venue,x.node,x.kind,x.won}),log=s.log.ToArray()});
}
