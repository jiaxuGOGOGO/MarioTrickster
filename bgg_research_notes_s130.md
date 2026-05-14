# BGG 机制调研笔记（S130）

## 已浏览来源

1. BGG Blog: `Pushing your luck - the most important mechanism in modern board games`
   - URL: https://boardgamegeek.com/blog/5824/blogpost/119628/pushing-your-luck-the-most-important-mechanism-in
   - 浏览结果：页面在当前环境只加载出标题和导航，正文未能从 Markdown 抽取，因此暂不引用正文观点，只保留为补充线索。

2. BGG Mechanic: `Hidden Movement`
   - URL: https://boardgamegeek.com/boardgamemechanic/2967/hidden-movement
   - 可用定义：隐藏移动是“Movement occurs that is not visible to all players.”
   - 额外说明：BGG 页面提到 Scotland Yard 是经典实现；关键挑战是让移动规则足够简单，使隐藏方不容易误操作，同时游戏结束后路径可追溯。
   - 对 MarioTrickster 的启发：Trickster 的暗处移动必须留下可追溯证据，不能只是不可见地任意改局。Mario 侧需要通过残留、标记、路径复用和扫描窗口完成推理闭环。

## 新增浏览来源

3. BGG Mechanic: `Push Your Luck`
   - URL: https://boardgamegeek.com/boardgamemechanic/2661/push-your-luck
   - 可用定义：玩家必须在“保住已有收益”和“冒着失去收益的风险换取更高奖励”之间选择；该机制也称 press-your-luck。
   - 页面引文要点：Bruno Faidutti 的说明强调“继续或停止、兑现收益或继续下注”；Reiner Knizia 的说明强调“赌注在上升，出错就失去成果，玩家需要知道何时停手”。
   - 对 MarioTrickster 的启发：Trickster 的连锁与 Heat 应该让玩家主动选择“再搞一次还是撤”，而不是被系统强迫等待冷却；Mario 侧也应有对应选择“赶路、查证、诱捕、绕路”。

4. BGG Mechanic: `Take That`
   - URL: https://boardgamegeek.com/boardgamemechanic/2686/take-that
   - 可用定义：竞争性动作直接针对对手的胜利进度，但不直接淘汰对手；常见形式包括偷取、取消、强制弃掉对手资源、行动或能力，并会在短时间内造成权力位置的剧烈变化。
   - 对 MarioTrickster 的启发：Trickster 的阻碍很容易滑向 Take That 负体验。若 Mario 感觉自己的行动、路线或能力被连续取消，而补偿信息不足，就会从“被聪明地骗了”变成“被恶心地针对了”。

5. BGG Game: `Clank!: A Deck-Building Adventure`
   - URL: https://boardgamegeek.com/boardgame/201808/clank-a-deck-building-adventure
   - 可用描述：玩家潜入龙穴偷宝，深入可获得更高价值战利品，但每次粗心声响都会吸引龙的注意；拿到宝物后只有活着逃出才算享受战利品。
   - 关键结构：深入收益、噪音风险、拿宝撤离形成同一个闭环。玩家被鼓励贪，但系统也持续提醒“再贪会死”。
   - 对 MarioTrickster 的启发：MarioTrickster 的阻碍不应只是拖慢 Mario，而应转化为路线选择、风险选择和撤离压力；Trickster 的捣乱也必须反向提高暴露风险。

6. BGG Game: `Burgle Bros.`
   - URL: https://boardgamegeek.com/boardgame/172081/burgle-bros
   - 可用描述：玩家作为盗窃小队潜入高安保建筑，不被抓住地偷取战利品并逃离。每名玩家有三个潜行 token；与守卫同格会失去一个，耗尽后被抓则失败。
   - 关键结构：危险不是直接封路，而是通过有限容错、守卫接近和撤离目标持续压迫玩家。
   - 对 MarioTrickster 的启发：Mario 的体验护栏应更像“消耗容错与迫使改道”，而不是“把路彻底封死”。如果只剩两条路，Trickster 不能连续把两条都变成无效选择。

7. BGG Thread: `What is the trouble with downtime?`
   - URL: https://boardgamegeek.com/thread/1913833/what-is-the-trouble-with-downtime
   - 浏览结果：当前环境未能抽取正文，只能确认主题与搜索摘要，不作为强引用。
   - 对 MarioTrickster 的启发：如果 Mario 被迫停下来等待 Trickster 暗处操作，或者每次异常都必须停下来做低收益排查，就会形成玩家感知上的 downtime。

8. BGG Blog: `Games Cause Downtime Too`
   - URL: https://boardgamegeek.com/blog/5824/blogpost/90060/games-cause-downtime-too
   - 浏览结果：当前环境未能抽取正文，只能确认主题与搜索摘要，不作为强引用。
   - 对 MarioTrickster 的启发：需要把排查和反制设计成“边走边做”的轻动作，而不是让 Mario 暂停核心跑酷循环。
