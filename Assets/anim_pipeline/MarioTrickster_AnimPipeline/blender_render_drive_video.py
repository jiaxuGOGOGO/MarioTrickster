#!/usr/bin/env python3
"""
Blender 自动渲染驱动视频
=========================
将 Mixamo 下载的 FBX 自动渲染为侧视角 MP4 驱动视频。

本版重点增强：
1. 严格区分“存在 MESH 对象”与“存在可渲染有效网格”，避免 animation-only FBX 被误判。
2. 当导入后没有有效网格时，自动为骨架生成白色代理人体，确保驱动视频中始终有可见主体。
3. 对导入网格或代理人体统一应用白色发光材质，并按对象包围盒自动适配正交相机，减少纯绿幕与构图裁切。

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

from __future__ import annotations

import argparse
import json
import math
import os
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

IN_BLENDER = False
try:
    import bpy
    from mathutils import Matrix, Quaternion, Vector
    IN_BLENDER = True
except ImportError:
    bpy = None
    Matrix = None
    Vector = None


def get_args():
    """解析 Blender -- 之后的参数。"""
    if IN_BLENDER:
        argv = sys.argv
        argv = argv[argv.index("--") + 1:] if "--" in argv else []
    else:
        argv = sys.argv[1:]

    parser = argparse.ArgumentParser(description="Blender 自动渲染 Mixamo FBX → 驱动视频")
    parser.add_argument("--input", type=str, help="输入 FBX 文件路径")
    parser.add_argument("--output", type=str, help="输出 MP4 路径")
    parser.add_argument("--input-dir", type=str, help="批量模式：FBX 目录")
    parser.add_argument("--output-dir", type=str, help="批量模式：输出目录")
    parser.add_argument("--preset", type=str, default="run", help="动作预设名称")
    parser.add_argument("--auto-detect", action="store_true", help="自动检测动作类型")
    parser.add_argument(
        "--presets-file",
        type=str,
        default=str(SCRIPT_DIR / "mixamo_presets.json"),
        help="预设文件路径",
    )
    parser.add_argument("--width", type=int, default=None, help="强制覆盖输出宽度")
    parser.add_argument("--height", type=int, default=None, help="强制覆盖输出高度")
    parser.add_argument("--fps", type=int, default=None, help="强制覆盖输出帧率")
    parser.add_argument(
        "--proxy-mode",
        choices=["auto", "always", "never"],
        default="auto",
        help="代理人体模式：auto=仅在无有效网格时启用；always=始终启用；never=禁用",
    )
    parser.add_argument(
        "--ortho-padding",
        type=float,
        default=1.4,
        help="正交相机构图留白系数，越大越不容易裁切",
    )
    return parser.parse_args(argv)


def detect_action_from_filename(filename, presets_file=None):
    """从文件名动态检测动作类型。"""
    name = Path(filename).stem.lower()
    presets_file = presets_file or str(SCRIPT_DIR / "mixamo_presets.json")

    keywords = {}
    try:
        with open(presets_file, encoding="utf-8") as f:
            data = json.load(f)
        for action_key, action_data in data.get("actions", {}).items():
            kws = [action_key]
            search_term = action_data.get("search_term", "")
            mixamo_id = action_data.get("mixamo_id", "")
            for word in search_term.lower().replace("-", " ").replace("_", " ").split():
                if len(word) > 2:
                    kws.append(word)
            for word in mixamo_id.lower().replace("-", " ").replace("_", " ").split():
                if len(word) > 2:
                    kws.append(word)
            keywords[action_key] = list(dict.fromkeys(kws))
    except Exception:
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
    return next(iter(keywords), "run")


def _merge_knowledge_blender_settings(preset_name, render_settings):
    """把 distilled_knowledge 中的 Blender 设置并入渲染配置。"""
    try:
        import knowledge_loader

        kb_settings = knowledge_loader.get_blender_settings(preset_name) or {}
        merged = dict(render_settings)
        if "distance" in kb_settings:
            merged["camera_distance"] = kb_settings["distance"]
        if "rot_z" in kb_settings:
            merged["rotation_z_deg"] = kb_settings["rot_z"]
        if "fps" in kb_settings:
            merged["fps"] = kb_settings["fps"]
        if "res" in kb_settings and isinstance(kb_settings["res"], (list, tuple)) and len(kb_settings["res"]) == 2:
            merged["resolution"] = list(kb_settings["res"])
        if "bg" in kb_settings:
            merged["background"] = kb_settings["bg"]
        if "proxy_mode" in kb_settings:
            merged["proxy_mode"] = kb_settings["proxy_mode"]
        return merged
    except Exception:
        return dict(render_settings)


def load_preset(presets_file, preset_name):
    """加载渲染预设，并叠加知识库中的 Blender 参数。"""
    with open(presets_file, encoding="utf-8") as f:
        data = json.load(f)
    actions = data.get("actions", {})
    preset = dict(actions.get(preset_name) or actions.get("run") or {})
    render_settings = dict(preset.get("blender_render_settings", {}))
    preset["blender_render_settings"] = _merge_knowledge_blender_settings(preset_name, render_settings)
    return preset


def resolve_render_settings(args, preset, preset_name):
    """解析最终生效的渲染设置。"""
    rs = dict((preset or {}).get("blender_render_settings", {}))
    rs = _merge_knowledge_blender_settings(preset_name, rs)
    resolution = rs.get("resolution") or [480, 480]
    width = int(args.width or resolution[0] or 480)
    height = int(args.height or resolution[1] or 480)
    fps = int(args.fps or rs.get("fps") or 16)
    rs["resolution"] = [width, height]
    rs["fps"] = fps
    rs["proxy_mode"] = args.proxy_mode or rs.get("proxy_mode", "auto")
    rs["ortho_padding"] = float(args.ortho_padding or rs.get("ortho_padding") or 1.4)
    rs.setdefault("camera_distance", 5.0)
    rs.setdefault("rotation_z_deg", 90.0)
    rs.setdefault("background", "green_screen")
    return rs


def is_renderable_mesh_object(obj, min_dimension=1e-4):
    """判断对象是否为真正可渲染的有效网格，而不是空壳 MESH。"""
    if getattr(obj, "type", None) != "MESH":
        return False
    if getattr(obj, "hide_render", False):
        return False

    data = getattr(obj, "data", None)
    if data is None:
        return False

    vertices = getattr(data, "vertices", None)
    polygons = getattr(data, "polygons", None)
    if vertices is None or polygons is None:
        return False
    if len(vertices) == 0 or len(polygons) == 0:
        return False

    dimensions = getattr(obj, "dimensions", None)
    if dimensions is None:
        return False
    try:
        max_dim = max(float(v) for v in dimensions)
    except Exception:
        return False
    if max_dim <= float(min_dimension):
        return False

    return True


def reset_scene():
    """清空场景并恢复空白工程。"""
    if not IN_BLENDER:
        return
    bpy.ops.wm.read_factory_settings(use_empty=True)


def _set_render_engine(scene):
    """优先启用 EEVEE，并尽量保持色彩稳定。"""
    if not IN_BLENDER:
        return

    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            scene.render.engine = engine
            break
        except Exception:
            continue

    try:
        scene.view_settings.view_transform = "Standard"
    except Exception:
        pass

    try:
        scene.eevee.taa_render_samples = 8
    except Exception:
        pass


def _setup_world(scene, background="green_screen"):
    """创建背景世界。S105 起默认使用深灰背景，避免纯绿/纯黑对 VAE 造成强刺激。"""
    if not IN_BLENDER:
        return

    world = bpy.data.worlds.new("DriveBackground")
    world.use_nodes = True
    nodes = world.node_tree.nodes
    bg_node = nodes.get("Background")
    if bg_node is None:
        bg_node = nodes.new(type="ShaderNodeBackground")

    color = (0.2, 0.2, 0.2, 1.0) if background == "green_screen" else (0.12, 0.12, 0.12, 1.0)
    bg_node.inputs[0].default_value = color
    bg_node.inputs[1].default_value = 1.0
    scene.world = world


def setup_scene(render_settings):
    """初始化 Blender 场景。"""
    if not IN_BLENDER:
        return None, None

    reset_scene()
    scene = bpy.context.scene
    _set_render_engine(scene)

    width, height = render_settings["resolution"]
    fps = render_settings["fps"]

    render = scene.render
    render.resolution_x = int(width)
    render.resolution_y = int(height)
    render.fps = int(fps)
    render.image_settings.file_format = "FFMPEG"
    render.ffmpeg.format = "MPEG4"
    render.ffmpeg.codec = "H264"
    render.ffmpeg.constant_rate_factor = "HIGH"
    render.ffmpeg.ffmpeg_preset = "GOOD"

    _setup_world(scene, render_settings.get("background", "green_screen"))

    bpy.ops.object.camera_add(location=(0, 0, 0))
    camera = bpy.context.active_object
    camera.name = "DriveCamera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 3.0
    scene.camera = camera
    return scene, camera


def _collect_new_objects(before_names):
    return [obj for obj in bpy.data.objects if obj.name not in before_names]


def _find_primary_armature(imported_objects):
    armatures = [obj for obj in imported_objects if getattr(obj, "type", None) == "ARMATURE"]
    if not armatures:
        return None
    return max(armatures, key=lambda obj: len(getattr(getattr(obj, "data", None), "bones", [])))


def _find_renderable_meshes(imported_objects):
    return [obj for obj in imported_objects if is_renderable_mesh_object(obj)]


def _make_emission_material(name="DriveProxyGray", rgba=(0.5, 0.5, 0.5, 1.0), strength=2.2):
    """创建统一中灰发光材质，降低代理体与背景的极端反差。"""
    if not IN_BLENDER:
        return None

    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    out = nodes.new(type="ShaderNodeOutputMaterial")
    emission = nodes.new(type="ShaderNodeEmission")
    emission.inputs[0].default_value = rgba
    emission.inputs[1].default_value = strength
    links.new(emission.outputs[0], out.inputs[0])
    return mat


def _apply_material_to_meshes(meshes, material):
    if not IN_BLENDER or material is None:
        return
    for obj in meshes:
        if getattr(obj, "type", None) != "MESH" or getattr(obj, "data", None) is None:
            continue
        obj.hide_render = False
        obj.display_type = "TEXTURED"
        obj.color = (1.0, 1.0, 1.0, 1.0)
        obj.data.materials.clear()
        obj.data.materials.append(material)


def _generate_frame_samples(frame_start, frame_end, count=5):
    if frame_end <= frame_start:
        return [int(frame_start)]
    count = max(2, int(count))
    if frame_end - frame_start + 1 <= count:
        return list(range(int(frame_start), int(frame_end) + 1))
    frames = []
    for i in range(count):
        t = i / (count - 1)
        frame = int(round(frame_start + (frame_end - frame_start) * t))
        frames.append(frame)
    return sorted(set(frames))


def _infer_frame_range(imported_objects, armature=None):
    """尽量从导入对象的 Action 中推导动画范围。"""
    ranges = []
    for obj in imported_objects:
        anim_data = getattr(obj, "animation_data", None)
        action = getattr(anim_data, "action", None) if anim_data else None
        if action:
            ranges.append(tuple(action.frame_range))

    if armature and not ranges:
        anim_data = getattr(armature, "animation_data", None)
        action = getattr(anim_data, "action", None) if anim_data else None
        if action:
            ranges.append(tuple(action.frame_range))

    if not ranges and IN_BLENDER:
        for action in bpy.data.actions:
            if getattr(action, "users", 0) > 0:
                ranges.append(tuple(action.frame_range))

    if not ranges:
        return 1, 32

    start = int(min(r[0] for r in ranges))
    end = int(max(r[1] for r in ranges))
    if end <= start:
        end = start + 1
    return start, end


def _bone_radius_scale(name_lower, bone_length):
    if any(k in name_lower for k in ("spine", "hips", "pelvis", "chest", "torso")):
        return max(0.035, bone_length * 0.18)
    if "head" in name_lower or "neck" in name_lower:
        return max(0.03, bone_length * 0.16)
    if any(k in name_lower for k in ("thigh", "upleg", "leg", "calf", "arm", "forearm", "shoulder")):
        return max(0.02, bone_length * 0.12)
    return max(0.015, bone_length * 0.10)


def _iter_proxy_candidate_bones(armature):
    pose_bones = list(getattr(getattr(armature, "pose", None), "bones", []))
    if not pose_bones:
        return []

    candidates = []
    for bone in pose_bones:
        name_lower = bone.name.lower()
        length = float((bone.tail - bone.head).length)
        if length <= 1e-5:
            continue
        if any(skip in name_lower for skip in ("ik", "pole", "target", "twist", "end", "nub", "ctrl", "control")):
            continue
        candidates.append(bone)

    return candidates or [b for b in pose_bones if float((b.tail - b.head).length) > 1e-5]


def _create_bone_proxy_segment(armature, pose_bone, material):
    head_world = armature.matrix_world @ pose_bone.head
    tail_world = armature.matrix_world @ pose_bone.tail
    direction = tail_world - head_world
    length = float(direction.length)
    if length <= 1e-5:
        return None

    radius = _bone_radius_scale(pose_bone.name.lower(), length)
    midpoint = (head_world + tail_world) * 0.5
    quat = direction.normalized().to_track_quat("Z", "Y")

    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=radius, depth=length, location=midpoint)
    obj = bpy.context.active_object
    obj.name = f"proxy_{pose_bone.name}"
    obj.rotation_euler = quat.to_euler()

    if material is not None:
        obj.data.materials.clear()
        obj.data.materials.append(material)

    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = pose_bone.name
    obj.matrix_world = Matrix.Translation(midpoint) @ quat.to_matrix().to_4x4()
    obj.hide_render = False
    return obj


def _create_head_proxy(armature, material):
    head_bone = None
    for bone in getattr(getattr(armature, "pose", None), "bones", []):
        if "head" in bone.name.lower():
            head_bone = bone
            break
    if head_bone is None:
        return None

    head_world = armature.matrix_world @ head_bone.tail
    neck_world = armature.matrix_world @ head_bone.head
    radius = max(0.05, float((head_world - neck_world).length) * 0.35)

    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=head_world, segments=16, ring_count=8)
    obj = bpy.context.active_object
    obj.name = "proxy_head"
    if material is not None:
        obj.data.materials.clear()
        obj.data.materials.append(material)
    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = head_bone.name
    obj.matrix_world = Matrix.Translation(head_world)
    obj.hide_render = False
    return obj


def build_proxy_body_for_armature(armature, material):
    """为 animation-only FBX 创建简洁但可见的白色代理人体。"""
    if not IN_BLENDER or armature is None:
        return []

    proxies = []
    for bone in _iter_proxy_candidate_bones(armature):
        proxy = _create_bone_proxy_segment(armature, bone, material)
        if proxy is not None:
            proxies.append(proxy)

    head_proxy = _create_head_proxy(armature, material)
    if head_proxy is not None:
        proxies.append(head_proxy)

    return proxies


def _bbox_world_points_for_objects(scene, objects, sample_frames):
    points = []
    for frame in sample_frames:
        scene.frame_set(int(frame))
        bpy.context.view_layer.update()
        for obj in objects:
            if getattr(obj, "type", None) != "MESH" or getattr(obj, "hide_render", False):
                continue
            bound_box = getattr(obj, "bound_box", None)
            if not bound_box:
                continue
            matrix_world = obj.matrix_world.copy()
            for corner in bound_box:
                points.append(matrix_world @ Vector(corner))
    return points


def _collect_keyframe_times(fcurves):
    frames = set()
    for fcurve in fcurves:
        if fcurve is None:
            continue
        for keyframe in getattr(fcurve, "keyframe_points", []):
            frames.add(round(float(keyframe.co[0]), 6))
    return sorted(frames)


def _anchor_frame_for_fcurves(fcurves):
    frames = _collect_keyframe_times(fcurves)
    return frames[0] if frames else None


def _evaluate_fcurve_reference(fcurve, frame, default=0.0):
    if fcurve is None:
        return float(default)
    try:
        return float(fcurve.evaluate(frame))
    except Exception:
        points = getattr(fcurve, "keyframe_points", None) or []
        if points:
            return float(points[0].co[1])
    return float(default)


def _scale_fcurve_around_reference(fcurve, reference, amplitude):
    keyframes = getattr(fcurve, "keyframe_points", None) or []
    changed = 0
    for keyframe in keyframes:
        original = float(keyframe.co[1])
        new_value = float(reference) + (original - float(reference)) * float(amplitude)
        left_offset = float(keyframe.handle_left[1]) - original
        right_offset = float(keyframe.handle_right[1]) - original
        keyframe.co[1] = new_value
        keyframe.handle_left[1] = new_value + left_offset * float(amplitude)
        keyframe.handle_right[1] = new_value + right_offset * float(amplitude)
        changed += 1
    if changed:
        fcurve.update()
    return changed


def _normalize_quaternion_values(values, fallback=None):
    vals = [float(v) for v in values]
    length = math.sqrt(sum(v * v for v in vals))
    if length <= 1e-8:
        if fallback is None:
            return [1.0, 0.0, 0.0, 0.0]
        vals = [float(v) for v in fallback]
        length = math.sqrt(sum(v * v for v in vals))
        if length <= 1e-8:
            return [1.0, 0.0, 0.0, 0.0]
    return [v / length for v in vals]


def _scale_quaternion_fcurves(channel_map, amplitude):
    existing = [channel_map.get(i) for i in range(4) if channel_map.get(i) is not None]
    if len(existing) < 4:
        return 0, 0

    anchor_frame = _anchor_frame_for_fcurves(existing)
    if anchor_frame is None:
        return 0, 0

    reference = _normalize_quaternion_values([
        _evaluate_fcurve_reference(channel_map.get(i), anchor_frame, 1.0 if i == 0 else 0.0)
        for i in range(4)
    ])

    indexed = {}
    for idx in range(4):
        indexed[idx] = {
            round(float(point.co[0]), 6): point
            for point in getattr(channel_map[idx], "keyframe_points", [])
        }

    common_frames = sorted(set(indexed[0]).intersection(indexed[1], indexed[2], indexed[3]))
    if not common_frames:
        return 0, 0

    for frame in common_frames:
        scaled_values = []
        for idx in range(4):
            keyframe = indexed[idx][frame]
            original = float(keyframe.co[1])
            new_value = reference[idx] + (original - reference[idx]) * float(amplitude)
            left_offset = float(keyframe.handle_left[1]) - original
            right_offset = float(keyframe.handle_right[1]) - original
            keyframe.co[1] = new_value
            keyframe.handle_left[1] = new_value + left_offset * float(amplitude)
            keyframe.handle_right[1] = new_value + right_offset * float(amplitude)
            scaled_values.append(new_value)

        normalized_values = _normalize_quaternion_values(scaled_values, fallback=reference)
        for idx in range(4):
            keyframe = indexed[idx][frame]
            delta = normalized_values[idx] - float(keyframe.co[1])
            keyframe.co[1] = normalized_values[idx]
            keyframe.handle_left[1] += delta
            keyframe.handle_right[1] += delta

    for fcurve in existing:
        fcurve.update()
    return len(existing), len(common_frames)


def amplify_armature_action_motion(armature, amplitude=1.3):
    """在渲染前统一放大骨架动作振幅，增强微动作光流。"""
    if not IN_BLENDER or armature is None:
        return
    if abs(float(amplitude) - 1.0) <= 1e-6:
        return

    anim_data = getattr(armature, "animation_data", None)
    action = getattr(anim_data, "action", None) if anim_data else None
    if action is None:
        print("  [Blender] 未找到可放大的动作曲线，跳过 motion amp")
        return

    grouped = {}
    for fcurve in getattr(action, "fcurves", []):
        data_path = str(getattr(fcurve, "data_path", ""))
        if not data_path.startswith('pose.bones["'):
            continue
        if not (
            data_path.endswith(".location")
            or data_path.endswith(".rotation_euler")
            or data_path.endswith(".rotation_quaternion")
        ):
            continue
        grouped.setdefault(data_path, {})[int(getattr(fcurve, "array_index", 0))] = fcurve

    changed_channels = 0
    changed_keys = 0
    skipped_quaternion_groups = 0

    for data_path, channel_map in grouped.items():
        if data_path.endswith(".rotation_quaternion"):
            cur_channels, cur_keys = _scale_quaternion_fcurves(channel_map, amplitude)
            if cur_channels == 0:
                skipped_quaternion_groups += 1
                continue
            changed_channels += cur_channels
            changed_keys += cur_keys
            continue

        existing = [channel_map.get(i) for i in sorted(channel_map.keys()) if channel_map.get(i) is not None]
        anchor_frame = _anchor_frame_for_fcurves(existing)
        if anchor_frame is None:
            continue
        for _, fcurve in channel_map.items():
            reference = _evaluate_fcurve_reference(fcurve, anchor_frame, 0.0)
            changed = _scale_fcurve_around_reference(fcurve, reference, amplitude)
            if changed:
                changed_channels += 1
                changed_keys += changed

    if changed_channels:
        print(
            f"  [Blender] 动作振幅已放大 {float(amplitude):.2f}x："
            f"channels={changed_channels}, keyframes={changed_keys}"
        )
    else:
        print("  [Blender] 未找到可放大的 location / rotation 曲线")
    if skipped_quaternion_groups:
        print(f"  [Blender] 提示：{skipped_quaternion_groups} 组四元数曲线因关键帧不同步被跳过")


def fit_camera_to_targets(scene, camera, targets, render_settings, frame_start, frame_end):
    """根据目标对象包围盒自动调整相机位置与正交缩放。"""
    if not IN_BLENDER or not targets:
        return

    sample_frames = _generate_frame_samples(frame_start, frame_end, count=5)
    points = _bbox_world_points_for_objects(scene, targets, sample_frames)
    if not points:
        print("  [Blender] 未能从目标对象获取包围盒，保留默认相机设置")
        return

    min_v = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    max_v = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    center = (min_v + max_v) * 0.5

    rot_z = float(render_settings.get("rotation_z_deg", 90.0))
    distance = float(render_settings.get("camera_distance", 5.0))
    padding = float(render_settings.get("ortho_padding", 1.4))
    aspect = max(1e-6, scene.render.resolution_x / max(1, scene.render.resolution_y))

    rad = math.radians(rot_z)
    direction = Vector((math.cos(rad), math.sin(rad), 0.0))
    camera.location = center + direction * distance
    camera.rotation_euler = (math.radians(90.0), 0.0, math.radians(rot_z + 90.0))
    bpy.context.view_layer.update()

    cam_inv = camera.matrix_world.inverted()
    cam_space_points = [cam_inv @ p for p in points]
    min_x = min(p.x for p in cam_space_points)
    max_x = max(p.x for p in cam_space_points)
    min_y = min(p.y for p in cam_space_points)
    max_y = max(p.y for p in cam_space_points)

    offset_x = (min_x + max_x) * 0.5
    offset_y = (min_y + max_y) * 0.5
    camera.location += camera.matrix_world.to_quaternion() @ Vector((offset_x, offset_y, 0.0))
    bpy.context.view_layer.update()

    width = max(1e-4, max_x - min_x)
    height = max(1e-4, max_y - min_y)
    camera.data.ortho_scale = max(width * padding, height * padding * aspect, 1.0)

    print(
        f"  [Blender] 相机自动构图：bbox=({width:.3f}×{height:.3f})，"
        f"ortho_scale={camera.data.ortho_scale:.3f}，padding={padding:.2f}"
    )


def _set_mesh_visibility(imported_objects, render_targets):
    target_names = {obj.name for obj in render_targets}
    for obj in imported_objects:
        if getattr(obj, "type", None) != "MESH":
            continue
        obj.hide_render = obj.name not in target_names


def _import_fbx(filepath):
    before_names = {obj.name for obj in bpy.data.objects}
    bpy.ops.import_scene.fbx(filepath=str(filepath))
    imported = _collect_new_objects(before_names)
    return imported


def import_and_render(fbx_path, output_path, preset_name, args):
    """导入 FBX 并渲染驱动视频。"""
    preset = load_preset(args.presets_file, preset_name)
    render_settings = resolve_render_settings(args, preset, preset_name)

    if not IN_BLENDER:
        print(f"[模拟] 渲染 {fbx_path} → {output_path}")
        print(f"[模拟] preset={preset_name}, render_settings={render_settings}")
        return True

    scene, camera = setup_scene(render_settings)
    proxy_mat = _make_emission_material()

    imported_objects = _import_fbx(fbx_path)
    armature = _find_primary_armature(imported_objects)
    renderable_meshes = _find_renderable_meshes(imported_objects)

    proxy_mode = str(render_settings.get("proxy_mode", "auto"))
    needs_proxy = proxy_mode == "always" or (proxy_mode == "auto" and len(renderable_meshes) == 0)

    print(f"  [Blender] 导入对象数: {len(imported_objects)}")
    print(f"  [Blender] 有效可渲染网格数: {len(renderable_meshes)}")
    if armature is not None:
        print(f"  [Blender] 主骨架: {armature.name}，骨骼数={len(armature.data.bones)}")
    else:
        print("  [Blender] 未检测到骨架对象")

    if renderable_meshes:
        _apply_material_to_meshes(renderable_meshes, proxy_mat)

    proxy_meshes = []
    proxy_takeover = needs_proxy and len(renderable_meshes) == 0
    if needs_proxy and armature is not None:
        print("  [Blender] 检测到 animation-only / 空网格 FBX，自动生成代理人体")
        proxy_meshes = build_proxy_body_for_armature(armature, proxy_mat)
        print(f"  [Blender] 代理人体部件数: {len(proxy_meshes)}")
    elif needs_proxy:
        print("  [Blender] 需要代理人体，但未检测到骨架，无法生成代理主体")

    render_targets = renderable_meshes if renderable_meshes and proxy_mode != "always" else (proxy_meshes or renderable_meshes)
    _set_mesh_visibility(imported_objects, render_targets)

    if not render_targets:
        print("  [Blender] 错误：既没有有效网格，也无法生成代理人体，终止渲染")
        return False

    amplify_armature_action_motion(armature, amplitude=1.3)

    frame_start, frame_end = _infer_frame_range(imported_objects, armature=armature)
    scene.frame_start = int(frame_start)
    scene.frame_end = int(frame_end)
    scene.frame_set(scene.frame_start)
    bpy.context.view_layer.update()

    camera_render_settings = dict(render_settings)
    if proxy_takeover:
        camera_render_settings["ortho_padding"] = max(2.5, float(camera_render_settings.get("ortho_padding", 1.4)))
        print(
            "  [Blender] Proxy 接管已启用极限留白："
            f"padding={camera_render_settings['ortho_padding']:.2f}"
        )

    fit_camera_to_targets(scene, camera, render_targets, camera_render_settings, scene.frame_start, scene.frame_end)

    scene.render.filepath = str(output_path)
    Path(output_path).parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.render.render(animation=True)
    print(f"  [Blender] 渲染完成: {output_path}")
    return True


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

    if args.input_dir:
        input_dir = Path(args.input_dir)
        output_dir = Path(args.output_dir or "./drive_videos")
        output_dir.mkdir(parents=True, exist_ok=True)

        fbx_files = list(input_dir.glob("*.fbx"))
        print(f"\n找到 {len(fbx_files)} 个 FBX 文件")

        ok = True
        for fbx in sorted(fbx_files):
            preset_name = detect_action_from_filename(fbx.name, args.presets_file) if args.auto_detect else args.preset
            output_path = output_dir / f"{fbx.stem}.mp4"
            print(f"\n  {fbx.name} → [{preset_name}] → {output_path.name}")
            ok = import_and_render(str(fbx), str(output_path), preset_name, args) and ok
        return 0 if ok else 1

    if args.input:
        preset_name = detect_action_from_filename(args.input, args.presets_file) if args.auto_detect and args.preset == "run" else args.preset
        output_path = args.output or f"{Path(args.input).stem}_drive.mp4"
        print(f"\n  {args.input} → [{preset_name}] → {output_path}")
        return 0 if import_and_render(args.input, output_path, preset_name, args) else 1

    print("\n[错误] 请指定 --input 或 --input-dir")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
