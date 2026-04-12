#!/usr/bin/env python3
"""
MarioTrickster 蒸馏知识加载器
==============================
统一加载 distilled_knowledge.json，为管线各环节提供知识驱动的参数和规则。

职责：
  1. 加载并缓存知识库
  2. 根据 action 类型返回最优首次生成参数
  3. 构建知识增强的 Prompt（正向 + 负向）
  4. 提供 QC 阈值和智能调参计算
  5. 支持热更新：PDF 蒸馏新知识后，下次调用自动生效
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
    用蒸馏知识增强 Prompt。

    策略：
    - positive: 追加 action_knowledge.prompt_boost + style_consistency.positive_prompt_anchors
    - negative: 追加 pixel_art_rules.negative_prompt_additions + style_consistency.negative_prompt_anchors

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

    # ── 正向增强 ──
    boosts = []

    # 动作专属知识
    action_kb = kb.get("action_knowledge", {}).get(action_type, {})
    if action_kb.get("prompt_boost"):
        boosts.append(action_kb["prompt_boost"])

    # 画风一致性锚点
    style = kb.get("style_consistency", {})
    anchors = style.get("positive_prompt_anchors", [])
    if anchors:
        boosts.extend(anchors)

    enhanced_positive = base_positive
    if boosts:
        # 去重：不重复添加已有的词
        existing_lower = base_positive.lower()
        new_boosts = [b for b in boosts if b.lower() not in existing_lower]
        if new_boosts:
            enhanced_positive = base_positive + ", " + ", ".join(new_boosts)

    # ── 负向增强 ──
    neg_additions = []

    # 像素风格负面词
    pixel_rules = kb.get("pixel_art_rules", {})
    neg_additions.extend(pixel_rules.get("negative_prompt_additions", []))

    # 画风一致性负面词
    neg_additions.extend(style.get("negative_prompt_anchors", []))

    enhanced_negative = base_negative
    if neg_additions:
        existing_neg_lower = base_negative.lower()
        new_negs = [n for n in neg_additions if n.lower() not in existing_neg_lower]
        if new_negs:
            enhanced_negative = base_negative + ", " + ", ".join(new_negs)

    return enhanced_positive, enhanced_negative


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
    根据知识库的 retune_strategy 计算精确的调参值。

    参数:
        issue_type: "crop" / "consistency" / "color"
        severity_value: 问题严重程度数值（如 diff_ratio, color_shift 等）
        current_config: 当前配置
        action_type: 动作类型

    返回:
        dict: 调参建议 {param_name: new_value}
    """
    kb = _load()
    thresholds = get_qc_thresholds()
    adjustments = {}

    if issue_type == "crop":
        # 根据 composition_rules 计算目标尺寸
        comp = kb.get("composition_rules", {})
        aspect = comp.get("aspect_ratio_by_action", {}).get(action_type, [1, 1])
        cur_w = current_config.get("width", 480)
        cur_h = current_config.get("height", 480)

        if aspect[0] != aspect[1]:
            # 非正方形画布
            target_w = max(cur_w, int(480 * aspect[0] / min(aspect)))
            target_h = max(cur_h, int(480 * aspect[1] / min(aspect)))
        else:
            # 正方形但裁切了，扩大 20%
            target_w = min(int(cur_w * 1.2), 832)
            target_h = min(int(cur_h * 1.2), 832)

        # 对齐 16 的倍数
        target_w = (target_w // 16) * 16
        target_h = (target_h // 16) * 16

        if target_w != cur_w:
            adjustments["width"] = target_w
        if target_h != cur_h:
            adjustments["height"] = target_h

    elif issue_type == "consistency":
        cur_steps = current_config.get("steps", 6)
        threshold = thresholds.get("consistency_max_diff_warning", 20)
        # 智能计算：超出阈值越多，增加越多步数
        excess = max(0, severity_value - threshold)
        step_increase = max(2, math.ceil(excess / 5) * 2)
        new_steps = min(cur_steps + step_increase, 20)

        if new_steps != cur_steps:
            adjustments["steps"] = new_steps

        # 如果 steps 已经很高但仍不一致，提示可能是驱动视频问题
        if cur_steps >= 12 and severity_value > thresholds.get("consistency_max_diff_critical", 35):
            adjustments["_hint"] = "steps 已较高但仍不一致，可能需要检查驱动视频质量"

    elif issue_type == "color":
        cur_palette = current_config.get("palette_colors", 32)
        threshold = thresholds.get("color_shift_warning", 35)
        # 智能计算：偏移越大，增加越多颜色
        excess = max(0, severity_value - threshold)
        palette_increase = max(8, math.ceil(excess / 10) * 8)
        new_palette = min(cur_palette + palette_increase, 128)

        if new_palette != cur_palette:
            adjustments["palette_colors"] = new_palette

        # 如果偏移极大，可能是 prompt 问题
        if severity_value > thresholds.get("color_shift_critical", 60):
            adjustments["_hint"] = "色彩偏移极大，可能需要调整 Prompt 而非仅增加调色板"

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
# 独立运行：打印知识库摘要
# ─────────────────────────────────────────────

if __name__ == "__main__":
    kb = get_knowledge()
    if not kb:
        print("[错误] 知识库文件不存在或为空")
    else:
        print(f"知识库版本: {kb.get('_meta', {}).get('version', '?')}")
        print(f"像素规则: {len(kb.get('pixel_art_rules', {}).get('outline_rules', []))} 条")
        print(f"动作知识: {list(kb.get('action_knowledge', {}).keys())}")
        print(f"生成预设: {list(kb.get('generation_presets', {}).keys())}")
        print(f"QC 阈值: {len(kb.get('qc_thresholds', {}))} 项")

        # 演示：获取 jump 的最优参数
        from config_manager import load_config
        cfg = load_config()
        flat = {**cfg.get("generation", {}), **cfg.get("postprocess", {})}
        optimal = get_optimal_params("jump", flat)
        print(f"\njump 最优参数: {json.dumps({k: v for k, v in optimal.items() if k in ['width','height','length','steps','cfg']}, indent=2)}")
