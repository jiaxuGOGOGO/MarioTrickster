# S211：小镇 ↔ 房间平滑切换 + 场景自动更新

## 一句话
你只管画小镇和房间。进门、回小镇都是**淡出 → 黑屏里后台加载 → 淡入**，还有标题卡；改了任何房间，按 ▶ 时会**自动只重建变了的那几个**。

## S210 的问题（这次修了什么）
| 以前 | 现在 |
|---|---|
| 直接 `LoadScene`：画面卡一下、硬切 | `SceneTransit`：淡出 0.25 秒 → 黑屏里 `LoadSceneAsync` 后台加载（加载好了才激活）→ 淡入 0.3 秒；标题卡"门 2 · 两层监狱 / 埋伏成功！"、"回到小镇 13:30" |
| 在关卡工坊改了房间，要记得回小镇工坊再点一次 ▶ 试玩小镇，不然还是旧房间 | 指纹 = 小镇 + 每个房间内容 + 构建器版本 + 主题；在 Town 场景直接按 Unity 的 ▶ 也会自动发现过期 → 只重建变了的 → 自动开始 |
| 场景按名字加载（项目里别处有同名 Town 就可能进错） | 按完整路径登记和加载 |
| 门从 4 扇改成 3 扇，旧的 Room_4 场景一直留在 Build Settings | 自动删除 |
| 切换中连按 E/Enter 可能重复加载 | 切换中不吃按键、不走时间；只允许一次切换 |
| — | 小镇工坊右栏显示"场景：已是最新 ✓ / 需要更新" |

## 参考的 GitHub 方案
| 项目 | 学到 | 没照搬的 |
|---|---|---|
| [mygamedevtools/scene-loader](https://github.com/mygamedevtools/scene-loader) | 一行 `TransitionAsync(目标, 加载画面)`：先盖住再加载，有进度 | 整个包（要装 UPM 包；我们只需要它的节奏，60 行自己写） |
| [Advanced Scene Manager](https://github.com/Lazy-Solutions/AdvancedSceneManager) | Loading Screen + Transitions + 常驻场景 | 场景集合/常驻场景（两个场景来回，用不上） |
| [vimsos/unity-scene-handling](https://github.com/vimsos/unity-scene-handling) | 普通 LoadScene 是同步的 → 掉帧；异步 + 明确控制何时激活 | 叠加场景（小镇和房间都有相机/GameManager，叠加会打架） |
| [Eflatun.SceneReference](https://github.com/starikcetin/Eflatun.SceneReference) | 场景必须登记且勾选才能加载 → 自动检查、按路径引用 | 属性面板小工具（我们的场景由构建器自动管） |

Unity 官方文档：[allowSceneActivation](https://docs.unity3d.com/ScriptReference/AsyncOperation-allowSceneActivation.html) 为 false 时会堵住之后所有异步加载——所以**不提前预加载下一个房间**（写了防坑注释）。

## 你怎么确认
沙盒：运行时 + 编辑器编译通过；157 条测试文字检查通过；切换节奏模拟（加载瞬间完成时一次切换 0.9 秒；加载一直不好 10 秒后也会揭幕，不会永远黑屏；重复按不叠加）。
**要你在 Unity 里看**：黑幕和标题卡的样子、切换是否顺滑；在关卡工坊改一个房间 → 回 Town 场景按 ▶ → 应该自动重建后进入，门里是新房间。
