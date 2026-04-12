#!/usr/bin/env python3
"""
MarioTrickster 批量动画生成器
==============================
一次性为角色生成全套动作 Sprite Sheet。

用法:
  python batch_generate.py --ref trickster.png --video-dir ./mixamo_videos/

视频目录结构（按动作命名）:
  mixamo_videos/
    ├── run.mp4
    ├── idle.mp4
    ├── jump.mp4
    ├── attack.mp4
    ├── walk.mp4
    └── death.mp4

脚本会自动匹配文件名到动作类型，逐个生成并拼合。
"""

import argparse
import os
import sys
import json
import subprocess
from pathlib import Path

# 动作名称 → 文件名关键词映射
ACTION_KEYWORDS = {
    "run":    ["run", "running", "sprint", "dash"],
    "idle":   ["idle", "breathing", "stand", "rest"],
    "jump":   ["jump", "jumping", "leap"],
    "attack": ["attack", "slash", "sword", "hit", "strike", "punch", "kick"],
    "walk":   ["walk", "walking", "stroll"],
    "death":  ["death", "die", "dying", "fall", "defeat"],
}


def detect_action(filename):
    """根据文件名自动检测动作类型"""
    name = Path(filename).stem.lower()
    for action, keywords in ACTION_KEYWORDS.items():
        for kw in keywords:
            if kw in name:
                return action
    return "custom"


import config_manager

def main():
    config = config_manager.load_config()
    default_server = config.get("server", "127.0.0.1:8188")

    parser = argparse.ArgumentParser(description="MarioTrickster 批量动画生成")
    parser.add_argument("--ref", required=True, help="参考图路径")
    parser.add_argument("--video-dir", required=True, help="驱动视频目录")
    parser.add_argument("--output", default="./batch_output", help="输出根目录")
    parser.add_argument("--server", default=default_server, help="ComfyUI 地址")
    parser.add_argument("--postprocess-only", action="store_true", help="仅后处理已有视频")
    args = parser.parse_args()

    video_dir = Path(args.video_dir)
    if not video_dir.exists():
        print(f"[错误] 视频目录不存在: {video_dir}")
        sys.exit(1)

    # 扫描视频文件
    video_exts = {".mp4", ".mov", ".webm", ".avi", ".mkv"}
    videos = [f for f in video_dir.iterdir() if f.suffix.lower() in video_exts]

    if not videos:
        print(f"[错误] 目录中没有视频文件: {video_dir}")
        sys.exit(1)

    print(f"找到 {len(videos)} 个视频文件:")
    tasks = []
    for v in sorted(videos):
        action = detect_action(v.name)
        print(f"  {v.name} → 动作类型: [{action}]")
        tasks.append((v, action))

    print(f"\n开始批量生成...")
    print("=" * 60)

    results = []
    for i, (video_path, action) in enumerate(tasks, 1):
        print(f"\n[{i}/{len(tasks)}] 处理: {video_path.name} ({action})")
        output_dir = Path(args.output) / action

        cmd = [
            sys.executable, "run_pipeline.py",
            "--video", str(video_path),
            "--action", action,
            "--output", str(output_dir),
            "--server", args.server,
        ]

        if args.postprocess_only:
            cmd.append("--postprocess-only")
        else:
            cmd.extend(["--ref", args.ref])

        try:
            result = subprocess.run(cmd, check=True, capture_output=False)
            results.append({"action": action, "status": "success", "output": str(output_dir)})
        except subprocess.CalledProcessError as e:
            print(f"  [失败] {action}: {e}")
            results.append({"action": action, "status": "failed", "error": str(e)})

    # 汇总报告
    print("\n" + "=" * 60)
    print("批量生成完成！汇总:")
    print("=" * 60)
    for r in results:
        status = "✓" if r["status"] == "success" else "✗"
        print(f"  {status} {r['action']:10s} → {r.get('output', r.get('error', ''))}")

    # 保存报告
    report_path = Path(args.output) / "batch_report.json"
    report_path.parent.mkdir(parents=True, exist_ok=True)
    with open(report_path, "w") as f:
        json.dump(results, f, indent=2, ensure_ascii=False)
    print(f"\n报告已保存: {report_path}")


if __name__ == "__main__":
    main()
