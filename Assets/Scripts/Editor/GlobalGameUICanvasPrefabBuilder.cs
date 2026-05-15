using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GlobalGameUICanvasPrefabBuilder — 编辑器侧 UGUI HUD 预制体资产保障工具。
///
/// PlayableEnvironmentBuilder 会通过该工具优先实例化 Assets/Prefabs/UI/GlobalGameUICanvas.prefab；
/// 如果资产尚不存在，则即时创建标准 Canvas + GlobalGameUICanvas 预制体，避免 TestConsole 生成的
/// 可玩场景继续依赖旧 OnGUI 灰盒 HUD。
/// </summary>
public static class GlobalGameUICanvasPrefabBuilder
{
    public const string PrefabPath = "Assets/Prefabs/UI/GlobalGameUICanvas.prefab";

    [MenuItem("MarioTrickster/UI/Rebuild Global Game UI Canvas Prefab")]
    public static void RebuildPrefabFromMenu()
    {
        GameObject prefab = EnsurePrefabAsset(true);
        Selection.activeObject = prefab;
        Debug.Log($"[GlobalGameUICanvasPrefabBuilder] Rebuilt prefab: {PrefabPath}");
    }

    public static GameObject EnsurePrefabAsset(bool forceRebuild = false)
    {
        Directory.CreateDirectory("Assets/Prefabs/UI");

        if (!forceRebuild)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;
        }

        GameObject temp = new GameObject("GlobalGameUICanvas", typeof(RectTransform));
        temp.AddComponent<Canvas>();
        temp.AddComponent<CanvasScaler>();
        temp.AddComponent<GraphicRaycaster>();
        temp.AddComponent<GlobalGameUICanvas>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
        Object.DestroyImmediate(temp);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return prefab;
    }
}
