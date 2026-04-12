#!/usr/bin/env python3
"""
Blender 自动渲染驱动视频
=========================
将 Mixamo 下载的 FBX 自动渲染为侧视角 MP4 驱动视频。

用法（在 Blender 中运行）:
  blender --background --python blender_render_drive_video.py -- \
    --input mixamo_run.fbx \
    --output run_drive.mp4 \
    --preset run

或批量处理:
  blender --background --python blender_render_drive_video.py -- \
    --input-dir ./mixamo_fbx/ \
    --output-dir ./drive_videos/ \
    --auto-detect

注意: 此脚本需要在 Blender 环境中运行（blender --background --python）
"""

import sys
import os
import json
import argparse
from pathlib import Path

# ============================================================
# 检测是否在 Blender 环境中
# ============================================================

IN_BLENDER = False
try:
    import bpy
    import mathutils
    IN_BLENDER = True
except ImportError:
    pass


def get_args():
    """解析 Blender -- 之后的参数"""
    if IN_BLENDER:
        argv = sys.argv
        if "--" in argv:
            argv = argv[argv.index("--") + 1:]
        else:
            argv = []
    else:
        argv = sys.argv[1:]

    parser = argparse.ArgumentParser(description="Blender 自动渲染 Mixamo FBX → 驱动视频")
    parser.add_argument("--input", type=str, help="输入 FBX 文件路径")
    parser.add_argument("--output", type=str, help="输出 MP4 路径")
    parser.add_argument("--input-dir", type=str, help="批量模式：FBX 目录")
    parser.add_argument("--output-dir", type=str, help="批量模式：输出目录")
    parser.add_argument("--preset", type=str, default="run", help="动作预设名称")
    parser.add_argument("--auto-detect", action="store_true", help="自动检测动作类型")
    parser.add_argument("--presets-file", type=str,
                        default=str(Path(__file__).parent / "mixamo_presets.json"),
                        help="预设文件路径")
    parser.add_argument("--width", type=int, default=480)
    parser.add_argument("--height", type=int, default=480)
    parser.add_argument("--fps", type=int, default=16)
    return parser.parse_args(argv)


def load_preset(presets_file, preset_name):
    """加载渲染预设"""
    with open(presets_file) as f:
        data = json.load(f)
    actions = data.get("actions", {})
    if preset_name in actions:
        return actions[preset_name]
    return actions.get("run", {})  # 默认 fallback


def detect_action_from_filename(filename, presets_file=None):
    """从文件名检测动作类型（动态读取 mixamo_presets.json，无需硬编码）"""
    name = Path(filename).stem.lower()

    # 尝试从 presets_file 动态构建关键词表
    if presets_file is None:
        presets_file = str(Path(__file__).parent / "mixamo_presets.json")

    keywords = {}
    try:
        with open(presets_file, encoding="utf-8") as f:
            data = json.load(f)
        for action_key, action_data in data.get("actions", {}).items():
            # 用 search_term 和 mixamo_id 以及 action_key 本身作为关键词
            kws = [action_key]
            search_term = action_data.get("search_term", "")
            mixamo_id = action_data.get("mixamo_id", "")
            # 把 search_term 拆成单词
            for word in search_term.lower().replace("-", " ").replace("_", " ").split():
                if len(word) > 2:
                    kws.append(word)
            for word in mixamo_id.lower().replace("-", " ").replace("_", " ").split():
                if len(word) > 2:
                    kws.append(word)
            keywords[action_key] = list(dict.fromkeys(kws))  # 去重保序
    except Exception:
        # 读取失败时使用内置兜底关键词表
        keywords = {
            "run": ["run", "running", "sprint"],
            "idle": ["idle", "breathing", "stand"],
            "jump": ["jump", "leap"],
            "attack_sword": ["sword", "slash", "attack"],
            "walk": ["walk", "walking"],
            "death": ["death", "dying", "die"],
            "dash": ["dash", "fast"],
        }

    for action, kws in keywords.items():
        for kw in kws:
            if kw in name:
                return action
    # 默认返回第一个动作，或 run
    return next(iter(keywords), "run")


def setup_scene(preset, width, height, fps):
    """配置 Blender 场景"""
    if not IN_BLENDER:
        return

    # 清空场景
    bpy.ops.wm.read_factory_settings(use_empty=True)

    scene = bpy.context.scene
    render = scene.render

    # 渲染设置
    render.resolution_x = width
    render.resolution_y = height
    render.fps = fps
    render.image_settings.file_format = 'FFMPEG'
    scene.render.ffmpeg.format = 'MPEG4'
    scene.render.ffmpeg.codec = 'H264'
    scene.render.ffmpeg.constant_rate_factor = 'HIGH'

    # 绿幕背景
    bpy.context.scene.world = bpy.data.worlds.new("GreenScreen")
    bpy.context.scene.world.use_nodes = True
    bg_node = bpy.context.scene.world.node_tree.nodes["Background"]
    bg_node.inputs[0].default_value = (0, 1, 0, 1)  # 纯绿色

    # 正交相机
    bpy.ops.object.camera_add(location=(0, 0, 0))
    camera = bpy.context.active_object
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 3.0
    scene.camera = camera

    # 相机位置（侧视角）
    rs = preset.get("blender_render_settings", {})
    cam_dist = rs.get("camera_distance", 5.0)
    rot_z = rs.get("rotation_z_deg", 90)

    import math
    rad = math.radians(rot_z)
    camera.location = (cam_dist * math.cos(rad), cam_dist * math.sin(rad), 1.0)
    camera.rotation_euler = (math.radians(90), 0, math.radians(rot_z + 90))

    # 灯光
    bpy.ops.object.light_add(type='SUN', location=(0, 0, 5))
    light = bpy.context.active_object
    light.data.energy = 3.0


def import_and_render(fbx_path, output_path, preset, width, height, fps):
    """导入 FBX 并渲染"""
    if not IN_BLENDER:
        print(f"[模拟] 渲染 {fbx_path} → {output_path}")
        return

    setup_scene(preset, width, height, fps)

    # 导入 FBX
    bpy.ops.import_scene.fbx(filepath=str(fbx_path))

    # 获取动画长度
    scene = bpy.context.scene
    if bpy.context.active_object and bpy.context.active_object.animation_data:
        action = bpy.context.active_object.animation_data.action
        if action:
            scene.frame_start = int(action.frame_range[0])
            scene.frame_end = int(action.frame_range[1])

    # 渲染输出
    scene.render.filepath = str(output_path)
    bpy.ops.render.render(animation=True)
    print(f"  ✓ 渲染完成: {output_path}")


def main():
    args = get_args()

    if not IN_BLENDER:
        print("=" * 60)
        print("  此脚本需要在 Blender 中运行")
        print("=" * 60)
        print(f"\n用法:")
        print(f"  blender --background --python {__file__} -- --input your_animation.fbx --output drive_video.mp4")
        print(f"\n批量用法:")
        print(f"  blender --background --python {__file__} -- --input-dir ./fbx/ --output-dir ./videos/ --auto-detect")
        print(f"\n当前为预览模式，显示将要执行的操作:")

    # 批量模式
    if args.input_dir:
        input_dir = Path(args.input_dir)
        output_dir = Path(args.output_dir or "./drive_videos")
        output_dir.mkdir(parents=True, exist_ok=True)

        fbx_files = list(input_dir.glob("*.fbx"))
        print(f"\n找到 {len(fbx_files)} 个 FBX 文件")

        for fbx in sorted(fbx_files):
            if args.auto_detect:
                preset_name = detect_action_from_filename(fbx.name)
            else:
                preset_name = args.preset

            preset = load_preset(args.presets_file, preset_name)
            output_path = output_dir / f"{fbx.stem}.mp4"

            print(f"\n  {fbx.name} → [{preset_name}] → {output_path.name}")
            import_and_render(fbx, output_path, preset, args.width, args.height, args.fps)

    # 单文件模式
    elif args.input:
        preset = load_preset(args.presets_file, args.preset)
        output_path = args.output or f"{Path(args.input).stem}_drive.mp4"
        print(f"\n  {args.input} → [{args.preset}] → {output_path}")
        import_and_render(args.input, output_path, preset, args.width, args.height, args.fps)

    else:
        print("\n[错误] 请指定 --input 或 --input-dir")


if __name__ == "__main__":
    main()
