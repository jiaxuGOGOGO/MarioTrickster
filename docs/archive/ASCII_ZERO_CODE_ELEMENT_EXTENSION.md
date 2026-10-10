# ASCII 元素零代码新增机制

本文档定义 `AsciiLevelGenerator` 重构后的元素扩展规则。自本机制生效起，**新增 ASCII 关卡元素不再允许修改生成器核心代码**。生成器只负责读取 `AsciiElementRegistry`，并按照数据配置实例化 GameObject、Collider、Visual 子节点与逻辑组件。

## 设计原则

`AsciiLevelGenerator` 的职责边界已经收敛为三类：读取 ASCII 模板、合并生成 `Ground` / `OneWayPlatform` 地形，以及处理 `M` / `T` 玩家出生点。除这些特殊逻辑外，所有普通元素都由 `AsciiElementRegistry.AsciiElementEntry` 描述。

| 配置字段 | 作用 | 维护要求 |
|---|---|---|
| `asciiChar` | 绑定 ASCII 字符，例如 `^`、`@`、`X`。 | 每个元素必须唯一。 |
| `elementName` | 生成 GameObject 名称前缀，也是主题系统识别元素类型的关键。 | 必须保持稳定，避免破坏换肤匹配。 |
| `componentTypeNames` | 要挂载到根物体上的逻辑脚本类名数组。 | 可以填写短类名，例如 `SpikeTrap`，也可以填写完整类型名。 |
| `customColliderSize` | 根物体 `BoxCollider2D.size`。 | 必须使用 `PhysicsMetrics` 常量或经验证的尺寸。 |
| `customColliderOffset` | 根物体 `BoxCollider2D.offset`。 | 默认为 `Vector2.zero`。 |
| `isTrigger` | 根物体碰撞体是否为 Trigger。 | 陷阱、收集物、终点、隐藏通道通常为 `true`。 |
| `visualColor` / `visualScale` / `sortingOrder` | 白盒 Visual 子节点外观与排序。 | 必须与旧模板视觉表现兼容。 |
| `generateObject` | 是否走通用元素生成路径。 | 空气、`M`、`T`、合并地形等特殊字符应为 `false`。 |

## 新增元素流程

新增元素只允许执行以下流程。首先，新建逻辑脚本，例如 `ConveyorBelt.cs`，并让脚本自包含其运行时行为。如果元素需要刚体、碰撞体或其他 Unity 组件，应优先在脚本内部用 `RequireComponent` 或 `Awake` 做安全兜底。

其次，在 `AsciiElementRegistry.CreateDefaultInstance()` 中增加一条 `AsciiElementEntry` 默认配置。该配置必须写明字符、元素名、逻辑脚本、碰撞体尺寸、Trigger 状态、Visual 颜色与排序。例如：

```csharp
new AsciiElementEntry
{
    asciiChar = '<', elementName = "ConveyorBelt", isSolid = true, isHazard = false, jumpBoost = 0f,
    componentTypeNames = new[] { "ConveyorBelt" },
    visualColor = new Color(0.55f, 0.55f, 0.75f), visualScale = new Vector2(1f, 0.35f),
    customColliderSize = PhysicsMetrics.CONVEYOR_BELT_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
    sortingOrder = 1, isTrigger = false
},
```

最后，在 ASCII 模板中直接使用该字符，并运行模板验证与 PlayMode 测试。**严禁**为新元素在 `AsciiLevelGenerator` 中新增 `SpawnXXX` 方法，严禁向生成循环中新增 `if/else` 或 `switch` 分支。生成器核心代码只允许在架构级需求变更时修改。

## 向后兼容承诺

现有元素的生成参数已经被固化进 `CreateDefaultInstance()` 默认表。旧模板依赖的视碰分离层级保持不变：根物体承载逻辑组件与 `BoxCollider2D`，`Visual` 子节点承载 `SpriteRenderer`。`Ground` 与 `OneWayPlatform` 继续走合并生成逻辑，`M` 与 `T` 继续走玩家出生点特殊逻辑。

| 元素类别 | 兼容策略 |
|---|---|
| 合并地形 `#`、`=`、`-` | 保留 MergeAndSpawn 逻辑，颜色、尺寸、Trigger 等参数来自 Registry。 |
| 普通机关、陷阱、敌人、道具 | 统一走 `SpawnRegisteredElement`，由 `componentTypeNames` 反射挂载逻辑组件。 |
| 玩家出生点 `M` / `T` | 保留专门生成逻辑，避免破坏玩家对象标记与后续测试引用。 |
| 八个登记模板 | 使用字符集已经全部存在于默认 Registry；旧视觉、碰撞体、组件配置已经迁移到默认表。 |

## 代码审查红线

后续任何 PR 或 AI 接手任务中，只要出现下列改动，都应视为违反零代码新增机制并要求退回：在 `AsciiLevelGenerator` 中新增普通元素专属 `SpawnXXX` 方法；在生成循环中为普通元素新增字符分支；把元素行为硬编码到生成器；绕过 `AsciiElementRegistry` 直接在生成器里设置某个普通元素的碰撞体尺寸或组件。
