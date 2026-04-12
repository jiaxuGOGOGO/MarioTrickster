#!/usr/bin/env python3
"""
MarioTrickster 管线配置管理器（防变动 + 自愈版）
================================================
核心原则：
  1. pipeline_config.json 存在且路径有效 → 直接用
  2. config 丢失或路径失效 → 自动全盘扫描 ComfyUI，找到后自愈写回
  3. 扫描也找不到 → 打印精确提示，告诉你去哪里填一行就能修好
  4. 模型文件名用模糊匹配，版本号变了自动识别，不用改代码
"""

import os
import sys
import json
import glob
from pathlib import Path

# ─────────────────────────────────────────────
# 常量
# ─────────────────────────────────────────────

CONFIG_FILE = "pipeline_config.json"

# ComfyUI 根目录的特征文件（用于验证扫描结果是否真的是 ComfyUI）
COMFYUI_FINGERPRINTS = [
    "main.py",
    "comfy",
    "models",
    os.path.join("models", "diffusion_models"),
]

# Windows 全盘扫描候选根目录（按优先级排序）
SCAN_ROOTS_WINDOWS = [
    "E:\\", "D:\\", "C:\\", "F:\\", "G:\\",
]

# 每个盘下常见的安装子目录关键词
SCAN_SUBDIR_PATTERNS_WINDOWS = [
    "*ComfyUI*",
    "*comfyui*",
    "AI\\*ComfyUI*",
    "SD\\*ComfyUI*",
    "Stable*Diffusion\\*ComfyUI*",
    "tools\\*ComfyUI*",
]

# Blender 常见安装位置（按优先级排序）
BLENDER_SCAN_PATHS = [
    # 默认安装路径
    r"C:\Program Files\Blender Foundation",
    r"C:\Program Files (x86)\Blender Foundation",
    r"D:\Program Files\Blender Foundation",
    r"E:\Program Files\Blender Foundation",
    # 其他常见位置
    r"C:\Blender", r"D:\Blender", r"E:\Blender",
    r"C:\tools\Blender", r"D:\tools\Blender",
]

# 默认配置模板
DEFAULT_CONFIG_TEMPLATE = {
    "comfyui_root": "",   # 由自动扫描填入
    "blender_exe": "",    # 由自动扫描填入
    "server": "127.0.0.1:8188",
    "models": {
        "unet":        {"pattern": "Wan2*Animate*14B*.safetensors",          "folder": "diffusion_models"},
        "clip":        {"pattern": "umt5*xxl*.safetensors",                  "folder": "text_encoders"},
        "vae":         {"pattern": "wan*vae*.safetensors",                   "folder": "vae"},
        "clip_vision": {"pattern": "clip_vision_h*.safetensors",             "folder": "clip_vision"},
        "lora":        {"pattern": "lightx2v*I2V*14B*.safetensors",          "folder": "loras"},
    },
    "generation": {
        "width": 480, "height": 480, "length": 17,
        "steps": 6, "cfg": 1.0,
        "sampler": "euler", "scheduler": "simple", "seed": -1,
        "_note": "length 必须为 4n+1（12GB显存建议不超过 17，显存充裕可调至 33）"
    },
    "postprocess": {
        "pixel_size": 4, "palette_colors": 32,
        "remove_bg": True, "sprite_cols": 8,
        "output_dir": "./output",
    },
    "runtime": {
        "project_profile": "mariotrickster_12gb_safe",
        "vram_gb": 12,
        "enforce_vram_guard": True,
    },
    "project_generation_overrides": {
        "idle": {"width": 480, "height": 480, "length": 17, "steps": 6},
        "walk": {"width": 480, "height": 480, "length": 17, "steps": 6},
        "run": {"width": 480, "height": 480, "length": 17, "steps": 6},
        "jump": {"width": 416, "height": 544, "length": 17, "steps": 6},
        "attack_sword": {"width": 544, "height": 416, "length": 17, "steps": 6},
        "death": {"width": 544, "height": 416, "length": 17, "steps": 6},
        "dash": {"width": 480, "height": 480, "length": 17, "steps": 6},
    },
}


# ─────────────────────────────────────────────
# 核心：验证一个目录是否是合法的 ComfyUI 根目录
# ─────────────────────────────────────────────

def _is_valid_comfyui_root(path: str) -> bool:
    """检查目录是否包含 ComfyUI 的特征文件"""
    if not path or not os.path.isdir(path):
        return False
    hit = sum(1 for fp in COMFYUI_FINGERPRINTS if os.path.exists(os.path.join(path, fp)))
    return hit >= 2  # 至少命中 2 个特征才算


# ─────────────────────────────────────────────
# 核心：自动扫描 ComfyUI 安装位置
# ─────────────────────────────────────────────

def _auto_scan_comfyui() -> str | None:
    """
    在 Windows 各盘符下自动扫描 ComfyUI 安装目录。
    返回找到的第一个合法路径，找不到返回 None。
    """
    print("[自动扫描] 正在搜索 ComfyUI 安装位置，请稍候...")

    candidates = []

    for root in SCAN_ROOTS_WINDOWS:
        if not os.path.exists(root):
            continue
        for pattern in SCAN_SUBDIR_PATTERNS_WINDOWS:
            search = os.path.join(root, pattern)
            try:
                matches = glob.glob(search, recursive=False)
                for m in matches:
                    if os.path.isdir(m):
                        candidates.append(m)
            except Exception:
                pass

    # 对每个候选目录，递归向下找真正的 ComfyUI 根（包含 main.py 的那层）
    for candidate in candidates:
        # 先检查候选本身
        if _is_valid_comfyui_root(candidate):
            print(f"[自动扫描] 找到: {candidate}")
            return candidate
        # 再往下找一两层（portable 版本有多层嵌套）
        for sub in Path(candidate).rglob("main.py"):
            parent = str(sub.parent)
            if _is_valid_comfyui_root(parent):
                print(f"[自动扫描] 找到: {parent}")
                return parent

    print("[自动扫描] 未能自动找到 ComfyUI，需要手动配置。")
    return None


# ─────────────────────────────────────────────
# 核心：自动扫描 Blender 安装位置
# ─────────────────────────────────────────────

def _auto_scan_blender() -> str | None:
    """
    在常见安装位置自动扫描 Blender 可执行文件。
    支持多版本安装，自动选择最新版本。
    返回 blender.exe 的完整路径，找不到返回 None。
    """
    candidates = []

    for base in BLENDER_SCAN_PATHS:
        if not os.path.isdir(base):
            continue
        # 在基础目录下递归找 blender.exe
        for root, dirs, files in os.walk(base):
            if "blender.exe" in files:
                candidates.append(os.path.join(root, "blender.exe"))

    # 如果没找到，再全盘广播搜一次
    if not candidates:
        for drive in ["C:\\", "D:\\", "E:\\", "F:\\"]:
            if not os.path.exists(drive):
                continue
            for pattern in ["*Blender*", "*blender*"]:
                for match in glob.glob(os.path.join(drive, pattern)):
                    exe = os.path.join(match, "blender.exe")
                    if os.path.isfile(exe):
                        candidates.append(exe)
                    # 再向下一层
                    for sub in glob.glob(os.path.join(match, "*", "blender.exe")):
                        candidates.append(sub)

    if not candidates:
        return None

    # 按路径中的版本号排序，选最新版本
    def _version_key(path):
        import re
        nums = re.findall(r"(\d+\.\d+)", path)
        return [float(n) for n in nums] if nums else [0.0]

    candidates.sort(key=_version_key, reverse=True)
    best = candidates[0]
    print(f"[自动扫描] 找到 Blender: {best}")
    return best


# ─────────────────────────────────────────────
# 核心：加载配置（含自愈逻辑）
# ─────────────────────────────────────────────

def load_config() -> dict:
    """
    加载配置文件。
    - config 存在且 comfyui_root 有效 → 直接返回
    - config 不存在或 comfyui_root 失效 → 自动扫描修复后返回
    """
    config = _read_config_file()

    comfyui_root = config.get("comfyui_root", "")
    if _is_valid_comfyui_root(comfyui_root):
        pass  # ComfyUI 路径正常
    else:
        # ── ComfyUI 自愈流程 ──
        if comfyui_root:
            print(f"[警告] ComfyUI 路径失效: {comfyui_root}")
        else:
            print("[提示] 未找到 ComfyUI 路径配置，开始自动扫描...")
        found = _auto_scan_comfyui()
        if found:
            config["comfyui_root"] = found
            _write_config_file(config)
            print(f"[自愈成功] ComfyUI 路径已更新: {CONFIG_FILE}")
        else:
            _print_manual_fix_guide()

    # ── Blender 自愈流程 ──
    blender_exe = config.get("blender_exe", "")
    if not blender_exe or not os.path.isfile(blender_exe):
        if blender_exe:
            print(f"[警告] Blender 路径失效: {blender_exe}")
        found_blender = _auto_scan_blender()
        if found_blender:
            config["blender_exe"] = found_blender
            _write_config_file(config)
            print(f"[自愈成功] Blender 路径已更新: {found_blender}")

    return config


# ─────────────────────────────────────────────
# 模型模糊匹配
# ─────────────────────────────────────────────

def resolve_model(config: dict, model_key: str) -> tuple[str | None, str | None]:
    """
    根据模糊匹配规则查找实际模型文件名。
    采用全局递归搜索：在整个 models 目录下查找，不依赖固定子目录名。
    返回 (文件名, 错误信息)，成功时错误信息为 None。
    """
    comfyui_root = config.get("comfyui_root", "")
    if not _is_valid_comfyui_root(comfyui_root):
        return None, f"ComfyUI 根目录无效: {comfyui_root}"

    model_info = config.get("models", {}).get(model_key)
    if not model_info:
        return None, f"未知的模型键: {model_key}"

    pattern = model_info["pattern"]
    models_root = os.path.join(comfyui_root, "models")

    # 全局递归搜索：在整个 models 目录下找，不管放在哪个子目录
    search_path = os.path.join(models_root, "**", pattern)
    matches = glob.glob(search_path, recursive=True)

    if not matches:
        return None, f"未找到匹配模型（已搜索 models/ 全部子目录）: {pattern}"

    return os.path.basename(matches[0]), None


# ─────────────────────────────────────────────
# 环境验证
# ─────────────────────────────────────────────

def validate_environment(config: dict) -> tuple[bool, list, dict]:
    """
    验证 ComfyUI 路径和所有模型是否就绪。
    返回 (全部通过, 错误列表, 已解析模型字典)
    """
    errors = []
    resolved_models = {}

    comfyui_root = config.get("comfyui_root", "")
    if not _is_valid_comfyui_root(comfyui_root):
        errors.append(f"ComfyUI 根目录无效: {comfyui_root}")
        return False, errors, resolved_models

    for key in config.get("models", {}):
        filename, err = resolve_model(config, key)
        if err:
            errors.append(err)
        else:
            resolved_models[key] = filename

    return len(errors) == 0, errors, resolved_models


# ─────────────────────────────────────────────
# 内部工具函数
# ─────────────────────────────────────────────

def _read_config_file() -> dict:
    """读取 config 文件，不存在则返回默认模板"""
    if not os.path.exists(CONFIG_FILE):
        return dict(DEFAULT_CONFIG_TEMPLATE)
    try:
        with open(CONFIG_FILE, "r", encoding="utf-8") as f:
            return json.load(f)
    except (json.JSONDecodeError, IOError):
        print(f"[警告] {CONFIG_FILE} 读取失败，使用默认配置")
        return dict(DEFAULT_CONFIG_TEMPLATE)


def _write_config_file(config: dict):
    """写回 config 文件"""
    try:
        with open(CONFIG_FILE, "w", encoding="utf-8") as f:
            json.dump(config, f, indent=4, ensure_ascii=False)
    except IOError as e:
        print(f"[警告] 无法写入 {CONFIG_FILE}: {e}")


def save_config(config: dict):
    """公开接口：保存配置"""
    _write_config_file(config)


def _print_manual_fix_guide():
    """打印一次性手动修复指引（只有自动扫描也失败时才会出现）"""
    print()
    print("=" * 60)
    print("  [需要你做一件事] 自动扫描未找到 ComfyUI")
    print("=" * 60)
    print(f"  请打开 {CONFIG_FILE}，把第一行改成你的实际路径：")
    print()
    print('    "comfyui_root": "E:\\\\你的路径\\\\ComfyUI"')
    print()
    print("  改完保存，再运行脚本就自动恢复正常，以后不用再改。")
    print("=" * 60)
    print()


# ─────────────────────────────────────────────
# 独立运行：验证当前环境
# ─────────────────────────────────────────────

if __name__ == "__main__":
    print("=" * 60)
    print("  MarioTrickster 配置管理器 — 环境自检")
    print("=" * 60)

    config = load_config()
    ok, errors, models = validate_environment(config)

    print(f"\n[ComfyUI 根目录] {config.get('comfyui_root', '未配置')}")
    print(f"[ComfyUI 服务器] {config.get('server', '未配置')}")

    if ok:
        print("\n[模型] 全部就绪:")
        for k, v in models.items():
            print(f"  ✓  {k:12s} → {v}")
        print("\n✓ 环境正常，可以开始使用管线。")
    else:
        print("\n[模型] 以下项目有问题:")
        for err in errors:
            print(f"  ✗  {err}")
