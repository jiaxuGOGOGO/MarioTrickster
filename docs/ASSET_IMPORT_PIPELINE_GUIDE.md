# Asset Import Pipeline Guide

> **一句话定义**：本指南负责说明“外部美术素材如何进入项目，并变成可直接放进关卡的 Object 或 Prefab”。日常关卡生产请先看 [策划快速指南](./PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md)，项目进度以 [SESSION_TRACKER.md](../SESSION_TRACKER.md) 为准。[1] [2]

---

## 1. 最短工作流

| 你的素材 | 你做什么 | 系统自动做什么 |
| --- | --- | --- |
| 单张平台、陷阱、道具、背景 | 拖进 `Assets/Art/Imported/`，在导入窗口选择大类并一键导入。 | 规范化 Texture Importer、生成 Root/Visual 对象、挂 SpriteRenderer、碰撞体、SEF 材质和 Prefab。[1] |
| 多帧普通动画 | 拖入 Sprite Sheet 或散帧文件夹，再应用到目标物体。 | Apply Art 会优先读取已有切片；未切片贴图可用自动、AI 后台或手动参数切片，再挂 `SpriteFrameAnimator` 按帧循环播放。[1] |
| 角色状态动画 | 使用 `idle/run/jump/fall` 命名散帧或切片，再应用到角色 Visual。 | 分类器分组后自动挂 `SpriteStateAnimator`，按运动状态切换；单独 RUN 也可用于试跑。[1] |
| 大型合集图 | 先用 `Tools/sprite_sheet_slicer.py` 或 `Tools/ai_smart_slicer.py` 裁切；若只是规则动画条或网格，也可直接在 Apply Art 里用 AI 后台识别或手动指定切片。 | 输出独立素材或写入 Unity Sprite 切片后走同一条导入链路。[1] |
| 已有白盒物体换皮 | 选中 Root 或 Visual，打开 Apply Art 并应用素材。 | 后台归一到行为 Root，只替换视觉层，不破坏碰撞、脚本和 Prefab 归属。[2] |

推荐做法是先让白盒关卡可玩，再把验证通过的白盒对象逐个换成商业素材。美术替换必须服务关卡主线，不能反过来阻塞关卡迭代。[2]

---

## 2. 角色状态动画命名规则

角色状态动画只需要把帧名写清楚。分类器会按单帧自己的文件名或 Sprite 名识别状态，而不是依赖用户手动填表。[1]

| 状态 | 推荐命名 | 常见别名 | 播放策略 |
| --- | --- | --- | --- |
| Idle | `hero_idle_00.png`, `mario_idle_01` | `stand`, `breath` | 循环，默认 6 fps。 |
| Run | `hero_run_00.png`, `mario_run_01` | `walk`, `move` | 循环，默认 12 fps。 |
| Jump | `hero_jump_00.png`, `mario_jump_01` | `rise`, `up` | 非循环，默认 10 fps。 |
| Fall | `hero_fall_00.png`, `mario_fall_01` | `drop`, `down` | 非循环，默认 10 fps。 |

当素材应用到角色目标且识别到 `idle/run/jump/fall` 任意核心运动状态时，工具会优先使用 `SpriteStateAnimator`。完整四状态会按各自帧播放；只导入单独 RUN 时也能用于试跑，左右移动播放跑步帧，缺失的 Idle/Jump/Fall 会使用已有帧做静态兜底。普通多帧装饰物、道具或特效仍走 `SpriteFrameAnimator`，避免把火把、水面、传送门等循环素材误判成角色控制动画。[1]

> **入口澄清**：角色状态动画不是一个额外的 Unity 功能按钮或独立配置面板；它是 `Asset Import Pipeline` 与 `Apply Art To Selected` 内部的自动分支。把按 `idle/run/jump/fall` 命名的散帧或切片拖进去并应用后，系统会在目标 `Visual` 上自动挂 `SpriteStateAnimator`。

> **命名建议**：同一套角色素材尽量使用 `角色名_状态_两位编号`，例如 `hero_idle_00`、`hero_idle_01`、`hero_run_00`、`hero_run_01`。不要把 `background`、`ground`、`foreground` 这类场景词混在角色帧文件名里，以免降低人工排查效率。[1]
>
> **安全改名流程**：如果商业包命名混乱，先打开 `Ctrl+T → Art & Effects Hub → 策划生产助手` 生成语义报告。报告会给出建议命名；点击 **采纳建议并批量改名** 后会先显示预览和确认框，只有确认后才会修改 Sprite 切片名或单图资源名。

---

## 3. Unity 入口

| 入口 | 快捷键或菜单 | 用途 |
| --- | --- | --- |
| Asset Import Pipeline | `MarioTrickster → Asset Import Pipeline` 或 `Ctrl+Shift+I` | 从外部图片生成项目内 Object / Prefab。 |
| Apply Art To Selected | `Ctrl+Shift+A` | 给已有白盒对象换皮，支持散帧、Sprite Sheet、状态动画、SEF、AI 后台识别切片和手动 cell size / rows / columns 切片。 |
| SEF Quick Apply | `MarioTrickster → SEF Quick Apply` 或 `Ctrl+Shift+Q` | 给选中物体快速套视觉效果预设，并保存 Prefab。 |
| Planner Production Assistant | `Ctrl+T → Art & Effects Hub → 策划生产助手` | 对素材包做语义巡检、给出建议命名，经弹窗确认后批量改名，Theme 自动填槽，复制新机制请求模板。 |

---

## 4. 裁切工具

大型商业合集图通常需要先拆成独立素材。透明背景、明显网格或间距清晰的图，可以先用纯像素裁切；构图复杂、多个物件贴得很近时，再用 AI 智能裁切。[1]

Apply Art 内置的 **Sprite Sheet 切片**适合“给当前选中白盒对象直接换皮”的轻量场景。默认 **Auto Detect** 会处理文件名含帧尺寸、32px 网格和横向等宽动画条；当用户把 `Idle(32x32).png`、`Run(32x32).png`、`Jump(32x32).png` 这类横向状态条放进同一个文件夹再应用到角色时，工具会先逐张完成切片，再按 `idle/run/jump/fall` 语义分组挂载状态动画，不会再把整张 Sheet 当作单帧贴到角色上。对 Mario/Trickster 等已带角色控制器的对象，Apply Art 必须只改 `Visual` 上的 SpriteRenderer 与 SpriteStateAnimator，并在应用后兜底保证根物体 Rigidbody2D 仍为 Dynamic、可模拟、非 Trigger、仅冻结旋转；禁止因为素材名、旧 Trigger 或商业包分类把角色根物体改成陷阱/道具模板。**AI Backend** 会复用 AI Smart Slicer 的 API Key、Base URL 与模型配置，上传当前贴图预览给视觉模型判断 rows / columns / cell size / 裁切区域；**Manual Grid** 适合直接指定行列数，**Manual Cell Size** 适合商业素材说明中已给出单帧尺寸的情况。无论哪种模式，点击应用后都会写入 Unity Sprite 切片并继续走同一套动画与分类链路。

```bash
# 网格切割
python Tools/sprite_sheet_slicer.py input.png --cols 8 --rows 4

# 自动检测独立物体
python Tools/sprite_sheet_slicer.py input.png --auto

# 去除背景色后自动分割
python Tools/sprite_sheet_slicer.py input.png --auto --remove-bg "#00ff00"

# AI 智能分割
python Tools/ai_smart_slicer.py commercial_pack.png
```

| 工具 | 适合场景 | 注意事项 |
| --- | --- | --- |
| `sprite_sheet_slicer.py` | 网格规整、透明间距明显、批量低成本处理。 | 它按像素连通域工作，不理解“这是角色一组动画”。 |
| `ai_smart_slicer.py` | 商业包、多个物件混排、需要语义命名或动画归组。 | 需要可用的视觉模型 API 环境，结果仍应人工快速扫一遍。 |

---

## 5. Pivot 设置

所有素材导入和切片工具现在统一使用 `PivotPresetUtility` 管理 Pivot。默认为 **Auto** 模式，根据素材物理分类自动选择；用户可随时手动覆盖为任意预设或自定义坐标。

| Pivot 预设 | 坐标 (X, Y) | 适用场景 |
| --- | --- | --- |
| Auto | 按分类自动 | 默认选项，角色→BottomCenter，其他→Center |
| Center | (0.5, 0.5) | 地形、陷阱、特效、道具 |
| Bottom Center | (0.5, 0) | 角色、敌人（脚底对齐地面） |
| Top Center | (0.5, 1) | 悬挂物体、天花板装饰 |
| Bottom Left | (0, 0) | 左下角对齐的 UI 元素 |
| Custom | 用户自定义 | 任意特殊需求 |

> **Auto 模式智能推断**：当素材应用到没有 `ImportedAssetMarker` 的对象时（如原始 Mario 白盒），系统会检查目标对象是否有 `MarioController`、`TricksterController` 等角色组件，自动使用 BottomCenter。角色、敌人这类底部对齐素材现在会进一步扫描每帧不透明像素边界，把 Pivot 放在“可见脚底”而不是透明画布底边；因此即使原图下方有透明留白，也不会再出现换皮后脚底悬空的问题。

> **事后修正**：如果导入后发现 Pivot 不对，可以使用 `MarioTrickster → Art Pipeline → Pivot 修正工具` 单独修正，支持单个物体、单张贴图或批量文件夹。所有操作支持 Ctrl+Z 撤销。

---

## 6. 关键文件

| 文件 | 职责 |
| --- | --- |
| `Assets/Scripts/Editor/AssetImportPipeline.cs` | 主导入窗口，负责规范化、切片、生成对象和 Prefab。 |
| `Assets/Scripts/Editor/AssetApplyToSelected.cs` | 给已有对象换皮，负责 Root 归一、动画挂载、行为模板和碰撞体适配；对 Mario/Trickster 等角色启用控制链路保护，只允许更新 Visual/SpriteStateAnimator，不再套用陷阱/道具行为模板或重算角色碰撞体尺寸。 |
| `Assets/Scripts/Editor/ArtAssetClassifier.cs` | 素材分类器，识别角色、敌人、道具、陷阱、背景、特效、核心运动状态与通用商业素材语义状态。 |
| `Assets/Scripts/Core/SpriteFrameAnimator.cs` | 普通多帧循环动画播放器。 |
| `Assets/Scripts/Core/SpriteStateAnimator.cs` | 角色 idle/run/jump/fall 状态动画播放器。 |
| `Assets/Scripts/Core/ImportedAssetMarker.cs` | 记录导入元数据和状态摘要，帮助后续换皮、分类复用和新状态挂接。 |
| `Assets/SpriteEffectFactory/Editor/SEF_QuickApply.cs` | 视觉效果预设快速应用。 |
| `Tools/sprite_sheet_slicer.py` | 纯像素裁切工具。 |
| `Tools/ai_smart_slicer.py` | AI 语义裁切工具。 |
| `Assets/Scripts/Editor/PivotPresetUtility.cs` | 统一 Pivot 预设枚举、自动推断、GUI 绘制和转换工具。 |
| `Assets/Scripts/Editor/PivotRepairTool.cs` | 事后修正 Pivot 的独立编辑器窗口；角色/敌人素材同样使用可见脚底 Pivot，避免批量修复覆盖前述防护。 |

---

## References

[1]: ../SESSION_TRACKER.md "MarioTrickster Session Tracker"
[2]: ./PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md "Planner Fast Level Production Guide"
