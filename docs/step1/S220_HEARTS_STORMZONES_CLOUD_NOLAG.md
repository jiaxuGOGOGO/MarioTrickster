# S220 不卡了 + 心和伤害表 + 自己画的雷区 + 补心 / 能量 + Q 雷云 + 看得懂的像素图标

## 一句话
- **小镇工坊不卡了**：下拉菜单、拖动画笔都会马上响应。
  - 以前每拖过一格，就把整张小镇检查一遍、把关卡库里每个房间读一遍、把每个房间的所有随机变体验一遍。
  - 现在拖动时只改格子；松开鼠标（或停 0.12 秒）才检查一次。检查结果也缓存起来，同样的东西不重复算。
  - 网页设计台也一样：鼠标移动时只重画悬停框，松手才重算右边面板。
- **雷区自己画**：工具栏点 ⛈ 雷区（或按 Z），在地图上拖一个框。
  - 雷雨天每 10 秒左右，在框里**随机劈 最少–最多 道**闪电；最少几道、最多几道在右边面板改（1–6）。
  - 勾上"每天"就不管天气，每天都劈。
- **心和伤害**：马里奥和你都是 **3 颗心**，什么东西让谁掉几颗心，有一张固定的表。
- **补心 `+` 和能量 `*`**：
  - 少了心，踩到 `+` 补 1 颗。
  - 捡 `*`，或者用机关砸中马里奥，会攒能量。
  - 能量满 3 格按 **Q** 召唤雷云，但**雷云也会劈到你自己**。
- **看得懂**：大机关、陷阱和道具都有 16×16 像素图标（游戏里和两个编辑器里是同一套）。
  - 美术可以直接换图：菜单 MarioTrickster → Overworld → 导出像素图标模板，改 PNG 后保持同名即可。

## 伤害表（游戏、Unity 工坊、网页里都是这张表）
| 东西 | 谁会中 | 结果 |
|---|---|---|
| ⚡ 闪电（雷区 / 路灯 / 雷云） | 你和马里奥都会 | 十字形 1 格 → 掉 1 颗心 + 晕 2 秒 |
| 🪨 滚石碾到 | 两个都会 | 掉 1 颗心 + 晕 2 秒 |
| 🟫 泥石流冲到 | 两个都会 | 掉 1 颗心 + 晕 2 秒 |
| 💥 被巨炮"轰飞"（不是自己坐炮） | 两个都会 | 掉 1 颗心 + 晕 2 秒；**自己坐炮飞不掉心** |
| 🍌 香蕉皮 | 马里奥 | 只晕，不掉心 |
| 💧 水塔淹到 | 两个都会 | 只变慢，不掉心 |
| ❤ 心掉光 | — | 晕 3 秒，回到 1 颗心继续，**这一天不会结束** |
| 🛡 被打以后 2.5 秒 | — | 这段时间里不再掉心（但还会晕） |
| ➕ 补心 | 两个都能捡 | 少了心才捡，+1，每天每个只能捡一次 |
| ✦ 能量 | 只有你能捡 | +1；用机关砸中马里奥也 +1；最多 3 |

## 为什么这样做（第一性原理 + 调研，三轮取舍）
| 问题 | 结论 | 依据 |
|---|---|---|
| 为什么卡？ | Unity 编辑器窗口**每个鼠标事件都会把整个界面跑一遍**（布局一遍、画一遍，打开鼠标移动以后更多）。我们在里面读硬盘、验所有房间变体，所以拖动和下拉菜单会"攒着"一起执行。解决：重活只在松手 / 空闲时做一次；结果按"地图版本"缓存；界面状态只在一帧画完以后改（OnGUI 中途改布局会让下拉菜单失灵） | [Unity 论坛：OnGUI 每帧多次调用](https://discussions.unity.com/t/ongui-performance/406132)、[OnGUI 更新频率与鼠标移动](https://discussions.unity.com/t/unity-editor-ongui-update-frequency-very-unreliable-when-registering-mouse-position/824454)、[Martin Evans：Unity OnGUI 的 Layout / Repaint 两遍](https://martindevans.me/unity/2017/12/04/Unity-OnGUI/) |
| 闪电随机劈人会不会"坑"？ | 范围由**你画**，道数由**你定**；每道先蓝色闪 **1.2 秒**，最后 0.3 秒变白再劈，看见了就能躲。同一天、同一张图，落点永远一样（可以研究）。范围只劈能走的格子，不劈门口 / 家 / 出生点旁边 | 攻击预警是"给玩家反应时间"，太长又没意思（[GDKeys 攻击结构](https://gdkeys.com/keys-to-combat-design-1-anatomy-of-an-attack/)、[r/gamedesign 讨论过度预警](https://www.reddit.com/r/gamedesign/comments/1cgrzpa/how_can_i_avoid_overtelegraphing_attacks/)、[Game Developer：敌人攻击与预警](https://www.gamedeveloper.com/design/enemy-attacks-and-telegraphing)）；BotW 随机劈人被骂（S219 已调研） |
| 要不要血条？ | 用 **3 颗心**（像马里奥 / 星露谷的那样一眼数得清），不用数字血条。心掉光**不会让这一天结束**，只是晕 3 秒（保持 H1：一天一定能走完）。被打后 2.5 秒内不再掉心，防止连着挨打 | [i-frames 无敌时间](https://www.g2a.com/news/glossary/what-are-invincibility-frames-in-gaming-i-frames-explained/)、[Mario Wiki：闪电](https://www.mariowiki.com/Lightning) |
| 捣蛋者控制天气？ | 能量满了按 Q：雷云**停在你召唤的地方**（不跟着你），半径 3 格、持续 9 秒，每 2.5 秒劈 2 道：一道瞄着云里的马里奥，一道随机。**你站在云里也会被劈**（副作用）。召唤就是一种"动静"，他会注意到 | 用户要求"自然灾害可能同时影响到自己"；Don't Starve 的天气对所有人都公平（[Weather Pain](https://dontstarve.wiki.gg/wiki/Weather_Pain/DST)）；雷云如果跟着人走就太强了（[PoE 论坛：跟随型伤害区的教训](https://www.pathofexile.com/forum/view-thread/3710754)） |
| 色弱玩家分得清吗？ | 图标同时用**形状和颜色**区分：碎心 = 红，星形能量 = 黄，水滴 = 蓝，闪电是锯齿形 | [Colorblind Games：通用色弱编码](https://colorblindgames.com/2023/04/19/universal-colorblind-code/)、[IGDA：游戏里的颜色](https://igda.org/news-archive/color-stories-in-game-design/) |
| 美术换图会不会很麻烦？ | 不麻烦。图标是 16×16 PNG，**同名覆盖**：放进 `Assets/Resources/OverworldArt/` 就用美术的图，删掉就回到内置图标。导入时自动设成像素风（点采样、不压缩）。先做好"占位图"，后面再换，玩法不用改 | [Unity：占位资源问题](https://unity.com/blog/placeholder-asset-problem)、[Unity Aseprite 导入器](https://docs.unity3d.com/Packages/com.unity.2d.aseprite@3.0/manual/ImporterFeatures.html)、[LDtk 实体图标](https://ldtk.io/docs/general/editor-components/entities/)、[星露谷 16px 图块](http://forums.stardewvalley.net/threads/sprite-size-first-time-modding-sv.7128/) |
| 进房间心怎么算？ | 马里奥带着小镇里的心进房间（最少 2 颗，不让房间一开局就差点输）；你的心 = 房间里的命数（最少 1 条）。房间打完，两个人都回满心 | 保持房间平衡（H10：马里奥不碰键盘也能 ≥95% 通关） |

**本轮自我修正（研究后改掉的地方）**：
1. 闪电预警从 1 段改成 2 段：先蓝色闪 1.2 秒，最后 0.3 秒变白。
2. 图标同时用形状和颜色区分（照顾色弱玩家）。
3. 雷云不跟着你走，停在召唤的地方。
4. 编辑器只在一帧画完后改界面状态（以前 OnFocus 时直接重算，下拉菜单会卡住）。
5. 更正我上一版的说法："被巨炮轰飞只晕"是错的。被别人轰飞要掉 1 颗心 + 晕 2 秒；自己坐炮飞不掉心。

**质疑后没做的**：
- 洪水 / 山洪扣血：水只让人变慢，泥石流已经会伤人。
- 全图随机天灾。
- 心掉光直接结束这一天（违反 H1）。
- 重画横版房间里的贴图（这次只做小镇）。
- 山丘 / 山的图标（地形还是只用颜色，以免画面太乱）。

## 怎么用
1. 解压后双击 `apply_S220.bat`，问上传时选 **Y**。
2. Unity → Test Runner → EditMode → Run All（新增 4 项 S220 测试）。
3. Ctrl+Alt+O 打开小镇工坊 → 样板 ▾ → **星露雷镇** → ▶ 试玩小镇。
   - 左上 3 颗心是你，马里奥头上 3 颗心是他，下面 3 格是能量。
   - 雷区里地面蓝色闪 = 马上要劈，赶紧走开。
   - 捡 3 个 ✦ 后按 **Q** 召唤雷云，在马里奥头上召唤，然后自己赶紧跑开。
4. 自己画雷区：工具栏 ⛈ 雷区（或按 Z），拖一个框；右边"⛈ 雷区"改最少 / 最多几道、勾"每天"；右键点雷区 = 删。
5. 换图：菜单 MarioTrickster → Overworld → 导出像素图标模板，用 Aseprite / Procreate 改 PNG（保持同名）。

## 诚实说明
- 沙盒里没有 Unity，只能验证这些：编译通过、规则模拟（S210–S220 全绿；雷区落点、伤害、补心、雷云逐项实跑）、网页和 Unity 规则逐行一致（57 行），以及网页截图（雷区框、图标、伤害表都显示正常）。
- **画面手感和"还卡不卡"要你在 Unity 里试**。如果还卡，按 F8 截图并写一句"在哪一步卡"。
- 调参版本 v21（8 个新字段，在 MarioMindTuning 里都能改）。
