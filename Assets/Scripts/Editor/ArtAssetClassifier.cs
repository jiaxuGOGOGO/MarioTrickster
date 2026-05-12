#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ArtAssetClassifier — 商业美术素材后台分类器。
///
/// 只在 Editor 中运行，负责把用户拖入的 Sprite/SpriteSheet 归档为：角色状态动画、循环场景动画、
/// 道具/陷阱/背景/VFX 等应用策略。它不直接修改 GameObject，避免与具体窗口强耦合。
/// </summary>
public static class ArtAssetClassifier
{
    public enum AssetRole
    {
        Unknown,
        Character,
        Enemy,
        Prop,
        Collectible,
        Hazard,
        Platform,
        Background,
        SceneAnimation,
        VFX,
        UI
    }

    public enum AnimationMode
    {
        None,
        Loop,
        StateDriven,
        OneShot
    }

    public enum RuntimeBehavior
    {
        KeepExisting,
        PlayerStateDriven,
        PhysicsProp,
        PickupConsume,
        HazardContact,
        BackgroundLayer,
        AmbientLoop,
        VFX
    }

    public sealed class Classification
    {
        public AssetRole role = AssetRole.Unknown;
        public AnimationMode animationMode = AnimationMode.None;
        public RuntimeBehavior runtimeBehavior = RuntimeBehavior.KeepExisting;
        public float confidence;
        public string notes = string.Empty;
        public Dictionary<SpriteStateAnimator.MotionState, Sprite[]> stateFrames = new Dictionary<SpriteStateAnimator.MotionState, Sprite[]>();

        public bool IsStateDriven => animationMode == AnimationMode.StateDriven && stateFrames.Count > 0;

        public string StateSummary
        {
            get
            {
                if (stateFrames == null || stateFrames.Count == 0) return string.Empty;
                return string.Join(",", stateFrames.Keys.Select(k => k.ToString().ToLowerInvariant()).OrderBy(s => s));
            }
        }
    }

    public static Classification Classify(GameObject target, Sprite[] sprites, int physicsTypeHint = -1)
    {
        Classification result = new Classification();
        sprites = sprites ?? new Sprite[0];

        string joined = BuildSearchText(target, sprites);
        result.role = DetectRole(target, joined, physicsTypeHint);
        result.runtimeBehavior = DetectRuntimeBehavior(result.role, target, joined);
        result.stateFrames = BuildStateGroups(sprites, joined);
        result.animationMode = DetectAnimationMode(result.role, result.runtimeBehavior, result.stateFrames, sprites, joined);
        result.confidence = EstimateConfidence(result, joined, sprites);
        result.notes = BuildNotes(result, joined, sprites.Length);
        return result;
    }

    public static void ApplyToMarker(ImportedAssetMarker marker, Classification classification)
    {
        if (marker == null || classification == null) return;
        marker.assetRole = classification.role.ToString();
        marker.animationMode = classification.animationMode.ToString();
        marker.runtimeBehavior = classification.runtimeBehavior.ToString();
        marker.animationStates = classification.StateSummary;
        marker.classificationConfidence = Mathf.Clamp01(classification.confidence);
        marker.classificationNotes = classification.notes;
    }

    private static string BuildSearchText(GameObject target, Sprite[] sprites)
    {
        List<string> parts = new List<string>();
        if (target != null) parts.Add(target.name);
        foreach (Sprite sprite in sprites)
        {
            if (sprite == null) continue;
            parts.Add(sprite.name);
            string path = AssetDatabase.GetAssetPath(sprite);
            if (!string.IsNullOrEmpty(path)) parts.Add(path);
        }
        return Normalize(string.Join(" ", parts));
    }

    private static AssetRole DetectRole(GameObject target, string text, int physicsTypeHint)
    {
        if (target != null)
        {
            if (target.GetComponentInParent<MarioController>() != null) return AssetRole.Character;
            if (target.GetComponentInChildren<BaseHazard>() != null || target.GetComponentInChildren<DamageDealer>() != null) return AssetRole.Hazard;
            if (target.GetComponentInChildren<SawBlade>() != null || target.GetComponentInChildren<ControllableHazard>() != null) return AssetRole.Hazard;
        }

        if (ContainsAny(text, "mario", "player", "hero", "character", "avatar", "idle", "run", "jump", "fall")) return AssetRole.Character;
        if (ContainsAny(text, "enemy", "monster", "goomba", "slime", "trickster", "boss")) return AssetRole.Enemy;
        if (ContainsAny(text, "coin", "gem", "pickup", "collect", "key", "heart", "powerup", "power_up")) return AssetRole.Collectible;
        if (ContainsAny(text, "hazard", "spike", "fire", "lava", "bomb", "explosion", "blade", "saw", "trap")) return AssetRole.Hazard;
        if (ContainsAny(text, "platform", "ground", "tile", "block", "brick")) return AssetRole.Platform;
        if (ContainsAny(text, "background", "bg", "parallax", "sky", "cloud", "mountain", "tree", "backdrop")) return AssetRole.Background;
        if (ContainsAny(text, "water", "torch", "portal", "ambient", "sceneanim", "scene_animation")) return AssetRole.SceneAnimation;
        if (ContainsAny(text, "vfx", "fx", "spark", "smoke", "poof", "slash", "impact")) return AssetRole.VFX;
        if (ContainsAny(text, "ui", "icon", "button", "hud")) return AssetRole.UI;

        switch (physicsTypeHint)
        {
            case 0: return AssetRole.Character;
            case 1: return AssetRole.Platform;
            case 2: return AssetRole.Hazard;
            case 3: return AssetRole.VFX;
            case 4: return AssetRole.Prop;
            default: return AssetRole.Unknown;
        }
    }

    private static RuntimeBehavior DetectRuntimeBehavior(AssetRole role, GameObject target, string text)
    {
        if (role == AssetRole.Character)
        {
            if (target != null && target.GetComponentInParent<MarioController>() != null) return RuntimeBehavior.PlayerStateDriven;
            if (ContainsAny(text, "player", "mario", "hero")) return RuntimeBehavior.PlayerStateDriven;
            return RuntimeBehavior.KeepExisting;
        }

        switch (role)
        {
            case AssetRole.Collectible: return RuntimeBehavior.PickupConsume;
            case AssetRole.Hazard: return RuntimeBehavior.HazardContact;
            case AssetRole.Background: return RuntimeBehavior.BackgroundLayer;
            case AssetRole.SceneAnimation: return RuntimeBehavior.AmbientLoop;
            case AssetRole.VFX: return RuntimeBehavior.VFX;
            case AssetRole.Prop: return RuntimeBehavior.PhysicsProp;
            default: return RuntimeBehavior.KeepExisting;
        }
    }

    private static AnimationMode DetectAnimationMode(AssetRole role, RuntimeBehavior behavior, Dictionary<SpriteStateAnimator.MotionState, Sprite[]> states, Sprite[] sprites, string text)
    {
        if (states != null && states.Count >= 2 && (role == AssetRole.Character || behavior == RuntimeBehavior.PlayerStateDriven)) return AnimationMode.StateDriven;
        if (ContainsAny(text, "oneshot", "one_shot", "explode", "explosion", "impact", "poof")) return sprites.Length > 1 ? AnimationMode.OneShot : AnimationMode.None;
        if (sprites != null && sprites.Length > 1) return AnimationMode.Loop;
        return AnimationMode.None;
    }

    public static Dictionary<SpriteStateAnimator.MotionState, Sprite[]> BuildStateGroups(Sprite[] sprites, string extraSearchText = "")
    {
        Dictionary<SpriteStateAnimator.MotionState, List<Sprite>> grouped = new Dictionary<SpriteStateAnimator.MotionState, List<Sprite>>();
        foreach (Sprite sprite in sprites ?? new Sprite[0])
        {
            if (sprite == null) continue;
            string text = Normalize(sprite.name + " " + AssetDatabase.GetAssetPath(sprite) + " " + extraSearchText);
            if (!TryDetectState(text, out SpriteStateAnimator.MotionState state)) continue;
            if (!grouped.TryGetValue(state, out List<Sprite> list))
            {
                list = new List<Sprite>();
                grouped[state] = list;
            }
            list.Add(sprite);
        }

        return grouped.ToDictionary(pair => pair.Key, pair => pair.Value.OrderBy(s => s.name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static bool TryDetectState(string text, out SpriteStateAnimator.MotionState state)
    {
        if (ContainsAny(text, "idle", "stand", "standing", "wait"))
        {
            state = SpriteStateAnimator.MotionState.Idle;
            return true;
        }
        if (ContainsAny(text, "run", "running", "walk", "walking", "move"))
        {
            state = SpriteStateAnimator.MotionState.Run;
            return true;
        }
        if (ContainsAny(text, "jump", "jumping", "rise", "rising", "up"))
        {
            state = SpriteStateAnimator.MotionState.Jump;
            return true;
        }
        if (ContainsAny(text, "fall", "falling", "drop", "down"))
        {
            state = SpriteStateAnimator.MotionState.Fall;
            return true;
        }
        state = SpriteStateAnimator.MotionState.Idle;
        return false;
    }

    private static float EstimateConfidence(Classification result, string text, Sprite[] sprites)
    {
        float confidence = 0.25f;
        if (result.role != AssetRole.Unknown) confidence += 0.25f;
        if (result.runtimeBehavior != RuntimeBehavior.KeepExisting) confidence += 0.15f;
        if (result.animationMode == AnimationMode.StateDriven) confidence += 0.25f;
        else if (sprites != null && sprites.Length > 1) confidence += 0.1f;
        if (ContainsAny(text, "idle", "run", "jump", "fall", "pickup", "hazard", "background", "vfx")) confidence += 0.1f;
        return Mathf.Clamp01(confidence);
    }

    private static string BuildNotes(Classification result, string text, int spriteCount)
    {
        string stateInfo = string.IsNullOrEmpty(result.StateSummary) ? "未识别状态帧组" : $"状态帧组={result.StateSummary}";
        return $"自动分类: role={result.role}, animation={result.animationMode}, behavior={result.runtimeBehavior}, frames={spriteCount}, {stateInfo}.";
    }

    private static string Normalize(string text)
    {
        return (text ?? string.Empty).Replace('-', '_').Replace('.', '_').Replace('/', '_').Replace('\\', '_').ToLowerInvariant();
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (string token in tokens)
        {
            string normalized = Normalize(token);
            if (text.Contains(normalized)) return true;
        }
        return false;
    }
}
#endif
