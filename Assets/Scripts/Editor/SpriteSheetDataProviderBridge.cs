#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

/// <summary>
/// SpriteSheetDataProviderBridge — 统一封装 Sprite 切片元数据读写。
///
/// 说明：部分项目 Unity 版本没有新版 Sprite 切片编辑器编译期 API。
/// 为保证编辑器脚本在当前项目环境稳定编译，这里集中使用 TextureImporter.spritesheet
/// 兼容路径，并在桥接层内部屏蔽弃用警告，避免警告扩散到业务工具脚本。
/// </summary>
internal static class SpriteSheetDataProviderBridge
{
#pragma warning disable 0618
    public static SpriteMetaData[] GetSpriteMetaData(TextureImporter importer)
    {
        if (importer == null) return Array.Empty<SpriteMetaData>();
        SpriteMetaData[] spritesheet = importer.spritesheet;
        return spritesheet != null ? spritesheet.ToArray() : Array.Empty<SpriteMetaData>();
    }

    public static void SetSpriteMetaData(TextureImporter importer, IEnumerable<SpriteMetaData> metaData)
    {
        if (importer == null || metaData == null) return;
        importer.spritesheet = metaData.ToArray();
    }
#pragma warning restore 0618
}
#endif
