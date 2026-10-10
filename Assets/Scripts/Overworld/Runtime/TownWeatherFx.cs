using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S245：小镇天气粒子（挂在镜头上）。下雨 = 雨丝、暴雨 = 更密更斜、酸雨 = 绿雨、大风 = 落叶打转、大雾 = 慢慢飘的雾团。
/// 只在镜头看得见的范围里生成，飘出屏幕就从另一边绕回来（不会越积越多）。密度 = 调参 artWeatherDensity（0 = 关）。
/// 纯画面：没有碰撞体，不影响他的眼睛 / 耳朵（H3/H4）。图 = WorldArt.WeatherOf → Step1ArtSkin.Get（素材包里换了图就用新的）。
/// </summary>
public sealed class TownWeatherFx : MonoBehaviour
{
    private readonly List<SpriteRenderer> drops = new List<SpriteRenderer>();
    private readonly List<float> spin = new List<float>();
    private OverworldEvents.Kind kind = OverworldEvents.Kind.Clear;
    private float density = -1f;
    private WorldArt.Weather w;
    private Camera cam;
    private Transform root;

    public void Set(OverworldEvents.Kind k, float dens)
    {
        if (k == kind && Mathf.Approximately(dens, density)) return;
        kind = k; density = dens; w = WorldArt.WeatherOf(k);
        Rebuild();
    }

    private void Rebuild()
    {
        if (cam == null) cam = GetComponent<Camera>() ?? Camera.main;
        if (root == null) root = new GameObject("S245_TownWeather").transform;
        int want = w.key == null || cam == null ? 0 : Mathf.RoundToInt(w.perScreen * Mathf.Clamp(density, 0f, 2f));
        var sp = w.key != null ? Step1ArtSkin.Get(w.key, false) : null;
        if (sp == null) want = 0;
        while (drops.Count < want)
        {
            var go = new GameObject("drop"); go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = 3900; // 在高草（3000）上面、名字（4000）下面
            drops.Add(sr); spin.Add(Random.Range(-240f, 240f));
            Scatter(sr.transform, true);
        }
        for (int i = 0; i < drops.Count; i++)
        {
            bool on = i < want; drops[i].enabled = on; if (!on) continue;
            drops[i].sprite = sp; drops[i].color = new Color(1f, 1f, 1f, w.alpha);
            float s = kind == OverworldEvents.Kind.Fog ? Random.Range(2.5f, 4f) : kind == OverworldEvents.Kind.Wind ? 0.7f : 0.8f;
            drops[i].transform.localScale = new Vector3(s, s, 1f);
            drops[i].transform.rotation = Quaternion.identity;
        }
    }

    private void Scatter(Transform t, bool anywhere)
    {
        if (cam == null) return;
        float hh = cam.orthographicSize, hw = hh * cam.aspect; var c = cam.transform.position;
        t.position = new Vector3(c.x + Random.Range(-hw - 1f, hw + 1f), c.y + Random.Range(-hh - 1f, hh + 1f), 0f);
    }

    private void LateUpdate()
    {
        if (cam == null || w.key == null) return;
        float hh = cam.orthographicSize + 1.5f, hw = hh * cam.aspect + 1.5f; var c = cam.transform.position; float dt = Time.deltaTime;
        for (int i = 0; i < drops.Count; i++)
        {
            var sr = drops[i]; if (!sr.enabled) continue;
            var t = sr.transform; var p = t.position;
            p.x += w.vx * dt; p.y += w.vy * dt;
            // 绕回：出了屏幕从对面进来（镜头动了也跟着）
            if (p.x < c.x - hw) p.x += hw * 2f; else if (p.x > c.x + hw) p.x -= hw * 2f;
            if (p.y < c.y - hh) { p.y += hh * 2f; p.x = c.x + Random.Range(-hw, hw); } else if (p.y > c.y + hh) p.y -= hh * 2f;
            t.position = p;
            if (w.spin) t.Rotate(0f, 0f, spin[i] * dt);
        }
    }

    private void OnDestroy() { if (root != null) Destroy(root.gameObject); }
}
