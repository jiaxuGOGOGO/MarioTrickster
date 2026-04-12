"""
MarioTrickster 自动质检 & 智能调参模块 v2.1（知识驱动版）
========================================================
跑完一次生成后，自动对比参考图与产出帧，检测四类问题：
  1. 裁切检测 — 角色是否被画布边缘截断
  2. 帧间一致性 — 帧与帧之间的结构漂移程度
  3. 色彩保真度 — 产出色彩是否偏离参考图
  4. 画面合规性 — 角色占比是否合理

所有阈值从 distilled_knowledge.json 动态读取。
调参建议由 knowledge_loader.compute_retune() 智能计算。
首次生成已用知识优化参数，QC 主要做验证而非大幅调参。
"""

import os
import json
import math
from pathlib import Path

try:
    from PIL import Image
    import numpy as np
    HAS_DEPS = True
except ImportError:
    HAS_DEPS = False

try:
    import knowledge_loader
    HAS_KNOWLEDGE = True
except ImportError:
    HAS_KNOWLEDGE = False


# ─────────────────────────────────────────────
# 质检结果数据结构
# ─────────────────────────────────────────────

class QCResult:
    """单次质检结果"""
    def __init__(self):
        self.passed = True
        self.issues = []
        self.adjustments = {}
        self.scores = {}

    def add_issue(self, issue_type, severity, detail, adjustment=None):
        self.passed = False
        self.issues.append({
            "type": issue_type,
            "severity": severity,
            "detail": detail,
        })
        if adjustment:
            real_adj = {k: v for k, v in adjustment.items() if not k.startswith("_")}
            self.adjustments.update(real_adj)
            hint = adjustment.get("_hint")
            if hint:
                self.issues.append({
                    "type": issue_type,
                    "severity": "info",
                    "detail": f"[知识提示] {hint}",
                })
            prompt_patch = adjustment.get("_prompt_patch")
            if prompt_patch:
                self.issues.append({
                    "type": issue_type,
                    "severity": "info",
                    "detail": f"[Prompt补丁] {prompt_patch[:80]}...",
                })

    def summary(self):
        lines = []
        if self.passed:
            lines.append("  \u2713 质检通过，无需调整")
        else:
            lines.append(f"  \u25b3 发现 {len(self.issues)} 个问题：")
            for iss in self.issues:
                icon = "\u2717" if iss["severity"] == "critical" else "\u25b3" if iss["severity"] == "warning" else "\u2139"
                lines.append(f"    {icon} [{iss['type']}] {iss['detail']}")
        if self.scores:
            score_parts = [f"{k}={v}" for k, v in self.scores.items()]
            lines.append(f"  [评分] {', '.join(score_parts)}")
        if self.adjustments:
            lines.append(f"  [智能调参] {json.dumps(self.adjustments, ensure_ascii=False)}")
        return "\n".join(lines)


# ─────────────────────────────────────────────
# 检测 1：裁切检测
# ─────────────────────────────────────────────

def check_crop(ref_image_path, frames_dir, current_config, action_type="custom"):
    result = QCResult()
    if not HAS_DEPS:
        return result

    thresholds = knowledge_loader.get_qc_thresholds() if HAS_KNOWLEDGE else {}
    edge_margin_ratio = thresholds.get("crop_edge_margin_ratio", 0.05)
    fill_threshold = thresholds.get("crop_fill_threshold", 0.20)

    nobg_dir = Path(frames_dir).parent / "02_nobg"
    if not nobg_dir.exists():
        nobg_dir = Path(frames_dir)

    frames = sorted(nobg_dir.glob("*.png"))
    if not frames:
        return result

    crop_count = 0
    for fp in frames[:5]:
        img = Image.open(fp).convert("RGBA")
        w, h = img.size
        alpha = np.array(img)[:, :, 3]
        margin_px = max(int(h * edge_margin_ratio), 2)
        top_fill = np.mean(alpha[:margin_px, :] > 128)
        bottom_fill = np.mean(alpha[-margin_px:, :] > 128)
        if top_fill > fill_threshold or bottom_fill > fill_threshold:
            crop_count += 1

    if crop_count >= 2:
        if HAS_KNOWLEDGE:
            adj = knowledge_loader.compute_retune("crop", crop_count, current_config, action_type)
        else:
            cur_w = current_config.get("width", 480)
            cur_h = current_config.get("height", 480)
            adj = {"height": min((int(cur_h * 1.2) // 16) * 16, 832)}
        result.add_issue("crop", "critical",
            f"角色被裁切（{crop_count}/{min(len(frames),5)} 帧触发边缘检测）", adj)

    result.scores["构图完整度"] = max(0, 100 - crop_count * 25)
    if crop_count == 0:
        result.passed = True
    return result


# ─────────────────────────────────────────────
# 检测 2：帧间一致性
# ─────────────────────────────────────────────

def check_consistency(frames_dir, current_config, action_type="custom"):
    result = QCResult()
    if not HAS_DEPS:
        return result

    thresholds = knowledge_loader.get_qc_thresholds() if HAS_KNOWLEDGE else {}
    max_diff_warning = thresholds.get("consistency_max_diff_warning", 20)
    avg_diff_acceptable = thresholds.get("consistency_avg_diff_acceptable", 12)

    nobg_dir = Path(frames_dir).parent / "02_nobg"
    if not nobg_dir.exists():
        nobg_dir = Path(frames_dir)

    frames = sorted(nobg_dir.glob("*.png"))
    if len(frames) < 2:
        return result

    diffs = []
    for i in range(len(frames) - 1):
        img_a = np.array(Image.open(frames[i]).convert("L")).astype(float)
        img_b = np.array(Image.open(frames[i + 1]).convert("L")).astype(float)
        diff_ratio = np.mean(np.abs(img_a - img_b) > 30) * 100
        diffs.append(diff_ratio)

    avg_diff = sum(diffs) / len(diffs) if diffs else 0
    max_diff = max(diffs) if diffs else 0
    result.scores["帧间一致性"] = max(0, 100 - int(avg_diff * 5))

    if max_diff > max_diff_warning:
        if HAS_KNOWLEDGE:
            adj = knowledge_loader.compute_retune("consistency", max_diff, current_config, action_type)
        else:
            adj = {"steps": min(current_config.get("steps", 6) + 4, 20)}
        result.add_issue("consistency", "warning",
            f"帧间形变较大（最大 {max_diff:.1f}%，平均 {avg_diff:.1f}%，阈值 {max_diff_warning}%）", adj)
    elif avg_diff > avg_diff_acceptable:
        result.add_issue("consistency", "info",
            f"帧间有轻微漂移（平均 {avg_diff:.1f}%，阈值 {avg_diff_acceptable}%），可接受")

    return result


# ─────────────────────────────────────────────
# 检测 3：色彩保真度
# ─────────────────────────────────────────────

def check_color_fidelity(ref_image_path, frames_dir, current_config, action_type="custom"):
    result = QCResult()
    if not HAS_DEPS:
        return result

    thresholds = knowledge_loader.get_qc_thresholds() if HAS_KNOWLEDGE else {}
    color_shift_warning = thresholds.get("color_shift_warning", 35)
    saturation_loss_warning = thresholds.get("saturation_loss_warning", 15)

    nobg_dir = Path(frames_dir).parent / "02_nobg"
    if not nobg_dir.exists():
        nobg_dir = Path(frames_dir)

    frames = sorted(nobg_dir.glob("*.png"))
    if not frames:
        return result

    ref = Image.open(ref_image_path).convert("RGBA")
    ref_arr = np.array(ref)
    ref_mask = ref_arr[:, :, 3] > 128
    if ref_mask.sum() == 0:
        return result

    ref_rgb = ref_arr[:, :, :3][ref_mask]
    ref_mean = ref_rgb.mean(axis=0)
    ref_std = ref_rgb.std(axis=0)

    frame_means = []
    for fp in frames[:5]:
        img = Image.open(fp).convert("RGBA")
        arr = np.array(img)
        mask = arr[:, :, 3] > 128
        if mask.sum() > 0:
            frame_means.append(arr[:, :, :3][mask].mean(axis=0))

    if not frame_means:
        return result

    output_mean = np.mean(frame_means, axis=0)
    color_shift = float(np.sqrt(np.sum((ref_mean - output_mean) ** 2)))

    ref_saturation = float(ref_std.mean())
    output_sats = []
    for fp in frames[:3]:
        img = Image.open(fp).convert("RGBA")
        arr = np.array(img)
        mask = arr[:, :, 3] > 128
        if mask.sum() > 0:
            output_sats.append(float(arr[:, :, :3][mask].std(axis=0).mean()))
    output_saturation = sum(output_sats) / len(output_sats) if output_sats else 0
    saturation_loss = max(0, ref_saturation - output_saturation)

    result.scores["色彩保真度"] = max(0, 100 - int(color_shift / 2))

    if color_shift > color_shift_warning:
        if HAS_KNOWLEDGE:
            adj = knowledge_loader.compute_retune("color", color_shift, current_config, action_type)
        else:
            adj = {}
        result.add_issue("color", "warning",
            f"色彩偏移 {color_shift:.0f}（阈值 {color_shift_warning}，参考 RGB={ref_mean.astype(int).tolist()}，产出 RGB={output_mean.astype(int).tolist()}）", adj)

    if saturation_loss > saturation_loss_warning:
        result.add_issue("color", "info",
            f"饱和度下降 {saturation_loss:.0f}（参考 {ref_saturation:.0f} → 产出 {output_saturation:.0f}），阈值 {saturation_loss_warning}")

    return result


# ─────────────────────────────────────────────
# 检测 4：画面合规性（角色占比 + 轮廓清晰度）
# ─────────────────────────────────────────────

def check_visual_compliance(frames_dir, current_config, action_type="custom"):
    """
    检测产出帧的画面合规性：
    - 角色占比：不透明像素是否在合理范围
    - 轮廓清晰度：边缘是否有清晰的描边（trickster_style 核心特征）
    """
    result = QCResult()
    if not HAS_DEPS:
        return result

    thresholds = knowledge_loader.get_qc_thresholds() if HAS_KNOWLEDGE else {}
    min_opaque = thresholds.get("min_opaque_pixel_ratio", 0.10)
    max_opaque = thresholds.get("max_opaque_pixel_ratio", 0.70)

    nobg_dir = Path(frames_dir).parent / "02_nobg"
    if not nobg_dir.exists():
        nobg_dir = Path(frames_dir)

    frames = sorted(nobg_dir.glob("*.png"))
    if not frames:
        return result

    opaque_ratios = []
    edge_sharpness_scores = []
    for fp in frames[:5]:
        img = Image.open(fp).convert("RGBA")
        alpha = np.array(img)[:, :, 3]
        opaque_ratio = np.mean(alpha > 128)
        opaque_ratios.append(opaque_ratio)

        # 轮廓清晰度：计算 alpha 边缘的锐利程度
        # 清晰描边 = alpha 从 0 到 255 的过渡区域很窄
        mid_alpha = np.logical_and(alpha > 30, alpha < 225)
        edge_ratio = np.mean(mid_alpha)
        # edge_ratio 越低越好（说明边缘锐利）
        sharpness = max(0, 100 - int(edge_ratio * 500))
        edge_sharpness_scores.append(sharpness)

    avg_opaque = sum(opaque_ratios) / len(opaque_ratios) if opaque_ratios else 0
    avg_sharpness = sum(edge_sharpness_scores) / len(edge_sharpness_scores) if edge_sharpness_scores else 100

    score = 100
    if avg_opaque < min_opaque:
        score -= 30
        result.add_issue("visual_compliance", "warning",
            f"角色太小（不透明像素仅占 {avg_opaque*100:.1f}%，最低 {min_opaque*100:.0f}%），画布可能过大")
    elif avg_opaque > max_opaque:
        score -= 20
        result.add_issue("visual_compliance", "info",
            f"角色占比偏高（{avg_opaque*100:.1f}%，上限 {max_opaque*100:.0f}%），可能影响动作展示空间")

    if avg_sharpness < 60:
        score -= 20
        result.add_issue("visual_compliance", "warning",
            f"轮廓模糊（清晰度 {avg_sharpness:.0f}/100），trickster_style 要求清晰黑描边")

    result.scores["画面合规"] = score
    if not result.issues:
        result.passed = True
    return result


# ─────────────────────────────────────────────
# 主入口：运行全部质检
# ─────────────────────────────────────────────

def run_qc(ref_image_path, output_dir, current_config, action_type="custom"):
    """
    运行全部质检项，返回汇总结果。
    """
    kb_status = "知识驱动" if HAS_KNOWLEDGE else "基础模式"
    print(f"\n[质检] 自动对比参考图与产出结果（{kb_status}）...")

    frames_dir = Path(output_dir) / "01_frames"
    if not frames_dir.exists():
        print("  [跳过] 未找到帧目录，跳过质检")
        r = QCResult()
        r.passed = True
        return r

    crop_result = check_crop(ref_image_path, frames_dir, current_config, action_type)
    consistency_result = check_consistency(frames_dir, current_config, action_type)
    color_result = check_color_fidelity(ref_image_path, frames_dir, current_config, action_type)
    visual_result = check_visual_compliance(frames_dir, current_config, action_type)

    final = QCResult()
    for r in [crop_result, consistency_result, color_result, visual_result]:
        final.issues.extend(r.issues)
        final.scores.update(r.scores)
        final.adjustments.update(r.adjustments)

    final.passed = all(r.passed for r in [crop_result, consistency_result, color_result, visual_result])

    print(final.summary())

    report_path = Path(output_dir) / "qc_report.json"
    report = {
        "passed": final.passed,
        "knowledge_driven": HAS_KNOWLEDGE,
        "scores": final.scores,
        "issues": final.issues,
        "adjustments": final.adjustments,
    }
    with open(report_path, "w", encoding="utf-8") as f:
        json.dump(report, f, indent=2, ensure_ascii=False)
    print(f"  [质检报告] → {report_path}")

    return final


# ─────────────────────────────────────────────
# 独立运行
# ─────────────────────────────────────────────

if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="对已有管线产出进行质检（知识驱动版）")
    parser.add_argument("--ref", required=True, help="参考图路径")
    parser.add_argument("--output-dir", default="./output", help="管线输出目录")
    parser.add_argument("--action", default="custom", help="动作类型")
    args = parser.parse_args()

    import config_manager
    config = config_manager.load_config()
    flat_config = {
        "server": config.get("server", "127.0.0.1:8188"),
        **config.get("generation", {}),
        **config.get("postprocess", {}),
    }

    result = run_qc(args.ref, args.output_dir, flat_config, args.action)
    if not result.passed:
        print(f"\n建议调参后重跑: {result.adjustments}")
