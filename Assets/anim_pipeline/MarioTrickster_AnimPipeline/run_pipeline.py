#!/usr/bin/env python3
"""
MarioTrickster 一键动画管线
=============================
用法:
  python run_pipeline.py --ref trickster.png --video run_cycle.mp4

只需提供参考图和驱动视频，脚本自动完成：
  1. 上传素材到 ComfyUI
  2. 注入 Prompt 模板（自动匹配动作类型）
  3. 提交 WAN 2.2 Animate Move 工作流
  4. 等待生成完成并下载视频
  5. 拆帧 → 去背景 → 像素化量化 → 拼合 Sprite Sheet
  6. 输出最终 Sprite Sheet PNG + 元数据 JSON

环境要求:
  - ComfyUI 运行在本地 (默认 127.0.0.1:8188)
  - 已安装: comfyui_controlnet_aux (DWPreprocessor)
  - 已下载所有模型文件 (见 README)
"""

import argparse
import json
import os
import sys
import time
import uuid
import shutil
import math
from collections import deque
from pathlib import Path

import numpy as np

# ============================================================
# 配置区（用户可按需修改）
# ============================================================

import config_manager

# 知识加载器（可选依赖，缺失时退化为旧版行为）
try:
    import knowledge_loader
    HAS_KNOWLEDGE = True
except ImportError:
    HAS_KNOWLEDGE = False

# 产出物适配器（可选依赖，缺失时跳过自动适配）
try:
    import asset_adapter
    HAS_ADAPTER = True
except ImportError:
    HAS_ADAPTER = False

# 从 config_manager 加载配置
config_data = config_manager.load_config()
DEFAULT_CONFIG = {
    "server": config_data.get("server", "127.0.0.1:8188"),
    **config_data.get("generation", {}),
    **config_data.get("postprocess", {})
}


def should_lock_idle_safe_resolution(action_type, config, args=None):
    """idle 默认锁定 12GB 安全档位 480x480，避免 QC 自动放大分辨率。"""
    if action_type != "idle":
        return False
    if args is not None and (
        getattr(args, "width", None) is not None or
        getattr(args, "height", None) is not None
    ):
        return False
    cur_w = int(config.get("width", 480))
    cur_h = int(config.get("height", 480))
    return cur_w == 480 and cur_h == 480



def sanitize_qc_adjustments(adjustments, action_type, config, args=None):
    """过滤不该在当前档位自动生效的 QC 调参建议。"""
    cleaned = dict(adjustments or {})
    if should_lock_idle_safe_resolution(action_type, config, args):
        removed = []
        for key in ("width", "height"):
            if key in cleaned:
                removed.append(f"{key}={cleaned.pop(key)}")
        if removed:
            print(f"  [安全锁] idle 维持 480x480 安全档位，忽略自动分辨率调参: {', '.join(removed)}")
            print("  [安全锁] 若仍有裁切，请优先检查 drive video 构图、代理人体缩放与角色在画面中的占比")
    return cleaned


# ============================================================
# Prompt 模板库（优先从知识库加载，回退到 mixamo_presets.json）
# ============================================================

def _load_prompt_templates():
    """
    优先从 distilled_knowledge.json 构建 PROMPT_TEMPLATES（知识驱动版）。
    回退到 mixamo_presets.json（兼容旧版）。
    知识库中的 boost 词和画风 Prompt 会在 enhance_prompt 阶段叠加，
    这里只构建基础模板。
    """
    base_suffix = "clean sprite style, 2D side-scrolling platformer, solid color background, sharp edges, thick black outlines"

    # 内置兜底模板
    fallback = {
        "run": {
            "positive": f"A game character running in side view, dynamic running animation, arms swinging, legs in full stride, {base_suffix}",
            "negative_extra": "",
            "mixamo_tips": "选择带预备蹲的跑步动画"
        },
        "idle": {
            "positive": f"A game character standing idle, subtle breathing animation, slight body sway, relaxed pose, {base_suffix}",
            "negative_extra": "",
            "mixamo_tips": "选择 Breathing Idle"
        },
        "jump": {
            "positive": f"A game character jumping, anticipation squat to full jump arc, arms raised, dynamic pose, {base_suffix}",
            "negative_extra": "",
            "mixamo_tips": "选择包含预备蹲的 Jump"
        },
        "walk": {
            "positive": f"A game character walking in side view, natural walking cycle, arms gently swinging, {base_suffix}",
            "negative_extra": "",
            "mixamo_tips": "选择 Walking"
        },
        "death": {
            "positive": f"A game character falling down defeated, dramatic collapse animation, {base_suffix}",
            "negative_extra": "",
            "mixamo_tips": "选择 Dying"
        },
        "attack_sword": {
            "positive": f"A game character swinging sword, wind-up to strike, impact freeze, {base_suffix}",
            "negative_extra": "",
            "mixamo_tips": "选择 Sword And Shield Slash"
        },
        "dash": {
            "positive": f"A game character dashing forward, extreme lean, speed lines, {base_suffix}",
            "negative_extra": "",
            "mixamo_tips": "选择 Sprint"
        },
        "custom": {
            "positive": f"A game character performing action, {base_suffix}",
            "negative_extra": "",
            "mixamo_tips": "自定义动作"
        }
    }

    # 优先从知识库构建
    if HAS_KNOWLEDGE:
        try:
            kb = knowledge_loader.get_knowledge()
            actions = kb.get("actions", {})
            if actions:
                templates = {}
                for action_key, act_data in actions.items():
                    if action_key.startswith("_"):
                        continue
                    boost = act_data.get("boost", "")
                    mixamo = act_data.get("mixamo", {})
                    search = mixamo.get("search", action_key)
                    positive = f"A game character performing {search.lower()} animation in side view"
                    if boost:
                        positive += f", {boost}"
                    positive += f", {base_suffix}"
                    defects = act_data.get("defects", [])
                    neg_extra = ", ".join(defects[:3]) if defects else ""
                    templates[action_key] = {
                        "positive": positive,
                        "negative_extra": neg_extra,
                        "mixamo_tips": f"选择 {search}"
                    }
                if "custom" not in templates:
                    templates["custom"] = fallback["custom"]
                return templates
        except Exception:
            pass

    # 回退到 mixamo_presets.json
    presets_file = Path(__file__).parent / "mixamo_presets.json"
    try:
        with open(presets_file, encoding="utf-8") as f:
            data = json.load(f)
    except Exception:
        return fallback

    templates = {}
    for action_key, action_data in data.get("actions", {}).items():
        search_term = action_data.get("search_term", action_key)
        principles = action_data.get("animation_principles", [])
        principles_text = ", ".join(principles) if principles else ""
        positive = f"A game character performing {search_term.lower()} animation in side view"
        if principles_text:
            positive += f", {principles_text}"
        positive += f", {base_suffix}"

        quality_checklist = action_data.get("quality_checklist", [])
        mixamo_tips = "；".join(quality_checklist) if quality_checklist else f"选择 {search_term}"

        templates[action_key] = {
            "positive": positive,
            "negative_extra": "",
            "mixamo_tips": mixamo_tips
        }

    # 确保 custom 兜底始终存在
    if "custom" not in templates:
        templates["custom"] = fallback["custom"]

    return templates


PROMPT_TEMPLATES = _load_prompt_templates()

BASE_NEGATIVE = "色调艳丽，过曝，静态，细节模糊不清，字幕，风格，作品，画作，画面，静止，整体发灰，最差质量，低质量，JPEG压缩残留，丑陋的，残缺的，多余的手指，画得不好的手部，画得不好的脸部，畸形的，毁容的，形态畸形的肢体，手指融合，静止不动的画面，杂乱的背景，三条腿，背景人很多，倒着走"


# ============================================================
# ComfyUI API 通信层
# ============================================================

def upload_image(server, filepath, subfolder="", image_type="input"):
    """上传图片/视频到 ComfyUI input 目录"""
    import urllib.request
    import mimetypes

    filename = os.path.basename(filepath)
    content_type = mimetypes.guess_type(filepath)[0] or "application/octet-stream"

    with open(filepath, "rb") as f:
        file_data = f.read()

    boundary = uuid.uuid4().hex
    body = (
        f"--{boundary}\r\n"
        f'Content-Disposition: form-data; name="image"; filename="{filename}"\r\n'
        f"Content-Type: {content_type}\r\n\r\n"
    ).encode() + file_data + (
        f"\r\n--{boundary}\r\n"
        f'Content-Disposition: form-data; name="type"\r\n\r\n'
        f"{image_type}\r\n"
        f"--{boundary}\r\n"
        f'Content-Disposition: form-data; name="subfolder"\r\n\r\n'
        f"{subfolder}\r\n"
        f"--{boundary}--\r\n"
    ).encode()

    req = urllib.request.Request(
        f"http://{server}/upload/image",
        data=body,
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"}
    )
    resp = urllib.request.urlopen(req)
    return json.loads(resp.read())


def queue_prompt(server, prompt, client_id):
    """提交工作流到 ComfyUI 执行队列"""
    import urllib.request
    p = {"prompt": prompt, "client_id": client_id}
    data = json.dumps(p).encode("utf-8")
    req = urllib.request.Request(
        f"http://{server}/prompt",
        data=data,
        headers={"Content-Type": "application/json"}
    )
    resp = json.loads(urllib.request.urlopen(req).read())
    return resp.get("prompt_id", str(uuid.uuid4()))


def get_history(server, prompt_id):
    """获取执行历史"""
    import urllib.request
    with urllib.request.urlopen(f"http://{server}/history/{prompt_id}") as resp:
        return json.loads(resp.read())


def download_output(server, filename, subfolder, folder_type, save_path):
    """下载生成的文件"""
    import urllib.request
    import urllib.parse
    data = {"filename": filename, "subfolder": subfolder, "type": folder_type}
    url = f"http://{server}/view?{urllib.parse.urlencode(data)}"
    with urllib.request.urlopen(url) as resp:
        with open(save_path, "wb") as f:
            f.write(resp.read())
    return save_path


def wait_for_completion(server, prompt_id, client_id, timeout=600):
    """通过 WebSocket 等待工作流执行完成，断线自动切换 HTTP 轮询兜底"""
    try:
        import websocket
    except ImportError:
        print("[INFO] websocket-client 未安装，使用轮询模式...")
        return _poll_for_completion(server, prompt_id, timeout)

    start = time.time()
    last_progress = 0

    # 尝试 WebSocket 连接
    ws = None
    try:
        ws = websocket.WebSocket()
        ws.connect(f"ws://{server}/ws?clientId={client_id}")
        ws.settimeout(60)  # 单次 recv 超时 60 秒（不是总超时）
    except Exception as e:
        print(f"  [提示] WebSocket 连接失败 ({e})，切换到轮询模式...")
        return _poll_for_completion(server, prompt_id, timeout)

    try:
        while time.time() - start < timeout:
            try:
                out = ws.recv()
            except websocket.WebSocketTimeoutException:
                # 单次 recv 超时，检查是否已经完成
                if _check_history_done(server, prompt_id):
                    print("\n  [完成] 工作流执行结束")
                    return True
                # 还没完成，继续等
                elapsed = int(time.time() - start)
                print(f"\r  [等待中] 已等 {elapsed}s，仍在生成...", end="", flush=True)
                continue
            except (ConnectionError, OSError, Exception) as e:
                # WebSocket 断连，切换到轮询模式
                print(f"\n  [提示] WebSocket 断连 ({type(e).__name__})，切换到轮询模式...")
                remaining = max(10, timeout - int(time.time() - start))
                return _poll_for_completion(server, prompt_id, remaining)

            if isinstance(out, str):
                try:
                    msg = json.loads(out)
                except json.JSONDecodeError:
                    continue

                if msg.get("type") == "progress":
                    d = msg["data"]
                    pct = d["value"] / d["max"] * 100 if d["max"] > 0 else 0
                    last_progress = pct
                    print(f"\r  [进度] {pct:.0f}% ({d['value']}/{d['max']})", end="", flush=True)
                elif msg.get("type") == "executing":
                    d = msg["data"]
                    if d.get("node") is None and d.get("prompt_id") == prompt_id:
                        print("\n  [完成] 工作流执行结束")
                        return True
                elif msg.get("type") == "execution_error":
                    d = msg.get("data", {})
                    err_msg = d.get("exception_message", "未知错误")
                    err_detail = d.get("traceback_str", "")
                    print(f"\n  [错误] {err_msg}")
                    if err_detail:
                        print(f"  [详情] {err_detail[:500]}")
                    return False
    except KeyboardInterrupt:
        print("\n  [中断] 用户手动中断")
        return False
    finally:
        try:
            if ws:
                ws.close()
        except Exception:
            pass

    # 超时后最后检查一次是否已完成
    if _check_history_done(server, prompt_id):
        print("\n  [完成] 工作流执行结束（超时前已完成）")
        return True
    print("\n  [超时] 等待超时")
    return False


def _check_history_done(server, prompt_id):
    """快速检查 prompt 是否已在 history 中（已完成）"""
    import urllib.request
    try:
        with urllib.request.urlopen(f"http://{server}/history/{prompt_id}", timeout=5) as resp:
            history = json.loads(resp.read())
            return prompt_id in history
    except Exception:
        return False


def _poll_for_completion(server, prompt_id, timeout=600):
    """轮询模式等待完成"""
    import urllib.request
    start = time.time()
    while time.time() - start < timeout:
        try:
            with urllib.request.urlopen(f"http://{server}/history/{prompt_id}") as resp:
                history = json.loads(resp.read())
                if prompt_id in history:
                    print("  [完成] 工作流执行结束")
                    return True
        except Exception:
            pass
        time.sleep(3)
        print(".", end="", flush=True)
    print("\n  [超时] 等待超时")
    return False


# ============================================================
# 工作流构建器（匹配 ComfyUI 实际节点参数）
# ============================================================

def build_workflow(config, ref_image_name, video_name, action_type="custom", custom_prompt=None):
    # 动态解析模型文件名
    cfg_data = config_manager.load_config()
    unet_name, _ = config_manager.resolve_model(cfg_data, "unet")
    clip_name, _ = config_manager.resolve_model(cfg_data, "clip")
    vae_name, _ = config_manager.resolve_model(cfg_data, "vae")
    clip_vision_name, _ = config_manager.resolve_model(cfg_data, "clip_vision")
    lora_name, _ = config_manager.resolve_model(cfg_data, "lora")
    
    # 如果解析失败，使用默认值兜底
    unet_name = unet_name or "Wan2_2-Animate-14B_fp8_scaled_e4m3fn_KJ_v2.safetensors"
    clip_name = clip_name or "umt5_xxl_fp8_e4m3fn_scaled.safetensors"
    vae_name = vae_name or "wan_2.1_vae.safetensors"
    clip_vision_name = clip_vision_name or "clip_vision_h.safetensors"
    lora_name = lora_name or "lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors"
    """
    构建 WAN 2.2 Animate 工作流 (API 格式)

    节点链路:
      UNETLoader → LoraLoaderModelOnly → ModelSamplingSD3 → model
      CLIPLoader → clip
      VAELoader → vae
      CLIPVisionLoader → CLIPVisionEncode → clip_vision_output
      LoadImage (ref) → reference_image
      LoadVideo → GetVideoComponents → ImageScale → DWPreprocessor (pose/face)
      CLIPTextEncode (pos/neg) → conditioning
      WanAnimateToVideo → positive_out, negative_out, latent
      KSampler(model, pos_out, neg_out, latent) → samples
      VAEDecode(samples, vae) → images
      CreateVideo(images, fps) → VIDEO
      SaveVideo(VIDEO) → output
    """

    # 获取 Prompt
    template = PROMPT_TEMPLATES.get(action_type, PROMPT_TEMPLATES["custom"])
    positive = custom_prompt if custom_prompt else template["positive"]
    negative = BASE_NEGATIVE + (", " + template["negative_extra"] if template["negative_extra"] else "")

    # 知识增强：用蒸馏知识库补强 Prompt（首次出图即生效）
    if HAS_KNOWLEDGE:
        positive, negative = knowledge_loader.enhance_prompt(action_type, positive, negative)

    seed = config["seed"] if config["seed"] >= 0 else int(time.time() * 1000) % (2**53)

    workflow = {
        # === 1. 模型加载 ===
        "1": {
            "class_type": "UNETLoader",
            "inputs": {
                "unet_name": unet_name,
                "weight_dtype": "default"
            }
        },
        "2": {
            "class_type": "CLIPLoader",
            "inputs": {
                "clip_name": clip_name,
                "type": "wan"
            }
        },
        "3": {
            "class_type": "VAELoader",
            "inputs": {
                "vae_name": vae_name
            }
        },
        "4": {
            "class_type": "CLIPVisionLoader",
            "inputs": {
                "clip_name": clip_vision_name
            }
        },

        # === 2. LoRA 加速 + ModelSampling ===
        "5": {
            "class_type": "LoraLoaderModelOnly",
            "inputs": {
                "model": ["1", 0],
                "lora_name": lora_name,
                "strength_model": 1.0
            }
        },
        "6": {
            "class_type": "ModelSamplingSD3",
            "inputs": {
                "model": ["5", 0],
                "shift": 8.0
            }
        },

        # === 3. 素材加载 ===
        "10": {
            "class_type": "LoadImage",
            "inputs": {
                "image": ref_image_name
            }
        },
        "11": {
            "class_type": "LoadVideo",
            "inputs": {
                "file": video_name
            }
        },

        # === 4. 视频 → 图像帧 ===
        "12": {
            "class_type": "GetVideoComponents",
            "inputs": {
                "video": ["11", 0]
            }
        },

        # === 5. 缩放视频帧 ===
        "13": {
            "class_type": "ImageScale",
            "inputs": {
                "image": ["12", 0],
                "upscale_method": "lanczos",
                "width": config["width"],
                "height": config["height"],
                "crop": "center"
            }
        },

        # === 6. DWPose 骨骼提取 (pose_video) ===
        "14": {
            "class_type": "DWPreprocessor",
            "inputs": {
                "image": ["13", 0],
                "detect_hand": "enable",
                "detect_body": "enable",
                "detect_face": "enable",
                "resolution": 512,
                "bbox_detector": "yolox_l.onnx",
                "pose_estimator": "dw-ll_ucoco_384_bs5.torchscript.pt"
            }
        },

        # === 7. DWPose 面部提取 (face_video) ===
        "15": {
            "class_type": "DWPreprocessor",
            "inputs": {
                "image": ["13", 0],
                "detect_hand": "disable",
                "detect_body": "disable",
                "detect_face": "enable",
                "resolution": 512,
                "bbox_detector": "yolox_l.onnx",
                "pose_estimator": "dw-ll_ucoco_384_bs5.torchscript.pt"
            }
        },

        # === 8. CLIP Vision 编码参考图 ===
        "16": {
            "class_type": "CLIPVisionEncode",
            "inputs": {
                "clip_vision": ["4", 0],
                "image": ["10", 0],
                "crop": "center"
            }
        },

        # === 9. 文本编码 ===
        "20": {
            "class_type": "CLIPTextEncode",
            "inputs": {
                "clip": ["2", 0],
                "text": positive
            }
        },
        "21": {
            "class_type": "CLIPTextEncode",
            "inputs": {
                "clip": ["2", 0],
                "text": negative
            }
        },

        # === 10. WanAnimateToVideo (核心节点) ===
        # 输出: [0]=positive_out, [1]=negative_out, [2]=latent,
        #        [3]=trim_latent, [4]=trim_image, [5]=video_frame_offset
        "30": {
            "class_type": "WanAnimateToVideo",
            "inputs": {
                "positive": ["20", 0],
                "negative": ["21", 0],
                "vae": ["3", 0],
                "width": config["width"],
                "height": config["height"],
                "length": config["length"],
                "batch_size": 1,
                "continue_motion_max_frames": 5,
                "video_frame_offset": 0,
                "clip_vision_output": ["16", 0],
                "reference_image": ["10", 0],
                "face_video": ["15", 0],
                "pose_video": ["14", 0]
            }
        },

        # === 11. KSampler (采样) ===
        "31": {
            "class_type": "KSampler",
            "inputs": {
                "model": ["6", 0],
                "positive": ["30", 0],
                "negative": ["30", 1],
                "latent_image": ["30", 2],
                "seed": seed,
                "steps": config["steps"],
                "cfg": config["cfg"],
                "sampler_name": config["sampler"],
                "scheduler": config["scheduler"],
                "denoise": 1.0
            }
        },

        # === 12. VAE 解码 ===
        "32": {
            "class_type": "VAEDecode",
            "inputs": {
                "samples": ["31", 0],
                "vae": ["3", 0]
            }
        },

        # === 13. 图像帧 → 视频 ===
        "33": {
            "class_type": "CreateVideo",
            "inputs": {
                "images": ["32", 0],
                "fps": 16.0
            }
        },

        # === 14. 保存视频 ===
        "40": {
            "class_type": "SaveVideo",
            "inputs": {
                "video": ["33", 0],
                "filename_prefix": "MarioTrickster_anim",
                "format": "mp4",
                "codec": "h264"
            }
        }
    }

    return workflow, seed


# ============================================================
# 后处理：视频 → Sprite Sheet
# ============================================================

def video_to_frames(video_path, output_dir):
    """用 ffmpeg 将视频拆成帧序列"""
    os.makedirs(output_dir, exist_ok=True)
    cmd = f'ffmpeg -y -i "{video_path}" -vsync vfr "{output_dir}/frame_%04d.png"'
    ret = os.system(cmd)
    if ret != 0:
        # Windows 上 2>/dev/null 不支持，重试不带重定向
        pass
    frames = sorted(Path(output_dir).glob("frame_*.png"))
    print(f"  [拆帧] 共提取 {len(frames)} 帧")
    return frames


def remove_background(frame_paths, output_dir, reference_image_path=None):
    """使用 rembg 去除背景，并在 02_nobg 阶段完成防裁切安全构图与前景颜色回正。"""
    os.makedirs(output_dir, exist_ok=True)
    results = []

    try:
        from rembg import remove as rembg_remove
        from PIL import Image

        for i, fp in enumerate(frame_paths):
            img = Image.open(fp)
            out = rembg_remove(img)
            save_path = Path(output_dir) / fp.name
            out.save(save_path)
            results.append(save_path)
            if (i + 1) % 10 == 0:
                print(f"\r  [去背景] {i+1}/{len(frame_paths)}", end="", flush=True)
        print(f"\r  [去背景] rembg 处理完成，{len(results)} 帧")

    except ImportError:
        print("  [去背景] rembg 未安装，跳过去背景步骤")
        for fp in frame_paths:
            save_path = Path(output_dir) / fp.name
            shutil.copy2(fp, save_path)
            results.append(save_path)

    _normalize_foreground_sequence(results)
    if reference_image_path:
        _apply_reference_color_match_to_frames(results, reference_image_path)
    return results


def pixelize_frames(frame_paths, output_dir, pixel_size=4, palette_colors=32):
    """像素化 + 调色板量化后处理"""
    from PIL import Image
    os.makedirs(output_dir, exist_ok=True)
    results = []

    for fp in frame_paths:
        img = Image.open(fp).convert("RGBA")
        w, h = img.size

        # 缩小再放大实现像素化
        small = img.resize((max(1, w // pixel_size), max(1, h // pixel_size)), Image.NEAREST)
        pixelized = small.resize((w, h), Image.NEAREST)

        # 分离 alpha 通道
        r, g, b, a = pixelized.split()
        rgb = Image.merge("RGB", (r, g, b))

        # 调色板量化
        quantized = rgb.quantize(colors=palette_colors, method=Image.Quantize.MEDIANCUT)
        quantized_rgb = quantized.convert("RGB")

        # 合并回 alpha
        final = Image.merge("RGBA", (*quantized_rgb.split(), a))

        save_path = Path(output_dir) / fp.name
        final.save(save_path)
        results.append(save_path)

    print(f"  [像素化] {len(results)} 帧已量化 (pixel_size={pixel_size}, colors={palette_colors})")
    return results


def _estimate_background_color(rgb_array):
    h, w = rgb_array.shape[:2]
    sample = max(1, min(h, w) // 12)
    corners = np.concatenate([
        rgb_array[:sample, :sample].reshape(-1, 3),
        rgb_array[:sample, -sample:].reshape(-1, 3),
        rgb_array[-sample:, :sample].reshape(-1, 3),
        rgb_array[-sample:, -sample:].reshape(-1, 3),
    ], axis=0)
    return np.median(corners, axis=0).astype(np.uint8)


def _erode_mask(mask, iterations=1):
    result = mask.astype(bool)
    for _ in range(max(0, int(iterations))):
        padded = np.pad(result, 1, mode="constant", constant_values=False)
        neighbors = [
            padded[1 + dy:1 + dy + result.shape[0], 1 + dx:1 + dx + result.shape[1]]
            for dy in (-1, 0, 1)
            for dx in (-1, 0, 1)
        ]
        result = np.logical_and.reduce(neighbors)
    return result


def _dilate_mask(mask, iterations=1):
    result = mask.astype(bool)
    for _ in range(max(0, int(iterations))):
        padded = np.pad(result, 1, mode="constant", constant_values=False)
        neighbors = [
            padded[1 + dy:1 + dy + result.shape[0], 1 + dx:1 + dx + result.shape[1]]
            for dy in (-1, 0, 1)
            for dx in (-1, 0, 1)
        ]
        result = np.logical_or.reduce(neighbors)
    return result


def _largest_connected_component(mask):
    mask = mask.astype(bool)
    if not np.any(mask):
        return mask

    h, w = mask.shape
    visited = np.zeros((h, w), dtype=bool)
    best_pixels = []

    for y in range(h):
        for x in range(w):
            if not mask[y, x] or visited[y, x]:
                continue
            queue = deque([(y, x)])
            visited[y, x] = True
            pixels = []
            while queue:
                cy, cx = queue.popleft()
                pixels.append((cy, cx))
                for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not visited[ny, nx]:
                        visited[ny, nx] = True
                        queue.append((ny, nx))
            if len(pixels) > len(best_pixels):
                best_pixels = pixels

    cleaned = np.zeros((h, w), dtype=bool)
    if best_pixels:
        ys, xs = zip(*best_pixels)
        cleaned[list(ys), list(xs)] = True
    return cleaned


def _mask_bbox(mask):
    ys, xs = np.where(mask)
    if len(xs) == 0 or len(ys) == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())


def _extract_foreground_mask(image):
    rgba = image.convert("RGBA")
    arr = np.array(rgba, dtype=np.uint8)
    alpha = arr[..., 3]
    if np.any(alpha > 8):
        mask = alpha > 24
    else:
        rgb = arr[..., :3]
        bg = _estimate_background_color(rgb).astype(np.int16)
        diff = np.linalg.norm(rgb.astype(np.int16) - bg, axis=2)
        mask = diff > 18.0
        if mask.mean() > 0.95:
            luminance = np.abs(rgb.astype(np.int16).mean(axis=2) - int(bg.mean()))
            mask = luminance > 20

    mask = _largest_connected_component(mask)
    if np.any(mask):
        mask = _dilate_mask(mask, iterations=1)
        mask = _erode_mask(mask, iterations=1)
        arr[..., 3] = np.where(mask, np.maximum(arr[..., 3], 255), 0).astype(np.uint8)
    return mask, arr


def _normalize_foreground_sequence(frame_paths, safe_margin_ratio=0.05):
    from PIL import Image

    if not frame_paths:
        return False

    frames = []
    bboxes = []
    for fp in frame_paths:
        img = Image.open(fp).convert("RGBA")
        mask, arr = _extract_foreground_mask(img)
        bbox = _mask_bbox(mask)
        frames.append((fp, arr, mask, bbox))
        if bbox is not None:
            bboxes.append(bbox)

    if not bboxes:
        return False

    w = int(frames[0][1].shape[1])
    h = int(frames[0][1].shape[0])
    union_left = min(b[0] for b in bboxes)
    union_top = min(b[1] for b in bboxes)
    union_right = max(b[2] for b in bboxes)
    union_bottom = max(b[3] for b in bboxes)
    union_w = max(1, union_right - union_left + 1)
    union_h = max(1, union_bottom - union_top + 1)

    margin_x = max(int(round(w * 0.06)), 8)
    margin_y = max(int(round(h * safe_margin_ratio)), 12)
    target_w = max(1, w - margin_x * 2)
    target_h = max(1, h - margin_y * 2)
    scale = min(1.0, target_w / union_w, target_h / union_h)

    union_cx = (union_left + union_right) / 2.0
    union_cy = (union_top + union_bottom) / 2.0
    target_cx = (w - 1) / 2.0
    target_cy = (h - 1) / 2.0
    offset_x = int(round(target_cx - union_cx * scale))
    offset_y = int(round(target_cy - union_cy * scale))

    changed = False
    for fp, arr, mask, bbox in frames:
        rgba_img = Image.fromarray(arr, mode="RGBA")
        if scale < 0.999:
            new_size = (
                max(1, int(round(rgba_img.width * scale))),
                max(1, int(round(rgba_img.height * scale))),
            )
            rgba_img = rgba_img.resize(new_size, Image.Resampling.BICUBIC)
        canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        canvas.alpha_composite(rgba_img, dest=(offset_x, offset_y))
        clean_mask, clean_arr = _extract_foreground_mask(canvas)
        clean_arr[..., 3] = np.where(clean_mask, clean_arr[..., 3], 0).astype(np.uint8)
        Image.fromarray(clean_arr, mode="RGBA").save(fp)
        changed = True

    if changed:
        print(
            "  [防裁切] 已对 02_nobg 序列做安全构图重排："
            f"scale={scale:.3f}, offset=({offset_x},{offset_y}), margin_y={margin_y}px"
        )
    return changed


def _masked_histogram_match_channel(source_channel, source_mask, reference_channel, reference_mask):
    src_vals = source_channel[source_mask]
    ref_vals = reference_channel[reference_mask]
    if src_vals.size < 64 or ref_vals.size < 64:
        return None

    src_hist = np.bincount(src_vals, minlength=256).astype(np.float64)
    ref_hist = np.bincount(ref_vals, minlength=256).astype(np.float64)
    src_cdf = np.cumsum(src_hist)
    ref_cdf = np.cumsum(ref_hist)
    if src_cdf[-1] <= 0 or ref_cdf[-1] <= 0:
        return None

    src_cdf /= src_cdf[-1]
    ref_cdf /= ref_cdf[-1]
    lookup = np.interp(src_cdf, ref_cdf, np.arange(256))
    matched = lookup[source_channel[source_mask]]
    return np.clip(matched, 0, 255).astype(np.uint8)


def _apply_reference_color_match_to_frames(frame_paths, reference_image_path):
    from PIL import Image

    reference_image_path = Path(reference_image_path)
    if not frame_paths or not reference_image_path.exists():
        return False

    reference_image = Image.open(reference_image_path).convert("RGBA")
    reference_mask, reference_rgba = _extract_foreground_mask(reference_image)
    reference_pixels = int(reference_mask.sum())
    if reference_pixels < 128:
        print(f"  [颜色回正] 参考图前景像素不足，跳过帧级回正 (ref={reference_pixels})")
        return False

    updated = 0
    for fp in frame_paths:
        img = Image.open(fp).convert("RGBA")
        frame_mask, frame_rgba = _extract_foreground_mask(img)
        frame_pixels = int(frame_mask.sum())
        if frame_pixels < 128:
            continue

        matched_rgb = frame_rgba[..., :3].copy()
        changed_channels = 0
        for channel in range(3):
            matched_vals = _masked_histogram_match_channel(
                frame_rgba[..., channel], frame_mask,
                reference_rgba[..., channel], reference_mask,
            )
            if matched_vals is None:
                continue
            matched_rgb[..., channel][frame_mask] = matched_vals
            changed_channels += 1

        if changed_channels == 0:
            continue

        matched_rgba = np.concatenate([matched_rgb, frame_rgba[..., 3:4]], axis=2)
        matched_rgba = np.clip(matched_rgba, 0, 255).astype(np.uint8)
        Image.fromarray(matched_rgba, mode="RGBA").save(fp)
        updated += 1

    if updated:
        print(f"  [颜色回正] 已在 02_nobg 阶段完成逐帧回正：{updated}/{len(frame_paths)} 帧")
    return updated > 0


def apply_reference_color_match(sprite_sheet_path, reference_image_path, final_no_alpha_path):
    from PIL import Image

    sprite_sheet_path = Path(sprite_sheet_path)
    reference_image_path = Path(reference_image_path)
    final_no_alpha_path = Path(final_no_alpha_path)

    if not sprite_sheet_path.exists() or not reference_image_path.exists():
        print("  [颜色回正] 缺少 sprite sheet 或参考图，跳过")
        return False

    result_image = Image.open(sprite_sheet_path).convert("RGBA")
    reference_image = Image.open(reference_image_path).convert("RGBA")

    result_mask, result_rgba = _extract_foreground_mask(result_image)
    reference_mask, reference_rgba = _extract_foreground_mask(reference_image)

    result_pixels = int(result_mask.sum())
    reference_pixels = int(reference_mask.sum())
    if result_pixels < 128 or reference_pixels < 128:
        print(f"  [颜色回正] 前景像素不足，跳过 (result={result_pixels}, ref={reference_pixels})")
        rgb_no_alpha = np.array(result_image.convert("RGB"), dtype=np.uint8)
        Image.fromarray(rgb_no_alpha, mode="RGB").save(final_no_alpha_path)
        return False

    matched_rgb = result_rgba[..., :3].copy()
    changed_channels = 0
    for channel in range(3):
        matched_vals = _masked_histogram_match_channel(
            result_rgba[..., channel], result_mask,
            reference_rgba[..., channel], reference_mask,
        )
        if matched_vals is None:
            continue
        matched_rgb[..., channel][result_mask] = matched_vals
        changed_channels += 1

    if changed_channels == 0:
        print("  [颜色回正] 直方图匹配未生效，保留原图")
        rgb_no_alpha = np.array(result_image.convert("RGB"), dtype=np.uint8)
        Image.fromarray(rgb_no_alpha, mode="RGB").save(final_no_alpha_path)
        return False

    alpha = result_rgba[..., 3:4]
    matched_rgba = np.concatenate([matched_rgb, alpha], axis=2)
    matched_rgba = np.clip(matched_rgba, 0, 255).astype(np.uint8)
    Image.fromarray(matched_rgba, mode="RGBA").save(sprite_sheet_path)

    alpha_float = (alpha.astype(np.float32) / 255.0)
    rgb_no_alpha = np.clip(matched_rgb.astype(np.float32) * alpha_float, 0, 255).astype(np.uint8)
    Image.fromarray(rgb_no_alpha, mode="RGB").save(final_no_alpha_path)
    print(f"  [颜色回正] 已按参考图回正并写回: {sprite_sheet_path.name} / {final_no_alpha_path.name}")
    return True


def assemble_sprite_sheet(frame_paths, output_path, cols=8, metadata_path=None):
    """将帧序列拼合成 Sprite Sheet"""
    from PIL import Image

    if not frame_paths:
        print("  [错误] 没有帧可以拼合")
        return None

    sample = Image.open(frame_paths[0])
    fw, fh = sample.size
    total = len(frame_paths)
    rows = math.ceil(total / cols)

    sheet = Image.new("RGBA", (fw * cols, fh * rows), (0, 0, 0, 0))

    for i, fp in enumerate(frame_paths):
        frame = Image.open(fp).convert("RGBA")
        col = i % cols
        row = i // cols
        sheet.paste(frame, (col * fw, row * fh))

    sheet.save(output_path)
    print(f"  [Sprite Sheet] {total} 帧 → {cols}x{rows} 网格 → {output_path}")

    if metadata_path:
        meta = {
            "sprite_sheet": os.path.basename(output_path),
            "frame_width": fw,
            "frame_height": fh,
            "total_frames": total,
            "columns": cols,
            "rows": rows,
            "fps": 16,
            "frames": []
        }
        for i in range(total):
            col = i % cols
            row = i // cols
            meta["frames"].append({
                "index": i,
                "x": col * fw,
                "y": row * fh,
                "w": fw,
                "h": fh
            })
        with open(metadata_path, "w") as f:
            json.dump(meta, f, indent=2)
        print(f"  [元数据] → {metadata_path}")

    return output_path


# ============================================================
# 离线后处理模式（不需要 ComfyUI）
# ============================================================

def postprocess_video(video_path, config):
    """对已有视频执行后处理流水线"""
    output_dir = Path(config["output_dir"])
    output_dir.mkdir(parents=True, exist_ok=True)

    frames_dir = output_dir / "01_frames"
    frames = video_to_frames(video_path, frames_dir)
    if not frames:
        print("[错误] 拆帧失败")
        return

    if config["remove_bg"]:
        nobg_dir = output_dir / "02_nobg"
        frames = remove_background(frames, nobg_dir, config.get("_reference_image_path"))

    pixel_dir = output_dir / "03_pixelized"
    frames = pixelize_frames(
        frames, pixel_dir,
        pixel_size=config["pixel_size"],
        palette_colors=config["palette_colors"]
    )

    sheet_path = output_dir / "sprite_sheet.png"
    meta_path = output_dir / "sprite_meta.json"
    final_no_alpha_path = output_dir / "final_no_alpha.png"
    assemble_sprite_sheet(frames, sheet_path, cols=config["sprite_cols"], metadata_path=meta_path)

    reference_image_path = config.get("_reference_image_path")
    if reference_image_path:
        try:
            apply_reference_color_match(sheet_path, reference_image_path, final_no_alpha_path)
        except Exception as e:
            print(f"  [颜色回正] 执行失败，保留原图: {e}")
            from PIL import Image
            Image.open(sheet_path).convert("RGB").save(final_no_alpha_path)
    else:
        from PIL import Image
        Image.open(sheet_path).convert("RGB").save(final_no_alpha_path)
        print("  [颜色回正] 未提供参考图，已输出未带 Alpha 的最终成图")

    print(f"\n{'='*50}")
    print(f"  完成！最终产出:")
    print(f"  Sprite Sheet: {sheet_path}")
    print(f"  Final RGB:    {final_no_alpha_path}")
    print(f"  元数据:       {meta_path}")
    print(f"{'='*50}")

    # 自动适配：改名 + 复制到 Art 仓库约定目录
    if HAS_ADAPTER:
        _action = config.get("_action_type", "unknown")
        _character = config.get("_character", "trickster")
        _asset_type = config.get("_asset_type", "character")
        if _action != "unknown":
            print(f"\n[产出物适配] 正在将产出物适配到 Art 仓库...")
            adapt_result = asset_adapter.adapt_output(
                str(output_dir), _action, _character, _asset_type
            )
            if adapt_result:
                print(f"  [产出物适配] ✓ 已适配: {adapt_result['sheet']}")
            else:
                print(f"  [产出物适配] △ 适配未执行，产出物保留在原位")


# ============================================================
# 主流程
# ============================================================

def _find_latest_video_from_comfyui(local_output_dir):
    """
    兜底方案：当 API 未返回视频信息时，
    直接去 ComfyUI output 目录找最近 5 分钟内生成的 mp4 文件。
    找到后复制到本地 output 目录。
    """
    cfg = config_manager.load_config()
    comfyui_root = cfg.get("comfyui_root", "")
    if not comfyui_root:
        print("  [错误] 未配置 ComfyUI 路径，无法查找输出")
        return None

    comfyui_output = Path(comfyui_root) / "output"
    if not comfyui_output.exists():
        print(f"  [错误] ComfyUI output 目录不存在: {comfyui_output}")
        return None

    # 找最近 5 分钟内的 mp4 文件
    import glob as _glob
    now = time.time()
    candidates = []
    for mp4 in comfyui_output.glob("*.mp4"):
        age = now - mp4.stat().st_mtime
        if age < 300:  # 5 分钟内
            candidates.append((mp4, age))

    if not candidates:
        # 放宽到最近 30 分钟
        for mp4 in comfyui_output.glob("*.mp4"):
            age = now - mp4.stat().st_mtime
            if age < 1800:
                candidates.append((mp4, age))

    if not candidates:
        print("  [错误] ComfyUI output 目录中未找到最近生成的 mp4 文件")
        return None

    # 按修改时间排序，取最新的
    candidates.sort(key=lambda x: x[1])
    source = candidates[0][0]
    dest = Path(local_output_dir) / f"generated_{source.name}"
    Path(local_output_dir).mkdir(parents=True, exist_ok=True)
    shutil.copy2(str(source), str(dest))
    print(f"  ✓ 从 ComfyUI output 找到视频: {source.name}")
    print(f"  ✓ 已复制到: {dest}")
    return dest


def run_full_pipeline(args, config):
    """完整管线：上传 → 生成 → 下载 → 后处理"""

    print("=" * 60)
    print("  MarioTrickster WAN 2.2 Animate 自动化管线")
    print("=" * 60)

    server = config["server"]
    client_id = str(uuid.uuid4())

    # 1. 上传素材
    print("\n[1/5] 上传素材到 ComfyUI...")
    ref_name = os.path.basename(args.ref)
    vid_name = os.path.basename(args.video)

    try:
        upload_image(server, args.ref)
        print(f"  ✓ 参考图: {ref_name}")
        upload_image(server, args.video)
        print(f"  ✓ 驱动视频: {vid_name}")
    except Exception as e:
        print(f"  [错误] 上传失败: {e}")
        print(f"  [提示] 请确认 ComfyUI 正在运行于 {server}")
        sys.exit(1)

    # 2. 构建工作流
    print(f"\n[2/5] 构建工作流 (动作类型: {args.action})...")
    workflow, seed = build_workflow(
        config, ref_name, vid_name,
        action_type=args.action,
        custom_prompt=args.prompt
    )
    print(f"  ✓ Seed: {seed}")
    print(f"  ✓ 分辨率: {config['width']}x{config['height']}")
    print(f"  ✓ 帧数: {config['length']}, 步数: {config['steps']}, CFG: {config['cfg']}")

    # 保存工作流副本
    output_dir = Path(config["output_dir"])
    output_dir.mkdir(parents=True, exist_ok=True)
    wf_path = output_dir / "workflow_submitted.json"
    with open(wf_path, "w") as f:
        json.dump(workflow, f, indent=2)
    print(f"  ✓ 工作流已保存: {wf_path}")

    # 3. 提交并等待
    print(f"\n[3/5] 提交工作流并等待生成...")
    try:
        prompt_id = queue_prompt(server, workflow, client_id)
        print(f"  ✓ Prompt ID: {prompt_id}")
        success = wait_for_completion(server, prompt_id, client_id, timeout=args.timeout)
        if not success:
            print("  [错误] 生成失败或超时")
            sys.exit(1)
    except Exception as e:
        print(f"  [错误] 提交失败: {e}")
        # 尝试获取更详细的错误信息
        try:
            import urllib.request
            err_resp = e.read() if hasattr(e, 'read') else None
            if err_resp:
                err_data = json.loads(err_resp)
                print(f"  [详情] {json.dumps(err_data, indent=2, ensure_ascii=False)[:800]}")
        except Exception:
            pass
        sys.exit(1)

    # 4. 下载结果
    print(f"\n[4/5] 下载生成的视频...")
    try:
        history = get_history(server, prompt_id)
        outputs = history[prompt_id]["outputs"]
        video_path = None
        for node_id, node_out in outputs.items():
            # SaveVideo 输出格式
            if "videos" in node_out:
                for vid in node_out["videos"]:
                    save_path = output_dir / f"generated_{vid['filename']}"
                    download_output(server, vid["filename"], vid.get("subfolder", ""), vid["type"], save_path)
                    video_path = save_path
                    print(f"  ✓ 视频已下载: {save_path}")
            if "gifs" in node_out:
                for gif in node_out["gifs"]:
                    save_path = output_dir / f"generated_{gif['filename']}"
                    download_output(server, gif["filename"], gif.get("subfolder", ""), gif["type"], save_path)
                    video_path = save_path
                    print(f"  ✓ 视频已下载: {save_path}")

        if not video_path:
            # API 未返回视频信息，尝试从 ComfyUI output 目录直接找最新 mp4
            print("  [提示] API 未返回视频信息，尝试从 ComfyUI output 目录查找...")
            video_path = _find_latest_video_from_comfyui(output_dir)
            if not video_path:
                print("  [警告] 未找到视频输出，请检查 ComfyUI output 目录")
                sys.exit(1)
    except Exception as e:
        print(f"  [错误] 下载失败: {e}")
        # 兜底：尝试从 ComfyUI output 目录直接找
        print("  [提示] 尝试从 ComfyUI output 目录查找...")
        video_path = _find_latest_video_from_comfyui(output_dir)
        if not video_path:
            sys.exit(1)

    # 5. 后处理
    print(f"\n[5/5] 后处理：拆帧 → 去背景 → 像素化 → Sprite Sheet...")
    postprocess_video(str(video_path), config)

    # 6. 自动质检
    return str(video_path)


# ============================================================
# 固定目录扫描：自动从 assets/ 选取素材
# ============================================================

ASSETS_REFS_DIR   = Path(__file__).parent / "assets" / "refs"
ASSETS_VIDEOS_DIR = Path(__file__).parent / "assets" / "videos"
ASSETS_FBX_DIR    = Path(__file__).parent / "assets" / "fbx"

REF_EXTS   = {".png", ".jpg", ".jpeg", ".webp"}
VIDEO_EXTS = {".mp4", ".mov", ".webm", ".avi", ".mkv"}
FBX_EXTS   = {".fbx"}


def _pick_file(directory: Path, exts: set, label: str) -> str:
    """
    从指定目录扫描文件：
    - 只有一个文件 → 直接返回
    - 多个文件 → 列出编号让用户选择
    - 目录为空 → 返回 None
    """
    directory.mkdir(parents=True, exist_ok=True)
    files = sorted([f for f in directory.iterdir() if f.suffix.lower() in exts])

    if not files:
        return None

    if len(files) == 1:
        print(f"  [{label}] 自动选择: {files[0].name}")
        return str(files[0])

    print(f"\n  [{label}] 请选择一个文件:")
    for i, f in enumerate(files, 1):
        print(f"    {i}. {f.name}")
    while True:
        try:
            choice = int(input(f"  输入编号 (1-{len(files)}): ").strip())
            if 1 <= choice <= len(files):
                return str(files[choice - 1])
        except (ValueError, KeyboardInterrupt):
            pass
        print(f"  请输入 1 到 {len(files)} 之间的数字")


def render_fbx_to_video(fbx_path: str, output_video_path: str, action: str) -> bool:
    """
    调用 Blender 将 FBX 渲染为驱动视频。
    自动从 config 读取 Blender 路径，路径失效时自动扫描修复。
    """
    cfg_data = config_manager.load_config()
    blender_exe = cfg_data.get("blender_exe", "")

    if not blender_exe or not os.path.isfile(blender_exe):
        print(f"  [错误] 找不到 Blender，请检查 pipeline_config.json 中的 blender_exe 配置")
        return False

    blender_script = str(Path(__file__).parent / "blender_render_drive_video.py")
    cmd = [
        blender_exe, "--background", "--python", blender_script, "--",
        "--input", fbx_path,
        "--output", output_video_path,
        "--preset", action,
        "--auto-detect",
    ]

    print(f"  [Blender] 渲染中: {os.path.basename(fbx_path)} → {os.path.basename(output_video_path)}")
    import subprocess
    try:
        result = subprocess.run(cmd, capture_output=False, timeout=300)
        if result.returncode == 0 and os.path.exists(output_video_path):
            print(f"  [Blender] 渲染完成: {output_video_path}")
            return True
        else:
            print(f"  [Blender] 渲染失败，返回码: {result.returncode}")
            return False
    except subprocess.TimeoutExpired:
        print("  [Blender] 渲染超时（300s）")
        return False
    except Exception as e:
        print(f"  [Blender] 渲染出错: {e}")
        return False


def main():
    parser = argparse.ArgumentParser(
        description="MarioTrickster WAN 2.2 一键动画管线",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
示例:
  # 完整管线（需要 ComfyUI 运行中）
  python run_pipeline.py --ref trickster.png --video run_mixamo.mp4 --action run

  # 仅后处理已有视频（不需要 ComfyUI）
  python run_pipeline.py --postprocess-only --video output/generated_video.mp4

  # 自定义 Prompt
  python run_pipeline.py --ref trickster.png --video dance.mp4 --prompt "pixel art character dancing"

可用动作类型: run, idle, jump, attack, walk, death, custom
        """
    )

    parser.add_argument("--ref", type=str, help="参考图路径 (Trickster 角色图)")
    parser.add_argument("--video", type=str, required=False, help="驱动视频路径 (Mixamo 导出的 MP4)")
    parser.add_argument("--action", type=str, default="custom",
                        help="动作类型，自动匹配 Prompt 模板 (默认: custom)。可用动作: " + ", ".join(PROMPT_TEMPLATES.keys()))
    parser.add_argument("--prompt", type=str, default=None, help="自定义正向 Prompt (覆盖模板)")
    parser.add_argument("--server", type=str, default=None, help="ComfyUI 服务器地址 (默认: 127.0.0.1:8188)")
    parser.add_argument("--output", type=str, default=None, help="输出目录 (默认: ./output)")
    parser.add_argument("--width", type=int, default=None, help="生成宽度 (默认: 480)")
    parser.add_argument("--height", type=int, default=None, help="生成高度 (默认: 480)")
    parser.add_argument("--length", type=int, default=None, help="生成帧数 (默认: 33, 必须为4n+1)")
    parser.add_argument("--steps", type=int, default=None, help="采样步数 (默认: 6)")
    parser.add_argument("--seed", type=int, default=None, help="随机种子 (-1=随机)")
    parser.add_argument("--pixel-size", type=int, default=None, help="像素化因子 (默认: 4)")
    parser.add_argument("--palette", type=int, default=None, help="调色板颜色数 (默认: 32)")
    parser.add_argument("--no-bg-remove", action="store_true", help="跳过去背景步骤")
    parser.add_argument("--timeout", type=int, default=600, help="生成超时秒数 (默认: 600)")
    parser.add_argument("--postprocess-only", action="store_true", help="仅执行后处理（跳过 ComfyUI 生成）")
    parser.add_argument("--character", type=str, default="trickster",
                        help="角色名，用于产出物命名和目录分类 (默认: trickster)")
    parser.add_argument("--asset-type", type=str, default="character",
                        choices=["character", "enemy", "environment", "hazard", "vfx", "prop", "ui"],
                        help="资产类型，决定产出物放入 Art 仓库的哪个子目录 (默认: character)")
    parser.add_argument("--no-adapt", action="store_true",
                        help="跳过产出物自动适配（不复制到 Art 仓库）")
    parser.add_argument("--list-actions", action="store_true", help="列出所有可用动作类型及其 Mixamo 建议")

    args = parser.parse_args()

    # 列出动作类型
    if args.list_actions:
        print("\n可用动作类型及 Mixamo 选片建议:")
        print("=" * 60)
        for name, tmpl in PROMPT_TEMPLATES.items():
            print(f"\n  [{name}]")
            print(f"  Prompt: {tmpl['positive'][:80]}...")
            print(f"  Mixamo: {tmpl['mixamo_tips']}")
        sys.exit(0)

    # 应用配置覆盖
    config = DEFAULT_CONFIG.copy()
    if args.server: config["server"] = args.server
    if args.output: config["output_dir"] = args.output
    if args.width: config["width"] = args.width
    if args.height: config["height"] = args.height
    if args.length: config["length"] = args.length
    if args.steps: config["steps"] = args.steps
    if args.seed is not None: config["seed"] = args.seed
    if args.pixel_size: config["pixel_size"] = args.pixel_size
    if args.palette: config["palette_colors"] = args.palette
    if args.no_bg_remove: config["remove_bg"] = False

    # 注入动作类型和角色名到 config，供产出物适配器使用
    config["_action_type"] = args.action
    config["_character"] = args.character
    config["_asset_type"] = args.asset_type
    if getattr(args, "no_adapt", False):
        config["_action_type"] = "unknown"  # 禁用适配器

    # 知识驱动的首次参数优化：根据 action 类型自动应用蒸馏知识中的最优参数
    # 只在用户未手动指定对应参数时才生效，手动指定的优先级最高
    if HAS_KNOWLEDGE and not args.postprocess_only:
        optimal = knowledge_loader.get_optimal_params(
            args.action,
            config,
            project_overrides=config_data.get("project_generation_overrides", {}),
            runtime=config_data.get("runtime", {}),
        )
        user_overrides = {
            "width": args.width, "height": args.height, "length": args.length,
            "steps": args.steps, "cfg": None, "pixel_size": args.pixel_size,
            "palette_colors": args.palette,
        }
        applied = []
        for key, user_val in user_overrides.items():
            if user_val is None and key in optimal and optimal[key] != config.get(key):
                config[key] = optimal[key]
                applied.append(f"{key}={optimal[key]}")
        if applied:
            print(f"[\u77e5\u8bc6\u4f18\u5316] \u9996\u6b21\u751f\u6210\u53c2\u6570\u5df2\u6839\u636e\u84b8\u998f\u77e5\u8bc6\u4e0e\u9879\u76ee\u6863\u4f4d\u8c03\u6574: {', '.join(applied)}")

    if HAS_KNOWLEDGE:
        safe_keys = ("width", "height", "length", "steps", "cfg", "pixel_size", "palette_colors")
        safe_input = {k: config[k] for k in safe_keys if k in config}
        safe_params, guard = knowledge_loader.get_runtime_safe_params(
            safe_input,
            config_data.get("runtime", {}),
        )
        clamp_applied = []
        for key, value in safe_params.items():
            if key in config and config.get(key) != value:
                clamp_applied.append(f"{key}={config.get(key)}→{value}")
                config[key] = value
        if clamp_applied:
            print(f"[12GB护栏] 已按项目显存预算收敛参数: {', '.join(clamp_applied)}")
        elif guard.get("changed"):
            compact = ", ".join(f"{k}={old}→{new}" for k, old, new in guard["changed"])
            print(f"[12GB护栏] 已执行规格归一化: {compact}")
        print(f"[运行档位] profile={guard.get('profile')} vram={guard.get('vram_gb')}GB budget={guard.get('budget')} final={guard.get('final_cost')}")

    # 仅后处理模式
    if args.ref and os.path.exists(args.ref):
        config["_reference_image_path"] = args.ref

    if args.postprocess_only:
        if not args.video or not os.path.exists(args.video):
            print(f"[错误] 视频文件不存在: {args.video}")
            sys.exit(1)
        postprocess_video(args.video, config)
        return

    # 完整管线
    # 如果未手动指定 --ref，自动从 assets/refs/ 扫描
    if not args.ref:
        print("\n[扫描素材] 从 assets/refs/ 自动选取参考图...")
        args.ref = _pick_file(ASSETS_REFS_DIR, REF_EXTS, "参考图")
        if not args.ref:
            print(f"[错误] assets/refs/ 目录为空，请将角色参考图放入: {ASSETS_REFS_DIR}")
            sys.exit(1)

    if not os.path.exists(args.ref):
        print(f"[错误] 参考图不存在: {args.ref}")
        sys.exit(1)
    config["_reference_image_path"] = args.ref

    # 如果未手动指定 --video，先扫描 assets/videos/，再扫描 assets/fbx/
    if not args.video:
        print("\n[扫描素材] 从 assets/videos/ 自动选取驱动视频...")
        args.video = _pick_file(ASSETS_VIDEOS_DIR, VIDEO_EXTS, "驱动视频")

        # videos/ 为空时，自动从 fbx/ 扫描并用 Blender 渲染
        if not args.video:
            print("  assets/videos/ 为空，扫描 assets/fbx/ 尝试自动渲染...")
            fbx_path = _pick_file(ASSETS_FBX_DIR, FBX_EXTS, "FBX 文件")
            if not fbx_path:
                print(f"[错误] assets/videos/ 和 assets/fbx/ 都为空")
                print(f"  请将驱动视频放入: {ASSETS_VIDEOS_DIR}")
                print(f"  或将 Mixamo FBX 放入: {ASSETS_FBX_DIR}")
                sys.exit(1)

            # 自动渲染：FBX → 驱动视频
            fbx_stem = Path(fbx_path).stem
            rendered_video = str(ASSETS_VIDEOS_DIR / f"{fbx_stem}_drive.mp4")
            ASSETS_VIDEOS_DIR.mkdir(parents=True, exist_ok=True)
            print(f"\n[自动渲染] 调用 Blender 渲染驱动视频...")
            ok = render_fbx_to_video(fbx_path, rendered_video, args.action)
            if not ok:
                print("[错误] Blender 渲染失败，请检查上方错误信息")
                sys.exit(1)
            args.video = rendered_video

    if not os.path.exists(args.video):
        print(f"[错误] 驱动视频不存在: {args.video}")
        sys.exit(1)

    # ============================================================
    # 自动质检 + 智能迭代循环（知识驱动版）
    # ============================================================
    # 首次生成已用知识优化参数，迭代次数从 3 降为 2
    MAX_ITERATIONS = 2
    iteration = 0
    _last_video_path = None

    while iteration < MAX_ITERATIONS:
        iteration += 1
        if iteration > 1:
            print(f"\n{'='*60}")
            print(f"  智能迭代第 {iteration} 轮（知识驱动调参）")
            print(f"{'='*60}")
            import shutil as _shutil
            out_dir = Path(config["output_dir"])
            if out_dir.exists():
                _shutil.rmtree(out_dir)
                print(f"  [清理] 已删除上一轮不合格产出: {out_dir}")
            if _last_video_path and os.path.exists(_last_video_path):
                try:
                    os.remove(_last_video_path)
                    print(f"  [清理] 已删除上一轮视频: {os.path.basename(_last_video_path)}")
                except Exception:
                    pass
            try:
                _cfg = config_manager.load_config()
                _comfyui_output = os.path.join(_cfg.get("comfyui_root", ""), "output")
                if os.path.isdir(_comfyui_output):
                    for _f in sorted(Path(_comfyui_output).glob("MarioTrickster_anim_*.mp4"), key=lambda x: x.stat().st_mtime):
                        if time.time() - _f.stat().st_mtime < 600:
                            _f.unlink()
                            print(f"  [清理] 已删除 ComfyUI 旧视频: {_f.name}")
            except Exception:
                pass

        video_path = run_full_pipeline(args, config)
        _last_video_path = video_path

        # 质检（传入 action_type 以启用知识驱动检测）
        try:
            import auto_qc
            qc_result = auto_qc.run_qc(args.ref, config["output_dir"], config, action_type=args.action)

            if qc_result.passed:
                print(f"\n  ✓ 质检通过，无需进一步调整")
                break
            else:
                if iteration >= MAX_ITERATIONS:
                    print(f"\n  △ 已达到最大迭代次数 ({MAX_ITERATIONS})，使用当前结果")
                    break

                effective_adjustments = sanitize_qc_adjustments(qc_result.adjustments, args.action, config, args)
                if effective_adjustments:
                    print(f"\n  [智能调参] 应用知识驱动的调整后重跑:")
                    for k, v in effective_adjustments.items():
                        old_val = config.get(k, "?")
                        config[k] = v
                        print(f"    {k}: {old_val} → {v}")
                else:
                    print(f"\n  △ 发现问题但当前档位已锁定自动调参，使用当前结果")
                    break
        except ImportError:
            print("\n  [质检] auto_qc 模块未找到，跳过自动迭代")
            break
        except Exception as e:
            print(f"\n  [质检] 质检出错: {e}，跳过自动迭代")
            break


if __name__ == "__main__":
    main()
