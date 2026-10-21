using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S252 世界演出音效：全部用代码合成（AudioClip.Create），不引入外部素材，不依赖导入设置。
/// 名称与 WorldParkStageCues.Cue.sound 对应：neon 霓虹亮灯 / beep 倒计时 / beep_last 最后几秒 / laser 激光处决 /
/// pass 通过 / clear 通关和弦 / gameover 低沉终结 / end 旅程终点 / alarm 被看见 / rustle 草晃 / step 脚步。
/// 播放源挂在 DontDestroyOnLoad 物体上并忽略 AudioListener.pause；Time.timeScale=0 时也能响。
/// </summary>
public static class WorldParkSfx
{
    public static float Volume = 0.55f;
    private const int Rate = 44100;
    private static AudioSource source;
    private static AudioListener ownListener;
    private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private static uint noiseState = 12345u;

    public static void Play(string name, float volume = 1f)
    {
        if (string.IsNullOrEmpty(name) || !Application.isPlaying) return;
        Ensure();
        AudioClip clip = Get(name);
        if (clip != null && source != null) source.PlayOneShot(clip, Mathf.Clamp01(Volume * volume));
    }

    private static void Ensure()
    {
        if (source == null)
        {
            var go = new GameObject("世界演出音效");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
        }
        // 场景里没有 AudioListener 时补一个；场景自带后移除自己的，避免“两个监听器”警告。
        AudioListener[] all = Object.FindObjectsOfType<AudioListener>();
        if (all.Length == 0 && ownListener == null) ownListener = source.gameObject.AddComponent<AudioListener>();
        else if (all.Length > 1 && ownListener != null) { Object.Destroy(ownListener); ownListener = null; }
    }

    private static AudioClip Get(string name)
    {
        if (clips.TryGetValue(name, out AudioClip cached) && cached != null) return cached;
        float[] data = Synth(name);
        if (data == null) return null;
        AudioClip clip = AudioClip.Create("worldpark_" + name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        clips[name] = clip;
        return clip;
    }

    private static float Noise()
    {
        noiseState = noiseState * 1664525u + 1013904223u;
        return ((noiseState >> 8) & 0xFFFF) / 32768f - 1f;
    }

    private static float[] Buffer(float seconds) { return new float[Mathf.Max(1, (int)(seconds * Rate))]; }

    private static float[] Synth(string name)
    {
        switch (name)
        {
            case "neon": return Neon();
            case "beep": return Tone(880f, 0.11f, 0.5f);
            case "beep_last": return Tone(1320f, 0.16f, 0.6f);
            case "laser": return Laser();
            case "pass": return Arpeggio(new[] { 659.3f, 987.8f }, 0.09f, 0.35f);
            case "clear": return Arpeggio(new[] { 523.3f, 659.3f, 784f, 1046.5f }, 0.11f, 0.9f);
            case "gameover": return Drone();
            case "end": return Arpeggio(new[] { 392f, 587.3f }, 0.2f, 0.9f);
            case "alarm": return Alarm();
            case "rustle": return Rustle();
            case "step": return Step();
        }
        return null;
    }

    /// <summary>霓虹灯管通电：几声“咔哒”+ 闪烁的 120Hz 嗡鸣，最后稳定。</summary>
    private static float[] Neon()
    {
        float[] d = Buffer(1.3f);
        float[] clicks = { 0f, 0.13f, 0.24f, 0.42f };
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float flicker = t < 0.5f ? (Mathf.Sin(t * 90f) > 0.2f ? 1f : 0.15f) : 1f;
            float fade = t < 1.0f ? 1f : Mathf.Clamp01((1.3f - t) / 0.3f);
            float hum = (Mathf.Sin(2 * Mathf.PI * 120f * t) * 0.22f + Mathf.Sin(2 * Mathf.PI * 240f * t) * 0.08f + Noise() * 0.015f) * flicker * fade * Mathf.Clamp01(t / 0.05f);
            float click = 0f;
            foreach (float c in clicks) { float dt = t - c; if (dt >= 0 && dt < 0.012f) click += Noise() * 0.6f * (1f - dt / 0.012f); }
            d[i] = hum + click;
        }
        return d;
    }

    private static float[] Tone(float freq, float seconds, float amp)
    {
        float[] d = Buffer(seconds);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Clamp01(t / 0.005f) * Mathf.Clamp01((seconds - t) / 0.04f);
            d[i] = Mathf.Sin(2 * Mathf.PI * freq * t) * amp * env;
        }
        return d;
    }

    /// <summary>激光：短促充能高音 → 从 1800Hz 急速下滑 → 白噪声余烬。</summary>
    private static float[] Laser()
    {
        float[] d = Buffer(0.9f);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float f = t < 0.08f ? 2400f : Mathf.Lerp(1800f, 110f, Mathf.Clamp01((t - 0.08f) / 0.45f));
            phase += 2 * Mathf.PI * f / Rate;
            float body = Mathf.Sin(phase) * (t < 0.6f ? 0.5f : 0.5f * Mathf.Clamp01((0.75f - t) / 0.15f));
            float tail = t > 0.1f ? Noise() * 0.25f * Mathf.Exp(-(t - 0.1f) * 6f) : 0f;
            d[i] = body + tail;
        }
        return d;
    }

    private static float[] Arpeggio(float[] notes, float step, float sustain)
    {
        float seconds = notes.Length * step + sustain;
        float[] d = Buffer(seconds);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate, v = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                float start = k * step;
                if (t < start) continue;
                float dt = t - start;
                v += Mathf.Sin(2 * Mathf.PI * notes[k] * dt) * 0.18f * Mathf.Exp(-dt * 2.2f) * Mathf.Clamp01(dt / 0.004f);
            }
            d[i] = v * Mathf.Clamp01((seconds - t) / 0.08f);
        }
        return d;
    }

    private static float[] Drone()
    {
        float[] d = Buffer(1.4f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 1.8f);
            d[i] = (Mathf.Sin(2 * Mathf.PI * 55f * t) * 0.45f + Mathf.Sin(2 * Mathf.PI * 82.4f * t) * 0.3f + Mathf.Sin(2 * Mathf.PI * 110f * t) * 0.12f) * env;
        }
        return d;
    }

    private static float[] Alarm()
    {
        float[] d = Buffer(0.5f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float f = ((int)(t / 0.125f)) % 2 == 0 ? 880f : 660f;
            float s = Mathf.Sin(2 * Mathf.PI * f * t);
            d[i] = Mathf.Clamp(s * 2.5f, -1f, 1f) * 0.22f * Mathf.Clamp01((0.5f - t) / 0.05f);
        }
        return d;
    }

    private static float[] Rustle()
    {
        float[] d = Buffer(0.35f);
        float low = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            low += (Noise() - low) * 0.25f;
            d[i] = low * 0.6f * Mathf.Sin(Mathf.PI * t / 0.35f);
        }
        return d;
    }

    private static float[] Step()
    {
        float[] d = Buffer(0.09f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            d[i] = (Mathf.Sin(2 * Mathf.PI * 90f * t) * 0.5f + Noise() * 0.2f) * Mathf.Exp(-t * 45f);
        }
        return d;
    }
}
