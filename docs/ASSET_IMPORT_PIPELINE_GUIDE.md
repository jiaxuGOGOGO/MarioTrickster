# Asset Import Pipeline — 素材导入自动化指南

> **一句话定义**：从"下载/购买的美术素材图片"到"项目中可直接用于 Sprite Effect Factory 的 Object + 可复用 Prefab 蓝图"，全程最多 3 次点击。

---

## 全链路概览

```
外部素材（PNG/PSD/Sprite Sheet）
        │
        ▼ [可选] Python 批量裁切（大图拆分为单 Object）
        │
        ▼ 拖入 Assets/Art/Imported/
        │
        ▼ [自动] ART_BIBLE 规范化（PPU=32, Point, Uncompressed）
        │
        ▼ [自动] 弹出 Asset Import Pipeline 窗口
        │
        ▼ 选择物理类型 → 一键导入
        │
        ├─→ 场景 Object（Root + Visual 视碰分离架构）
        │       └─ SpriteRenderer + SEF Material + SpriteEffectController
        │
        ▼ [可选] SEF Quick Apply 选效果预设
        │
        └─→ Prefab 蓝图（自动保存，可直接拖入关卡）
```

---

## 快速上手（3 步完成）

### 第 1 步：素材准备

**情况 A：单个 Object 图片（如一个地刺、一个平台）**
- 直接拖入 Unity 的 `Assets/Art/Imported/` 目录即可

**情况 B：大型 Sprite Sheet（如一行 8 帧的角色动画）**
- 直接拖入，导入时选择"手动切片"并指定列数

**情况 C：一张大图包含多个不同 Object（如一个合集图）**
- 先用 Python 工具裁切：
```bash
cd MarioTrickster/Tools
python sprite_sheet_slicer.py 合集图.png --auto --remove-bg "#ff00ff"
# 输出到 ./sliced/ 目录，每个 Object 一个独立 PNG
```
- 然后将 `sliced/` 目录下的文件拖入 Unity

### 第 2 步：导入生成 Object

1. 拖入素材后，**Asset Import Pipeline** 窗口自动弹出
   - 也可手动打开：菜单 `MarioTrickster → Asset Import Pipeline` 或快捷键 `Ctrl+Shift+I`
2. 选择**物理类型**（角色/地形/陷阱/特效/道具）
3. 点击 **"一键导入并生成 Object"**

完成！场景中已生成带有正确物理设置和 SEF 效果控制器的 Object。

### 第 3 步：选择效果 → 保存蓝图

1. 选中刚生成的 Object
2. 打开 `MarioTrickster → SEF Quick Apply`（快捷键 `Ctrl+Shift+Q`）
3. 点击想要的效果预设（如"全套战斗"、"危险描边"等）
4. 效果自动应用，Prefab 蓝图自动保存到 `Assets/Art/Prefabs/`

**之后使用**：直接从 Project 窗口拖 Prefab 到场景即可，效果参数已内嵌。

---

## 工具详解

### 1. Asset Import Pipeline（Unity Editor 窗口）

| 功能 | 说明 |
|------|------|
| 拖拽导入 | 支持拖入单图、多图、文件夹 |
| 自动规范化 | PPU=32, Point 滤镜, 无压缩, Alpha 透明, Read/Write |
| 智能切片 | 自动模式根据宽高比推测帧数；手动模式指定行列 |
| 物理类型 | 5 种预设（角色/地形/陷阱/特效/道具），自动设置碰撞体和 Pivot |
| 视碰分离 | 生成的 Object 遵循 S37 架构（Root 承载碰撞体 + Visual 子物体承载渲染） |
| SEF 集成 | 自动挂载 UberSprite Shader + SpriteEffectController |
| Prefab 输出 | 一键保存为可复用蓝图 |

### 2. SEF Quick Apply（Unity Editor 窗口）

| 预设 | 适用场景 |
|------|---------|
| 受击闪白 | 所有可受击物体 |
| 选中描边 | 可交互物体、Trickster 伪装目标 |
| 危险描边 | 陷阱、危险区域 |
| 溶解死亡 | 可消灭的敌人 |
| 幽灵剪影 | Trickster 幽灵形态 |
| 冰冻效果 | 冰冻状态 |
| 像素化隐藏 | 隐藏/模糊效果 |
| 投影 | 增加立体感 |
| 全套战斗 | 核心战斗角色（闪白+描边+溶解+投影） |

选择预设后，效果直接应用在物体上，同时自动保存/更新 Prefab。

### 3. Python Sprite Sheet Slicer（命令行工具）

```bash
# 网格切割
python Tools/sprite_sheet_slicer.py input.png --cols 8 --rows 4

# 自动检测独立物体
python Tools/sprite_sheet_slicer.py input.png --auto

# 去除背景色后自动分割
python Tools/sprite_sheet_slicer.py input.png --auto --remove-bg "#00ff00"

# 批量处理整个文件夹
python Tools/sprite_sheet_slicer.py ./raw/ --cols 8 --rows 1 -o ./sliced/
```

### 3b. AI Smart Slicer（AI 智能裁切 — 推荐）

调用 GPT-4.1 视觉模型，让 AI “看”图片并判断哪些是独立物体、哪些是动画帧组，然后精准裁切。

```bash
# AI 智能分割（最推荐，需要 OPENAI_API_KEY）
python Tools/ai_smart_slicer.py commercial_pack.png

# 去除背景后 AI 分析
python Tools/ai_smart_slicer.py sheet.png --remove-bg "#ff00ff"

# 批量处理文件夹
python Tools/ai_smart_slicer.py ./raw_assets/ -o ./ai_sliced/
```

**AI 模式 vs 纯像素模式的区别**：

| 能力 | `--auto`（纯像素） | `ai_smart_slicer.py`（AI） |
|------|---------|--------|
| 区分独立物体 | ✅ 基于透明间距 | ✅ 基于语义理解 |
| 识别动画帧组 | ❌ 会拆成单帧 | ✅ 自动归组并标注帧数 |
| 处理相邻/重叠物体 | ❌ 会合并 | ✅ 语义分离 |
| 自动命名 | ❌ obj001/obj002 | ✅ hero_walk/tree_01 |
| 离线可用 | ✅ | ❌ 需要网络 |
| 成本 | 免费 | 极低（约 $0.01/张） |

### 4. Art Drop Auto-Importer（自动触发）

当文件被拖入 `Assets/Art/Imported/` 目录时，自动弹出 Asset Import Pipeline 窗口。无需手动打开菜单。

---

## 与现有系统的关系

| 现有系统 | 本管线的关系 |
|---------|------------|
| TA_AssetValidator（防御塔） | 互补：防御塔拦截 `Assets/Art/` 全目录；本管线额外覆盖 `Imported/` 子目录并触发 UI |
| AI_SpriteSlicer（切片母机） | 升级替代：本管线包含切片功能且更智能（自动检测+手动+单帧三模式） |
| Sprite Effect Factory（效果工厂） | 上游衔接：本管线生成的 Object 已预装 SEF，可直接送入效果工厂精细调参 |
| SEF Quick Apply（快速应用） | 下游简化：对于不需要精细调参的场景，直接选预设即可 |
| LevelThemeProfile（主题换肤） | 平行：本管线生成的 Prefab 可被 Theme Profile 引用作为元素 Sprite |
| AsciiLevelGenerator（关卡生成） | 平行：白盒关卡生成后，可用本管线导入的 Prefab 做换肤 |

---

## 回答你的问题

### "选择完效果就直接应用在具体物体上了么？"

**是的。** SEF Quick Apply 选择预设后，效果参数立即写入物体上的 `SpriteEffectController` 组件，运行时自动生效。

### "然后自己拖到本地项目形成一个蓝图是么？"

**是的，而且是自动的。** 选择效果后，系统自动将物体保存为 Prefab（蓝图）。之后你只需从 Project 窗口拖 Prefab 到场景，就是一个完整的、带效果的可用物体。

### 完整工作流总结

```
你的操作                          系统自动完成
─────────                        ──────────
下载素材图片                      —
[可选] 用 Python 裁切大图          —
拖入 Assets/Art/Imported/         → 自动规范化 + 弹出导入窗口
选物理类型 + 点"一键导入"          → 切片 + 生成 Object + 挂载 SEF
选效果预设                        → 应用效果 + 保存 Prefab 蓝图
                                  → 完成！Prefab 可直接拖入关卡
```

**你的精力只需放在两个决策上：这个素材是什么类型（角色/地形/陷阱...）、想要什么效果。其余全自动。**

---

## 文件清单

| 文件 | 职责 |
|------|------|
| `Assets/Scripts/Editor/AssetImportPipeline.cs` | 主导入管线 Editor 窗口 |
| `Assets/SpriteEffectFactory/Editor/SEF_QuickApply.cs` | 效果预设快速应用 + 蓝图保存 |
| `Assets/Scripts/Editor/ArtDropAutoImporter.cs` | 拖入自动触发 |
| `Assets/Scripts/Core/ImportedAssetMarker.cs` | 导入元数据标记组件 |
| `Tools/sprite_sheet_slicer.py` | Python 批量裁切工具（纯像素模式） |
| `Tools/ai_smart_slicer.py` | AI 智能裁切工具（视觉模型模式，推荐） |
| `docs/ASSET_IMPORT_PIPELINE_GUIDE.md` | 本文档 |
