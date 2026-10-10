# MarioTrickster 新机制元素注册标准操作流程 (SOP)

本文档旨在说明如何在 MarioTrickster 项目中安全地添加全新机制元素，并确保其能够被正确保存、烘焙 (Bake) 和生成，从而避免对项目其他潜在开发功能产生破坏。

## 背景说明

MarioTrickster 采用纯数据驱动的白盒生成模式，关卡元素由 `AsciiLevelGenerator` 动态创建。当你通过 Inspector 在场景中手动添加一个新类型的机关（例如“旋转锯片2.0”）时，它在当前场景中可以正常工作。

但是，当你尝试将该场景保存为关卡片段（Snippet）或模板时，系统需要将场景对象“反向烘焙”回 ASCII 字符：
- **如果该元素在 `AsciiElementRegistry` 中已有对应的字符映射**：系统能正确识别并将其转换为对应的 ASCII 字符，保存与再次生成均无问题。
- **如果是全新的、未注册的元素**：烘焙系统无法识别该元素，在转换为 ASCII 模板时它会被忽略（或者在验证器中被标记为 `[Misc] Unknown Element`）。**保存后再重新生成该模板时，这个新机关就会永久丢失。**

## 标准操作流程 (SOP)

为了避免新机制丢失或破坏项目结构，请严格遵循以下三步流程：

### 1. 编写独立的行为脚本
首先，为你的新机制编写一个自包含的 C# 逻辑脚本（例如 `NewTrap.cs`）。
- **规则**：脚本应自行处理所有逻辑。如果需要刚体或碰撞体，优先在脚本内部使用 `RequireComponent` 或在 `Awake` 中处理兜底。
- **红线**：**绝对不要**修改 `AsciiLevelGenerator.cs` 核心代码来添加专属的 `SpawnXXX` 方法。

### 2. 在 AsciiElementRegistry 中注册新元素
你需要将新元素注册到 ASCII 字典中，使其获得一个专属字符。

1. **定位或创建 Registry**：在 Project 面板中找到 `AsciiElementRegistry` 资产（如果没有，可以右键 `Create -> MarioTrickster -> Ascii Element Registry` 创建一个）。或者，你可以直接修改 `AsciiElementRegistry.cs` 中的 `CreateDefaultInstance()` 方法添加内置默认条目。
2. **添加条目 (AsciiElementEntry)**：
   - `asciiChar`：分配一个未被占用的 ASCII 字符（例如 `&` 或 `*`）。
   - `elementName`：设置元素名称（例如 `NewTrap`），需保持稳定。
   - `componentTypeNames`：填入你刚才编写的逻辑脚本名称（例如 `NewTrap`）。
   - `customColliderSize` / `isTrigger`：根据需要配置物理属性。
   - `visualColor` / `visualScale`：配置白盒阶段的视觉表现。

### 3. 测试与验证
1. **测试生成**：在 Level Studio 的 `Custom Template Editor` 中输入你分配的新字符，点击生成，确认新机关能够被正确实例化并挂载了相应的脚本。
2. **测试烘焙 (Bake)**：在场景中摆放你的新机关，使用 `Save Scene to Snippet Library` 功能。检查生成的 ASCII 预览中是否正确包含了你分配的新字符。
3. **QA 验证**：运行 Full Level Validator，确保没有出现 `Unknown Element` 警告。

## 总结
**“无注册，不保存”**。所有新机制必须先在 `AsciiElementRegistry` 中注册字符映射，这是确保关卡数据可持久化、可复用且不破坏现有架构的唯一标准路径。
