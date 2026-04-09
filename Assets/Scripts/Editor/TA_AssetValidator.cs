#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// TA 资产校验双重防御塔 (TA_AssetValidator)
///
/// 核心职责：
///   防御塔 1 — 事前拦截 (AssetPostprocessor.OnPreprocessTexture)
///     当任何新图片被拖入 Assets/Art/ 目录时，强行自动将其：
///     PPU → 32, AlphaIsTransparency → true, FilterMode → Point, Compression → Uncompressed
///     不给人类犯错的机会！
///
///   防御塔 2 — 主动扫描 (MenuItem "一键合规巡检")
///     校验全工程 Assets/Art/ 下所有 Sprite 的 PPU/FilterMode，
///     并重点校验切片 Pivot（如 Characters/ 目录下的重心 Y 必须绝对等于 0）。
///     如有违规，报红错截停。
///
/// [AI防坑警告]
///   本脚本是美术管线的安全网。严禁修改 PPU 和 FilterMode 的强制值。
///   如需调整校验规则，请先更新 ART_BIBLE.md 并经过 Code Review。
/// </summary>

// ═══════════════════════════════════════════════════
// 防御塔 1：事前拦截 — AssetPostprocessor
// ═══════════════════════════════════════════════════
public class TA_ArtImportEnforcer : AssetPostprocessor
{
    /// <summary>
    /// 当任何贴图被导入/重新导入时自动触发。
    /// 仅对 Assets/Art/ 目录下的贴图生效，强制执行 ART_BIBLE 规范。
    /// </summary>
    private void OnPreprocessTexture()
    {
        // 仅拦截 Assets/Art/ 目录下的资产
        if (!assetPath.StartsWith("Assets/Art/")) return;

        TextureImporter ti = assetImporter as TextureImporter;
        if (ti == null) return;

        // ── 强制执行 ART_BIBLE 规范 ──

        // 1. 贴图类型 → Sprite (2D and UI)
        ti.textureType = TextureImporterType.Sprite;

        // 2. PPU → 32 (PhysicsMetrics.STANDARD_PPU_32)
        ti.spritePixelsPerUnit = 32;

        // 3. 透明通道 → 开启
        ti.alphaIsTransparency = true;

        // 4. 滤镜 → Point (像素游戏防模糊铁律)
        ti.filterMode = FilterMode.Point;

        // 5. 压缩 → 无压缩 (保证像素清晰)
        ti.textureCompression = TextureImporterCompression.Uncompressed;

        // 6. Mesh Type → Full Rect (Tiled 模式必需)
        TextureImporterSettings settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(settings);

        Debug.Log($"<color=cyan>[TA 防御塔 1]</color> 自动强制规范: {assetPath} → PPU=32, Point, Uncompressed, AlphaTransparency=ON");
    }
}

// ═══════════════════════════════════════════════════
// 防御塔 2：主动扫描 — MenuItem 一键合规巡检
// ═══════════════════════════════════════════════════
public static class TA_AssetValidator
{
    private const int REQUIRED_PPU = 32;
    private const FilterMode REQUIRED_FILTER = FilterMode.Point;

    // 需要校验 Pivot.y == 0 (Bottom Center) 的目录
    private static readonly string[] BOTTOM_CENTER_DIRS = new string[]
    {
        "Assets/Art/Characters",
        "Assets/Art/Enemies"
    };

    // 需要校验 Pivot.y == 0.5 (Center) 的目录
    private static readonly string[] CENTER_PIVOT_DIRS = new string[]
    {
        "Assets/Art/Environment",
        "Assets/Art/Hazards"
    };

    [MenuItem("MarioTrickster/Art Pipeline/一键合规巡检 (校验全工程 PPU-Filter-Pivot)")]
    public static void RunFullAudit()
    {
        Debug.Log("<color=yellow>══════════════════════════════════════</color>");
        Debug.Log("<color=yellow>[TA 防御塔 2] 开始全工程合规巡检...</color>");
        Debug.Log("<color=yellow>══════════════════════════════════════</color>");

        int totalChecked = 0;
        int totalViolations = 0;
        List<string> violations = new List<string>();

        // 扫描 Assets/Art/ 下所有贴图
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art" });

        if (guids.Length == 0)
        {
            Debug.Log("<color=green>[TA 防御塔 2] Assets/Art/ 目录下暂无贴图资产，巡检跳过。</color>");
            return;
        }

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;

            totalChecked++;

            // ── 校验 1: PPU ──
            if (ti.spritePixelsPerUnit != REQUIRED_PPU)
            {
                string msg = $"[PPU 违规] {path}: PPU={ti.spritePixelsPerUnit}, 要求={REQUIRED_PPU}";
                violations.Add(msg);
                totalViolations++;
            }

            // ── 校验 2: FilterMode ──
            if (ti.filterMode != REQUIRED_FILTER)
            {
                string msg = $"[Filter 违规] {path}: Filter={ti.filterMode}, 要求={REQUIRED_FILTER}";
                violations.Add(msg);
                totalViolations++;
            }

            // ── 校验 3: Pivot (仅对切片后的 Multiple 模式 Sprite) ──
            if (ti.spriteImportMode == SpriteImportMode.Multiple && ti.spritesheet != null)
            {
                foreach (SpriteMetaData smd in ti.spritesheet)
                {
                    // 角色/敌人目录: Pivot.y 必须 == 0 (Bottom Center)
                    if (BOTTOM_CENTER_DIRS.Any(dir => path.StartsWith(dir)))
                    {
                        if (!Mathf.Approximately(smd.pivot.y, 0f))
                        {
                            string msg = $"[Pivot 违规] {path} → 帧 '{smd.name}': Pivot.y={smd.pivot.y}, 要求=0 (Bottom Center)";
                            violations.Add(msg);
                            totalViolations++;
                        }
                    }
                    // 地形/陷阱目录: Pivot.y 必须 == 0.5 (Center)
                    else if (CENTER_PIVOT_DIRS.Any(dir => path.StartsWith(dir)))
                    {
                        if (!Mathf.Approximately(smd.pivot.y, 0.5f))
                        {
                            string msg = $"[Pivot 违规] {path} → 帧 '{smd.name}': Pivot.y={smd.pivot.y}, 要求=0.5 (Center)";
                            violations.Add(msg);
                            totalViolations++;
                        }
                    }
                }
            }
        }

        // ── 输出报告 ──
        Debug.Log("<color=yellow>══════════════════════════════════════</color>");

        if (totalViolations == 0)
        {
            Debug.Log($"<color=green>[TA 防御塔 2] ✅ 巡检通过！共检查 {totalChecked} 个资产，0 个违规。基建安全！</color>");
        }
        else
        {
            foreach (string v in violations)
            {
                Debug.LogError($"[TA 防御塔 2] {v}");
            }
            Debug.LogError($"[TA 防御塔 2] ❌ 巡检失败！共检查 {totalChecked} 个资产，发现 {totalViolations} 个违规。请立即修复！");
        }

        Debug.Log("<color=yellow>══════════════════════════════════════</color>");
    }
}
#endif
