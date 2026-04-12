#!/usr/bin/env python3
"""
MarioTrickster 管线环境检查 & 安装指引
========================================
运行此脚本检查你的环境是否满足管线要求。

用法:
  python setup_check.py
  python setup_check.py --install   # 自动安装缺失的 Python 依赖
"""

import argparse
import os
import sys
import json
import subprocess
from pathlib import Path


def check_python_package(name, install_name=None):
    """检查 Python 包是否已安装"""
    try:
        __import__(name)
        return True
    except ImportError:
        return False


def check_command(cmd):
    """检查系统命令是否可用"""
    try:
        subprocess.run([cmd, "--version"], capture_output=True, timeout=5)
        return True
    except (FileNotFoundError, subprocess.TimeoutExpired):
        return False


def check_comfyui_server(server="127.0.0.1:8188"):
    """检查 ComfyUI 是否在运行"""
    import urllib.request
    try:
        with urllib.request.urlopen(f"http://{server}/system_stats", timeout=3) as resp:
            data = json.loads(resp.read())
            vram = data.get("devices", [{}])[0].get("vram_total", 0)
            vram_gb = vram / (1024**3) if vram else 0
            return True, vram_gb
    except Exception:
        return False, 0


import config_manager

def check_comfyui_models(server="127.0.0.1:8188"):
    """检查 ComfyUI 中是否有所需模型"""
    config = config_manager.load_config()
    ok, errors, models = config_manager.validate_environment(config)
    if not ok:
        for err in errors:
            print(f"  [错误] {err}")
    return ok, models


def main():
    parser = argparse.ArgumentParser(description="环境检查")
    parser.add_argument("--install", action="store_true", help="自动安装缺失的 Python 依赖")
    parser.add_argument("--server", default="127.0.0.1:8188", help="ComfyUI 服务器地址")
    args = parser.parse_args()

    print("=" * 60)
    print("  MarioTrickster 管线环境检查")
    print("=" * 60)

    all_ok = True

    # 1. Python 版本
    print(f"\n[Python] {sys.version.split()[0]}", end="")
    if sys.version_info >= (3, 9):
        print(" ✓")
    else:
        print(" ✗ (需要 3.9+)")
        all_ok = False

    # 2. 必需 Python 包
    print("\n[Python 依赖]")
    packages = {
        "PIL": ("Pillow", "pillow"),
        "numpy": ("NumPy", "numpy"),
        "websocket": ("websocket-client", "websocket-client"),
    }
    optional_packages = {
        "rembg": ("rembg (去背景)", "rembg"),
    }

    for import_name, (display_name, pip_name) in packages.items():
        ok = check_python_package(import_name)
        status = "✓" if ok else "✗ (必需)"
        print(f"  {display_name:30s} {status}")
        if not ok:
            all_ok = False
            if args.install:
                print(f"    → 正在安装 {pip_name}...")
                subprocess.run([sys.executable, "-m", "pip", "install", pip_name, "-q"])

    for import_name, (display_name, pip_name) in optional_packages.items():
        ok = check_python_package(import_name)
        status = "✓" if ok else "○ (可选，推荐安装)"
        print(f"  {display_name:30s} {status}")
        if not ok and args.install:
            print(f"    → 正在安装 {pip_name}...")
            subprocess.run([sys.executable, "-m", "pip", "install", pip_name, "-q"])

    # 3. 系统工具
    print("\n[系统工具]")
    tools = {"ffmpeg": "视频拆帧必需"}
    for tool, desc in tools.items():
        ok = check_command(tool)
        status = "✓" if ok else f"✗ ({desc})"
        print(f"  {tool:30s} {status}")
        if not ok:
            all_ok = False

    # 4. ComfyUI 连接
    print(f"\n[ComfyUI 服务器] {args.server}")
    running, vram = check_comfyui_server(args.server)
    if running:
        print(f"  状态: ✓ 运行中")
        if vram > 0:
            print(f"  显存: {vram:.1f} GB", end="")
            if vram >= 12:
                print(" ✓ (14B 模型可用)")
            elif vram >= 8:
                print(" △ (建议使用 fp8 量化版)")
            else:
                print(" ✗ (显存不足，需要至少 8GB)")
    else:
        print(f"  状态: ○ 未运行（后处理模式仍可用）")

    # 5. 模型文件清单
    print("\n[所需模型文件]")
    models_ok, resolved_models = check_comfyui_models(args.server)
    if models_ok:
        for k, v in resolved_models.items():
            print(f"  ✓ {k}: {v}")
    else:
        all_ok = False
        print("  ✗ 模型文件缺失或路径配置错误，请检查 pipeline_config.json")

    # 6. 自定义节点
    print("\n[所需 ComfyUI 自定义节点]")
    custom_nodes = [
        ("ComfyUI-KJNodes", "https://github.com/kijai/ComfyUI-KJNodes"),
        ("comfyui_controlnet_aux", "https://github.com/Fannovel16/comfyui_controlnet_aux"),
    ]
    for name, url in custom_nodes:
        print(f"  {name}: {url}")

    # 汇总
    print("\n" + "=" * 60)
    if all_ok:
        print("  ✓ 环境检查通过！可以开始使用管线。")
    else:
        print("  △ 部分依赖缺失，请按上方提示安装。")
        print("  提示: 运行 python setup_check.py --install 自动安装 Python 依赖")
    print("=" * 60)


if __name__ == "__main__":
    main()
