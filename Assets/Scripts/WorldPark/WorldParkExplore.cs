using System;
using System.Collections.Generic;

/// <summary>
/// 现场探索的纯逻辑运行。它只复用 OverworldMap 的解析、碰撞和视线，不启动小镇日程或守宝房。
/// 场景适配器负责输入、渲染和在玩家确认后调用 Session.ResolveExplore。
/// S252：kind=chaser 的现场点是“鬼”（追逐者）：按巡逻路线走，看见玩家就追，看不见就去最后看见处搜寻，碰到即出局。
/// 感知完全复用小镇视线规则（视锥、距离、墙体遮挡、高草只在贴身 1.5 格内可见、草里走动会晃）。
/// </summary>
public static class WorldParkExplore
{
    public const int DefaultChaserSpeed = WorldParkPack.DefaultChaserSpeed;   // 十分之一格/秒：3.4 格/秒（玩家 4.0）
    public const int DefaultChaserSight = WorldParkPack.DefaultChaserSight;    // 格
    public const int MaxChasers = 4;
    public const int MaxChaserRoute = 32;
    public const double PlayerViewRadius = 8.0; // 玩家看得见现场点/追逐者的距离（与渲染一致）
    public const double CatchRadius = 0.6;
    public const double ChaserHalfAngle = 60.0;
    public const double ChaserNearRadius = 1.5;
    public const double SearchSeconds = 2.0;
    /// <summary>S252：搜寻中再看到草晃，离当前搜寻点不到这么远就不换目标（防止玩家在草里左右挪一格把鬼“抖”在原地）。</summary>
    public const double RustleRetarget = 1.5;
    /// <summary>S252：一次搜寻最多走这么久；走不到（被挡住、目标一直在变）就原地张望，然后回去巡逻。</summary>
    public const double SearchTravelSeconds = 8.0;
    /// <summary>S252：目标在这么近、且身体沿直线走得过去时，鬼不按格子绕路，直接扑向目标的实际位置。</summary>
    public const double DirectRange = 1.5;
    private const double ChaseSlice = 0.05;
    private static readonly List<OverworldMap.Cell> NoLamps = new List<OverworldMap.Cell>();

    /// <summary>追逐事件：seen 看见你、lost 跟丢、rustle 听见/看见草晃、giveup 放弃搜寻回巡逻、caught 抓住。</summary>
    public struct ChaseEvent
    {
        public double time;
        public string chaser;
        public string name;
        public string type;
    }

    public sealed class Chaser
    {
        public readonly WorldParkPack.ExplorePoint point;
        public double x, y;
        public double fx, fy = 1;
        /// <summary>patrol 巡逻 / chase 追击 / search 搜寻。</summary>
        public string mode = "patrol";
        public bool SeesPlayer;
        public double lastX, lastY;
        internal double searchLeft, turnLeft, repathLeft, searchTravel;
        internal readonly List<OverworldMap.Cell> route = new List<OverworldMap.Cell>();
        internal int routeIndex;
        internal List<OverworldMap.Cell> path;
        internal int pathIndex, goalX = int.MinValue, goalY = int.MinValue;

        internal Chaser(WorldParkPack.ExplorePoint source)
        {
            point = source;
            x = source.x + 0.5;
            y = source.y + 0.5;
        }

        public string Name { get { return string.IsNullOrEmpty(point.label) ? point.id : point.label; } }
        public double ChaseSpeed { get { return (point.speed > 0 ? point.speed : DefaultChaserSpeed) / 10.0; } }
        public double PatrolSpeed { get { return ChaseSpeed * 0.6; } }
        public double Sight { get { return point.sight > 0 ? point.sight : DefaultChaserSight; } }

        public OverworldMap.SightRules Rules
        {
            get
            {
                return new OverworldMap.SightRules
                {
                    range = Sight, nightRange = Sight, halfAngleDeg = ChaserHalfAngle,
                    nearRadius = ChaserNearRadius, grassRadius = ChaserNearRadius, lampRadius = 0, night = false
                };
            }
        }
    }

    public sealed class Run
    {
        public readonly WorldParkPack.Session session;
        public readonly OverworldMap.Map map;
        public readonly WorldParkPack.Node node;

        public double x;
        public double y;
        public double Elapsed;
        /// <summary>有倒计时时的剩余秒数；timeout=0（无倒计时）时为 0。</summary>
        public double Remaining;
        public bool Ended;
        public bool Won;
        public string LastMessage = "";
        /// <summary>被哪只追逐者抓住（未被抓为空）。</summary>
        public string CaughtBy = "";
        public readonly List<Chaser> Chasers = new List<Chaser>();
        public readonly List<ChaseEvent> Events = new List<ChaseEvent>();

        private const double Speed = 4.0;
        private const double NearbyRadius = 1.1;
        private const double MaxTickSeconds = 1.0;

        internal Run(WorldParkPack.Session source, OverworldMap.Map sourceMap, WorldParkPack.Node sourceNode, double startX, double startY)
        {
            session = source;
            map = sourceMap;
            node = sourceNode;
            x = startX;
            y = startY;
            BuildChasers();
            RefreshRemaining();
        }

        /// <summary>玩家此刻站在可藏身的格子里（高草、山洞）。</summary>
        public bool PlayerHidden { get { return OverworldCatalog.Hides(map.At((int)Math.Floor(x), (int)Math.Floor(y))); } }

        /// <summary>任何追逐者正在追你。</summary>
        public bool Hunted
        {
            get
            {
                foreach (Chaser c in Chasers) if (c.mode == "chase") return true;
                return false;
            }
        }

        /// <summary>玩家能否看见这只追逐者（与渲染同一规则：8 格内且无墙遮挡）。</summary>
        public bool PlayerSees(Chaser c)
        {
            if (c == null) return false;
            double dx = c.x - x, dy = c.y - y;
            return dx * dx + dy * dy <= PlayerViewRadius * PlayerViewRadius && OverworldMap.LineOfSight(map, x, y, c.x, c.y);
        }

        /// <summary>
        /// 每帧更新。输入仅用于方向；斜向输入会标准化，OverworldMap.Move 负责小步碰撞与贴墙滑动。
        /// 暂停由外部停止调用 Tick 实现。有追逐者时把时间切成 ≤0.05 秒小片，保证抓捕判定连续。
        /// </summary>
        public void Tick(double dt, double h, double v)
        {
            if (Ended)
            {
                LastMessage = "探索已经结束，等待确认结果";
                return;
            }
            if (!Finite(dt) || !Finite(h) || !Finite(v))
            {
                LastMessage = "移动输入或时间不是有限数值";
                return;
            }
            if (dt < 0 || dt > MaxTickSeconds)
            {
                LastMessage = "本次 Tick 时间必须在 0 到 " + MaxTickSeconds + " 秒之间";
                return;
            }

            double usable = dt;
            if (node.timeout > 0)
                usable = Math.Min(usable, Math.Max(0.0, node.timeout - Elapsed));

            double length = Math.Sqrt(h * h + v * v);
            if (length > 1.0)
            {
                h /= length;
                v /= length;
            }
            bool blocked;
            if (Chasers.Count == 0)
            {
                double dx = h * Speed * usable;
                double dy = v * Speed * usable;
                double wantedX = x + dx, wantedY = y + dy;
                var moved = OverworldMap.Move(map, x, y, dx, dy);
                x = moved.x;
                y = moved.y;
                Elapsed += usable;
                blocked = Math.Abs(x - wantedX) > 1e-7 || Math.Abs(y - wantedY) > 1e-7;
            }
            else
            {
                blocked = false;
                double left = usable;
                while (left > 1e-12 && !Ended)
                {
                    double step = Math.Min(left, ChaseSlice);
                    double dx = h * Speed * step, dy = v * Speed * step;
                    double wantedX = x + dx, wantedY = y + dy;
                    var moved = OverworldMap.Move(map, x, y, dx, dy);
                    bool playerMoved = Math.Abs(moved.x - x) > 1e-9 || Math.Abs(moved.y - y) > 1e-9;
                    x = moved.x;
                    y = moved.y;
                    if (Math.Abs(x - wantedX) > 1e-7 || Math.Abs(y - wantedY) > 1e-7) blocked = true;
                    Elapsed += step;
                    left -= step;
                    UpdateChasers(step, playerMoved);
                }
            }

            RefreshRemaining();
            if (Ended) return; // 被抓：保留抓捕信息
            LastMessage = blocked ? "移动被地图边界或障碍阻挡" : "";
            if (node.timeout > 0 && Elapsed >= node.timeout - 1e-9)
            {
                Ended = true;
                Won = false;
                Remaining = 0;
                LastMessage = "探索超时，已锁定失败结果";
            }
        }

        /// <summary>最近、可见且在 1.1 格内的现场点；同距离按 points 列表顺序稳定选择。追逐者不是可交互点。</summary>
        public WorldParkPack.ExplorePoint NearbyPoint
        {
            get
            {
                if (Ended) return null;
                List<WorldParkPack.ExplorePoint> points = node.points ?? new List<WorldParkPack.ExplorePoint>();
                WorldParkPack.ExplorePoint best = null;
                double bestDistance = double.MaxValue;
                double radiusSquared = NearbyRadius * NearbyRadius;
                for (int i = 0; i < points.Count; i++)
                {
                    WorldParkPack.ExplorePoint point = points[i];
                    if (point == null || point.kind == "chaser") continue;
                    double px = point.x + 0.5, py = point.y + 0.5;
                    double dx = px - x, dy = py - y;
                    double distance = dx * dx + dy * dy;
                    if (distance > radiusSquared + 1e-9) continue;
                    if (!OverworldMap.LineOfSight(map, x, y, px, py)) continue;
                    if (distance < bestDistance - 1e-9)
                    {
                        best = point;
                        bestDistance = distance;
                    }
                }
                return best;
            }
        }

        /// <summary>
        /// 交互必须指定本节点现场点，并同时满足近身与 LOS。出口只锁存结果，绝不自行 ResolveExplore。
        /// </summary>
        public bool Interact(string pointId, out string message)
        {
            message = "";
            if (Ended)
            {
                message = "探索已经结束，等待确认结果";
                LastMessage = message;
                return false;
            }
            WorldParkPack.ExplorePoint point = FindPoint(pointId);
            if (point == null)
            {
                message = "当前探索没有现场点：" + Safe(pointId);
                LastMessage = message;
                return false;
            }
            double px = point.x + 0.5, py = point.y + 0.5;
            double dx = px - x, dy = py - y;
            if (dx * dx + dy * dy > NearbyRadius * NearbyRadius + 1e-9)
            {
                message = "离现场点太远，必须在 " + NearbyRadius + " 格内交互";
                LastMessage = message;
                return false;
            }
            if (!OverworldMap.LineOfSight(map, x, y, px, py))
            {
                message = "现场点被墙体或遮挡隔开，无法交互";
                LastMessage = message;
                return false;
            }

            if (point.kind == "clue")
            {
                bool observed = session.ObserveClue(point.clue, out message);
                LastMessage = message;
                return observed;
            }
            if (point.kind != "exit")
            {
                message = "现场点类型不能交互：" + Safe(point.kind);
                LastMessage = message;
                return false;
            }

            List<string> missing = MissingRequirements(point.requires);
            if (missing.Count > 0)
            {
                message = "出口还缺少线索：" + string.Join("、", missing.ToArray());
                LastMessage = message;
                return false;
            }

            Ended = true;
            Won = point.outcome == "win";
            RefreshRemaining();
            message = "出口已锁定；请确认后提交现场结果";
            LastMessage = message;
            return true;
        }

        // ── 追逐者 ─────────────────────────────────────────
        private void BuildChasers()
        {
            List<WorldParkPack.ExplorePoint> points = node.points ?? new List<WorldParkPack.ExplorePoint>();
            foreach (WorldParkPack.ExplorePoint point in points)
            {
                if (point == null || point.kind != "chaser" || Chasers.Count >= MaxChasers) continue;
                if (!OverworldMap.Walkable(map, point.x, point.y)) continue;
                var c = new Chaser(point);
                foreach (string raw in point.route ?? new List<string>())
                {
                    int rx, ry;
                    if (WorldParkPack.TryChaserCell(raw, out rx, out ry) && OverworldMap.Walkable(map, rx, ry) && c.route.Count < MaxChaserRoute)
                        c.route.Add(new OverworldMap.Cell(rx, ry));
                }
                if (c.route.Count > 0) Face(c, c.route[0].x + 0.5 - c.x, c.route[0].y + 0.5 - c.y);
                c.turnLeft = 1.5;
                Chasers.Add(c);
            }
        }

        private void UpdateChasers(double step, bool playerMoved)
        {
            foreach (Chaser c in Chasers)
            {
                OverworldMap.SightRules rules = c.Rules;
                bool sees = OverworldMap.CanSee(map, NoLamps, c.x, c.y, c.fx, c.fy, x, y, rules);
                bool rustle = !sees && playerMoved && OverworldMap.SeesRustle(map, NoLamps, c.x, c.y, c.fx, c.fy, x, y, rules);
                if (sees)
                {
                    if (c.mode != "chase") AddEvent(c, "seen");
                    c.mode = "chase";
                    c.lastX = x;
                    c.lastY = y;
                }
                else if (c.mode == "chase")
                {
                    c.mode = "search";
                    c.searchLeft = SearchSeconds;
                    c.searchTravel = SearchTravelSeconds;
                    c.path = null;
                    AddEvent(c, "lost");
                }
                else if (rustle)
                {
                    bool fresh = c.mode != "search";
                    double rx = x - c.lastX, ry = y - c.lastY;
                    if (fresh || rx * rx + ry * ry > RustleRetarget * RustleRetarget)
                    {
                        if (fresh) { AddEvent(c, "rustle"); c.searchTravel = SearchTravelSeconds; }
                        c.mode = "search";
                        c.searchLeft = SearchSeconds;
                        c.lastX = x;
                        c.lastY = y;
                        c.path = null;
                    }
                }
                c.SeesPlayer = sees;
                c.repathLeft -= step;

                if (c.mode == "chase")
                    MoveToward(c, x, y, c.ChaseSpeed * step, true);
                else if (c.mode == "search")
                {
                    double ddx = c.lastX - c.x, ddy = c.lastY - c.y;
                    if (ddx * ddx + ddy * ddy > 0.04 && c.searchTravel > 0)
                    {
                        c.searchTravel -= step;
                        MoveToward(c, c.lastX, c.lastY, c.ChaseSpeed * 0.8 * step, false);
                    }
                    else
                    {
                        c.searchLeft -= step;
                        c.turnLeft -= step;
                        if (c.turnLeft <= 0) { Turn(c); c.turnLeft = 0.5; }
                        if (c.searchLeft <= 0)
                        {
                            c.mode = "patrol";
                            c.path = null;
                            c.turnLeft = 1.5;
                            AddEvent(c, "giveup");
                        }
                    }
                }
                else
                    Patrol(c, step);

                double cx = c.x - x, cy = c.y - y;
                if (cx * cx + cy * cy <= CatchRadius * CatchRadius)
                {
                    Ended = true;
                    Won = false;
                    CaughtBy = c.Name;
                    LastMessage = "被「" + c.Name + "」抓住了";
                    AddEvent(c, "caught");
                    return;
                }
            }
        }

        private void Patrol(Chaser c, double step)
        {
            if (c.route.Count == 0)
            {
                double hx = c.point.x + 0.5 - c.x, hy = c.point.y + 0.5 - c.y;
                if (hx * hx + hy * hy > 0.04) { MoveToward(c, c.point.x + 0.5, c.point.y + 0.5, c.PatrolSpeed * step, false); return; }
                c.turnLeft -= step;
                if (c.turnLeft <= 0) { Turn(c); c.turnLeft = 1.5; }
                return;
            }
            OverworldMap.Cell target = c.route[c.routeIndex % c.route.Count];
            double tx = target.x + 0.5, ty = target.y + 0.5;
            double dx = tx - c.x, dy = ty - c.y;
            if (dx * dx + dy * dy <= 0.0025)
            {
                c.routeIndex = (c.routeIndex + 1) % c.route.Count;
                c.path = null;
                return;
            }
            MoveToward(c, tx, ty, c.PatrolSpeed * step, false);
        }

        /// <summary>沿 Dijkstra 格子路线朝目标走 distance 格；同一格时直接逼近。追击时每 0.25 秒或目标换格重算路线。</summary>
        private void MoveToward(Chaser c, double tx, double ty, double distance, bool chasing)
        {
            int cx = (int)Math.Floor(c.x), cy = (int)Math.Floor(c.y);
            int gx = (int)Math.Floor(tx), gy = (int)Math.Floor(ty);
            double startX = c.x, startY = c.y;
            double nearX = tx - c.x, nearY = ty - c.y;
            // S252：同一格，或 1.5 格内且身体能沿直线过去 → 直接扑过去。否则玩家在它面前左右挪一格，
            // 格子路线每次重算都会翻向另一边，鬼会停在 1 格外原地抖动、永远抓不到（剧情模拟发现的漏洞）。
            if ((cx == gx && cy == gy) || (nearX * nearX + nearY * nearY <= DirectRange * DirectRange && ClearLine(c.x, c.y, tx, ty)))
            {
                c.path = null;
                Direct(c, tx, ty, distance);
            }
            else
            {
                bool need = c.path == null || c.goalX != gx || c.goalY != gy || (chasing && c.repathLeft <= 0);
                if (need)
                {
                    c.path = OverworldMap.Path(map, new OverworldMap.Cell(cx, cy), new OverworldMap.Cell(gx, gy));
                    c.pathIndex = 1;
                    c.goalX = gx;
                    c.goalY = gy;
                    c.repathLeft = 0.25;
                }
                if (c.path == null) return;
                while (distance > 1e-9 && c.pathIndex < c.path.Count)
                {
                    double nx = c.path[c.pathIndex].x + 0.5, ny = c.path[c.pathIndex].y + 0.5;
                    double ddx = nx - c.x, ddy = ny - c.y, d = Math.Sqrt(ddx * ddx + ddy * ddy);
                    if (d <= distance)
                    {
                        c.x = nx;
                        c.y = ny;
                        distance -= d;
                        c.pathIndex++;
                    }
                    else
                    {
                        c.x += ddx / d * distance;
                        c.y += ddy / d * distance;
                        distance = 0;
                    }
                }
                if (distance > 1e-9 && c.pathIndex >= c.path.Count) Direct(c, tx, ty, distance);
            }
            Face(c, c.x - startX, c.y - startY);
        }

        /// <summary>身体（半径 BodyRadius）沿 a→b 直线每 0.1 格取样都不压到墙/树/水/栅栏。</summary>
        private bool ClearLine(double ax, double ay, double bx, double by)
        {
            double dx = bx - ax, dy = by - ay, d = Math.Sqrt(dx * dx + dy * dy);
            int n = Math.Max(1, (int)Math.Ceiling(d / 0.1));
            for (int i = 1; i <= n; i++)
            {
                double t = (double)i / n;
                if (!OverworldMap.Free(map, ax + dx * t, ay + dy * t)) return false;
            }
            return true;
        }
        private void Direct(Chaser c, double tx, double ty, double distance)
        {
            double dx = tx - c.x, dy = ty - c.y, d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 1e-9) return;
            double s = Math.Min(distance, d) / d;
            var moved = OverworldMap.Move(map, c.x, c.y, dx * s, dy * s);
            c.x = moved.x;
            c.y = moved.y;
        }

        private static void Face(Chaser c, double dx, double dy)
        {
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return;
            c.fx = dx / len;
            c.fy = dy / len;
        }

        private static void Turn(Chaser c)
        {
            double fx = c.fx;
            c.fx = -c.fy;
            c.fy = fx;
        }

        private void AddEvent(Chaser c, string type)
        {
            Events.Add(new ChaseEvent { time = Elapsed, chaser = c.point.id, name = c.Name, type = type });
        }

        private WorldParkPack.ExplorePoint FindPoint(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            List<WorldParkPack.ExplorePoint> points = node.points ?? new List<WorldParkPack.ExplorePoint>();
            foreach (WorldParkPack.ExplorePoint point in points)
                if (point != null && string.Equals(point.id, id, StringComparison.Ordinal)) return point;
            return null;
        }

        private List<string> MissingRequirements(List<string> requires)
        {
            var missing = new List<string>();
            if (requires == null) return missing;
            WorldParkPack.Venue venue = session.Venue;
            foreach (string clueId in requires)
            {
                string key = Safe(venue == null ? "" : venue.id) + "/" + Safe(clueId);
                if (session.found.Contains(key)) continue;
                string label = Safe(clueId);
                if (venue != null && venue.clues != null)
                    foreach (WorldParkPack.Clue clue in venue.clues)
                        if (clue != null && string.Equals(clue.id, clueId, StringComparison.Ordinal))
                        {
                            if (!string.IsNullOrEmpty(clue.name)) label = clue.name;
                            break;
                        }
                missing.Add(label);
            }
            return missing;
        }

        private void RefreshRemaining()
        {
            Remaining = node.timeout > 0 ? Math.Max(0.0, node.timeout - Elapsed) : 0.0;
        }
    }

    /// <summary>在 Session 的当前 explore 节点上创建纯运行；不调用 Start，也不会改动流程状态。</summary>
    public static Run Create(WorldParkPack.Session session, out string error)
    {
        error = "";
        if (session == null)
        {
            error = "探索会话不存在";
            return null;
        }
        WorldParkPack.Node node = session.Current;
        if (node == null || node.kind != "explore")
        {
            error = "当前阶段不是 explore";
            return null;
        }
        if (node.timeout < 0)
        {
            error = "explore timeout 不能小于 0";
            return null;
        }
        if (string.IsNullOrWhiteSpace(node.mapText))
        {
            error = "explore 缺少 mapText";
            return null;
        }

        OverworldMap.Map map = OverworldMap.Parse(node.mapText);
        if (map.W <= 0 || map.H <= 0)
        {
            error = "explore 地图为空";
            return null;
        }
        List<OverworldMap.Cell> spawns = OverworldMap.Find(map, 'T');
        if (spawns.Count != 1)
        {
            error = "explore 地图需要且只能有一个 T 出生点";
            return null;
        }
        double x = spawns[0].x + 0.5, y = spawns[0].y + 0.5;
        if (!OverworldMap.Free(map, x, y))
        {
            error = "explore T 出生点不可站立";
            return null;
        }
        return new Run(session, map, node, x, y);
    }

    private static bool Finite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static string Safe(string value)
    {
        return value ?? "";
    }
}
