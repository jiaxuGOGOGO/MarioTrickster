# S212 更顺的转场 + 地图指引 + 编辑器顺手

## 为什么
- S211 已经是"淡出 → 黑屏里加载 → 淡入"，但：直线淡入淡出有点机械；新场景激活那一帧会卡几百毫秒，淡入第一帧直接跳掉一截；小镇镜头从默认位置滑过来；在从小镇进来的房间里按 F5，编辑器会直接退出 Play（一天进度全丢）。
- 小镇上不知道"下一扇门在哪、来不来得及"；编辑器缺快捷键、重做、吸管，改门里的房间要自己去找。

## 插件：为什么最后没装
| 方案 | 结论 |
|---|---|
| mygamedevtools/scene-loader | 4.1.2 起要 Unity 6；4.1.1 能用，但它记录"当前场景"，和项目里 F5 直接 LoadScene 混用会记错；比现在的做法只多一点点顺滑 |
| PrimeTween / DOTween | 只为缓动曲线装一个库不值：smoothstep 一行 |
| 自己写（采用） | 借它"盖住 → 加载 → 揭开"的流程；零依赖，国内网络也不怕装不上 |

## 做了什么
| 项 | 说明 |
|---|---|
| 圆形转场 | 进门：圆从门口收拢；进房间：在你身上展开；回小镇同理。软边，缓入缓出 |
| 卡顿帧不跳 | 动画阶段单帧最多推进 1/30 秒；等加载阶段按真实时间（10 秒保护照样准） |
| 声音 | 跟着画面淡出淡入 |
| 内存 | 黑屏里释放旧场景没用的资源 |
| 镜头 | 小镇一开始就在你身上（不再滑过来），跟随与帧率无关 |
| F5 | 房间里 F5 = 平滑重开同一扇门，小镇时间不变；打完（结果记下）后不许 F5 刷 |
| 边缘箭头 | 下一扇门不在屏幕里 → 屏幕边上一个粉色三角指过去（来不及时变红） |
| 赛跑提示 | 左上："你 12 秒 / 他 35 秒 ✓ 来得及埋伏"（很紧 / 追不上 / 他已进门） |
| Tab / M | 按住 Tab 看去门的最短路线；M 开关小地图 |
| 门头倒计时 | "门 2 · 18 秒后出发" |
| 小镇工坊 | B R F E I 快捷键、Alt+点吸管、1–9 门、Ctrl+Z/Y、Ctrl+S、F5 试玩、Ctrl+滚轮缩放、中键拖动、悬停信息、矩形预览、点问题定位、◎ 找门、✎ 打开门里的房间、⏱ 时间滑条、Ctrl+C/V 和网页互通、重新编译不丢草稿 |
| 网页大地图 | 同一套快捷键与功能；文件可以直接拖进画布 |

## 规则保障
- 指引只用你本来就知道的信息（公开时间表 + 你看得见的马里奥），马里奥 AI 侧没改（H4）。
- 赛跑提示和检查里的"你能提前 N 秒"是同一个算法（sim 核对第一扇门两者一致）。
- 时间滑条：每一分钟他都在能走的格子上、不瞬移；网页与 Unity 逐字一致（verify）。
- 转场不会永远黑屏（10 秒保护），切换中不吃按键。

## 参考
- 场景加载流程：https://github.com/mygamedevtools/scene-loader
- 激活卡顿随物体数量增长：https://developers.meta.com/horizon/blog/avoiding-hitches-when-loading-scenes-in-unity/
- 用淡出盖住卡顿：https://www.reddit.com/r/Unity3D/comments/m7j2xh/best_way_to_load_a_scene_async_without_stutter/
- S 曲线转场更顺：https://www.reddit.com/r/Unity3D/comments/1u5tg2f/made_a_themed_loading_transition_is_it_cool_or/
- 告诉玩家往哪走：https://www.reddit.com/r/gamedesign/comments/lh58im/other_ways_to_tell_the_player_where_to_go/
- 面包屑讨论：https://www.reddit.com/r/Games/comments/29shsy/rgames_mechanic_discussion_breadcrumb_trail/
- 屏幕外指示器：https://github.com/jinincarnate/off-screen-indicator
- 编辑器手感（快捷键、吸管、悬停）：https://github.com/deepnight/ldtk

## 下一步
- 你在 Unity 里试：圆形转场观感、箭头/小地图位置是否挡视线、F5 重开。
- 可选：小镇上的"埋伏点推荐"（门口附近最近的高草/木箱高亮）。
