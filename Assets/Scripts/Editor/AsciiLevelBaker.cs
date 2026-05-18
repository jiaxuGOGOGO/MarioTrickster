#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene-to-ASCII 烘焙器：把当前 AsciiLevel_Root 反向导出为 ASCII 模板。
///
/// S6 双向同步工作流要求：
///   1. Scene 里 Ctrl+D/拖拽出来的对象能反向回写 ASCII；
///   2. MovingPlatform/ControllablePlatform 的 pointB 以 Override 标签保留；
///   3. 合并地形按宽度展开，机关/出生点/终点在冲突时优先覆盖地形；
///   4. 输出到系统剪贴板，便于粘回 Level Template。
/// </summary>
public static class AsciiLevelBaker
{
    private const string GENERATED_ROOT_NAME = "AsciiLevel_Root";
    private const char EMPTY_CHAR = '.';

    // Generator 只会合并这些地形字符；机关、敌人、宽平台不能因 collider.size.x > 1.5 被误展开。
    private static readonly HashSet<char> MergeableTerrainChars = new HashSet<char>
    {
        '#', '=', '-'
    };

    [MenuItem("MarioTrickster/Level Builder/Bake Scene to ASCII (Clipboard)")]
    public static void BakeSceneToAscii()
    {
        GameObject root = GameObject.Find(GENERATED_ROOT_NAME);
        if (root == null)
        {
            Debug.LogError($"[AsciiLevelBaker] No '{GENERATED_ROOT_NAME}' found in scene. Generate a level first.");
            return;
        }

        if (root.transform.childCount == 0)
        {
            Debug.LogWarning($"[AsciiLevelBaker] {GENERATED_ROOT_NAME} has no children.");
            return;
        }

        AsciiElementRegistry registry = AsciiElementRegistry.GetDefault();
        if (registry == null)
        {
            Debug.LogError("[AsciiLevelBaker] AsciiElementRegistry.GetDefault() returned null; cannot build reverse map.");
            return;
        }

        registry.BuildCache();
        BuildReverseMaps(registry, out Dictionary<string, char> elementNameToChar, out Dictionary<string, char> componentTypeToChar);

        List<BakedObject> bakedObjects = new List<BakedObject>();
        int maxX = 0;
        int maxY = 0;

        foreach (Transform child in root.transform)
        {
            Vector2Int gridPosition = new Vector2Int(
                Mathf.RoundToInt(child.position.x),
                Mathf.RoundToInt(child.position.y));

            if (gridPosition.x < 0 || gridPosition.y < 0)
            {
                Debug.LogWarning($"[AsciiLevelBaker] Skipping '{child.name}' at negative grid position {gridPosition}.");
                continue;
            }

            char asciiChar = ResolveCharForObject(child.gameObject, elementNameToChar, componentTypeToChar);
            if (asciiChar == '\0' || asciiChar == EMPTY_CHAR)
            {
                continue;
            }

            bool mergeableTerrain = IsMergeableTerrainChar(asciiChar);
            int width = mergeableTerrain ? Mathf.Max(1, GetMergedTerrainWidth(child)) : 1;
            int startX = mergeableTerrain ? GetMergedTerrainStartX(child, gridPosition.x, width) : gridPosition.x;

            if (startX < 0)
            {
                Debug.LogWarning($"[AsciiLevelBaker] Skipping '{child.name}' because merged start X is negative: {startX}.");
                continue;
            }

            bakedObjects.Add(new BakedObject
            {
                transform = child,
                asciiChar = asciiChar,
                gridPosition = gridPosition,
                startX = startX,
                y = gridPosition.y,
                width = width
            });

            maxX = Mathf.Max(maxX, startX + width - 1);
            maxY = Mathf.Max(maxY, gridPosition.y);
        }

        if (bakedObjects.Count == 0)
        {
            Debug.LogWarning($"[AsciiLevelBaker] No recognizable ASCII objects found under {GENERATED_ROOT_NAME}.");
            return;
        }

        char[,] grid = new char[maxX + 1, maxY + 1];
        bool[,] priority = new bool[maxX + 1, maxY + 1];
        for (int y = 0; y <= maxY; y++)
        {
            for (int x = 0; x <= maxX; x++)
            {
                grid[x, y] = EMPTY_CHAR;
                priority[x, y] = false;
            }
        }

        List<string> overrides = new List<string>();
        foreach (BakedObject baked in bakedObjects)
        {
            bool isPriorityObject = IsPriorityChar(baked.asciiChar);
            for (int dx = 0; dx < baked.width; dx++)
            {
                int x = baked.startX + dx;
                int y = baked.y;
                if (x < 0 || x > maxX || y < 0 || y > maxY)
                {
                    continue;
                }

                // 冲突规则：出生点、终点、机关、敌人、道具等非地形元素永远压过地形。
                // 非地形之间的同格冲突按层级遍历顺序以后写入者覆盖，便于设计师手工调整 Hierarchy。
                if (isPriorityObject || !priority[x, y])
                {
                    grid[x, y] = baked.asciiChar;
                    priority[x, y] = isPriorityObject;
                }
            }

            TryAppendPlatformOverride(baked.transform, baked.gridPosition, overrides);
        }

        StringBuilder sb = new StringBuilder();
        for (int y = maxY; y >= 0; y--)
        {
            char[] row = new char[maxX + 1];
            for (int x = 0; x <= maxX; x++)
            {
                row[x] = grid[x, y];
            }
            sb.AppendLine(new string(row));
        }

        if (overrides.Count > 0)
        {
            sb.AppendLine();
            foreach (string overrideLine in overrides)
            {
                sb.AppendLine(overrideLine);
            }
        }

        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log($"[AsciiLevelBaker] Baked {bakedObjects.Count} object(s) to ASCII ({maxX + 1}x{maxY + 1}). Result copied to clipboard.\n{EditorGUIUtility.systemCopyBuffer}");
        Debug.Log("[AI Arena] 第六阶段：所见即所得双向工作流部署完成");
    }

    private static void BuildReverseMaps(
        AsciiElementRegistry registry,
        out Dictionary<string, char> elementNameToChar,
        out Dictionary<string, char> componentTypeToChar)
    {
        elementNameToChar = new Dictionary<string, char>(StringComparer.Ordinal);
        componentTypeToChar = new Dictionary<string, char>(StringComparer.Ordinal);

        if (registry.entries == null)
        {
            return;
        }

        foreach (AsciiElementEntry entry in registry.entries)
        {
            if (entry == null)
            {
                continue;
            }

            char asciiChar = entry.AsciiChar;
            if (asciiChar == '\0')
            {
                continue;
            }

            if (!string.IsNullOrEmpty(entry.elementName))
            {
                elementNameToChar[entry.elementName] = asciiChar;
            }

            if (entry.componentTypeNames == null)
            {
                continue;
            }

            foreach (string componentTypeName in entry.componentTypeNames)
            {
                if (string.IsNullOrWhiteSpace(componentTypeName))
                {
                    continue;
                }

                componentTypeToChar[componentTypeName] = asciiChar;

                int lastDot = componentTypeName.LastIndexOf('.');
                if (lastDot >= 0 && lastDot < componentTypeName.Length - 1)
                {
                    componentTypeToChar[componentTypeName.Substring(lastDot + 1)] = asciiChar;
                }
            }
        }

        // 内置默认 Registry 没有 ControllablePlatform 字符。若项目/自定义 Registry 未显式配置，
        // 至少把它烘焙为 MovingPlatform 字符以保留 pointB 轨迹，不让 Scene 中的可控平台丢失。
        if (!componentTypeToChar.ContainsKey(nameof(ControllablePlatform)) &&
            componentTypeToChar.TryGetValue(nameof(MovingPlatform), out char movingPlatformChar))
        {
            componentTypeToChar[nameof(ControllablePlatform)] = movingPlatformChar;
        }
    }

    private static char ResolveCharForObject(
        GameObject obj,
        Dictionary<string, char> elementNameToChar,
        Dictionary<string, char> componentTypeToChar)
    {
        // 先按组件类型反查，避免 MovingPlatform 名称中包含 Platform 而被误判为 '='。
        Component[] components = obj.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component == null)
            {
                continue;
            }

            Type componentType = component.GetType();
            if (IsGenericUnityComponent(componentType))
            {
                continue;
            }

            if (componentTypeToChar.TryGetValue(componentType.Name, out char charByShortType))
            {
                return charByShortType;
            }

            if (!string.IsNullOrEmpty(componentType.FullName) &&
                componentTypeToChar.TryGetValue(componentType.FullName, out char charByFullType))
            {
                return charByFullType;
            }
        }

        // 再按 ElementName 反查，只接受精确名或 Generator 命名模式 ElementName_x_y / ElementName_x_y_wN。
        foreach (KeyValuePair<string, char> pair in elementNameToChar)
        {
            if (NameMatchesElement(obj.name, pair.Key))
            {
                return pair.Value;
            }
        }

        return '\0';
    }

    private static bool IsGenericUnityComponent(Type componentType)
    {
        return componentType == typeof(Transform) ||
               componentType == typeof(Rigidbody2D) ||
               componentType == typeof(BoxCollider2D) ||
               componentType == typeof(CircleCollider2D) ||
               componentType == typeof(PolygonCollider2D) ||
               componentType == typeof(EdgeCollider2D) ||
               componentType == typeof(CapsuleCollider2D) ||
               componentType == typeof(SpriteRenderer) ||
               componentType == typeof(Animator) ||
               componentType == typeof(AudioSource);
    }

    private static bool NameMatchesElement(string objectName, string elementName)
    {
        if (string.IsNullOrEmpty(objectName) || string.IsNullOrEmpty(elementName))
        {
            return false;
        }

        if (string.Equals(objectName, elementName, StringComparison.Ordinal))
        {
            return true;
        }

        if (objectName.StartsWith(elementName + "_", StringComparison.Ordinal))
        {
            return true;
        }

        // Unity Ctrl+D 会追加 " (1)"，仍允许精确元素名的副本被识别。
        return objectName.StartsWith(elementName + " (", StringComparison.Ordinal);
    }

    private static bool IsMergeableTerrainChar(char asciiChar)
    {
        return MergeableTerrainChars.Contains(asciiChar);
    }

    private static bool IsPriorityChar(char asciiChar)
    {
        return !IsMergeableTerrainChar(asciiChar);
    }

    private static int GetMergedTerrainWidth(Transform obj)
    {
        if (TryParseWidthFromName(obj.name, out int widthFromName))
        {
            return Mathf.Max(1, widthFromName);
        }

        BoxCollider2D collider = obj.GetComponent<BoxCollider2D>();
        if (collider != null && collider.size.x > 1.5f)
        {
            return Mathf.Max(1, Mathf.RoundToInt(collider.size.x));
        }

        return 1;
    }

    private static int GetMergedTerrainStartX(Transform obj, int roundedPositionX, int width)
    {
        if (width <= 1)
        {
            return roundedPositionX;
        }

        // Generator 合并地形的 Transform 位于长条中心，而不是起始格。
        // 用中心点反推起点，既能无损回烘焙生成物，也能支持 Ctrl+D 后拖动宽地形对象。
        return Mathf.RoundToInt(obj.position.x - (width - 1) * 0.5f);
    }

    private static bool TryParseWidthFromName(string objectName, out int width)
    {
        width = 1;
        int marker = objectName.LastIndexOf("_w", StringComparison.Ordinal);
        if (marker < 0)
        {
            return false;
        }

        int start = marker + 2;
        int end = start;
        while (end < objectName.Length && char.IsDigit(objectName[end]))
        {
            end++;
        }

        return end > start && int.TryParse(objectName.Substring(start, end - start), out width);
    }

    private static void TryAppendPlatformOverride(Transform child, Vector2Int gridPosition, List<string> overrides)
    {
        MovingPlatform movingPlatform = child.GetComponent<MovingPlatform>();
        if (movingPlatform != null && TryReadPointB(movingPlatform, out Vector3 movingPointB))
        {
            overrides.Add(string.Format(
                CultureInfo.InvariantCulture,
                "# Override_{0}_{1}: pointB={2:F2},{3:F2}",
                gridPosition.x,
                gridPosition.y,
                movingPointB.x,
                movingPointB.y));
            return;
        }

        ControllablePlatform controllablePlatform = child.GetComponent<ControllablePlatform>();
        if (controllablePlatform != null && TryReadPointB(controllablePlatform, out Vector3 controllablePointB))
        {
            overrides.Add(string.Format(
                CultureInfo.InvariantCulture,
                "# Override_{0}_{1}: pointB={2:F2},{3:F2}",
                gridPosition.x,
                gridPosition.y,
                controllablePointB.x,
                controllablePointB.y));
        }
    }

    private static bool TryReadPointB(Component component, out Vector3 pointB)
    {
        pointB = Vector3.zero;
        FieldInfo pointBField = component.GetType().GetField("pointB", BindingFlags.NonPublic | BindingFlags.Instance);
        if (pointBField == null || pointBField.FieldType != typeof(Vector3))
        {
            Debug.LogWarning($"[AsciiLevelBaker] Component '{component.GetType().Name}' has no private Vector3 pointB field.");
            return false;
        }

        pointB = (Vector3)pointBField.GetValue(component);
        return true;
    }

    private struct BakedObject
    {
        public Transform transform;
        public char asciiChar;
        public Vector2Int gridPosition;
        public int startX;
        public int y;
        public int width;
    }
}
#endif
