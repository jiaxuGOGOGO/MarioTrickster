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
///
/// Session S-DynState 重构：
///   - stateFrames 从 Dictionary&lt;MotionState, Sprite[]&gt; 改为 Dictionary&lt;string, Sprite[]&gt;
///     字符串 tag 做 key，核心 4 状态用 "idle"/"run"/"jump"/"fall"，扩展状态用 "wallslide"/"swim"/"roll" 等。
///   - TryDetectState 改为返回字符串 tag，关键词表扩展覆盖更多商业素材命名。
///   - 向后兼容：提供 GetMotionStateFrames() 方法，将字符串 tag 映射回 MotionState 枚举。
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

    // ── 状态关键词定义（集中管理，方便扩展）──────────────────
    // 每个条目：tag → 关键词数组。文件名命中任一关键词即归入该 tag。
    // 优先级由数组顺序决定（TryDetectState 从前往后匹配，先命中先返回）。
    // [AI防坑警告] 核心 4 状态必须排在最前面，保证 idle/run/jump/fall 优先命中。
    // 新增状态只需在此处加一行，零代码改动。
    private static readonly StateKeywordEntry[] STATE_KEYWORDS = new StateKeywordEntry[]
    {
        // ── 核心 4 状态（物理自动驱动）──
        new StateKeywordEntry("idle",      "idle", "stand", "standing", "wait"),
        new StateKeywordEntry("run",       "run", "running", "walk", "walking", "move"),
        new StateKeywordEntry("jump",      "jump", "jumping", "rise", "rising"),
        new StateKeywordEntry("fall",      "fall", "falling", "drop"),

        // ── 扩展运动状态（未来由外部脚本驱动）──
        new StateKeywordEntry("wallslide", "wallslide", "wall_slide", "wall_climb", "wall_climbing", "wallcling", "wall_cling", "walljump", "wall_jump"),
        new StateKeywordEntry("swim",      "swim", "swimming", "swimming_dive", "dive", "underwater"),
        new StateKeywordEntry("roll",      "roll", "rolling", "dodge", "dodge_roll"),
        new StateKeywordEntry("crouch",    "crouch", "crouching", "duck", "ducking", "crawl", "crawling"),
        new StateKeywordEntry("slide",     "slide", "sliding", "dash", "dashing", "speed_boost"),
        new StateKeywordEntry("climb",     "climb", "climbing", "climbing_ladder", "descending_ladder", "ladder"),
        new StateKeywordEntry("doublejump","double_jump", "doublejump", "air_jump"),
        new StateKeywordEntry("glide",     "glide", "gliding", "gliding_jump", "float", "floating"),
        new StateKeywordEntry("land",      "land", "landing", "landing_with_impact", "landingwithimpact"),
        new StateKeywordEntry("attack",    "attack", "attacking", "atk", "melee", "slash", "stab", "strike"),
        new StateKeywordEntry("hurt",      "hurt", "hit", "damage", "damaged", "injured", "knockback"),
        new StateKeywordEntry("death",     "death", "dead", "die", "dying", "defeat"),
    };

    private struct StateKeywordEntry
    {
        public readonly string tag;
        public readonly string[] keywords;
        public StateKeywordEntry(string tag, params string[] keywords) { this.tag = tag; this.keywords = keywords; }
    }

    /// <summary>核心 4 状态的 tag 集合，用于快速判断。</summary>
    private static readonly HashSet<string> CORE_TAGS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "idle", "run", "jump", "fall"
    };

    public sealed class Classification
    {
        public AssetRole role = AssetRole.Unknown;
        public AnimationMode animationMode = AnimationMode.None;
        public RuntimeBehavior runtimeBehavior = RuntimeBehavior.KeepExisting;
        public float confidence;
        public string notes = string.Empty;
        /// <summary>
        /// 状态帧字典。key 为小写字符串 tag（如 "idle"、"run"、"wallslide"、"swim"）。
        /// </summary>
        public Dictionary<string, Sprite[]> stateFrames = new Dictionary<string, Sprite[]>(StringComparer.OrdinalIgnoreCase);
        public List<string> semanticStates = new List<string>();

        public bool IsStateDriven => animationMode == AnimationMode.StateDriven && stateFrames.Count > 0;

        /// <summary>向后兼容：获取核心 MotionState 枚举对应的帧数组。</summary>
        public Dictionary<SpriteStateAnimator.MotionState, Sprite[]> GetMotionStateFrames()
        {
            var result = new Dictionary<SpriteStateAnimator.MotionState, Sprite[]>();
            foreach (var tag in CORE_TAGS)
            {
                if (stateFrames.TryGetValue(tag, out Sprite[] frames) && frames != null && frames.Length > 0)
                {
                    result[SpriteStateAnimator.TagToMotionState(tag)] = frames;
                }
            }
            return result;
        }

        public string MotionStateSummary
        {
            get
            {
                if (stateFrames == null || stateFrames.Count == 0) return string.Empty;
                // 只列出核心 4 状态
                return string.Join(",", new[] { "idle", "run", "jump", "fall" }
                    .Where(tag => stateFrames.ContainsKey(tag)));
            }
        }

        public string StateSummary
        {
            get
            {
                List<string> ordered = new List<string>();
                // 先列核心状态
                foreach (string tag in new[] { "idle", "run", "jump", "fall" })
                {
                    if (stateFrames.ContainsKey(tag)) ordered.Add(tag);
                }
                // 再列扩展状态
                foreach (string tag in stateFrames.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                {
                    if (!ordered.Contains(tag)) ordered.Add(tag);
                }
                // 最后列语义状态（不在 stateFrames 中的）
                if (semanticStates != null)
                {
                    foreach (string state in OrderedSemanticStates())
                    {
                        if (semanticStates.Contains(state) && !ordered.Contains(state)) ordered.Add(state);
                    }
                    foreach (string state in semanticStates.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(state) && !ordered.Contains(state)) ordered.Add(state);
                    }
                }
                return string.Join(",", ordered);
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
        // [AI防坑警告] 状态分组必须只看单帧自身名称/路径，不能把整套图集 joined 文本塞进去。
        // 否则 idle/run/jump/fall 会同时污染每一帧，所有帧都会被第一个命中状态吃掉。
        result.stateFrames = BuildStateGroups(sprites);
        result.semanticStates = BuildSemanticStates(sprites, joined, result.stateFrames);
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

        if (ContainsAny(text, "enemy", "monster", "goomba", "goblin", "skeleton", "zombie", "slime", "bat", "orc", "demon", "creature", "npc", "boss")) return AssetRole.Enemy;
        if (ContainsAny(text, "mario", "player", "hero", "character", "avatar", "warrior", "knight", "rogue", "mage")) return AssetRole.Character;
        if (ContainsAny(text, "coin", "gem", "pickup", "collect", "key", "heart", "powerup", "power_up", "potion", "chest", "treasure", "loot")) return AssetRole.Collectible;
        if (ContainsAny(text, "hazard", "spike", "fire", "lava", "bomb", "explosion", "blade", "saw", "sawblade", "trap", "acid", "laser")) return AssetRole.Hazard;
        // [AI防坑警告] background 必须先于 ground/platform 判断；同时 ContainsAny 是边界化匹配，避免 background 被 ground 子串误判。
        if (ContainsAny(text, "background", "bg", "parallax", "sky", "cloud", "mountain", "tree", "backdrop")) return AssetRole.Background;
        if (ContainsAny(text, "platform", "ground", "tile", "block", "brick")) return AssetRole.Platform;
        if (ContainsAny(text, "water", "torch", "portal", "ambient", "sceneanim", "scene_animation", "wind", "rain", "fog")) return AssetRole.SceneAnimation;
        if (ContainsAny(text, "vfx", "fx", "spark", "smoke", "poof", "slash", "impact", "hit", "cast", "spell", "skill", "aura", "burst", "trail")) return AssetRole.VFX;
        if (ContainsAny(text, "ui", "icon", "button", "hud")) return AssetRole.UI;

        // 只把"纯运动状态包"作为最后的角色兜底；否则 chest_idle、spell_cast、trap_active 等商业素材会被误判为角色。
        if (ContainsAny(text, "idle", "run", "running", "walk", "walking", "jump", "fall", "falling")) return AssetRole.Character;

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

    private static AnimationMode DetectAnimationMode(AssetRole role, RuntimeBehavior behavior, Dictionary<string, Sprite[]> states, Sprite[] sprites, string text)
    {
        // 主角换皮允许单状态试跑：只有 run/idle/jump/fall 其中一组时，也要挂 SpriteStateAnimator。
        // 否则只导入 RUN 会退化成普通循环动画，无法验证"按左右才播放跑步"。
        if (HasAnyMotionState(states) && behavior == RuntimeBehavior.PlayerStateDriven) return AnimationMode.StateDriven;

        // 完整或半完整角色包：只要文件名已经明确分出两个及以上运动状态，就优先走状态机动画。
        // 这样用户把 idle/run/jump/fall 散帧丢进来时，不必再额外选择复杂菜单。
        if (states != null && states.Count >= 2 && (role == AssetRole.Character || HasCoreMotionStates(states))) return AnimationMode.StateDriven;
        if (ContainsAny(text, "oneshot", "one_shot", "explode", "explosion", "impact", "poof")) return sprites.Length > 1 ? AnimationMode.OneShot : AnimationMode.None;
        if (sprites != null && sprites.Length > 1) return AnimationMode.Loop;
        return AnimationMode.None;
    }

    /// <summary>
    /// 根据 Sprite 名称和路径，自动将帧分组到各状态 tag。
    /// 返回 Dictionary&lt;string, Sprite[]&gt;，key 为小写 tag。
    /// </summary>
    public static Dictionary<string, Sprite[]> BuildStateGroups(Sprite[] sprites, string extraSearchText = "")
    {
        Dictionary<string, List<Sprite>> grouped = new Dictionary<string, List<Sprite>>(StringComparer.OrdinalIgnoreCase);
        foreach (Sprite sprite in sprites ?? new Sprite[0])
        {
            if (sprite == null) continue;
            string text = Normalize(sprite.name + " " + AssetDatabase.GetAssetPath(sprite));
            if (!TryDetectState(text, out string tag)) continue;
            if (!grouped.TryGetValue(tag, out List<Sprite> list))
            {
                list = new List<Sprite>();
                grouped[tag] = list;
            }
            list.Add(sprite);
        }

        return grouped.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .OrderBy(s => ExtractFrameIndex(s != null ? s.name : string.Empty))
                .ThenBy(s => s != null ? s.name : string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 尝试从文本中检测状态 tag。使用 STATE_KEYWORDS 表按优先级匹配。
    /// </summary>
    private static bool TryDetectState(string text, out string tag)
    {
        foreach (var entry in STATE_KEYWORDS)
        {
            if (ContainsAny(text, entry.keywords))
            {
                tag = entry.tag;
                return true;
            }
        }
        tag = null;
        return false;
    }

    /// <summary>
    /// 向后兼容：旧签名的 TryDetectState，返回 MotionState 枚举。
    /// 仅匹配核心 4 状态，扩展状态不参与。
    /// </summary>
    public static bool TryDetectState(string text, out SpriteStateAnimator.MotionState state)
    {
        if (TryDetectState(text, out string tag) && CORE_TAGS.Contains(tag))
        {
            state = SpriteStateAnimator.TagToMotionState(tag);
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
        if (result.semanticStates != null && result.semanticStates.Count > 0) confidence += 0.1f;
        if (ContainsAny(text, "idle", "run", "jump", "fall", "pickup", "hazard", "background", "vfx")) confidence += 0.1f;
        return Mathf.Clamp01(confidence);
    }

    private static string BuildNotes(Classification result, string text, int spriteCount)
    {
        string motionInfo = string.IsNullOrEmpty(result.MotionStateSummary) ? "运动状态=无" : $"运动状态={result.MotionStateSummary}";
        string semanticInfo = string.IsNullOrEmpty(result.StateSummary) ? "通用状态=无" : $"通用状态={result.StateSummary}";
        return $"自动分类: role={result.role}, animation={result.animationMode}, behavior={result.runtimeBehavior}, frames={spriteCount}, {motionInfo}, {semanticInfo}.";
    }

    private static List<string> BuildSemanticStates(Sprite[] sprites, string joinedText, Dictionary<string, Sprite[]> tagStates)
    {
        HashSet<string> detected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 从 stateFrames 中已有的 tag 加入
        if (tagStates != null)
        {
            foreach (var kvp in tagStates)
            {
                if (kvp.Value != null && kvp.Value.Length > 0)
                    detected.Add(kvp.Key);
            }
        }

        foreach (Sprite sprite in sprites ?? new Sprite[0])
        {
            if (sprite == null) continue;
            string text = Normalize(sprite.name + " " + AssetDatabase.GetAssetPath(sprite));
            foreach (StateTokenGroup group in StateTokenGroups())
            {
                if (ContainsAny(text, group.tokens)) detected.Add(group.state);
            }
        }

        foreach (StateTokenGroup group in StateTokenGroups())
        {
            if (ContainsAny(joinedText, group.tokens)) detected.Add(group.state);
        }

        return OrderedSemanticStates()
            .Where(detected.Contains)
            .Concat(detected.Where(state => !OrderedSemanticStates().Contains(state)).OrderBy(state => state, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private struct StateTokenGroup
    {
        public readonly string state;
        public readonly string[] tokens;

        public StateTokenGroup(string state, params string[] tokens)
        {
            this.state = state;
            this.tokens = tokens;
        }
    }

    private static IEnumerable<StateTokenGroup> StateTokenGroups()
    {
        yield return new StateTokenGroup("idle", "idle", "stand", "standing", "wait");
        yield return new StateTokenGroup("run", "run", "running", "walk", "walking", "move");
        yield return new StateTokenGroup("jump", "jump", "jumping", "rise", "rising");
        yield return new StateTokenGroup("fall", "fall", "falling", "drop");
        yield return new StateTokenGroup("wallslide", "wallslide", "wall_slide", "wall_climb", "wall_climbing", "wallcling", "wall_cling", "walljump", "wall_jump");
        yield return new StateTokenGroup("swim", "swim", "swimming", "swimming_dive", "dive", "underwater");
        yield return new StateTokenGroup("roll", "roll", "rolling", "dodge", "dodge_roll");
        yield return new StateTokenGroup("crouch", "crouch", "crouching", "duck", "ducking", "crawl", "crawling");
        yield return new StateTokenGroup("slide", "slide", "sliding", "dash", "dashing", "speed_boost");
        yield return new StateTokenGroup("climb", "climb", "climbing", "climbing_ladder", "descending_ladder", "ladder");
        yield return new StateTokenGroup("doublejump", "double_jump", "doublejump", "air_jump");
        yield return new StateTokenGroup("glide", "glide", "gliding", "gliding_jump", "float", "floating");
        yield return new StateTokenGroup("land", "land", "landing", "landing_with_impact", "landingwithimpact");
        yield return new StateTokenGroup("attack", "attack", "attacking", "atk", "melee", "slash", "stab", "shoot", "fire", "strike");
        yield return new StateTokenGroup("cast", "cast", "casting", "skill", "spell", "magic", "ability", "charge", "release");
        yield return new StateTokenGroup("hurt", "hurt", "hit", "damage", "damaged", "injured", "knockback");
        yield return new StateTokenGroup("death", "death", "dead", "die", "dying", "defeat", "destroyed", "break");
        yield return new StateTokenGroup("stealth", "stealth", "sneak", "hide", "hidden");
        yield return new StateTokenGroup("disguise", "disguise", "mimic", "transform", "shapeshift");
        yield return new StateTokenGroup("blend", "blend", "blended", "camouflage", "camouflaged", "invisible", "vanish");
        yield return new StateTokenGroup("telegraph", "telegraph", "tell", "warning", "warn", "anticipation", "precast");
        yield return new StateTokenGroup("active", "active", "activate", "activated", "trigger", "triggered", "open", "opened");
        yield return new StateTokenGroup("cooldown", "cooldown", "recover", "recovery", "recharge", "reset");
        yield return new StateTokenGroup("exhausted", "exhausted", "empty", "spent", "disabled", "inactive");
        yield return new StateTokenGroup("pickup", "pickup", "collect", "collected", "item", "loot");
        yield return new StateTokenGroup("impact", "impact", "hitfx", "spark", "burst", "explosion", "explode", "poof", "smoke");
    }

    private static IEnumerable<string> OrderedSemanticStates()
    {
        yield return "idle";
        yield return "run";
        yield return "jump";
        yield return "fall";
        yield return "wallslide";
        yield return "swim";
        yield return "roll";
        yield return "crouch";
        yield return "slide";
        yield return "climb";
        yield return "doublejump";
        yield return "glide";
        yield return "land";
        yield return "attack";
        yield return "cast";
        yield return "hurt";
        yield return "death";
        yield return "stealth";
        yield return "disguise";
        yield return "blend";
        yield return "telegraph";
        yield return "active";
        yield return "cooldown";
        yield return "exhausted";
        yield return "pickup";
        yield return "impact";
    }


    private static IEnumerable<SpriteStateAnimator.MotionState> OrderedStates()
    {
        yield return SpriteStateAnimator.MotionState.Idle;
        yield return SpriteStateAnimator.MotionState.Run;
        yield return SpriteStateAnimator.MotionState.Jump;
        yield return SpriteStateAnimator.MotionState.Fall;
    }

    private static bool HasAnyMotionState(Dictionary<string, Sprite[]> states)
    {
        if (states == null || states.Count == 0) return false;
        return CORE_TAGS.Any(tag =>
            states.TryGetValue(tag, out Sprite[] frames) && frames != null && frames.Length > 0);
    }

    private static bool HasCoreMotionStates(Dictionary<string, Sprite[]> states)
    {
        if (states == null || states.Count < 2) return false;
        int nonEmpty = CORE_TAGS.Count(tag =>
            states.TryGetValue(tag, out Sprite[] frames) && frames != null && frames.Length > 0);
        return nonEmpty >= 2;
    }

    private static int ExtractFrameIndex(string name)
    {
        if (string.IsNullOrEmpty(name)) return int.MaxValue;
        int end = name.Length - 1;
        while (end >= 0 && !char.IsDigit(name[end])) end--;
        if (end < 0) return int.MaxValue;
        int start = end;
        while (start >= 0 && char.IsDigit(name[start])) start--;
        string digits = name.Substring(start + 1, end - start);
        return int.TryParse(digits, out int value) ? value : int.MaxValue;
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder(text.Length + 8);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c) || c == '-' || c == '.' || c == '/' || c == '\\')
            {
                AppendSeparator(builder);
                continue;
            }

            if (c == '_' || c == '(' || c == ')' || c == '[' || c == ']')
            {
                AppendSeparator(builder);
                continue;
            }

            if (builder.Length > 0)
            {
                char previous = builder[builder.Length - 1];
                // [FIX] CamelCase split: only insert separator when an uppercase letter
                // follows a lowercase letter. This prevents ALL-CAPS words like "RUN"
                // from being split into individual letters (r_u_n).
                if (char.IsUpper(c) && char.IsLetter(previous) && char.IsLower(text[i - 1]))
                {
                    AppendSeparator(builder);
                }
                else if (char.IsDigit(c) && char.IsLetter(previous))
                {
                    AppendSeparator(builder);
                }
                else if (char.IsLetter(c) && char.IsDigit(previous))
                {
                    AppendSeparator(builder);
                }
            }

            builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }

    private static void AppendSeparator(System.Text.StringBuilder builder)
    {
        if (builder.Length == 0 || builder[builder.Length - 1] == '_') return;
        builder.Append('_');
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        if (string.IsNullOrEmpty(text)) return false;
        string normalizedText = Normalize(text);
        foreach (string token in tokens)
        {
            string normalizedToken = Normalize(token);
            if (string.IsNullOrEmpty(normalizedToken)) continue;
            if (HasTokenMatch(normalizedText, normalizedToken)) return true;
        }
        return false;
    }

    private static bool HasTokenMatch(string text, string token)
    {
        int startIndex = 0;
        while (startIndex < text.Length)
        {
            int index = text.IndexOf(token, startIndex, StringComparison.Ordinal);
            if (index < 0) return false;

            int before = index - 1;
            int after = index + token.Length;
            if (IsTokenBoundary(text, before) && IsTokenBoundary(text, after)) return true;
            startIndex = index + token.Length;
        }
        return false;
    }

    private static bool IsTokenBoundary(string text, int index)
    {
        return index < 0 || index >= text.Length || !char.IsLetterOrDigit(text[index]);
    }
}
#endif
