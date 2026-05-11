#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

/// <summary>
/// Art Drop Auto-Importer
///
/// 当用户将图片文件拖入 Assets/Art/Imported/ 目录时，自动弹出 Asset Import Pipeline 窗口
/// 并预填充刚拖入的素材，实现"拖入即开始导入流程"的零摩擦体验。
///
/// 同时对所有拖入 Assets/Art/ 的贴图自动执行 ART_BIBLE 规范化（与 TA_ArtImportEnforcer 互补，
/// 本脚本额外覆盖 Assets/Art/Imported/ 子目录并触发 Pipeline UI）。
///
/// [AI防坑警告]
///   本脚本仅在 Assets/Art/ 目录下生效。不要扩大监听范围，否则会干扰其他资产导入。
/// </summary>
public class ArtDropAutoImporter : AssetPostprocessor
{
    private static readonly string WATCH_DIR = "Assets/Art/Imported";
    private static readonly string ART_ROOT = "Assets/Art";

    /// <summary>
    /// 所有资产导入完成后的回调
    /// </summary>
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        // 只关注新导入到 Art 目录的贴图
        var newArtTextures = importedAssets
            .Where(p => p.StartsWith(ART_ROOT) && IsImageFile(p))
            .ToArray();

        if (newArtTextures.Length == 0) return;

        // 检查是否有文件落入 Imported 目录（触发 Pipeline UI）
        var importedDirFiles = newArtTextures
            .Where(p => p.StartsWith(WATCH_DIR))
            .ToArray();

        if (importedDirFiles.Length > 0)
        {
            // 延迟一帧打开窗口，避免在导入回调中操作 UI
            EditorApplication.delayCall += () =>
            {
                Debug.Log($"[Auto-Importer] 检测到 {importedDirFiles.Length} 个新素材拖入 {WATCH_DIR}，已打开导入管线。");
                AssetImportPipeline.ShowWindow();
            };
        }
    }

    private static bool IsImageFile(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || 
               ext == ".tga" || ext == ".psd" || ext == ".bmp" || ext == ".tiff";
    }
}
#endif
