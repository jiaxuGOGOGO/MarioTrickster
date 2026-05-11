#!/usr/bin/env python3
"""
MarioTrickster — AI Smart Slicer
==================================

用途：
  调用 GPT-4.1 视觉模型，让 AI 识别 Sprite Sheet / 合集图中每个独立物体或动画组的
  精确边界，然后按 AI 的判断自动裁切。

  相比纯像素连通域检测（--auto），AI 模式能：
  - 区分"8帧走路动画是一组"vs"旁边的树是另一个物体"
  - 识别有重叠/相邻但逻辑上独立的物体
  - 给每个裁切结果命名（如 "hero_walk_8frames", "tree_01", "spike_trap"）
  - 告诉你每组是动画帧还是单个静态物体

用法：
  python ai_smart_slicer.py <input_image> [options]

示例：
  # AI 智能分割（需要 OPENAI_API_KEY 环境变量）
  python ai_smart_slicer.py commercial_pack.png

  # 指定输出目录
  python ai_smart_slicer.py tileset.png -o ./sliced/

  # 去除背景色后再让 AI 分析
  python ai_smart_slicer.py sheet.png --remove-bg "#ff00ff"

  # 批量处理
  python ai_smart_slicer.py ./raw_assets/ -o ./sliced/

环境变量：
  OPENAI_API_KEY — OpenAI API Key（项目已预配置）

依赖：
  pip install Pillow numpy openai
"""

import argparse
import base64
import io
import json
import os
import sys
from pathlib import Path

try:
    from PIL import Image
    import numpy as np
except ImportError:
    print("请先安装依赖: pip install Pillow numpy")
    sys.exit(1)

try:
    from openai import OpenAI
except ImportError:
    print("请先安装 openai: pip install openai")
    sys.exit(1)


# =========================================================================
# AI 视觉分析
# =========================================================================

SYSTEM_PROMPT = """You are a 2D game art asset analyst. You will receive a sprite sheet or asset collection image.

Your job is to identify every distinct object or animation group in the image and return their bounding boxes.

Rules:
1. If multiple frames of the same character/object are arranged in a row or grid (e.g., walk cycle, attack animation), group them as ONE entry with type "animation" and specify the frame count.
2. If an object appears only once (static prop, single tile, etc.), mark it as type "static".
3. Coordinates are in pixels, origin at TOP-LEFT corner of the image.
4. Include a short English name for each object (snake_case, descriptive).
5. If frames in an animation group are evenly spaced, provide the grid info (cols, rows).
6. Be precise with bounding boxes — include the full extent of each object/group with ~2px padding but don't include unrelated objects.

Return ONLY valid JSON in this exact format (no markdown, no explanation):
{
  "image_width": <int>,
  "image_height": <int>,
  "objects": [
    {
      "name": "hero_walk_cycle",
      "type": "animation",
      "frame_count": 8,
      "grid_cols": 8,
      "grid_rows": 1,
      "bbox": {"x": 0, "y": 0, "w": 512, "h": 64},
      "description": "8-frame walk cycle of the main character"
    },
    {
      "name": "tree_01",
      "type": "static",
      "frame_count": 1,
      "grid_cols": 1,
      "grid_rows": 1,
      "bbox": {"x": 520, "y": 10, "w": 48, "h": 80},
      "description": "Single decorative tree"
    }
  ]
}"""


def encode_image_to_base64(img: Image.Image, max_size: int = 2048) -> str:
    """将图片编码为 base64，必要时缩放以控制 token 消耗"""
    # 如果图片太大，等比缩放
    w, h = img.size
    if max(w, h) > max_size:
        scale = max_size / max(w, h)
        new_w = int(w * scale)
        new_h = int(h * scale)
        img = img.resize((new_w, new_h), Image.LANCZOS)
    
    # 转为 PNG bytes
    buffer = io.BytesIO()
    img.save(buffer, format="PNG")
    return base64.b64encode(buffer.getvalue()).decode("utf-8")


def analyze_with_ai(img: Image.Image, model: str = "gpt-4.1-mini") -> dict:
    """调用视觉模型分析图片中的物体"""
    client = OpenAI()  # 自动读取 OPENAI_API_KEY
    
    original_size = img.size  # 记录原始尺寸
    b64 = encode_image_to_base64(img)
    
    # 如果图片被缩放了，需要告诉 AI 原始尺寸
    w, h = original_size
    user_msg = f"This sprite sheet is {w}x{h} pixels. Identify all distinct objects and animation groups. Return bounding boxes in the ORIGINAL {w}x{h} coordinate space."
    
    # 检查是否需要缩放坐标
    encoded_size = img.size
    if max(w, h) > 2048:
        scale = 2048 / max(w, h)
        encoded_w = int(w * scale)
        encoded_h = int(h * scale)
        user_msg += f"\n\nNote: The image was resized to {encoded_w}x{encoded_h} for transmission. Please scale your bounding box coordinates back to the original {w}x{h} dimensions."
    
    print(f"  正在调用 AI 视觉模型分析 ({model})...")
    
    response = client.chat.completions.create(
        model=model,
        messages=[
            {"role": "system", "content": SYSTEM_PROMPT},
            {
                "role": "user",
                "content": [
                    {"type": "text", "text": user_msg},
                    {
                        "type": "image_url",
                        "image_url": {
                            "url": f"data:image/png;base64,{b64}",
                            "detail": "high"
                        }
                    }
                ]
            }
        ],
        temperature=0.1,
        max_tokens=4096
    )
    
    result_text = response.choices[0].message.content.strip()
    
    # 尝试解析 JSON（处理可能的 markdown 包裹）
    if result_text.startswith("```"):
        # 去掉 markdown 代码块
        lines = result_text.split("\n")
        lines = [l for l in lines if not l.startswith("```")]
        result_text = "\n".join(lines)
    
    try:
        result = json.loads(result_text)
    except json.JSONDecodeError as e:
        print(f"  [警告] AI 返回的 JSON 解析失败: {e}")
        print(f"  原始返回: {result_text[:500]}")
        return None
    
    return result


# =========================================================================
# 裁切执行
# =========================================================================

def execute_ai_slicing(img: Image.Image, analysis: dict, output_dir: Path, 
                       base_name: str, padding: int = 2) -> list:
    """根据 AI 分析结果执行裁切"""
    results = []
    w, h = img.size
    
    objects = analysis.get("objects", [])
    if not objects:
        print("  [警告] AI 未检测到任何物体")
        return results
    
    print(f"  AI 识别到 {len(objects)} 个物体/动画组：")
    
    for i, obj in enumerate(objects):
        name = obj.get("name", f"object_{i:03d}")
        obj_type = obj.get("type", "static")
        frame_count = obj.get("frame_count", 1)
        bbox = obj.get("bbox", {})
        desc = obj.get("description", "")
        grid_cols = obj.get("grid_cols", 1)
        grid_rows = obj.get("grid_rows", 1)
        
        x = max(0, bbox.get("x", 0) - padding)
        y = max(0, bbox.get("y", 0) - padding)
        bw = min(bbox.get("w", 0) + padding * 2, w - x)
        bh = min(bbox.get("h", 0) + padding * 2, h - y)
        
        if bw <= 0 or bh <= 0:
            continue
        
        print(f"    [{i+1}] {name} ({obj_type}, {frame_count}帧) — {desc}")
        
        # 裁切整个区域
        crop = img.crop((x, y, x + bw, y + bh))
        
        if obj_type == "animation" and frame_count > 1:
            # 动画组：保存为完整条带 + 单独帧
            # 保存完整条带
            strip_name = f"{base_name}_{name}_strip_{frame_count}f.png"
            strip_path = output_dir / strip_name
            crop.save(strip_path, "PNG")
            results.append(str(strip_path))
            
            # 按网格切割单帧
            frame_w = crop.width // grid_cols
            frame_h = crop.height // grid_rows
            
            if frame_w > 0 and frame_h > 0:
                frames_dir = output_dir / f"{base_name}_{name}_frames"
                frames_dir.mkdir(parents=True, exist_ok=True)
                
                for r in range(grid_rows):
                    for c in range(grid_cols):
                        frame_idx = r * grid_cols + c
                        if frame_idx >= frame_count:
                            break
                        fx = c * frame_w
                        fy = r * frame_h
                        frame = crop.crop((fx, fy, fx + frame_w, fy + frame_h))
                        
                        # 跳过全透明帧
                        if img.mode == "RGBA" or crop.mode == "RGBA":
                            frame_rgba = frame.convert("RGBA")
                            arr = np.array(frame_rgba)
                            if arr[:, :, 3].max() == 0:
                                continue
                        
                        frame_name = f"{name}_F{frame_idx:02d}.png"
                        frame_path = frames_dir / frame_name
                        frame.save(frame_path, "PNG")
                        results.append(str(frame_path))
        else:
            # 静态物体：直接保存
            out_name = f"{base_name}_{name}.png"
            out_path = output_dir / out_name
            crop.save(out_path, "PNG")
            results.append(str(out_path))
    
    return results


# =========================================================================
# 辅助函数
# =========================================================================

def hex_to_rgb(hex_str: str) -> tuple:
    hex_str = hex_str.lstrip('#')
    return tuple(int(hex_str[i:i+2], 16) for i in (0, 2, 4))


def remove_background(img: Image.Image, bg_color: tuple, tolerance: int = 30) -> Image.Image:
    """将指定背景色替换为透明"""
    img = img.convert("RGBA")
    data = np.array(img)
    bg = np.array(bg_color[:3])
    diff = np.abs(data[:, :, :3].astype(int) - bg.astype(int))
    mask = np.all(diff <= tolerance, axis=2)
    data[mask] = [0, 0, 0, 0]
    return Image.fromarray(data)


# =========================================================================
# 主流程
# =========================================================================

def process_single_file(input_path: Path, args, output_dir: Path) -> list:
    """处理单个文件"""
    img = Image.open(input_path).convert("RGBA")
    base_name = input_path.stem
    
    # 去除背景色
    if args.remove_bg:
        bg_color = hex_to_rgb(args.remove_bg)
        img = remove_background(img, bg_color, args.bg_tolerance)
        print(f"  已去除背景色 {args.remove_bg}")
    
    # AI 分析
    analysis = analyze_with_ai(img, model=args.model)
    if analysis is None:
        print(f"  [失败] AI 分析失败，跳过此文件")
        return []
    
    # 保存分析结果 JSON（方便复查和调试）
    json_path = output_dir / f"{base_name}_analysis.json"
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(analysis, f, indent=2, ensure_ascii=False)
    print(f"  分析结果已保存: {json_path}")
    
    # 执行裁切
    results = execute_ai_slicing(img, analysis, output_dir, base_name, padding=args.padding)
    
    return results


def main():
    parser = argparse.ArgumentParser(
        description="MarioTrickster AI Smart Slicer — 视觉模型智能裁切",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__
    )
    
    parser.add_argument("input", help="输入图片路径或文件夹路径")
    parser.add_argument("--output", "-o", help="输出目录（默认: ./ai_sliced/）", default="./ai_sliced")
    parser.add_argument("--model", "-m", help="视觉模型（默认: gpt-4.1-mini）", default="gpt-4.1-mini")
    parser.add_argument("--remove-bg", help="去除背景色（十六进制，如 #ff00ff）")
    parser.add_argument("--bg-tolerance", type=int, default=30, help="背景色容差（默认30）")
    parser.add_argument("--padding", type=int, default=2, help="裁切边距（像素）")
    
    args = parser.parse_args()
    
    # 检查 API Key
    if not os.environ.get("OPENAI_API_KEY"):
        print("错误: 未设置 OPENAI_API_KEY 环境变量")
        print("请设置: export OPENAI_API_KEY='your-key-here'")
        sys.exit(1)
    
    input_path = Path(args.input)
    output_dir = Path(args.output)
    output_dir.mkdir(parents=True, exist_ok=True)
    
    valid_exts = {'.png', '.jpg', '.jpeg', '.tga', '.bmp', '.psd', '.tiff'}
    
    if input_path.is_file():
        if input_path.suffix.lower() not in valid_exts:
            print(f"不支持的文件格式: {input_path.suffix}")
            sys.exit(1)
        print(f"处理: {input_path}")
        results = process_single_file(input_path, args, output_dir)
    elif input_path.is_dir():
        all_results = []
        files = [f for f in input_path.iterdir() if f.suffix.lower() in valid_exts]
        print(f"找到 {len(files)} 个图片文件")
        for f in sorted(files):
            print(f"\n处理: {f.name}")
            results = process_single_file(f, args, output_dir)
            all_results.extend(results)
        results = all_results
    else:
        print(f"路径不存在: {input_path}")
        sys.exit(1)
    
    print(f"\n{'='*50}")
    print(f"完成！共输出 {len(results)} 个文件到 {output_dir}")
    print(f"每个文件夹内有 _analysis.json 记录 AI 的判断结果，可人工复查。")
    print(f"\n下一步：将输出文件拖入 Unity 的 Assets/Art/Imported/ 目录，")
    print(f"然后使用 Asset Import Pipeline 工具一键导入。")
    print(f"  - 动画条带文件（*_strip_*f.png）→ 选「手动切片」模式")
    print(f"  - 单帧文件（*_frames/ 目录下）→ 选「单帧」模式")
    print(f"  - 静态物体 → 选「单帧」模式")


if __name__ == "__main__":
    main()
