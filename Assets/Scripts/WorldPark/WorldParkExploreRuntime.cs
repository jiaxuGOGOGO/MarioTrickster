using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>世界主视角：二维正交/斜俯视美术，规则只由纯探索内核产生，不绑定日程或守宝AI。</summary>
[DefaultExecutionOrder(50)]
public sealed class WorldParkExploreRuntime : MonoBehaviour
{
    private WorldParkExplore.Run run;
    private WorldParkRuntime owner;
    private Camera cam;
    private SpriteRenderer actor;
    private Transform root;
    private readonly List<SpriteRenderer> covers = new List<SpriteRenderer>();
    private readonly Dictionary<string, SpriteRenderer> markers = new Dictionary<string, SpriteRenderer>(StringComparer.Ordinal);
    private readonly List<UnityEngine.Object> ownedAssets = new List<UnityEngine.Object>();
    private readonly Dictionary<string, Color[]> groundCache = new Dictionary<string, Color[]>();
    private Sprite square;
    private bool ready, paused, notebook, submitted, confirmExit;
    private bool menuSettings; // S254 暂停菜单里切到“设置”页
    private WorldParkPack.ExplorePoint pendingExit;
    private float zoom = 5.5f, lastX, lastY;
    private GUIStyle text, title, small, box;
    private Vector2 noteScroll;
    private string message = "";
    // S252 追逐者视图与演出状态（只读内核状态，不改变规则）。
    private sealed class ChaserView { public WorldParkExplore.Chaser c; public SpriteRenderer body, cone; public float lx, ly, stepLeft; }
    private readonly List<ChaserView> chaserViews = new List<ChaserView>();
    private Sprite coneSprite;
    private int seenEvents;
    private bool endAnnounced;
    private GUIStyle mark, rich;

    private void Start()
    {
        owner = GetComponent<WorldParkRuntime>();
        // 兼容S249已生成的探索场景；新构建已将两组件放在同对象。
        if (owner == null) owner = FindObjectOfType<WorldParkRuntime>();
        var session = WorldParkRuntime.ActiveSession;
        if (owner == null || session == null) { enabled = false; return; }
        run = WorldParkExplore.Create(session,out string error);
        if (run == null) { message = "探索数据无效："+error; return; }
        BuildView(); SyncView(true);
    }
    private void Update()
    {
        if (run == null || submitted) return;
        if (Step1Keys.Down(KeyCode.Escape))
        {
            if (confirmExit) { confirmExit=false; pendingExit=null; }
            else if (paused && menuSettings) menuSettings=false;
            else if (ready && !run.Ended) { paused=!paused; menuSettings=false; }
        }
        if (Step1Keys.Down(KeyCode.N)) notebook=!notebook;
        if (Step1Keys.Down(KeyCode.C)) zoom = zoom < 6f ? 8f : 5.5f;
        if (Step1Keys.Down(KeyCode.Minus)) zoom=Mathf.Min(10f,zoom+0.5f);
        if (Step1Keys.Down(KeyCode.Equals)) zoom=Mathf.Max(3.5f,zoom-0.5f);
        if (ready && !paused && !confirmExit && !run.Ended)
        {
            double h = (Step1Keys.Held(KeyCode.D)||Step1Keys.Held(KeyCode.RightArrow) ? 1:0)-(Step1Keys.Held(KeyCode.A)||Step1Keys.Held(KeyCode.LeftArrow) ? 1:0);
            double v = (Step1Keys.Held(KeyCode.W)||Step1Keys.Held(KeyCode.UpArrow) ? 1:0)-(Step1Keys.Held(KeyCode.S)||Step1Keys.Held(KeyCode.DownArrow) ? 1:0);
            double frameTime=Time.unscaledDeltaTime;
            double before=run.Remaining;
            while(frameTime>0 && !run.Ended) { double step=Math.Min(frameTime,1.0);run.Tick(step,h,v);frameTime-=step; }
            if (run.node.timeout>0) { int beep=WorldParkStageCues.CountdownBeep(before,run.Remaining); if (beep>0) WorldParkSfx.Play(beep<=3 ? "beep_last":"beep"); }
            if (Step1Keys.Down(KeyCode.E) && run.NearbyPoint != null) RequestInteract(run.NearbyPoint);
        }
        StageFeedback();
        SyncView(false);
    }
    /// <summary>把内核里新发生的追逐事件、倒计时与结束结果转成演出（横幅、警报、闪红、激光）。</summary>
    private void StageFeedback()
    {
        if (ready && !run.Ended && run.node.timeout>0) WorldParkStage.SetCountdown((float)run.Remaining);
        for (; seenEvents<run.Events.Count; seenEvents++)
        {
            var e=run.Events[seenEvents];
            WorldParkStage.Banner(WorldParkStageCues.ChaseLine(e));
            WorldParkSfx.Play(WorldParkStageCues.ChaseSound(e));
            if (e.type=="seen") WorldParkStage.Flash(0.5f);
        }
        if (run.Ended && !endAnnounced)
        {
            endAnnounced=true;
            WorldParkStage.Show(WorldParkStageCues.Activity(WorldParkRuntime.ActiveSession==null ? null:WorldParkRuntime.ActiveSession.Venue,run.Won,run.LastMessage));
        }
    }
    private void RequestInteract(WorldParkPack.ExplorePoint point)
    {
        if (point == null || !ready || paused || run.Ended) return;
        // 有鬼的会场没有“停下来确认”的余地：碰到阵地/出口立即生效（Esc 仍可暂停）。
        if (point.kind == "exit" && run.Chasers.Count==0) { pendingExit=point; confirmExit=true; return; }
        run.Interact(point.id,out string detail); message=detail;
    }
    private void ChooseExit()
    {
        var point=pendingExit; confirmExit=false; pendingExit=null;
        if (point != null) { run.Interact(point.id,out string detail); message=detail; }
    }
    private SpriteRenderer Quad(string name, Sprite sp, float x,float y,float w,float h,Color color,int order)
    {
        var go=new GameObject(name); go.transform.SetParent(root,false);
        go.transform.localPosition=new Vector3(x,y,0); go.transform.localScale=new Vector3(w,h,1);
        var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=sp ?? square; sr.color=color; sr.sortingOrder=order; return sr;
    }
    private void BuildView()
    {
        root=new GameObject("会场空间").transform; root.SetParent(transform,false);
        var tex=new Texture2D(1,1); tex.SetPixel(0,0,Color.white); tex.Apply(); ownedAssets.Add(tex);
        square=Sprite.Create(tex,new Rect(0,0,1,1),new Vector2(0.5f,0.5f),1); ownedAssets.Add(square);
        var map=run.map; int ppc=WorldArt.GroundPixelsPerCell(map.W,map.H);
        var pixels=new Color[map.W*ppc*map.H*ppc];
        for (int y=0;y<map.H;y++) for (int x=0;x<map.W;x++)
        {
            char c=map.At(x,y); string gk=WorldArt.GroundKeyOf(c);
            var block=GroundBlock(gk,ppc);
            for (int py=0;py<ppc;py++) for (int px=0;px<ppc;px++) pixels[(y*ppc+py)*map.W*ppc+x*ppc+px]=block[py*ppc+px];
            if (c=='T' || c=='M' || c=='.' || c=='=' || c=='g' || c=='w') continue;
            bool wallFace=map.At(x,y-1)!='W' && !OverworldCatalog.IsDoor(map.At(x,y-1));
            string key=WorldArt.TownKeyOf(c,wallFace);
            var sp=key==null ? null:Step1ArtSkin.Get(key,false);
            if (sp==null) sp=OverworldGame.IconSprite(OverworldArt.IconOf(c));
            var tile=OverworldCatalog.Get(c);
            var sr=Quad("地物 "+c,sp,x+0.5f,y+(c=='t' ? 0.75f:0.5f),c=='t' ? 1.5f:1,c=='t' ? 1.5f:1,sp!=null ? Color.white:new Color(tile.r,tile.g,tile.b),2000-y*10);
            if (OverworldCatalog.Solid(c) || c=='"') covers.Add(sr);
        }
        var ground=new Texture2D(map.W*ppc,map.H*ppc,TextureFormat.RGBA32,false) {filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        ground.SetPixels(pixels); ground.Apply(); ownedAssets.Add(ground);
        var gs=Sprite.Create(ground,new Rect(0,0,ground.width,ground.height),Vector2.zero,ppc); ownedAssets.Add(gs);
        var g=Quad("地面",gs,0,0,1,1,Color.white,-2000);
        actor=Quad("参与者",Step1ArtSkin.Get("Hero0",false),(float)run.x,(float)run.y+0.1f,1,1,Color.white,2000-(int)(run.y*10));
        if (actor.sprite==square) { actor.color=new Color(0.25f,0.72f,1f); actor.transform.localScale=new Vector3(0.55f,0.8f,1); }
        foreach (var p in run.node.points)
        {
            if (p.kind=="chaser") continue; // 追逐者单独渲染
            // 分类可以区分；出口真实outcome不能泄漏到颜色、名称或标牌。
            var icon=OverworldGame.IconSprite(p.kind=="exit" ? "Door":"Box");
            var sr=Quad("交互点 "+p.id,icon,p.x+0.5f,p.y+0.5f,0.7f,0.7f,p.kind=="exit" ? new Color(1f,0.84f,0.45f):new Color(0.6f,0.85f,1f),2050-p.y*10);
            markers[p.id]=sr;
        }
        BuildChasers();
        cam=Camera.main;
        if (cam==null) { var go=new GameObject("Main Camera") {tag="MainCamera"}; cam=go.AddComponent<Camera>(); }
        cam.orthographic=true; cam.transform.rotation=Quaternion.identity; cam.orthographicSize=zoom;
        cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(0.055f,0.075f,0.105f);
        if (FindObjectOfType<AudioListener>()==null) cam.gameObject.AddComponent<AudioListener>();
        lastX=(float)run.x; lastY=(float)run.y;
    }
    /// <summary>视锥贴图：顶点在左中，±60° 扇形，越远越淡。按 sight 缩放、按朝向旋转。</summary>
    private Sprite ConeSprite()
    {
        if (coneSprite!=null) return coneSprite;
        const int w=64,h=111; float half=(float)WorldParkExplore.ChaserHalfAngle*Mathf.Deg2Rad;
        var tex=new Texture2D(w,h,TextureFormat.RGBA32,false) {filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        var px=new Color[w*h];
        for (int y=0;y<h;y++) for (int x=0;x<w;x++)
        {
            float dx=(x+0.5f)/w, dy=((y+0.5f)-h/2f)/w, d=Mathf.Sqrt(dx*dx+dy*dy);
            bool inside=d<=1f && Mathf.Abs(Mathf.Atan2(dy,dx))<=half;
            px[y*w+x]=inside ? new Color(1,1,1,0.32f*(1f-0.6f*d)) : new Color(1,1,1,0);
        }
        tex.SetPixels(px); tex.Apply(); ownedAssets.Add(tex);
        coneSprite=Sprite.Create(tex,new Rect(0,0,w,h),new Vector2(0f,0.5f),w); ownedAssets.Add(coneSprite);
        return coneSprite;
    }
    private void BuildChasers()
    {
        foreach (var c in run.Chasers)
        {
            var view=new ChaserView {c=c,lx=(float)c.x,ly=(float)c.y};
            view.cone=Quad("视野 "+c.Name,ConeSprite(),(float)c.x,(float)c.y,(float)c.Sight,(float)c.Sight,new Color(1f,0.95f,0.7f,1f),-1500);
            view.body=Quad("追逐者 "+c.Name,Step1ArtSkin.Get("Hero0",false),(float)c.x,(float)c.y+0.1f,1.15f,1.15f,new Color(1f,0.35f,0.38f),2000);
            if (view.body.sprite==square) view.body.transform.localScale=new Vector3(0.6f,0.9f,1);
            chaserViews.Add(view);
        }
    }
    private void SyncChasers()
    {
        foreach (var view in chaserViews)
        {
            var c=view.c; float x=(float)c.x,y=(float)c.y,dx=x-view.lx,dy=y-view.ly;
            bool moving=dx*dx+dy*dy>0.000001f; view.lx=x; view.ly=y;
            bool visible=run.PlayerSees(c);
            view.body.enabled=visible; view.cone.enabled=visible && !run.Ended;
            var frame=Step1ArtSkin.Get("Hero"+(moving ? (1+(int)(Time.unscaledTime*9)%2).ToString():"0"),false);
            if (frame!=null) view.body.sprite=frame;
            view.body.transform.position=new Vector3(x,y+0.1f,0); view.body.sortingOrder=2000-(int)(y*10);
            if (Mathf.Abs(dx)>0.0001f) view.body.flipX=dx<0;
            view.cone.transform.position=new Vector3(x,y,0);
            view.cone.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2((float)c.fy,(float)c.fx)*Mathf.Rad2Deg);
            view.cone.color=c.mode=="chase" ? new Color(1f,0.25f,0.25f) : c.mode=="search" ? new Color(1f,0.8f,0.3f) : new Color(1f,0.97f,0.8f);
            // 脚步声：7 格内即使看不见也听得到，越近越响（玩家能听，规则内核不读声音）。
            double ddx=c.x-run.x,ddy=c.y-run.y; float dist=(float)Math.Sqrt(ddx*ddx+ddy*ddy);
            view.stepLeft-=Time.unscaledDeltaTime;
            if (moving && dist<7f && view.stepLeft<=0f && !run.Ended && !paused) { WorldParkSfx.Play("step",0.25f+0.6f*(1f-dist/7f)); view.stepLeft=c.mode=="chase" ? 0.3f:0.5f; }
        }
    }
    private Color[] GroundBlock(string key,int ppc)
    {
        if (groundCache.TryGetValue(key,out var result)) return result;
        result=new Color[ppc*ppc]; var sp=Step1ArtSkin.Get(key,false);
        Texture2D src=sp!=null && sp.texture!=null && sp.texture.isReadable ? sp.texture:null;
        float[] raw=src==null ? Step1Art.Rgba(WorldArt.Rows(key)):null;
        for (int y=0;y<ppc;y++) for (int x=0;x<ppc;x++)
        {
            if (src!=null) { var rect=sp.textureRect; result[y*ppc+x]=src.GetPixel((int)(rect.x+(x+0.5f)*rect.width/ppc),(int)(rect.y+(y+0.5f)*rect.height/ppc)); }
            else { int i=((y*WorldArt.Size/ppc)*WorldArt.Size+x*WorldArt.Size/ppc)*4; result[y*ppc+x]=raw==null ? new Color(0.3f,0.4f,0.35f):new Color(raw[i],raw[i+1],raw[i+2],raw[i+3]); }
        }
        groundCache[key]=result; return result;
    }
    private void SyncView(bool snap)
    {
        if (actor==null || cam==null) return;
        SyncChasers();
        float x=(float)run.x,y=(float)run.y,dx=x-lastX,dy=y-lastY;
        bool moving=dx*dx+dy*dy>0.000001f;
        var frame=Step1ArtSkin.Get("Hero"+(moving ? (1+(int)(Time.unscaledTime*8)%2).ToString():"0"),false);
        if (frame!=null) actor.sprite=frame;
        actor.transform.position=new Vector3(x,y+0.1f,0); actor.sortingOrder=2000-(int)(y*10);
        if (Mathf.Abs(dx)>0.0001f) actor.flipX=dx<0; lastX=x; lastY=y;
        foreach (var cover in covers)
        {
            // 只改视觉，不开墙、不改变LOS或玩家可知事实。
            bool hidesActor=cover.sortingOrder>actor.sortingOrder && cover.bounds.Contains(new Vector3(x,y+0.35f,0));
            cover.color=new Color(cover.color.r,cover.color.g,cover.color.b,hidesActor ? 0.3f:1f);
        }
        foreach (var p in run.node.points)
        {
            if (p.kind=="chaser") continue;
            double dxp=p.x+0.5-run.x,dyp=p.y+0.5-run.y;
            markers[p.id].enabled=dxp*dxp+dyp*dyp<=64 && OverworldMap.LineOfSight(run.map,run.x,run.y,p.x+0.5,p.y+0.5);
        }
        cam.orthographicSize=zoom;
        float hw=zoom*cam.aspect;
        float cx=run.map.W<=2*hw ? run.map.W/2f:Mathf.Clamp(x,hw,run.map.W-hw);
        float cy=run.map.H<=2*zoom ? run.map.H/2f:Mathf.Clamp(y,zoom,run.map.H-zoom);
        Vector3 target=new Vector3(cx,cy,-10);
        cam.transform.position=snap ? target:Vector3.Lerp(cam.transform.position,target,1f-Mathf.Exp(-12f*Time.unscaledDeltaTime));
    }
    private void Styles()
    {
        if (text!=null) return;
        // S254：字号是 HUD 虚拟像素，OnGUI 用 WorldParkUi.BeginHud 按分辨率与玩家界面大小整体缩放。
        text=new GUIStyle(GUI.skin.label) {wordWrap=true,fontSize=15};
        title=new GUIStyle(text) {fontSize=20,fontStyle=FontStyle.Bold}; small=new GUIStyle(text) {fontSize=13}; rich=new GUIStyle(small) {richText=true};
        box=new GUIStyle(GUI.skin.box) {padding=new RectOffset(14,14,12,12)};
    }
    private void OnGUI()
    {
        if (owner==null || WorldParkRuntime.ActiveSession==null) return;
        Styles(); var session=WorldParkRuntime.ActiveSession; GUI.depth=-90;
        bool menu=paused && run!=null && ready && !confirmExit && !run.Ended;
        // 菜单打开时，下面的 HUD 只在重绘事件里画：看得见，但不接收点击（点击全部交给菜单）。
        if (!menu || Event.current.type==EventType.Repaint)
        {
            Matrix4x4 oldMatrix=WorldParkUi.BeginHud(out float W,out float H);
            try { DrawHud(session,W,H); }
            finally { GUI.matrix=oldMatrix; }
        }
        if (menu) DrawPauseMenu(session);
    }
    /// <summary>S254 暂停菜单（Esc）：继续 / 设置 / 重来本会场 / 回到标题 / 退出。现场探索中途不存档。</summary>
    private void DrawPauseMenu(WorldParkPack.Session session)
    {
        string a=WorldParkUi.DrawPauseMenu((session.Venue.name ?? session.venueId)+" · "+(run.node.name ?? run.node.id),
            "调查计时已暂停。现场探索的中途不存档：回到标题后“继续旅程”会从进入这段探索前的检查点重进。",false,true,ref menuSettings);
        if (a==WorldParkUi.Resume) { paused=false; menuSettings=false; }
        else if (a==WorldParkUi.Retry) { paused=false; submitted=true; owner.RestartVenue(); }
        else if (a==WorldParkUi.Title) { paused=false; submitted=true; owner.ReturnToTitle(); }
        else if (a==WorldParkUi.Quit) WorldParkUi.QuitGame();
    }
    /// <summary>在 HUD 虚拟坐标（W×H）里画探索界面。</summary>
    private void DrawHud(WorldParkPack.Session session,float W,float H)
    {
        if (run==null) { GUI.Box(new Rect(24,24,Mathf.Min(620,W-48),120),message,box); return; }
        float width=Mathf.Max(230,Mathf.Min(480,W-40));
        if (!ready || confirmExit || run.Ended)
        {
            GUILayout.BeginArea(new Rect((W-width)/2,Mathf.Max(20,H*0.16f),width,Mathf.Max(180,H*0.7f)),box);
            GUILayout.Label(session.Venue.name+" / "+run.node.name,title);
            if (!ready)
            {
                GUILayout.Label(session.Venue.rules,text); GUILayout.Space(10); GUILayout.Label(run.node.text,text);
                if (run.Chasers.Count>0) GUILayout.Label("有 "+run.Chasers.Count+" 个“鬼”在巡逻。红色扇形是它的视野；被看见它就会追（它比你略慢）。躲进高草静止不动，只有贴身才会被发现；在草里走动草会晃。碰到阵地/出口立即生效。",text);
                GUILayout.Label("WASD/方向键移动 · E近身调查/选择 · N线索笔记 · C镜头远近 · Esc菜单（设置 / 重来本会场 / 回到标题）",small);
                if (GUILayout.Button("进入会场",GUILayout.Height(36))) ready=true;
            }
            else if (run.Ended)
            {
                GUILayout.Label(run.Won ? "活动目标达成":"活动目标未达成",title);
                GUILayout.Label(run.LastMessage,text);
                if (!submitted && GUILayout.Button("确认结果，继续世界",GUILayout.Height(36))) { submitted=true; owner.CompleteExplore(run.Won,run.LastMessage); }
            }
            else if (confirmExit)
            {
                GUILayout.Label("确定选择“"+pendingExit.label+"”？",text);
                GUILayout.Label("这是出口选择，不会提前显示正确答案；你可取消并继续调查。",small);
                if (GUILayout.Button("确定选择",GUILayout.Height(34))) ChooseExit();
                if (GUILayout.Button("返回调查")) { confirmExit=false; pendingExit=null; }
            }
            GUILayout.EndArea(); return;
        }
        GUILayout.BeginArea(new Rect(16,16,Mathf.Min(500,W-32),run.Chasers.Count>0 ? 146:124),box);
        GUILayout.Label(session.Venue.name+" · "+run.node.name,title);
        GUILayout.Label(run.node.timeout>0 ? "剩余 "+Math.Ceiling(run.Remaining)+" 秒":"当前会场没有倒计时",small);
        if (run.Chasers.Count>0) GUILayout.Label(run.Hunted ? "<color=#ff6670>被盯上了！拐过墙角或钻进草丛甩开它</color>" : run.PlayerHidden ? "你藏在草里：静止时只有贴身才会被发现" : "没有被盯上",rich);
        GUILayout.Label("移动 WASD · 调查 E · 笔记 N · 镜头 C · 菜单 Esc",small); GUILayout.EndArea();
        DrawChaserMarks();
        if (notebook)
        {
            GUILayout.BeginArea(new Rect(16,152,Mathf.Min(420,W-32),Mathf.Max(160,H-174)),box);
            noteScroll=GUILayout.BeginScrollView(noteScroll); GUILayout.Label("当前可知信息",title);
            GUILayout.Label(session.Venue.rules,text);
            owner.DrawJourneySummary(small);
            foreach (var c in session.Venue.clues)
            {
                string key=session.venueId+"/"+c.id;
                if (session.found.Contains(key)) GUILayout.Label(c.name+"："+c.hint,text);
                if (session.revealed.Contains(key)) GUILayout.Label("重新解释："+c.truth,text);
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        GUILayout.BeginArea(new Rect(16,H-110,Mathf.Min(640,W-32),96),box);
        var nearby=run.NearbyPoint;
        if (nearby!=null && GUILayout.Button("E  "+(nearby.kind=="clue" ? "调查 ":"选择 ")+nearby.label,GUILayout.Height(32))) RequestInteract(nearby);
        else if (nearby==null) GUILayout.Label("靠近标记后才能调查；镜头拉远不等于已经发现线索。",small);
        GUILayout.Label(message,small); GUILayout.EndArea();
    }
    /// <summary>追逐者头顶：追你时“!”，搜寻时“?”（只在看得见它时显示）。屏幕坐标换算成 HUD 虚拟坐标。</summary>
    private void DrawChaserMarks()
    {
        if (cam==null || chaserViews.Count==0) return;
        if (mark==null) mark=new GUIStyle(GUI.skin.label) {fontSize=30,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
        float f=WorldParkUi.HudFactor;
        foreach (var view in chaserViews)
        {
            if (!view.body.enabled || view.c.mode=="patrol") continue;
            Vector3 s=cam.WorldToScreenPoint(new Vector3((float)view.c.x,(float)view.c.y+1.2f,0));
            mark.normal.textColor=view.c.mode=="chase" ? new Color(1f,0.3f,0.3f):new Color(1f,0.85f,0.3f);
            GUI.Label(new Rect(s.x/f-20,(Screen.height-s.y)/f-20,40,40),view.c.mode=="chase" ? "!":"?",mark);
        }
    }
    private void OnDestroy()
    {
        foreach (var item in ownedAssets) if (item!=null) Destroy(item);
        ownedAssets.Clear();
    }
}
