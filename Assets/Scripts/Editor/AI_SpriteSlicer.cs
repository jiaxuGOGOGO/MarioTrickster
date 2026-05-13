#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// AI 资产切片母机 (AI_SpriteSlicer)
/// 
/// 核心职责：
///   1. 强制执行 ART_BIBLE.md 规范：PPU=32, Filter=Point, AlphaIsTransparency=True。
///   2. 物理重心：默认 Auto 模式按素材分类自动选择 Pivot，用户可手动覆盖。
///   3. 一键工业化：自动识别长图帧数并精准切片。
/// 
/// [AI防坑警告] 
///   本脚本是美术管线的核心。Pivot 逻辑统一走 PivotPresetUtility。
///   切片前请确保素材已在 PROMPT_RECIPES.md 中登记。
/// </summary>
public class AI_SpriteSlicer : EditorWindow
{
    private Texture2D sourceTexture;
    private int colCount = 8; 
    private int sliceType = 0; // 0: 角色/敌人, 1: 视效特效, 2: 地形/平台

    // Pivot 选择（统一使用 PivotPresetUtility）
    private PivotPresetUtility.PivotPreset _pivotPreset = PivotPresetUtility.PivotPreset.Auto;
    private Vector2 _customPivot = new Vector2(0.5f, 0.5f);

    [MenuItem("MarioTrickster/Art Pipeline/一键工业化切图 (强制执行 ArtBible 规范)")]
    public static void ShowWindow()
    {
        GetWindow<AI_SpriteSlicer>("AI 资产切片母机");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("执行前，请确保你已在本地 git pull 了最新的配方图纸。", MessageType.Info);
        
        sourceTexture = (Texture2D)EditorGUILayout.ObjectField("AI 序列长图", sourceTexture, typeof(Texture2D), false);
        colCount = EditorGUILayout.IntField("目标帧数 (列):", colCount);
        sliceType = EditorGUILayout.Popup("资产物理类型:", sliceType, new string[] { "实体角色/敌人 (Bottom Center 防滑步)", "纯特效 VFX (Center 居中)", "地形/平台 (Center 居中, 适用于 Tiled)" });

        // Pivot 设置
        EditorGUILayout.Space(4);
        {
            // sliceType: 0=Character, 1=VFX(3), 2=Environment(1)
            int physicsTypeHint = sliceType == 0 ? 0 : (sliceType == 2 ? 1 : 3);
            var autoResolved = PivotPresetUtility.AutoDetectFromPhysicsType(physicsTypeHint);
            PivotPresetUtility.DrawPivotSelector("Pivot 预设", ref _pivotPreset, ref _customPivot, autoResolved);
        }

        EditorGUILayout.Space(4);
        if (GUILayout.Button("一键扣除纯色底并精准切片", GUILayout.Height(30)))
        {
            if (sourceTexture == null)
            {
                Debug.LogError("未选中贴图！");
                return;
            }
            
            SliceTexture();
        }
    }

    private void SliceTexture()
    {
        string path = AssetDatabase.GetAssetPath(sourceTexture);
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        
        if (ti == null) return;

        // 1. 严格执行 2D 像素与透明通道标准 (ART_BIBLE 规范)
        ti.isReadable = true;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.alphaIsTransparency = true; 
        ti.filterMode = FilterMode.Point; // 像素游戏防模糊铁律
        ti.spritePixelsPerUnit = 32; // 强制 32 PPU
        ti.textureCompression = TextureImporterCompression.Uncompressed; // 保证像素清晰

        // 2. 解析 Pivot
        // sliceType: 0=Character, 1=VFX(3), 2=Environment(1)
        int physicsTypeHint = sliceType == 0 ? 0 : (sliceType == 2 ? 1 : 3);
        var resolvedPreset = PivotPresetUtility.ResolvePreset(_pivotPreset, physicsTypeHint);
        Vector2 pivot = PivotPresetUtility.PivotToVector2(resolvedPreset, _customPivot);
        int alignment = PivotPresetUtility.PivotToAlignment(resolvedPreset);

        // 3. 计算切片
        int width = sourceTexture.width / colCount;
        int height = sourceTexture.height;
        
        List<SpriteMetaData> newData = new List<SpriteMetaData>();
        
        for (int i = 0; i < colCount; i++)
        {
            SpriteMetaData smd = new SpriteMetaData();
            smd.pivot = pivot;
            smd.alignment = alignment;
            smd.name = sourceTexture.name + "_F" + i;
            smd.rect = new Rect(i * width, 0, width, height);
            newData.Add(smd);
        }

        SpriteSheetDataProviderBridge.SetSpriteMetaData(ti, newData);
        
        // 4. 保存并重新导入
        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();
        
        string pivotName = PivotPresetUtility.GetPresetDisplayName(resolvedPreset);
        Debug.Log($"[AI_SpriteSlicer] {sourceTexture.name} 切片完成！帧数: {colCount}, Pivot: {pivotName} ({pivot.x:F1}, {pivot.y:F1}), PPU: 32");
        AssetDatabase.Refresh();
    }
}
#endif
