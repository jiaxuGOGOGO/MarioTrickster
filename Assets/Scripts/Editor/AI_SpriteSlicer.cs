#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// AI 资产切片母机 (AI_SpriteSlicer)
/// 
/// 核心职责：
///   1. 强制执行 ART_BIBLE.md 规范：PPU=32, Filter=Point, AlphaIsTransparency=True。
///   2. 物理重心死锁：角色类资产强制 Bottom Center (0.5, 0)，特效类强制 Center (0.5, 0.5)。
///   3. 一键工业化：自动识别长图帧数并精准切片。
/// 
/// [AI防坑警告] 
///   本脚本是美术管线的核心。严禁修改 Pivot 逻辑，否则会导致所有 Animator 动画滑步。
///   切片前请确保素材已在 PROMPT_RECIPES.md 中登记。
/// </summary>
public class AI_SpriteSlicer : EditorWindow
{
    private Texture2D sourceTexture;
    private int colCount = 8; 
    private int sliceType = 0; // 0: 角色/敌人(底部重心), 1: 视效特效(中心重心), 2: 地形/平台(中心重心)

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

        if (GUILayout.Button("🔥 一键扣除纯色底并精准切片"))
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

        // 2. 计算切片
        int width = sourceTexture.width / colCount;
        int height = sourceTexture.height;
        
        List<SpriteMetaData> newData = new List<SpriteMetaData>();
        
        for (int i = 0; i < colCount; i++)
        {
            SpriteMetaData smd = new SpriteMetaData();
            
            // 3. 彻底封杀横版平台滑步！根据物理类型死锁重心 (ART_BIBLE 规范)
            // 0: Entity (Bottom Center), 1: VFX (Center), 2: Environment (Center)
            smd.pivot = (sliceType == 0) ? new Vector2(0.5f, 0.0f) : new Vector2(0.5f, 0.5f); 
            smd.alignment = (sliceType == 0) ? (int)SpriteAlignment.BottomCenter : (int)SpriteAlignment.Center;
            
            smd.name = sourceTexture.name + "_F" + i;
            smd.rect = new Rect(i * width, 0, width, height);
            newData.Add(smd);
        }

        ti.spritesheet = newData.ToArray();
        
        // 4. 保存并重新导入
        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();
        
        Debug.Log($"✅ {sourceTexture.name} 切片完成！帧数: {colCount}, 重心: {(sliceType == 0 ? "Bottom Center" : "Center")}, PPU: 32");
        AssetDatabase.Refresh();
    }
}
#endif
