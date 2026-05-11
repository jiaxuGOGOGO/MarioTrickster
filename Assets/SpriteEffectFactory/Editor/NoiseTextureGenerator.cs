using UnityEngine;
using UnityEditor;

/// <summary>
/// 溶解噪声贴图生成器 — 一键生成 Perlin 噪声贴图供溶解效果使用
/// </summary>
public class NoiseTextureGenerator : EditorWindow
{
    [MenuItem("Window/MarioTrickster/生成溶解噪声贴图")]
    public static void ShowWindow()
    {
        GetWindow<NoiseTextureGenerator>("噪声贴图生成");
    }

    private int _size = 256;
    private float _scale = 20f;
    private Texture2D _preview;

    private void OnGUI()
    {
        EditorGUILayout.LabelField("溶解噪声贴图生成器", EditorStyles.boldLabel);
        _size = EditorGUILayout.IntSlider("尺寸", _size, 64, 512);
        _scale = EditorGUILayout.Slider("噪声缩放", _scale, 1f, 100f);

        if (GUILayout.Button("预览"))
        {
            _preview = Generate(_size, _scale);
        }

        if (_preview != null)
        {
            var rect = GUILayoutUtility.GetRect(200, 200);
            EditorGUI.DrawPreviewTexture(rect, _preview);
        }

        if (GUILayout.Button("保存为资产"))
        {
            if (_preview == null) _preview = Generate(_size, _scale);
            string path = EditorUtility.SaveFilePanelInProject(
                "保存噪声贴图", "SEF_DissolveNoise", "png", "选择保存位置",
                "Assets/SpriteEffectFactory/Resources");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllBytes(path, _preview.EncodeToPNG());
                AssetDatabase.Refresh();
                Debug.Log("[SEF] 噪声贴图已保存到 " + path);
            }
        }
    }

    private static Texture2D Generate(int size, float scale)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat
        };
        float offsetX = Random.Range(0f, 1000f);
        float offsetY = Random.Range(0f, 1000f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float val = Mathf.PerlinNoise(
                    (float)x / size * scale + offsetX,
                    (float)y / size * scale + offsetY);
                tex.SetPixel(x, y, new Color(val, val, val, 1f));
            }
        }
        tex.Apply();
        return tex;
    }
}
