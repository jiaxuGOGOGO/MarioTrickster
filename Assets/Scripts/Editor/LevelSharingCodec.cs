using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

/// <summary>
/// LevelSharingCodec — GBG 风格的关卡分享码系统
///
/// 核心理念（来自 Game Builder Garage 的 Share Code）：
///   GBG 中每个创作都可以生成一个短码，其他人输入短码就能下载并游玩。
///   这让创作者之间的分享变得极其简单。
///
/// 在 MarioTrickster 中的映射：
///   将 ASCII 关卡模板编码为可分享的短字符串（Base64 + 压缩）。
///   创作者可以：
///   1. 导出当前关卡为分享码（复制到剪贴板）
///   2. 从分享码导入关卡（粘贴即加载）
///   3. 分享码包含完整的关卡数据 + 元数据
///
/// 格式：MT1-{压缩Base64}
///   MT1 = MarioTrickster v1 格式标识
/// </summary>
public static class LevelSharingCodec
{
    private const string FORMAT_PREFIX = "MT1-";
    private const int MAX_TEMPLATE_SIZE = 10000; // 最大模板字符数

    // ═══════════════════════════════════════════════════
    // 数据结构
    // ═══════════════════════════════════════════════════

    [System.Serializable]
    private class LevelPackage
    {
        public string name;
        public string author;
        public string date;
        public string template;
        public string difficulty;
        public int width;
        public int height;
    }

    // ═══════════════════════════════════════════════════
    // 编码
    // ═══════════════════════════════════════════════════

    /// <summary>将 ASCII 模板编码为分享码</summary>
    public static string Encode(string asciiTemplate, string levelName = "Untitled", string author = "Anonymous")
    {
        if (string.IsNullOrEmpty(asciiTemplate))
            return null;

        if (asciiTemplate.Length > MAX_TEMPLATE_SIZE)
        {
            Debug.LogWarning("[LevelSharing] 模板过大，无法编码。");
            return null;
        }

        // 构建数据包
        string[] lines = asciiTemplate.Split('\n');
        var package = new LevelPackage
        {
            name = levelName,
            author = author,
            date = DateTime.Now.ToString("yyyy-MM-dd"),
            template = asciiTemplate,
            difficulty = EstimateDifficulty(asciiTemplate),
            width = 0,
            height = lines.Length
        };

        foreach (string line in lines)
            if (line.Length > package.width) package.width = line.Length;

        // 序列化 → 压缩 → Base64
        string json = JsonUtility.ToJson(package);
        byte[] compressed = Compress(Encoding.UTF8.GetBytes(json));
        string base64 = Convert.ToBase64String(compressed);

        return FORMAT_PREFIX + base64;
    }

    /// <summary>从分享码解码为 ASCII 模板</summary>
    public static string Decode(string shareCode, out string levelName, out string author, out string difficulty)
    {
        levelName = "";
        author = "";
        difficulty = "";

        if (string.IsNullOrEmpty(shareCode))
            return null;

        shareCode = shareCode.Trim();

        if (!shareCode.StartsWith(FORMAT_PREFIX))
        {
            Debug.LogWarning("[LevelSharing] 无效的分享码格式。");
            return null;
        }

        try
        {
            string base64 = shareCode.Substring(FORMAT_PREFIX.Length);
            byte[] compressed = Convert.FromBase64String(base64);
            byte[] decompressed = Decompress(compressed);
            string json = Encoding.UTF8.GetString(decompressed);

            var package = JsonUtility.FromJson<LevelPackage>(json);
            levelName = package.name;
            author = package.author;
            difficulty = package.difficulty;

            return package.template;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LevelSharing] 解码失败: {e.Message}");
            return null;
        }
    }

    // ═══════════════════════════════════════════════════
    // 压缩/解压
    // ═══════════════════════════════════════════════════

    private static byte[] Compress(byte[] data)
    {
        using (var output = new MemoryStream())
        {
            using (var gzip = new GZipStream(output, CompressionMode.Compress))
            {
                gzip.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
    }

    private static byte[] Decompress(byte[] data)
    {
        using (var input = new MemoryStream(data))
        using (var gzip = new GZipStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }

    // ═══════════════════════════════════════════════════
    // 难度估算
    // ═══════════════════════════════════════════════════

    private static string EstimateDifficulty(string template)
    {
        int hazards = 0;
        int gaps = 0;
        foreach (char c in template)
        {
            if (c == '^' || c == '~' || c == '@' || c == 'P') hazards++;
        }

        // 简单的间隙计数
        string[] lines = template.Split('\n');
        foreach (string line in lines)
        {
            bool onPlatform = false;
            int currentGap = 0;
            foreach (char c in line)
            {
                if (c == '#' || c == '=' || c == 'W')
                {
                    if (onPlatform && currentGap >= 3) gaps++;
                    onPlatform = true;
                    currentGap = 0;
                }
                else if (onPlatform)
                {
                    currentGap++;
                }
            }
        }

        int score = hazards * 2 + gaps * 3;
        if (score <= 3) return "Easy";
        if (score <= 8) return "Medium";
        if (score <= 15) return "Hard";
        return "Expert";
    }

    // ═══════════════════════════════════════════════════
    // Editor 集成
    // ═══════════════════════════════════════════════════

    [MenuItem("MarioTrickster/Share/Export Level Code", false, 200)]
    public static void ExportLevelCode()
    {
        // 尝试从当前 Level Studio 获取模板
        var window = EditorWindow.GetWindow<TestConsoleWindow>("Level Studio", false);
        if (window == null) return;

        var field = typeof(TestConsoleWindow).GetField("customAsciiTemplate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return;

        string template = (string)field.GetValue(window);
        if (string.IsNullOrEmpty(template))
        {
            EditorUtility.DisplayDialog("导出失败", "Custom Template Editor 中没有内容。\n请先在编辑器中创建关卡模板。", "OK");
            return;
        }

        string levelName = EditorUtility.SaveFilePanel("关卡名称", "", "MyLevel", "");
        if (string.IsNullOrEmpty(levelName))
            levelName = "MyLevel";
        else
            levelName = Path.GetFileNameWithoutExtension(levelName);

        string code = Encode(template, levelName);
        if (code != null)
        {
            EditorGUIUtility.systemCopyBuffer = code;
            EditorUtility.DisplayDialog("导出成功",
                $"分享码已复制到剪贴板！\n\n长度: {code.Length} 字符\n关卡: {levelName}\n\n发送给朋友即可分享。",
                "OK");
            Debug.Log($"[LevelSharing] 导出成功: {code.Substring(0, Mathf.Min(50, code.Length))}...");
        }
    }

    [MenuItem("MarioTrickster/Share/Import Level Code", false, 201)]
    public static void ImportLevelCode()
    {
        string code = EditorGUIUtility.systemCopyBuffer;
        if (string.IsNullOrEmpty(code) || !code.StartsWith(FORMAT_PREFIX))
        {
            code = EditorUtility.DisplayDialogComplex("导入分享码",
                "请将分享码粘贴到剪贴板后重试，\n或手动输入分享码。",
                "从剪贴板导入", "取消", "手动输入") == 0 ? code : null;

            if (code == null || !code.StartsWith(FORMAT_PREFIX))
                return;
        }

        string levelName, author, difficulty;
        string template = Decode(code, out levelName, out author, out difficulty);

        if (template == null)
        {
            EditorUtility.DisplayDialog("导入失败", "无法解析分享码，请检查是否完整。", "OK");
            return;
        }

        bool import = EditorUtility.DisplayDialog("导入关卡",
            $"关卡: {levelName}\n作者: {author}\n难度: {difficulty}\n\n是否导入到 Custom Template Editor？",
            "导入", "取消");

        if (import)
        {
            var window = EditorWindow.GetWindow<TestConsoleWindow>("Level Studio", true);
            var field = typeof(TestConsoleWindow).GetField("customAsciiTemplate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(window, template);
                window.Repaint();
                Debug.Log($"[LevelSharing] 已导入关卡: {levelName} by {author}");
            }
        }
    }
}
