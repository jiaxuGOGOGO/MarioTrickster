# Art & Effects 极简使用指南

> 本文档是你在 MarioTrickster 中处理**素材导入 + Shader 视觉效果**的唯一参考卡片。
> 所有操作都可以从 Level Studio（Ctrl+T）的 **★ Art & Effects Hub** 面板一站式完成。

---

## 一句话总结

| 你想做什么 | 用哪个工具 | 快捷键 |
|:---|:---|:---|
| 把新买的素材图导入项目 | 素材导入管线 | `Ctrl+Shift+I` |
| 让 AI 帮你裁切 Sprite Sheet | AI 智能裁切 | `Ctrl+Shift+S` |
| 把美术素材"穿"到已有白盒物体上 | Apply Art to Selected | `Ctrl+Shift+A` |
| 给物体一键加视觉效果（闪白/描边/溶解…） | SEF Quick Apply | `Ctrl+Shift+Q` |
| 精细调参（颜色替换/HSV/投影…） | 效果工厂 | `Ctrl+Shift+E` |
| 整关卡批量换肤 | Theme System（Level Builder Tab 内） | — |

---

## 标准工作流（按顺序走）

```
┌─────────────────────────────────────────────────────────┐
│  Step 0: 用 ASCII 模板生成白盒关卡（灰色方块）           │
│          → Level Builder Tab → Generate Whitebox Level   │
└───────────────────────────┬─────────────────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────┐
│  Step 1: 导入素材                                        │
│  ┌─ 方式A: 素材导入管线（拖入图 → 自动规范化 → 生成物体）│
│  └─ 方式B: AI 智能裁切（AI 识别边界 → 自动切片 → 导入）  │
└───────────────────────────┬─────────────────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────┐
│  Step 2: 把素材穿到白盒物体上                            │
│  → 切到 Visual 模式 → 选中白盒物体 → Apply Art to Selected│
│  → 拖入 Sprite → 选行为模板（AutoDetect 即可）→ 点"应用" │
└───────────────────────────┬─────────────────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────┐
│  Step 3: 加 Shader 视觉效果                              │
│  → 选中物体 → SEF Quick Apply → 点预设按钮即可           │
│  → 需要更细的调整？打开效果工厂逐项调参                   │
└───────────────────────────┬─────────────────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────┐
│  Step 4（可选）: 整关卡批量换肤                           │
│  → 创建 LevelThemeProfile → 填入各元素的 Sprite          │
│  → 回到 Level Builder Tab → Apply Theme                  │
└─────────────────────────────────────────────────────────┘
```

---

## Picking 模式怎么配合

| 模式 | 什么时候用 | 效果 |
|:---|:---|:---|
| **Root**（默认绿色） | 移动/旋转/批量摆放物体 | 点击任何子物体都自动选中 Root |
| **Visual**（蓝色） | 应用素材、调 SEF 效果、调视觉大小 | 点击 Root 自动跳到 Visual 子物体 |
| **Size Sync**（黄色） | 调完视觉大小后自动同步碰撞体 | Visual.localScale ↔ BoxCollider2D.size 联动 |

**经验法则**：
- 摆关卡时用 Root 模式
- 换皮/调效果时切 Visual 模式
- 调完大小后开一下 Size Sync 让碰撞体跟上

---

## 10 个 SEF 效果预设速查

| 预设名 | 适用场景 |
|:---|:---|
| 受击闪白 (Hit Flash) | 任何被攻击的物体 |
| 描边高亮 (Outline) | 可交互物体提示 |
| 冒险描边 (Danger Outline) | 陷阱/危险物体红色警告 |
| 溶解死亡 (Dissolve) | 敌人/陷阱被消灭时 |
| 幽灵剪影 (Silhouette) | Trickster 隐身态 |
| 冰冻 (Frozen) | 被冰冻的敌人/平台 |
| 像素化隐藏 (Pixelate) | 隐藏/模糊效果 |
| 投影 (Drop Shadow) | 增加立体感 |
| 全套战斗 (Full Combat) | 核心战斗角色（闪白+描边+溶解+投影） |
| 清除所有效果 (Reset) | 恢复原始状态 |

---

## 常见问题

**Q: 应用效果后看不到变化？**
A: 确认物体的 SpriteRenderer 使用的是 `MarioTrickster/SEF/UberSprite` Shader。如果不是，SEF Quick Apply 会自动帮你换上。

**Q: 选中物体后工具提示"没有 SpriteRenderer"？**
A: 你可能选中了 Root。切到 **Visual 模式**再试，或者手动展开 Hierarchy 选中 Visual 子物体。

**Q: Theme 换肤后某些元素没变？**
A: LevelThemeProfile 中对应元素的 Sprite 槽位留空了。填入素材后重新 Apply Theme 即可。

**Q: 导入的素材看起来模糊？**
A: 素材导入管线会自动设置 PPU=32 + Point Filter + Uncompressed。如果是手动拖入 Assets 的素材，用管线的"仅规范化"按钮修复。
