# S200：以身入局——提前布置连锁陷阱，把马里奥引进来

## 调研结论（借规则，不借素材）
| 来源 | 借来的规则 | 在本项目的落地 |
|---|---|---|
| Deception IV（Tecmo Koei）评测：主角不能攻击，只能把敌人**引到事先布置好的陷阱**；陷阱可以**串成连招**；陷阱有预警与冷却，需要提前规划 [Rice Digital](https://ricedigital.co.uk/review-deception-iv-blood-ties/)、[COGconnected](https://cogconnected.com/review/deception-iv-blood-ties-ps3-review/) | 先布置、再引诱、陷阱接力 | **F 连锁编排** + **自动接力** |
| 同一评测的差评：**触发时机太难**（敌人跑得比落下的陷阱快）、Boss 的"陷阱透视"让人只敢用固定套路 [COGconnected](https://cogconnected.com/review/deception-iv-blood-ties-ps3-review/) | 要解决"按不准"的挫败感；马里奥不能开透视 | 自动接力按"他的位置+速度×预警时长"**预判落点**；马里奥只会记住**自己被坑过的地方**（学习层），不会凭空知道陷阱 |
| 同一评测：Boss 会**记住你放陷阱的位置并绕开** [Rice Digital](https://ricedigital.co.uk/review-deception-iv-blood-ties/) | 敌人的学习 | 学习层：他被坑过的地方，下次经过会**放慢脚步、头顶显示"小心点"**（不绕路，保证 H10 通关率） |
| Stealth 分心工具清单："Tap/敲墙把敌人引到你的位置"、"绊线让追你的人摔一下"、"诱饵"、"远程引爆需要提前布置" [CritPoints](https://critpoints.net/2017/02/01/stealth-game-distraction-tools/) | 引诱 + 起手 | **T 挑衅**（他只听见一个位置）、**绊线 R**（踩到 = 启动连锁） |

## 新机制
| 名字 | 按键/字符 | 规则 | 代价 / 反制 | 可调参数（RushMarioTuning） |
|---|---|---|---|---|
| **连锁编排** | **F**（机关旁）/ **Shift+F**（一键） | 给机关编号①②③④（最多 4 环）。启动后，其余几环在马里奥**走到时自动触发**（预判落点）；每接一环续时 6 秒，顿帧 + "⛓ 连锁 2/4" 字幕；≥3 环 = "完美连锁" | 编号时"咔哒"一声（很近才听得见）；自动触发的机关被他**看见**照样起疑；冷却中的不会强制触发 | `maxChainLinks` `chainArmRange` `chainAutoRange` `chainLiveSeconds` `chainFireTolerance` `chainCannonRange` |
| **启动连锁** | L 或绊线 | 你按 L 触发编号里任意一环，或马里奥踩到绊线 | — | — |
| **挑衅** | **T** | 现形时喊一声"来抓我呀~"：马里奥听见你的位置（起疑 +70，转向你）→ 过来查看 → 看见你就追 | 必须现形；每局 3 次、冷却 8 秒；你把位置交给他了 | `tauntUses` `tauntCooldown` `tauntSuspicion` |
| **绊线** | `R` | 地上一根细线；马里奥踩到绊 0.4 秒并启动连锁；每局一次；你踩不触发 | 他看得见线（H3） | `tripStunSeconds` |
| **马里奥学习层** | — | 记住本局被坑过的位置（最多 6 个），赶路时前方 3 格内有这种地方 → 放慢到 70%，头顶"小心点"；追你时气昏头不小心；每局清空 | 同一个坑用第二次更难中 → 逼你换招、布新连锁 | `learnFromHurt` `cautiousRadius` `cautiousSpeedScale` |

## 典型玩法（诱捕走廊样板）
1. 开局 4 秒内：跑到走廊中段按 **Shift+F** → 香蕉皮①、火②、塌桥③、封路墙④ 自动编号。
2. 回到马里奥面前按 **T** 挑衅 → 他过来 → 你往右跑，**跳过绊线**。
3. 他追你踩中绊线 → 连锁启动：香蕉皮把他滑到火前 → 火点燃旁边的油桶 → 爆炸 → 塌桥掉下一层 → 下层还有火+油桶。

## 规则保障
- H3：自动触发调用机关自己的 `OnTricksterActivate`，**预警一个不少**。
- H4：连锁编排是你这边的工具；马里奥只从三条公开通道感知——看见机关动（同视锥）、听见咔哒/挑衅（只有位置）、自己被坑的记忆。`MindAndDriverNeverReadTricksterTruth` 仍通过。
- H9/H10：学习层只减速不绕路；绊线只绊 0.4 秒；三个样板 + 默认房间全部通过死局/可达检查。
- 工坊"连招路线"把绊线、油桶也算作连锁节点；新增样板"诱捕走廊"。
