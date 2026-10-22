using System;
using System.Collections.Generic;
using System.Linq;

public static class Probe
{
    const float dt = 1f / 30f;
    static OverworldMap.Map Sample(string txt) => OverworldPack.Parse(txt)[0];
    static OverworldTown Fresh(string txt, MarioMindTuningSO t, int day)
    {
        OverworldSession.ResetStatics(); var m = Sample(txt); OverworldSession.NewDay(m.name, "Town", day); OverworldSession.Active = true; return new OverworldTown(m, t);
    }

    public static void Main(string[] a)
    {
        if(a.Length>0){ if(a[0]=="2") Dbg2.Run(); else Dbg.Run(); return;} var t = MarioMindTuningSO.LoadOrDefault();
        Console.WriteLine($"tuning: stun {t.overworldBigStunSeconds} grace {t.overworldHurtGraceSeconds} ko {t.overworldKoSeconds} tele {t.overworldBoltTelegraphSeconds} cloud {t.overworldCloudSeconds}/{t.overworldCloudVolleySeconds} r{t.overworldCloudRadius} volley {t.overworldStormVolleySeconds}");

        // A. 雷云对马里奥：召唤在他身边，他最长连续晕多久、掉几颗心、能不能自由移动
        {
            var stats = new List<string>(); double worstStun = 0; int worstLost = 0; double minFree = 99;
            for (int k = 0; k < 10; k++)
            {
                var town = Fresh(OverworldPack.StormSampleText, t, 1);
                var cs = OverworldMap.Find(town.map, '.'); var c = cs[(k * 131 + 50) % cs.Count];
                OverworldSession.Minute = 7 * 60; OverworldSession.Energy = 3; OverworldSession.Clouds = k;
                town.mario.x = c.x + 0.5; town.mario.y = c.y + 0.5; town.mario.Clear();
                town.tx = c.x + 2.5; town.ty = c.y + 0.5;
                var go = new OverworldTown.Input { weather = true };
                town.Tick(dt, go);
                if (town.cloud == null) continue;
                // 你召唤后立刻跑开（往远离方向）
                double run = 0, cur = 0, longest = 0; int lost0 = OverworldSession.MarioHeartsLost; var freeGaps = new List<double>(); double free = 0; bool wasD = false;
                for (int i = 0; i < 30 * 14; i++)
                {
                    var inp = new OverworldTown.Input { h = 1f };
                    town.Tick(dt, inp);
                    bool d = town.lastOrder.state == OverworldMarioState.Dizzy;
                    if (d) { cur += dt; longest = Math.Max(longest, cur); if (!wasD && free > 0 && run > 0) freeGaps.Add(free); free = 0; } else { cur = 0; free += dt; }
                    if (d) run += dt; wasD = d;
                }
                int lost = OverworldSession.MarioHeartsLost - lost0;
                worstStun = Math.Max(worstStun, longest); worstLost = Math.Max(worstLost, lost); if (freeGaps.Count > 0) minFree = Math.Min(minFree, freeGaps.Min());
                stats.Add($"{lost}❤/{longest:0.0}s/总{run:0.0}s/KO{OverworldSession.Kos}");
            }
            Console.WriteLine($"A 雷云→马里奥（你召唤后跑开）：{string.Join(" ", stats)} ｜最长连续晕 {worstStun:0.0}s 最多掉 {worstLost} 两次晕之间最短自由 {(minFree > 90 ? -1 : minFree):0.00}s");
        }
        // B. 雷云对你自己：召唤后原地不动 vs 立刻往外跑
        foreach (bool flee in new[] { false, true })
        {
            int hits = 0, kos = 0; double worst = 0;
            for (int k = 0; k < 20; k++)
            {
                var town = Fresh(OverworldPack.StormSampleText, t, 1);
                var cs = OverworldMap.Find(town.map, '.'); var c = cs[(k * 97 + 11) % cs.Count];
                OverworldSession.Minute = 7 * 60; OverworldSession.Energy = 3; OverworldSession.Clouds = k;
                town.tx = c.x + 0.5; town.ty = c.y + 0.5; town.mario.x = 3.5; town.mario.y = 2.5; town.mario.Clear();
                town.Tick(dt, new OverworldTown.Input { weather = true });
                double cur = 0;
                for (int i = 0; i < 30 * 12; i++)
                {
                    // 往外跑：远离云心
                    var inp = new OverworldTown.Input();
                    if (flee && town.cloud != null) { var here=OverworldGuide.Near(town.map,town.tx,town.ty); OverworldMap.Cell? best=null; double bd=1e9; foreach(var q in OverworldMap.Find(town.map,'.')){ if(OverworldTown.Dist(q.x+.5,q.y+.5,town.cloud.x,town.cloud.y)<=t.overworldCloudRadius+1.5) continue; double dd=OverworldTown.Dist(q.x+.5,q.y+.5,town.tx,town.ty); if(dd<bd&&dd<8){bd=dd;best=q;} }
                      if(best.HasValue){ var pth=OverworldMap.Path(town.map,here,best.Value); if(pth!=null&&pth.Count>1){ double dx=pth[1].x+.5-town.tx, dy=pth[1].y+.5-town.ty, d=Math.Sqrt(dx*dx+dy*dy); if(d>1e-3){inp.h=(float)(dx/d); inp.v=(float)(dy/d);} } } }
                    town.Tick(dt, inp);
                    if (town.frozen > 0) { cur += dt; worst = Math.Max(worst, cur); } else cur = 0;
                }
                hits += OverworldSession.YouHeartsLost; kos += OverworldSession.Kos;
            }
            Console.WriteLine($"B 雷云→你自己（{(flee ? "立刻往外跑" : "原地不动")}）：20 次共掉 {hits} 心 KO {kos} 最长连续定身 {worst:0.0}s");
        }
        // C. 雷区 1（每天都劈）挂机：马里奥一天被劈几次；Idle 机器人
        foreach (var (nm, txt) in new[] { ("星露雷镇", OverworldPack.StormSampleText), ("星露山镇", OverworldPack.MountainSampleText), ("大镇", OverworldPack.BigSampleText) })
        {
            var m = Sample(txt);
            foreach (var kd in new[] { OverworldBots.Kind.Idle, OverworldBots.Kind.Hider, OverworldBots.Kind.Prankster, OverworldBots.Kind.Follower, OverworldBots.Kind.Chaos })
            {
                var rows = new List<string>(); double idle = 0, li = 0, ff = 0, real = 0; int mh = 0, yh = 0, ko = 0, cl = 0, am = 0, doors = 0, caught = 0, bolts = 0;
                for (int d = 1; d <= 4; d++)
                {
                    var r = OverworldBots.PlayDay(Sample(txt), t, kd, true, d, d);
                    idle += r.idleSeconds; li = Math.Max(li, r.longestIdle); ff += r.fastForwardSeconds; real += r.realSeconds;
                    mh += OverworldSession.MarioHeartsLost; yh += OverworldSession.YouHeartsLost; ko += OverworldSession.Kos; cl += OverworldSession.Clouds; am += r.ambush; doors += r.doors; caught += r.caught; bolts += OverworldSession.StormBolts;
                }
                Console.WriteLine($"C {nm} {kd,-9} 4天：用时 {real:0}s 干等 {idle:0}s(最长 {li:0}) 快进 {ff:0}s｜他掉心 {mh} 你掉心 {yh} KO {ko} 雷云 {cl} 闪电 {bolts}｜埋伏 {am}/{doors} 被抓 {caught}");
            }
        }
        // D. 雷云对没吃过闪电亏的他 vs 吃过亏的他：心变化时间线
        foreach (bool wary in new[]{false,true}) {
            var town = Fresh(OverworldPack.StormSampleText, t, 1); if (wary) OverworldSession.MarioWary.Add('i');
            var c = OverworldMap.Find(town.map,'.').First(q=>{for(int dy=-3;dy<=3;dy++)for(int dx=-3;dx<=3;dx++) if(!OverworldMap.Walkable(town.map,q.x+dx,q.y+dy)) return false; return q.x>20;}); OverworldSession.Minute = 7*60; OverworldSession.Energy = 3;
            town.mario.x = c.x+0.5; town.mario.y=c.y+0.5; town.mario.Clear(); town.tx=c.x+1.5; town.ty=c.y+0.5;
            town.Tick(dt,new OverworldTown.Input{weather=true}); var tl=new List<string>(); int h=OverworldSession.MarioHearts; double tt=0; string st="";
            for(int i=0;i<30*12;i++){ town.Tick(dt,new OverworldTown.Input{h=1f}); tt+=dt; var s2=town.lastOrder.state.ToString()+"/"+town.lastOrder.intent+"@"+town.mario.x.ToString("0.0")+","+town.mario.y.ToString("0.0")+(town.strikes.Count>0?"S"+string.Join("|",town.strikes.Select(q=>q.c.x+","+q.c.y)):""); if(s2!=st){ tl.Add($"{tt:0.0}:{s2}"); st=s2;} if(OverworldSession.MarioHearts!=h){ tl.Add($"{tt:0.0}:❤{OverworldSession.MarioHearts}"); h=OverworldSession.MarioHearts; } }
            Console.WriteLine($"D 雷云 他{(wary?"吃过亏":"没吃过亏")}：{string.Join(" ",tl)}");
        }
        // D2. 能量经济：Prankster 一天能拿几次 Q？能量来源
        // E. 预警可躲性：你站在落点、看到预警立刻跑（最短路径离开十字）需要多久
        {
            var town = Fresh(OverworldPack.StormSampleText, t, 1);
            double sp = t.overworldTricksterSpeed, msp = t.overworldMarioSpeed;
            Console.WriteLine($"E 逃出十字（中心→斜角外 ≈1.0 格）：你 {1.0 / sp:0.00}s 马里奥 {1.0 / msp:0.00}s；预警 {t.overworldBoltTelegraphSeconds}s；人类反应 ~0.25s");
        }
    }
}
