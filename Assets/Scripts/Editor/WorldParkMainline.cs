using System.Collections.Generic;
using UnityEditor;

/// <summary>存量地图工坊的试玩也进入同一世界会话，不再启动默认日程守宝。</summary>
public static class WorldParkMainline
{
    public static WorldParkPack.Pack MapPreview(string text)
    {
        var map=OverworldMap.Parse(text); OverworldMap.Cell spawn=new OverworldMap.Cell(-1,-1);
        for (int y=0;y<map.H;y++) for (int x=0;x<map.W;x++) if (map.At(x,y)=='T') spawn=new OverworldMap.Cell(x,y);
        var queue=new Queue<OverworldMap.Cell>(); var seen=new HashSet<OverworldMap.Cell>();
        var far=spawn; queue.Enqueue(spawn); seen.Add(spawn);
        int[] dx={1,0,-1,0},dy={0,1,0,-1};
        while (queue.Count>0)
        {
            var p=queue.Dequeue();far=p;
            for (int i=0;i<4;i++) { var n=new OverworldMap.Cell(p.x+dx[i],p.y+dy[i]); if (OverworldMap.Walkable(map,n.x,n.y)&&seen.Add(n)) queue.Enqueue(n); }
        }
        var activity=new WorldParkPack.Node {id="walk",kind="explore",name="地图空间预览",text="走到出口结束。这里只核对地图通行与镜头，不预设守宝或角色日程。",mapText=text,onWin="clear",onLose="fail"};
        activity.points.Add(new WorldParkPack.ExplorePoint {id="exit",kind="exit",label="结束地图预览",x=far.x,y=far.y,outcome="win"});
        var venue=new WorldParkPack.Venue {id="map",name=map.name,role="player",entry="walk",rules="这是当前作者地图的世界片段预览；不会把结果写回旧日程存档。"};
        venue.nodes.Add(activity);
        venue.nodes.Add(new WorldParkPack.Node {id="clear",kind="ending",name="地图预览结束",text="地图几何试玩完成；这不等于故事与玩法已验收。",result="clear"});
        venue.nodes.Add(new WorldParkPack.Node {id="fail",kind="ending",name="预览未完成",result="fail"});
        var pack=new WorldParkPack.Pack {id="map_preview",name="当前地图预览",entryVenue="map"};pack.venues.Add(venue);return pack;
    }
    public static void PreviewCurrentMap()
    {
        var pack=MapPreview(OverworldBuilder.CurrentText);
        if (!WorldParkBuilder.TryRun(pack,out string report)) EditorUtility.DisplayDialog("世界地图",report,"好");
    }
}
