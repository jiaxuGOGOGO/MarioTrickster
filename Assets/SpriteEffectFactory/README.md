# SpriteEffectFactory (SEF)

> 2D 像素素材一站式衍生效果工厂 — 拖入素材，自动拆解颜色，点选调整，实时预览，一键应用。

## 快速上手

1. 在 Unity 顶部菜单点击 **MarioTrickster → Sprite Effect Factory**
2. 拖入任意 Sprite（从 itch.io 等网站下载的素材均可）
3. 工具自动提取所有颜色块，点击颜色即可一键替换
4. 展开各效果面板（描边、闪白、溶解等），拖动滑块实时预览
5. 满意后点击「应用到选中物体」，工具自动挂载 `SpriteEffectController`

## 目录结构

```
Assets/SpriteEffectFactory/
├── Editor/                          # 编辑器工具（不打包进游戏）
│   ├── SpriteEffectFactory.Editor.asmdef
│   ├── SpriteEffectFactoryWindow.cs # 主面板
│   ├── SpriteColorAnalyzer.cs       # 颜色自动拆解
│   └── NoiseTextureGenerator.cs     # 溶解噪声贴图生成
├── Runtime/                         # 运行时代码（打包进游戏）
│   ├── SpriteEffectFactory.Runtime.asmdef
│   ├── Scripts/
│   │   ├── SpriteEffectController.cs    # 游戏代码调用的组件
│   │   └── ShaderBackendAdapter.cs      # 多Shader兼容适配器
│   └── Shaders/
│       ├── SEF_UberSprite.shader        # 全效果集成Shader（Built-in + URP双兼容）
│       └── SEF_SharedLogic.hlsl         # URP pass 共享逻辑
└── Resources/                       # 默认资源
```

## 游戏代码对接

```csharp
// 受击闪白
GetComponent<SpriteEffectController>().PlayHitFlash();

// 死亡序列（灰度化 → 溶解消失）
GetComponent<SpriteEffectController>().PlayDeathSequence();

// 反向溶解（出场效果）
GetComponent<SpriteEffectController>().PlayDissolveIn();
```

## 第三方 Shader 兼容

安装 All In 1 Sprite Shader / Sprite Shaders Ultimate 后，工具会自动检测并在面板中暴露其额外属性。操作体验不变。

## 隔离性

- 独立 asmdef 编译域，不影响 `MarioTrickster` / `MarioTrickster.Editor` 的编译
- 所有类无 namespace（与项目现有风格一致），但类名均以 `SEF` 或 `SpriteEffect` 为前缀避免冲突
- 删除整个 `Assets/SpriteEffectFactory/` 文件夹即可完全卸载，零残留
