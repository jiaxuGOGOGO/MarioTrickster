#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

/// <summary>
/// Scene-to-ASCII 反向烘焙器 (Session 6: 双向同步工作流)
///
/// 将 Scene 视图中策划摆放的关卡逆向输出为带有 Overrides 标签的 ASCII 文本，
/// 复制到剪贴板后可直接粘贴回模板文件，实现"所见即所得"的双向编辑闭环。
///
/// 核心逻辑:
///   1. 查找 AsciiLevel_Root 根节点，遍历所有子节点
///   2. 通过 AsciiElementRegistry 建立反向映射（elementName/Component → char）
///   3. 计算网格坐标，处理合并地形的多格填充
///   4. 提取 MovingPlatform/ControllablePlatform 的 pointB 生成 Override 标签
///   5. 输出完整 ASCII 文本 + Overrides 到剪贴板
/// </summary>
public static class AsciiLevelBaker
{
    [MenuItem("MarioTrickster/Level Builder/Bake Scene to ASCII (Clipboard)")]
    public static void BakeSceneToAscii()
    {
        // ── 查找根节点 ──
        GameObject root = GameObject.Find("AsciiLevel_Root");
        if (root == null)
        {
            Debug.LogError("[AsciiLevelBaker] No 'AsciiLevel_Root' found in scene. Generate a level first.");
            return;
        }

        Transform rootTransform = root.transform;
        if (rootTransform.childCount == 0)
        {
            Debug.LogWarning("[AsciiLevelBaker] AsciiLevel_Root has no children.");
            return;
        }

        // ── 建立反向映射字典：elementName -> char, componentTypeName -> char ──
        AsciiElementRegistry registry = AsciiElementRegistry.GetDefault();
        Dictionary<string, char> nameToChar = new Dictionary<string, char>();
        Dictionary<string, char> componentToChar = new Dictionary<string, char>();

        if (registry.entries != null)
        {
            foreach (var entry in registry.entries)
            {
                if (entry == null) continue;
                char c = entry.AsciiChar;
                if (c == '\0') continue;

                // elementName 映射
                if (!string.IsNullOrEmpty(entry.elementName) && !nameToChar.ContainsKey(entry.elementName))
                {
                    nameToChar[entry.elementName] = c;
                }

                // componentTypeNames 映射
                if (entry.componentTypeNames != null)
                {
                    foreach (string typeName in entry.componentTypeNames)
                    {
                        if (!string.IsNullOrEmpty(typeName) && !componentToChar.ContainsKey(typeName))
                        {
                            componentToChar[typeName] = c;
                        }
                    }
                }
            }
        }

        // ── 反射准备：读取 pointB ──
        FieldInfo movingPointBField = typeof(MovingPlatform).GetField("pointB",
            BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo controllablePointBField = typeof(ControllablePlatform).GetField("pointB",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // ── 第一遍遍历：确定网格范围 ──
        int maxX = 0;
        int maxY = 0;

        foreach (Transform child in rootTransform)
        {
            Vector3 pos = child.position;
            int gx = Mathf.RoundToInt(pos.x);
            int gy = Mathf.RoundToInt(pos.y);

            // 处理合并地形的宽度
            int width = GetBlockWidth(child);
            int endX = gx + width - 1;

            if (endX > maxX) maxX = endX;
            if (gy > maxY) maxY = gy;
        }

        // ── 创建二维数组，默认填 '.' ──
        char[,] grid = new char[maxX + 1, maxY + 1];
        for (int x = 0; x <= maxX; x++)
            for (int y = 0; y <= maxY; y++)
                grid[x, y] = '.';

        // 优先级标记：非地形字符优先（M、T、G、机关等）
        bool[,] isPriority = new bool[maxX + 1, maxY + 1];

        // ── Override 收集 ──
        List<string> overrideLines = new List<string>();

        // ── 第二遍遍历：填充网格 + 提取 Overrides ──
        foreach (Transform child in rootTransform)
        {
            Vector3 pos = child.position;
            int gx = Mathf.RoundToInt(pos.x);
            int gy = Mathf.RoundToInt(pos.y);

            // 确定该物体对应的 ASCII 字符
            char asciiChar = ResolveCharForObject(child, nameToChar, componentToChar);
            if (asciiChar == '\0') continue;

            // 判断是否为地形字符
            bool isTerrain = (asciiChar == '#' || asciiChar == '=' || asciiChar == '-' ||
                              asciiChar == 'W' || asciiChar == 'F');

            // 获取合并宽度
            int blockWidth = GetBlockWidth(child);

            // 填充网格
            for (int dx = 0; dx < blockWidth; dx++)
            {
                int fillX = gx + dx;
                if (fillX < 0 || fillX > maxX || gy < 0 || gy > maxY) continue;

                // 优先级判断：非地形优先保留
                if (!isTerrain)
                {
                    grid[fillX, gy] = asciiChar;
                    isPriority[fillX, gy] = true;
                }
                else if (!isPriority[fillX, gy])
                {
                    grid[fillX, gy] = asciiChar;
                }
            }

            // ── 提取 Override 参数 ──
            MovingPlatform mp = child.GetComponent<MovingPlatform>();
            if (mp != null && movingPointBField != null)
            {
                Vector3 pointB = (Vector3)movingPointBField.GetValue(mp);
                overrideLines.Add($"# Override_{gx}_{gy}: pointB={pointB.x:F2},{pointB.y:F2}");
            }

            ControllablePlatform cp = child.GetComponent<ControllablePlatform>();
            if (cp != null && controllablePointBField != null)
            {
                Vector3 pointB = (Vector3)controllablePointBField.GetValue(cp);
                overrideLines.Add($"# Override_{gx}_{gy}: pointB={pointB.x:F2},{pointB.y:F2}");
            }
        }

        // ── 输出 ASCII 文本（从 y=maxY 到 y=0，即从上到下）──
        StringBuilder sb = new StringBuilder();
        for (int y = maxY; y >= 0; y--)
        {
            for (int x = 0; x <= maxX; x++)
            {
                sb.Append(grid[x, y]);
            }
            // 去除行尾多余的 '.'
            string line = sb.ToString().TrimEnd('.');
            sb.Clear();
            sb.Append(line);
            // 写入最终输出
            if (y > 0)
                sb.Append('\n');
            // 重用 sb 做最终拼接不方便，改用独立 finalSb
        }

        // 重新构建最终输出
        StringBuilder finalSb = new StringBuilder();
        for (int y = maxY; y >= 0; y--)
        {
            StringBuilder lineSb = new StringBuilder();
            for (int x = 0; x <= maxX; x++)
            {
                lineSb.Append(grid[x, y]);
            }
            // 去除行尾多余的 '.'
            string line = lineSb.ToString().TrimEnd('.');
            finalSb.AppendLine(line);
        }

        // 附加 Override 标签
        if (overrideLines.Count > 0)
        {
            finalSb.AppendLine();
            foreach (string ol in overrideLines)
            {
                finalSb.AppendLine(ol);
            }
        }

        string result = finalSb.ToString().TrimEnd('\n', '\r');

        // ── 复制到剪贴板 ──
        EditorGUIUtility.systemCopyBuffer = result;

        Debug.Log($"[AsciiLevelBaker] Scene baked to ASCII ({maxX + 1}x{maxY + 1} grid, " +
            $"{overrideLines.Count} override(s)). Copied to clipboard!\n\n{result}");
    }

    // ═══════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 确定物体对应的 ASCII 字符。
    /// 优先通过 GameObject 名称中的 elementName 匹配，
    /// 其次通过挂载的组件类型匹配。
    /// </summary>
    private static char ResolveCharForObject(Transform obj,
        Dictionary<string, char> nameToChar, Dictionary<string, char> componentToChar)
    {
        string objName = obj.gameObject.name;

        // 尝试通过名称匹配（Generator 生成的物体名称通常包含 elementName）
        foreach (var kvp in nameToChar)
        {
            if (objName.Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }

        // 尝试通过组件匹配
        Component[] components = obj.GetComponents<Component>();
        foreach (var comp in components)
        {
            if (comp == null) continue;
            string typeName = comp.GetType().Name;
            if (componentToChar.TryGetValue(typeName, out char c))
            {
                return c;
            }
        }

        // 特殊处理：名称中包含常见关键字
        if (objName.Contains("Ground")) return '#';
        if (objName.Contains("Platform") && objName.Contains("OneWay")) return '-';
        if (objName.Contains("Platform")) return '=';

        return '.';
    }

    /// <summary>
    /// 获取合并地形块的宽度（格数）。
    /// 通过 BoxCollider2D 的 size.x 或名称中的 _wN 后缀判断。
    /// </summary>
    private static int GetBlockWidth(Transform obj)
    {
        string objName = obj.gameObject.name;

        // 检查名称中的 _wN 后缀（如 Ground_w5 表示宽度 5 格）
        int wIndex = objName.LastIndexOf("_w");
        if (wIndex >= 0 && wIndex + 2 < objName.Length)
        {
            string widthStr = "";
            for (int i = wIndex + 2; i < objName.Length; i++)
            {
                if (char.IsDigit(objName[i]))
                    widthStr += objName[i];
                else
                    break;
            }
            if (widthStr.Length > 0 && int.TryParse(widthStr, out int w) && w > 1)
            {
                return w;
            }
        }

        // 通过 BoxCollider2D 的 size.x 判断
        BoxCollider2D col = obj.GetComponent<BoxCollider2D>();
        if (col != null && col.size.x > 1.5f)
        {
            return Mathf.RoundToInt(col.size.x);
        }

        return 1;
    }
}
#endif
