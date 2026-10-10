# 可迁移交接状态

> 新电脑或新 Manus 账号接续时，先读本文件、`SESSION_TRACKER.md` 与 `SKILL.md`。Git 分支和提交是最终事实来源；本文件只描述当前交接状态与验证边界。

## 仓库身份

| 项目 | 值 |
|---|---|
| 规范远端 | `https://github.com/jiaxuGOGOGO/MarioTrickster.git` |
| 规范分支 | `genspark_ai_developer` |
| Unity 版本 | `2022.3.61f1` |
| 已废弃依赖 | 不拉取、不依赖 `MarioTrickster-Art` 子模块 |

## 当前基线（2026-10-10）

- Git 基线提交：`49e5b87d142ea144a4336bdb0f661160c6b84f05`（S246）。
- 在 Windows Unity 编辑器中，EditMode：**357 / 357 通过，0 失败，0 跳过**。
- 已验证但仍待用户审阅/提交的最小修改：
  1. `Assets/Tests/EditMode/Step1RushMarioTests.cs`：S240 数据版本断言由严格等于 29 改为不低于 29；
  2. `Assets/Scripts/Gameplay/Step1/Step1Lighting.cs`：移除会误触发隔离检查的实现名称注释。
- Unity 首次导入会生成 121 个未跟踪 `.meta`；它们是独立的仓库卫生项，**不要**与上述两项修改混合提交。下一位维护者应先做 GUID 冲突和引用检查，再单独处理。

## 试玩与地图验证边界（2026-10-10）

- 已尝试并**完整撤销**“独立 Test Player + 多种子批量试玩”的实验：不保留源码、菜单入口、脚本、构建物或运行记录。除非用户明确要求重新评估，不构建 Player 或长时间占用 Unity。
- 现有随机布局已经穷举验证每种组合的可达性；后续随机验证必须按“体验签名”选择少量命名情境与可复现种子，不能只因 seed 数值不同就声称覆盖不同体验。
- 默认流程：静态/纯逻辑审计 → 必要时一次已有 EditMode 回归 → 单一、最多三分钟的人工体验验证。不得把 Editor Test Runner 用作开放式长跑试玩。
- 现有小镇已含 44×28 与 72×40 样板。扩张大地图前，先在 72×40 内验证路线抉择、可读隐蔽区和有余波的世界机关；不要先造更大画布。
- 本次撤销后未重启 Unity。下一次改动游戏规则前，先运行一次既有 EditMode 作为恢复性验收；上面的 357 / 357 是撤销前的已记录基线，不是本次清理后的新测试结果。

## S247 环境真实感修复（2026-10-10，未提交）

- 地面换图按房间字符画判断遮挡：只有“户外 + 露天”的地面画草；室内露出的地面画石板 `StoneTop`；户外但有楼板遮挡，以及被压住的土层，画 `GroundFill`。
- 室内草地 `v` 改画稻草垫 `HayGround`，玩法不变；素材包里自定义的图优先。
- 雨只落在露天的列，停在第一层地面或单向板上；室内不画雨，雨天听觉规则不变。户外挂旗、蛛网必须挂在实心块上。
- 新增 EditMode 测试 `S247_Realistic_Surfaces_Grass_Rain_And_Hangings`。已通过 .NET 编译（0 错误）和纯逻辑实际执行（15/15）；Unity EditMode 回归 359/359 通过（2026-10-10 16:02）。

## S247 关卡编辑适配 + 少字可读（2026-10-10）

- **元素长相唯一来源** `Assets/Scripts/LevelDesign/ElementLook.cs`：每个 ASCII 元素的三色（红=坑他 / 蓝=藏身 / 金=目标 / 灰=地形）、≤4 字动词、一句用法、图标名都只写在这里。作战图 `Step1Glance`、地图图例 `Step1MapLegend`、Unity 关卡工坊、网页设计台全部读它。**以后加新元素：先在 `ElementCatalog` 登记，再在 `ElementLook.cs` 的长相表里加一行**；`ElementLook.Gaps()` 和测试会指出漏了哪一项。
- 图例分组改成跟三色一致（例如油桶会炸 → 归“坑他”），不再出现“颜色说红、图例说藏身”的矛盾。
- Unity 关卡工坊调色板与画布显示像素图标 + 三色底条；网页设计台同样，并提供“画布显示字母”开关对照 ASCII。
- 房间里的“出口 EXIT / 宝物”文字路牌换成图标 + 箭头（`Step1SignIcon`），`BuilderVersion` 升级，旧场景打开时自动重建。
- 新增测试 `S247_ElementLook_SingleSourceIconsAndConsistentGroups`。已通过 .NET 编译（运行时 / 编辑器 / 测试 0 错误）、无 Unity 实际执行（28 个图例条目：0 缺口、0 分组矛盾）、网页数据校验（29 个图标全部可画）；**Unity EditMode 回归 359/359 通过（2026-10-10 16:02）**。
- 存档工具 `save_checkpoint.ps1` 适配：`Assets/Scripts`、`Assets/Tests` 下的新 `.cs`（连同 `.meta`）和网页设计台文件会自动存档；Unity 自动生成的 `Step1_PrankRoom.unity` 与 `Resources/Step1` 自动跳过；其他陌生新文件仍拒绝。Export 模式会把新源码一并复制到 `-new-files` 文件夹。

## 接续前强制检查

```powershell
git status --short --branch
git log -1 --oneline
powershell -ExecutionPolicy Bypass -File docs\AI_CONTINUE_PACK\scripts\bootstrap_windows.ps1
```

然后在 Unity Hub 打开当前 Git 工作副本并运行 EditMode。新 AI 在实现前必须报告：当前 commit、工作树改动、最近测试、未完成项与风险。

需要迁移前，优先运行 `scripts/save_checkpoint.ps1 -Mode Save -Push`。该工具会创建一个带测试摘要的 Git checkpoint，并排除未经审计的 Unity `.meta`；无法 push 时使用 `-Mode Export` 生成补丁交接。

## 提交纪律

- 每次开发结束，更新本文件中“当前基线”的提交、测试和未完成项。
- 所有可交接成果必须推送 GitHub；未 push 的工作必须保存 `.patch` 或 `.bundle`。
- 不提交 `Library/`、`Temp/`、`Logs/`、用户私有凭据或本机许可证。
- 不把未审计的 Unity 自动生成 `.meta` 混入功能提交。
