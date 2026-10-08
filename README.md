# MarioTrickster

> **一句话定位**：非对称对抗游戏——你扮演会伪装的捣蛋者，在房间里布置机关坑一个"冲冲型"马里奥 AI；他只能看、听、记（不作弊），会起疑、学习、反制。

本文件是仓库唯一入口。**S238 起只保留现役的东西**：旧测试台（Ctrl+T）、AI Arena、旧探索系统、旧场景构建器、第二个调参文件都已删除，旧文档在 [docs/archive/](./docs/archive/)（只读，内容可能和现状矛盾）。

---

## 1. 打开 Unity 后先按这几个键

| 键 | 打开什么 | 用来干什么 |
| --- | --- | --- |
| **Ctrl+Alt+H** | 📖 开始页 | 最上面「⏱ 上次做到哪」；下面是全部功能在哪、怎么用来创作 |
| **Ctrl+Alt+T** | 测试中心 | ① 一键体检 → ② ▶ 试玩 → ③ 打包反馈；🧪 跑 EditMode 测试（**唯一的测试入口**） |
| **Ctrl+Alt+W** | 关卡工坊 | 画房间、选样板、检查能不能玩 |
| **Ctrl+Alt+O** | 小镇工坊 | 画小镇大地图、一天总览 |
| **Ctrl+Alt+L** | 台词编辑器 | 改小镇居民和马里奥说的话（你写的存在 `MyTownStories.json`） |

数值全部在**一个**调参文件：`Assets/Resources/Step1/RushMarioTuning.asset`（选中它，Inspector 里按 ⭐ 常用 + 9 组显示，能搜、能只看改过的）。

---

## 2. 文档在哪

| 你想 | 看这里 |
| --- | --- |
| 项目能做什么 / 某功能在哪（自动生成，和开始页同一份） | [docs/FEATURE_MAP.md](./docs/FEATURE_MAP.md) |
| 现在做到哪 / 每次升级改了什么 | [SESSION_TRACKER.md](./SESSION_TRACKER.md) 顶部 + [docs/step1/](./docs/step1/) |
| 设计规则（马里奥不作弊、每种优势要有代价…） | [docs/DESIGN_CONSTITUTION_v1.0.md](./docs/DESIGN_CONSTITUTION_v1.0.md) |
| 每个格子字符是什么元素 | [docs/ELEMENT_LEGEND.md](./docs/ELEMENT_LEGEND.md) |
| 关卡工坊怎么用 | [docs/LEVEL_WORKSHOP.md](./docs/LEVEL_WORKSHOP.md) |
| 换美术素材 | [docs/ASSET_IMPORT_PIPELINE_GUIDE.md](./docs/ASSET_IMPORT_PIPELINE_GUIDE.md) |
| 网页关卡设计台（不用开 Unity 也能画关卡） | [tools/LevelStudioWeb/](./tools/LevelStudioWeb/) |
| 让新账号的 AI 接着做 | [docs/AI_CONTINUE_PACK/SKILL.md](./docs/AI_CONTINUE_PACK/SKILL.md) |

---

## 3. 仓库结构

| 仓库 | 地址 | 职责 |
| --- | --- | --- |
| 主仓库 | [MarioTrickster](https://github.com/jiaxuGOGOGO/MarioTrickster) | 游戏代码、关卡、调参、文档 |
| 美术仓库 | [MarioTrickster-Art](https://github.com/jiaxuGOGOGO/MarioTrickster-Art) | 美术源文件（子模块，挂在 `Assets/MarioTrickster-Art/`） |

```bash
git clone --recurse-submodules https://github.com/jiaxuGOGOGO/MarioTrickster.git
```

用 Unity 2022.3 LTS 打开项目根目录。AI 交付的升级包里双击 `install_and_upload.bat`、选 Y 即可装好并上传。
