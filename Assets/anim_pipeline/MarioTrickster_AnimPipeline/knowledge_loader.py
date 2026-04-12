#!/usr/bin/env python3
"""
MarioTrickster 蒸馏知识加载器 v2.0
===================================
统一加载 distilled_knowledge.json（源自 PROMPT_RECIPES.md 全量蒸馏），
为管线各环节提供知识驱动的参数和规则。

职责：
  1. 加载并缓存知识库（带热更新）
  2. 根据 action 类型返回最优首次生成参数
  3. 构建知识增强的 Prompt（正向 + 负向）— v2.0: 画风已从像素风切换为 anime_cel_outline
  4. 提供 QC 阈值和智能调参计算
  5. 支持热更新：PDF 蒸馏新知识后，下次调用自动生效
  6. [v2.0新增] 动作核心规则查询 / 缺陷诊断 Prompt 补丁 / VFX 知识查询
"""

import json
import math
from pathlib import Path

# ─────────────────────────────────────────────
# 知识库路径
# ─────────────────────────────────────────────

KNOWLEDGE_FILE = Path(__file__).parent / "distilled_knowledge.json"

_cache = None
_cache_mtime = 0


def _load():
    """加载知识库，带文件修改时间缓存（热更新）"""
    global _cache, _cache_mtime

    if not KNOWLEDGE_FILE.exists():
        return {}

    mtime = KNOWLEDGE_FILE.stat().st_mtime
    if _cache is not None and mtime == _cache_mtime:
        return _cache

    try:
        with open(KNOWLEDGE_FILE, encoding="utf-8") as f:
            _cache = json.load(f)
            _cache_mtime = mtime
            return _cache
    except Exception:
        return {}


def get_knowledge():
    """获取完整知识库字典"""
    return _load()


# ─────────────────────────────────────────────
# 1. 首次生成最优参数
# ─────────────────────────────────────────────

def get_optimal_params(action_type, base_config):
    """
    根据知识库返回首次生成的最优参数。
    只覆盖知识库中有明确建议的字段，其余保持 base_config 原值。

    参数:
        action_type: 动作类型 (idle/run/jump/attack_sword/death/dash/walk/custom)
        base_config: 当前默认配置字典

    返回:
        dict: 合并后的最优配置
    """
    kb = _load()
    presets = kb.get("generation_presets", {})

    # 先取 action 专属预设，再 fallback 到 default
    action_preset = presets.get(action_type, presets.get("default", {}))
    default_preset = presets.get("default", {})

    result = dict(base_config)

    # 应用知识库预设（action 专属 > default > base_config）
    for key in ["width", "height", "length", "steps", "cfg", "pixel_size", "palette_colors"]:
        if key in action_preset and not key.startswith("_"):
            result[key] = action_preset[key]
        elif key in default_preset and not key.startswith("_"):
            # 只在 base_config 没有显式设置时才用 default
            pass  # base_config 已经有默认值

    # 确保 width/height 是 16 的倍数
    for dim in ["width", "height"]:
        if dim in result:
            result[dim] = (result[dim] // 16) * 16

    # 确保 length 满足 4n+1
    if "length" in result:
        length = result["length"]
        remainder = (length - 1) % 4
        if remainder != 0:
            result["length"] = length - remainder + 4 + 1

    return result


# ─────────────────────────────────────────────
# 2. 知识增强 Prompt
# ─────────────────────────────────────────────

def enhance_prompt(action_type, base_positive, base_negative):
    """
    用蒸馏知识增强 Prompt (v2.0: 画风从像素风切换为 anime_cel_outline)。

    策略：
    - positive: 画风基础 + 动作 prompt_boost + 核心动画规则 prompt_tags
    - negative: 画风负向 + 动作常见缺陷 + 风向不一致等

    参数:
        action_type: 动作类型
        base_positive: 原始正向 Prompt
        base_negative: 原始负向 Prompt

    返回:
        (enhanced_positive, enhanced_negative)
    """
    kb = _load()
    if not kb:
        return base_positive, base_negative

    pos_parts = [base_positive] if base_positive else []
    neg_parts = [base_negative] if base_negative else []

    # ── v2.0: 画风基础 Prompt (从 style_foundation 读取) ──
    style = kb.get("style_foundation", {})
    style_pos = style.get("positive_prompt_base", "")
    style_neg = style.get("negative_prompt_base", "")

    # v1 兼容: 如果没有 style_foundation，回退到旧版 style_consistency
    if not style_pos:
        old_style = kb.get("style_consistency", {})
        anchors = old_style.get("positive_prompt_anchors", [])
        if anchors:
            style_pos = ", ".join(anchors)
        old_neg = old_style.get("negative_prompt_anchors", [])
        if old_neg:
            style_neg = ", ".join(old_neg)

    if style_pos:
        pos_parts.append(style_pos)
    if style_neg:
        neg_parts.append(style_neg)

    # ── 动作专属知识 ──
    action_kb = kb.get("action_knowledge", {}).get(action_type, {})
    if action_kb.get("prompt_boost"):
        pos_parts.append(action_kb["prompt_boost"])

    # 常见缺陷转为负向 Prompt (取前3个)
    defects = action_kb.get("common_defects", [])
    if defects:
        neg_parts.extend(defects[:3])

    # ── v2.0: 核心动画规则的 prompt_tags ──
    anim_rules = kb.get("animation_core_rules", {})
    universal_tags = []
    for rule_key in ("contrapposto", "arc_of_motion", "follow_through", "anticipation"):
        rule_val = anim_rules.get(rule_key, {})
        if isinstance(rule_val, dict):
            tags = rule_val.get("prompt_tags", "")
            if tags:
                universal_tags.append(tags)
    if universal_tags:
        pos_parts.extend(universal_tags)

    # ── v2.0: 风向一致性负向 ──
    lighting = kb.get("lighting_material_rules", {})
    wind_neg = lighting.get("wind_force_expression", {}).get("negative_prompt", "")
    if wind_neg:
        neg_parts.append(wind_neg)

    # ── 去重并拼接 ──
    all_pos = ", ".join(pos_parts)
    all_neg = ", ".join(neg_parts)
    final_pos = ", ".join(dict.fromkeys(p.strip() for p in all_pos.split(",") if p.strip()))
    final_neg = ", ".join(dict.fromkeys(p.strip() for p in all_neg.split(",") if p.strip()))

    return final_pos, final_neg


# ─────────────────────────────────────────────
# 3. QC 阈值
# ─────────────────────────────────────────────

def get_qc_thresholds():
    """返回知识驱动的 QC 阈值字典"""
    kb = _load()
    return kb.get("qc_thresholds", {
        "crop_edge_margin_ratio": 0.03,
        "crop_fill_threshold": 0.30,
        "consistency_max_diff_warning": 25,
        "consistency_max_diff_critical": 40,
        "consistency_avg_diff_acceptable": 15,
        "color_shift_warning": 40,
        "color_shift_critical": 70,
        "saturation_loss_warning": 20,
        "min_opaque_pixel_ratio": 0.10,
        "max_opaque_pixel_ratio": 0.70,
    })


# ─────────────────────────────────────────────
# 4. 智能调参计算
# ─────────────────────────────────────────────

def compute_retune(issue_type, severity_value, current_config, action_type="custom"):
    """
    根据知识库的 retune_strategy 计算精确的调参值 (v2.0: 新增 animation_defect 类型)。

    参数:
        issue_type: "crop" / "consistency" / "color" / "animation_defect"
        severity_value: 问题严重程度数值 或 缺陷关键词字符串
        current_config: 当前配置
        action_type: 动作类型

    返回:
        dict: 调参建议 {param_name: new_value}
    """
    kb = _load()
    thresholds = get_qc_thresholds()
    adjustments = {}

    if issue_type == "crop":
        comp = kb.get("composition_rules", {})
        aspect = comp.get("aspect_ratio_by_action", {}).get(action_type, [1, 1])
        cur_w = current_config.get("width", 480)
        cur_h = current_config.get("height", 480)

        if aspect[0] != aspect[1]:
            target_w = max(cur_w, int(480 * aspect[0] / min(aspect)))
            target_h = max(cur_h, int(480 * aspect[1] / min(aspect)))
        else:
            target_w = min(int(cur_w * 1.2), 832)
            target_h = min(int(cur_h * 1.2), 832)

        target_w = (target_w // 16) * 16
        target_h = (target_h // 16) * 16

        if target_w != cur_w:
            adjustments["width"] = target_w
        if target_h != cur_h:
            adjustments["height"] = target_h

    elif issue_type == "consistency":
        cur_steps = current_config.get("steps", 6)
        threshold = thresholds.get("consistency_max_diff_warning", 20)
        excess = max(0, severity_value - threshold)
        step_increase = max(2, math.ceil(excess / 5) * 2)
        new_steps = min(cur_steps + step_increase, 20)

        if new_steps != cur_steps:
            adjustments["steps"] = new_steps

        if cur_steps >= 12 and severity_value > thresholds.get("consistency_max_diff_critical", 35):
            adjustments["_hint"] = "steps 已较高但仍不一致，可能需要检查驱动视频质量"

    elif issue_type == "color":
        # v2.0: 色偏优先强化 Prompt 锚点，而非仅增加调色板
        cur_palette = current_config.get("palette_colors", 32)
        threshold = thresholds.get("color_shift_warning", 35)
        excess = max(0, severity_value - threshold)
        palette_increase = max(8, math.ceil(excess / 10) * 8)
        new_palette = min(cur_palette + palette_increase, 128)

        if new_palette != cur_palette:
            adjustments["palette_colors"] = new_palette

        if severity_value > thresholds.get("color_shift_critical", 60):
            adjustments["_hint"] = "色彩偏移极大，建议强化画风 Prompt 锚点而非仅增加调色板"

    elif issue_type == "animation_defect":
        # v2.0 新增：动画缺陷 → 注入对应核心规则的 prompt 补丁
        fix_prompt = get_defect_fix_prompt(str(severity_value), action_type)
        if fix_prompt:
            adjustments["_prompt_patch"] = fix_prompt
            adjustments["_hint"] = f"动画缺陷 '{severity_value}' → 已注入核心规则 Prompt 补丁"
        else:
            adjustments["_hint"] = f"动画缺陷 '{severity_value}' → 未找到匹配的核心规则"

    return adjustments


# ─────────────────────────────────────────────
# 5. 获取动作的已知缺陷列表（用于 QC 语义检测）
# ─────────────────────────────────────────────

def get_common_defects(action_type):
    """返回指定动作的常见缺陷列表"""
    kb = _load()
    action_kb = kb.get("action_knowledge", {}).get(action_type, {})
    return action_kb.get("common_defects", [])


def get_critical_frames(action_type):
    """返回指定动作的关键帧描述列表"""
    kb = _load()
    action_kb = kb.get("action_knowledge", {}).get(action_type, {})
    return action_kb.get("critical_frames", [])


# ─────────────────────────────────────────────
# 6. [v2.0新增] 核心规则查询 / 缺陷诊断 / VFX 知识
# ─────────────────────────────────────────────

def get_core_rules_for_action(action_type):
    """返回指定动作应用的核心规则列表"""
    kb = _load()
    action_kb = kb.get("action_knowledge", {}).get(action_type, {})
    return action_kb.get("core_rules_applied", [])


def get_style_prompts():
    """返回当前画风的正向和负向基础 Prompt"""
    kb = _load()
    if not kb:
        return "", ""
    style = kb.get("style_foundation", {})
    if style:
        return style.get("positive_prompt_base", ""), style.get("negative_prompt_base", "")
    # v1 兼容
    old = kb.get("style_consistency", {})
    return ", ".join(old.get("positive_prompt_anchors", [])), ", ".join(old.get("negative_prompt_anchors", []))


def get_vfx_knowledge(effect_type):
    """返回指定特效类型的知识 (smoke/fire/explosion/water_splash)"""
    kb = _load()
    if not kb:
        return {}
    return kb.get("vfx_knowledge", {}).get(effect_type, {})


# ── 缺陷 → 核心规则映射 ──

_DEFECT_TO_RULE_MAP = {
    "滑步": "walk_heel_kickback_arc",
    "slide": "walk_heel_kickback_arc",
    "no_anticipation": "anticipation",
    "无预备": "anticipation",
    "僵硬": "contrapposto",
    "stiff": "contrapposto",
    "直线运动": "arc_of_motion",
    "linear": "arc_of_motion",
    "无跟随": "follow_through",
    "no_follow": "follow_through",
    "漂浮": "jump_anticipation",
    "floating": "jump_anticipation",
    "无空中帧": "run_aerial_phase",
    "no_aerial": "run_aerial_phase",
    "风向不一致": "wind_force",
    "wind_inconsistent": "wind_force",
}

_RULE_TO_PROMPT_FIX = {
    "walk_heel_kickback_arc": "trailing heel kicks up backward first then swings forward in arc, NOT straight slide forward, heel-to-toe foot roll",
    "anticipation": "clear anticipation before fast action, wind-up pull back, crouch before jump takeoff",
    "contrapposto": "contrapposto, opposing forces, natural S-curve pose, weight shift, not stiff",
    "arc_of_motion": "smooth arc motion trajectory, curved path for all body parts, no linear interpolation between keyframes",
    "follow_through": "hair follow-through 2-3 frame delay, cloth wave propagation, cape flowing, soft elements lag behind main body",
    "jump_anticipation": "anticipation squat with forward lean before takeoff, landing with deep knee bend cushion",
    "run_aerial_phase": "aerial phase with both feet off ground, bow-shaped body, strong forward lean",
    "wind_force": "unified wind direction for all elements, front clothes clinging, back clothes flowing downwind",
}


def get_defect_fix_prompt(defect_keyword, action_type=""):
    """
    根据缺陷关键词返回修复用的 Prompt 补丁。
    先查缺陷映射表，再回退到动作的 prompt_boost。
    """
    for key, rule_name in _DEFECT_TO_RULE_MAP.items():
        if key in str(defect_keyword).lower():
            fix = _RULE_TO_PROMPT_FIX.get(rule_name, "")
            if fix:
                return fix

    kb = _load()
    if kb and action_type:
        action_kb = kb.get("action_knowledge", {}).get(action_type, {})
        return action_kb.get("prompt_boost", "")
    return ""


# ─────────────────────────────────────────────
# 独立运行：打印知识库摘要
# ─────────────────────────────────────────────

if __name__ == "__main__":
    print("=== 知识库 v2.0 自测 ===")
    kb = get_knowledge()
    if not kb:
        print("[错误] 知识库文件不存在或为空")
    else:
        print(f"版本: {kb['_meta']['version']}")
        sf = kb.get('style_foundation', {})
        if sf:
            print(f"画风: {sf.get('current_style', '?')}")
            print(f"废弃: {sf.get('deprecated_style', '?')}")
        print(f"动作知识: {list(kb.get('action_knowledge', {}).keys())}")
        print(f"核心动画规则: {list(kb.get('animation_core_rules', {}).keys())}")
        print(f"VFX 知识: {list(kb.get('vfx_knowledge', {}).keys())}")
        print(f"光影材质: {list(kb.get('lighting_material_rules', {}).keys())}")
        print(f"生成预设: {list(kb.get('generation_presets', {}).keys())}")
        print(f"QC 阈值: {len(kb.get('qc_thresholds', {}))} 项")

        print("\n--- 画风 Prompt ---")
        sp, sn = get_style_prompts()
        print(f"正向: {sp[:80]}...")
        print(f"负向: {sn[:80]}...")

        print("\n--- walk 增强 Prompt ---")
        pos, neg = enhance_prompt("walk", "character walking")
        print(f"正向 (前120字): {pos[:120]}...")

        print("\n--- jump 最优参数 ---")
        params = get_optimal_params("jump", {"width": 480, "height": 480, "steps": 6})
        print(f"参数: { {k: v for k, v in params.items() if k in ['width','height','length','steps','cfg']} }")

        print("\n--- walk 核心规则 ---")
        for r in get_core_rules_for_action("walk"):
            print(f"  - {r}")

        print("\n--- 缺陷修复: 滑步 ---")
        print(f"修复: {get_defect_fix_prompt('滑步', 'walk')}")

        print("\n=== 自测完成 ===")
