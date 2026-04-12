#!/usr/bin/env python3
"""
knowledge_loader.py v2.2 — MarioTrickster 蒸馏知识加载器
========================================================
从 distilled_knowledge.json 加载全量蒸馏知识，为管线提供：
  1. get_optimal_params(action, base_cfg, project_overrides, runtime) → 首次出图最优参数
  2. get_runtime_safe_params(params, runtime)                          → 12GB/项目档位显存护栏
  3. enhance_prompt(action, pos, neg)                                  → 知识增强 Prompt
  4. get_qc_thresholds()                                               → QC 阈值
  5. compute_retune(issue, severity, cfg, action)                      → 智能调参
  6. get_blender_settings(action)                                      → Blender 渲染参数
  7. get_mixamo_preset(action)                                         → Mixamo 搜索参数
  8. get_defect_fix_prompt(defect, action)                             → 缺陷修复 Prompt 补丁
"""

import json
import math
import os

_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "distilled_knowledge.json")
_cache = {"data": None, "mtime": 0}

_BASE_12GB_BUDGET = 480 * 480 * 17 * 6
_DIM_FLOOR = 256
_MIN_LENGTH = 9
_MIN_STEPS = 4


def _load():
    try:
        mt = os.path.getmtime(_PATH)
        if _cache["data"] is None or mt > _cache["mtime"]:
            with open(_PATH, "r", encoding="utf-8") as f:
                _cache["data"] = json.load(f)
                _cache["mtime"] = mt
    except Exception:
        pass
    return _cache["data"] or {}


def get_knowledge():
    return _load()


# ── 通用工具 ──

def _align_down_16(value, floor=_DIM_FLOOR):
    value = max(int(value or floor), floor)
    return max(floor, (value // 16) * 16)


def _normalize_length(value):
    value = max(int(value or _MIN_LENGTH), _MIN_LENGTH)
    normalized = ((value - 1) // 4) * 4 + 1
    return max(_MIN_LENGTH, normalized)


def _normalize_steps(value):
    return max(_MIN_STEPS, int(value or _MIN_STEPS))


def _estimate_cost(params):
    width = int(params.get("width", 480) or 480)
    height = int(params.get("height", 480) or 480)
    length = int(params.get("length", 17) or 17)
    steps = int(params.get("steps", 6) or 6)
    return width * height * length * steps


def get_runtime_safe_params(params, runtime=None):
    """
    根据运行档位收敛生成参数，默认以 12GB 显存为安全基线。
    返回: (safe_params, guard_info)
    """
    runtime = dict(runtime or {})
    safe = dict(params or {})
    guard = {
        "changed": [],
        "vram_gb": float(runtime.get("vram_gb", 12) or 12),
        "profile": runtime.get("project_profile", "mariotrickster_12gb_safe"),
    }

    if not runtime.get("enforce_vram_guard", True):
        return safe, guard

    for dim in ("width", "height"):
        if dim in safe:
            new_val = _align_down_16(safe[dim])
            if new_val != safe[dim]:
                guard["changed"].append((dim, safe[dim], new_val))
                safe[dim] = new_val

    if "length" in safe:
        new_len = _normalize_length(safe["length"])
        if new_len != safe["length"]:
            guard["changed"].append(("length", safe["length"], new_len))
            safe["length"] = new_len

    if "steps" in safe:
        new_steps = _normalize_steps(safe["steps"])
        if new_steps != safe["steps"]:
            guard["changed"].append(("steps", safe["steps"], new_steps))
            safe["steps"] = new_steps

    vram_gb = max(4.0, guard["vram_gb"])
    budget = int(_BASE_12GB_BUDGET * (vram_gb / 12.0))

    if vram_gb <= 12:
        preferred_caps = {"length": 17, "steps": 6}
    elif vram_gb <= 16:
        preferred_caps = {"length": 21, "steps": 8}
    else:
        preferred_caps = {"length": 33, "steps": 10}

    for key, cap in preferred_caps.items():
        if key in safe and safe[key] > cap:
            guard["changed"].append((key, safe[key], cap))
            safe[key] = cap

    current_cost = _estimate_cost(safe)

    if current_cost > budget and "steps" in safe and safe["steps"] > _MIN_STEPS:
        target_steps = max(_MIN_STEPS, min(safe["steps"], int(budget / max(1, safe["width"] * safe["height"] * safe["length"])) ))
        target_steps = max(_MIN_STEPS, target_steps)
        if target_steps < safe["steps"]:
            guard["changed"].append(("steps", safe["steps"], target_steps))
            safe["steps"] = target_steps
            current_cost = _estimate_cost(safe)

    if current_cost > budget and "length" in safe and safe["length"] > _MIN_LENGTH:
        target_length = int(budget / max(1, safe["width"] * safe["height"] * safe["steps"]))
        target_length = _normalize_length(min(safe["length"], target_length))
        target_length = max(_MIN_LENGTH, target_length)
        if target_length < safe["length"]:
            guard["changed"].append(("length", safe["length"], target_length))
            safe["length"] = target_length
            current_cost = _estimate_cost(safe)

    if current_cost > budget and "width" in safe and "height" in safe:
        ratio = math.sqrt(budget / current_cost)
        new_w = _align_down_16(max(_DIM_FLOOR, int(safe["width"] * ratio)))
        new_h = _align_down_16(max(_DIM_FLOOR, int(safe["height"] * ratio)))
        if new_w != safe["width"]:
            guard["changed"].append(("width", safe["width"], new_w))
            safe["width"] = new_w
        if new_h != safe["height"]:
            guard["changed"].append(("height", safe["height"], new_h))
            safe["height"] = new_h
        current_cost = _estimate_cost(safe)

    guard["budget"] = budget
    guard["final_cost"] = current_cost
    return safe, guard


# ── 1. 首次出图参数 ──

def get_optimal_params(action_type, base_config=None, project_overrides=None, runtime=None):
    kb = _load()
    result = dict(base_config) if base_config else {}
    act = kb.get("actions", {}).get(action_type, {})
    gen = act.get("gen", kb.get("actions", {}).get("idle", {}).get("gen", {}))
    mapping = {"w": "width", "h": "height", "len": "length", "steps": "steps", "cfg": "cfg"}
    for short, full in mapping.items():
        if short in gen:
            result[full] = gen[short]

    override = (project_overrides or {}).get(action_type, {})
    for key in ("width", "height", "length", "steps", "cfg", "pixel_size", "palette_colors"):
        if key in override:
            result[key] = override[key]

    safe_result, _ = get_runtime_safe_params(result, runtime)
    return safe_result


# ── 2. Prompt 增强 ──

def enhance_prompt(action_type, base_positive="", base_negative=""):
    kb = _load()
    if not kb:
        return base_positive, base_negative

    pos = [base_positive] if base_positive else []
    neg = [base_negative] if base_negative else []

    # 画风基础
    style = kb.get("style", {})
    sp = style.get("positive_base", "")
    sn = style.get("negative_base", "")
    if sp:
        pos.append(sp)
    if sn:
        neg.append(sn)

    # 动作专属
    act = kb.get("actions", {}).get(action_type, {})
    boost = act.get("boost", "")
    if boost:
        pos.append(boost)
    defects = act.get("defects", [])
    if defects:
        neg.extend(defects[:3])

    # 核心动画规则 tags
    anim = kb.get("animation_rules", {})
    for key in ("contrapposto", "arc_of_motion", "follow_through", "anticipation", "gravity_tilt"):
        rule = anim.get(key, {})
        if isinstance(rule, dict):
            tags = rule.get("tags", "")
            if tags:
                pos.append(tags)

    # 风向负向
    wind_neg = kb.get("lighting", {}).get("wind", {}).get("neg", "")
    if wind_neg:
        neg.append(wind_neg)

    # 去重拼接
    all_pos = ", ".join(pos)
    all_neg = ", ".join(neg)
    final_pos = ", ".join(dict.fromkeys(p.strip() for p in all_pos.split(",") if p.strip()))
    final_neg = ", ".join(dict.fromkeys(p.strip() for p in all_neg.split(",") if p.strip()))
    return final_pos, final_neg


# ── 3. QC 阈值 ──

def get_qc_thresholds():
    kb = _load()
    qc = kb.get("qc", {})
    return {
        "crop_edge_margin_ratio": qc.get("crop_margin", 0.05),
        "crop_fill_threshold": qc.get("crop_fill", 0.20),
        "consistency_max_diff_warning": qc.get("consistency_warn", 20),
        "consistency_max_diff_critical": qc.get("consistency_crit", 35),
        "consistency_avg_diff_acceptable": qc.get("consistency_avg_ok", 12),
        "color_shift_warning": qc.get("color_warn", 35),
        "color_shift_critical": qc.get("color_crit", 60),
        "saturation_loss_warning": qc.get("sat_loss_warn", 15),
        "min_opaque_pixel_ratio": qc.get("opaque_min", 0.10),
        "max_opaque_pixel_ratio": qc.get("opaque_max", 0.70),
    }


# ── 4. 智能调参 ──

def compute_retune(issue_type, severity_value, current_config, action_type="idle"):
    kb = _load()
    thresholds = get_qc_thresholds()
    adj = {}

    if issue_type == "crop":
        act = kb.get("actions", {}).get(action_type, {})
        gen = act.get("gen", {})
        cur_w = current_config.get("width", 480)
        cur_h = current_config.get("height", 480)

        # idle 在 12GB 安全档位下固定 480x480，不允许 QC 自动放大。
        if action_type == "idle" and int(cur_w) == 480 and int(cur_h) == 480:
            adj["_hint"] = "idle 维持 480x480 安全档位；若仍有裁切，优先检查 drive video 构图、代理人体缩放或角色占画面比例"
            return adj

        tgt_w = gen.get("w", cur_w)
        tgt_h = gen.get("h", cur_h)
        # 如果已经是目标尺寸还裁切，扩大20%
        if cur_w >= tgt_w and cur_h >= tgt_h:
            tgt_w = min(int(cur_w * 1.2), 832)
            tgt_h = min(int(cur_h * 1.2), 832)
        tgt_w = (max(tgt_w, cur_w) // 16) * 16
        tgt_h = (max(tgt_h, cur_h) // 16) * 16
        if tgt_w != cur_w:
            adj["width"] = tgt_w
        if tgt_h != cur_h:
            adj["height"] = tgt_h

    elif issue_type == "consistency":
        cur_steps = current_config.get("steps", 6)
        threshold = thresholds["consistency_max_diff_warning"]
        excess = max(0, severity_value - threshold)
        boost = max(2, math.ceil(excess / 5) * 2)
        new_steps = min(cur_steps + boost, 20)
        if new_steps != cur_steps:
            adj["steps"] = new_steps
        if cur_steps >= 12:
            adj["_hint"] = "steps已高仍不一致→检查驱动视频"

    elif issue_type == "color":
        if severity_value > thresholds["color_shift_critical"]:
            adj["_hint"] = "严重色偏→检查Prompt准确性"
        else:
            adj["_hint"] = "色偏→强化画风Prompt锚点"

    elif issue_type == "animation_defect":
        fix = get_defect_fix_prompt(str(severity_value), action_type)
        if fix:
            adj["_prompt_patch"] = fix

    return adj


# ── 5. Blender / Mixamo ──

def get_blender_settings(action_type):
    kb = _load()
    return kb.get("actions", {}).get(action_type, {}).get("blender", {})


def get_mixamo_preset(action_type):
    kb = _load()
    return kb.get("actions", {}).get(action_type, {}).get("mixamo", {})


# ── 6. 便捷查询 ──

def get_style_prompts():
    kb = _load()
    s = kb.get("style", {})
    return s.get("positive_base", ""), s.get("negative_base", "")


def get_lora_config():
    kb = _load()
    return kb.get("style", {}).get("lora", {})


def get_common_defects(action_type):
    kb = _load()
    return kb.get("actions", {}).get(action_type, {}).get("defects", [])


def get_critical_frames(action_type):
    kb = _load()
    return kb.get("actions", {}).get(action_type, {}).get("frames", [])


def get_core_rules_for_action(action_type):
    kb = _load()
    return kb.get("actions", {}).get(action_type, {}).get("rules", [])


def get_vfx_knowledge(effect_type):
    kb = _load()
    return kb.get("vfx", {}).get(effect_type, {})


# ── 7. 缺陷修复 ──
_DEFECT_FIX = {
    "滑步": "trailing heel kicks up backward first then swings forward in arc, NOT straight slide",
    "slide": "trailing heel kicks up backward first then swings forward in arc, NOT straight slide",
    "无预备": "clear anticipation before fast action, wind-up pull back, crouch before jump",
    "no_anticipation": "clear anticipation before fast action, wind-up pull back, crouch before jump",
    "僵硬": "contrapposto, opposing forces, natural S-curve pose, weight shift",
    "stiff": "contrapposto, opposing forces, natural S-curve pose, weight shift",
    "直线": "smooth arc motion, curved path for all body parts, no linear interpolation",
    "linear": "smooth arc motion, curved path for all body parts, no linear interpolation",
    "无跟随": "hair follow-through 2-3 frame delay, cloth wave propagation, cape flowing",
    "no_follow": "hair follow-through 2-3 frame delay, cloth wave propagation, cape flowing",
    "漂浮": "anticipation squat with forward lean before takeoff, landing deep knee bend",
    "floating": "anticipation squat with forward lean before takeoff, landing deep knee bend",
    "风向": "unified wind direction, front clothes clinging, back clothes flowing downwind",
}


def get_defect_fix_prompt(defect_keyword, action_type=""):
    kw = str(defect_keyword).lower()
    for key, fix in _DEFECT_FIX.items():
        if key in kw:
            return fix
    # 回退到动作 boost
    kb = _load()
    if action_type:
        return kb.get("actions", {}).get(action_type, {}).get("boost", "")
    return ""


# ── 自测 ──
if __name__ == "__main__":
    kb = get_knowledge()
    print(f"v{kb['_meta']['version']} | style: {kb['style']['visual_identity'][:40]}...")
    print(f"actions: {list(kb['actions'].keys())}")

    sp, sn = get_style_prompts()
    print(f"\nstyle+: {sp[:80]}...")
    print(f"style-: {sn[:80]}...")

    print(f"\nlora: {get_lora_config()}")

    pos, neg = enhance_prompt("walk", "character walking")
    print(f"\nwalk+: {pos[:120]}...")

    p = get_optimal_params("jump", {"width": 480, "height": 480, "steps": 6})
    print(f"\njump params: {p}")

    safe, guard = get_runtime_safe_params({"width": 640, "height": 480, "length": 21, "steps": 8}, {"vram_gb": 12})
    print(f"\nruntime safe: {safe} | guard={guard}")

    print(f"\nwalk rules: {get_core_rules_for_action('walk')}")
    print(f"defect fix '滑步': {get_defect_fix_prompt('滑步', 'walk')}")
    print(f"blender idle: {get_blender_settings('idle')}")
    print(f"mixamo run: {get_mixamo_preset('run')}")
