#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// SpriteSheetDataProviderBridge — 统一封装 Unity 新版 Sprite 切片元数据读写接口。
///
/// Unity 已废弃 TextureImporter.spritesheet，编辑器脚本必须通过
/// UnityEditor.U2D.Sprites.ISpriteEditorDataProvider 读写 SpriteRect。
/// 该桥接层保留项目现有 SpriteMetaData 风格调用，避免各工具分散处理版本细节。
/// </summary>
internal static class SpriteSheetDataProviderBridge
{
    public static SpriteMetaData[] GetSpriteMetaData(TextureImporter importer)
    {
        return GetSpriteRects(importer).Select(ToMetaData).ToArray();
    }

    public static SpriteRect[] GetSpriteRects(TextureImporter importer)
    {
        ISpriteEditorDataProvider provider = GetProvider(importer);
        return provider != null ? provider.GetSpriteRects() : Array.Empty<SpriteRect>();
    }

    public static void SetSpriteMetaData(TextureImporter importer, IEnumerable<SpriteMetaData> metaData)
    {
        if (metaData == null) return;
        SetSpriteRects(importer, metaData.Select(ToSpriteRect));
    }

    public static void SetSpriteRects(TextureImporter importer, IEnumerable<SpriteRect> spriteRects)
    {
        ISpriteEditorDataProvider provider = GetProvider(importer);
        if (provider == null || spriteRects == null) return;

        SpriteRect[] rects = spriteRects.ToArray();
        provider.SetSpriteRects(rects);
        SyncNameFileIdPairs(provider, rects);
        provider.Apply();
    }

    private static ISpriteEditorDataProvider GetProvider(TextureImporter importer)
    {
        if (importer == null) return null;

        SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider?.InitSpriteEditorDataProvider();
        return provider;
    }

    private static SpriteMetaData ToMetaData(SpriteRect spriteRect)
    {
        return new SpriteMetaData
        {
            name = spriteRect.name,
            rect = spriteRect.rect,
            pivot = spriteRect.pivot,
            alignment = (int)spriteRect.alignment,
            border = spriteRect.border
        };
    }

    private static SpriteRect ToSpriteRect(SpriteMetaData metaData)
    {
        return new SpriteRect
        {
            name = metaData.name,
            rect = metaData.rect,
            pivot = metaData.pivot,
            alignment = (SpriteAlignment)metaData.alignment,
            border = metaData.border,
            spriteID = GUID.Generate()
        };
    }

    private static void SyncNameFileIdPairs(ISpriteEditorDataProvider provider, SpriteRect[] rects)
    {
        ISpriteNameFileIdDataProvider nameFileIdProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameFileIdProvider == null) return;

        IEnumerable<SpriteNameFileIdPair> pairs = rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID));
        nameFileIdProvider.SetNameFileIdPairs(pairs.ToList());
    }
}
#endif
