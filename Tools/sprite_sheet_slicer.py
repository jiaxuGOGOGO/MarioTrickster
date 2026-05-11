#!/usr/bin/env python3
"""
MarioTrickster — Sprite Sheet Batch Slicer
===========================================

用途：
  在将素材拖入 Unity 之前，对大型 Sprite Sheet 或包含多个 Object 的合集图
  进行批量裁切，输出单独的 Object 图片文件，方便逐个导入。

典型场景：
  - 购买的商业素材包含一张大图里有多个角色/道具/地形
  - 需要按不同 Object 分割后再分别导入项目
  - 需要去除纯色背景（如白色/粉色/绿色）

用法：
  python sprite_sheet_slicer.py <input_image> [options]

示例：
  # 按网格切割（8列4行）
  python sprite_sheet_slicer.py tileset.png --cols 8 --rows 4

  # 自动检测独立物体并分割
  python sprite_sheet_slicer.py characters.png --auto

  # 去除背景色后自动分割
  python sprite_sheet_slicer.py sheet.png --auto --remove-bg "#ff00ff"

  # 批量处理文件夹
  python sprite_sheet_slicer.py ./raw_assets/ --cols 8 --rows 1 --output ./sliced/

依赖：
  pip install Pillow numpy
"""

import argparse
import os
import sys
from pathlib import Path

try:
    from PIL import Image
    import numpy as np
except ImportError:
    print("请先安装依赖: pip install Pillow numpy")
    sys.exit(1)


def hex_to_rgb(hex_str: str) -> tuple:
    """将 #RRGGBB 转为 (R, G, B) 元组"""
    hex_str = hex_str.lstrip('#')
    return tuple(int(hex_str[i:i+2], 16) for i in (0, 2, 4))


def remove_background(img: Image.Image, bg_color: tuple, tolerance: int = 30) -> Image.Image:
    """将指定背景色替换为透明"""
    img = img.convert("RGBA")
    data = np.array(img)
    
    # 计算与背景色的距离
    bg = np.array(bg_color[:3])
    diff = np.abs(data[:, :, :3].astype(int) - bg.astype(int))
    mask = np.all(diff <= tolerance, axis=2)
    
    # 设置为透明
    data[mask] = [0, 0, 0, 0]
    
    return Image.fromarray(data)


def grid_slice(img: Image.Image, cols: int, rows: int, output_dir: Path, base_name: str) -> list:
    """按网格切割图片"""
    w, h = img.size
    frame_w = w // cols
    frame_h = h // rows
    
    results = []
    for r in range(rows):
        for c in range(cols):
            x = c * frame_w
            y = r * frame_h
            frame = img.crop((x, y, x + frame_w, y + frame_h))
            
            # 跳过全透明帧
            if img.mode == "RGBA":
                arr = np.array(frame)
                if arr[:, :, 3].max() == 0:
                    continue
            
            name = f"{base_name}_R{r:02d}_C{c:02d}.png"
            out_path = output_dir / name
            frame.save(out_path, "PNG")
            results.append(str(out_path))
    
    return results


def auto_slice(img: Image.Image, output_dir: Path, base_name: str, 
               min_size: int = 16, padding: int = 2) -> list:
    """自动检测独立物体区域并分割"""
    img = img.convert("RGBA")
    data = np.array(img)
    
    # 创建二值 mask（非透明区域）
    alpha = data[:, :, 3]
    binary = (alpha > 10).astype(np.uint8)
    
    # 简单的连通域检测（使用 flood fill 思路）
    h, w = binary.shape
    visited = np.zeros_like(binary, dtype=bool)
    regions = []
    
    def flood_fill_bounds(start_y, start_x):
        """BFS 找到连通域的边界框"""
        stack = [(start_y, start_x)]
        min_x, max_x = start_x, start_x
        min_y, max_y = start_y, start_y
        
        while stack:
            cy, cx = stack.pop()
            if cy < 0 or cy >= h or cx < 0 or cx >= w:
                continue
            if visited[cy, cx] or binary[cy, cx] == 0:
                continue
            
            visited[cy, cx] = True
            min_x = min(min_x, cx)
            max_x = max(max_x, cx)
            min_y = min(min_y, cy)
            max_y = max(max_y, cy)
            
            # 4-连通
            stack.extend([(cy-1, cx), (cy+1, cx), (cy, cx-1), (cy, cx+1)])
        
        return min_x, min_y, max_x, max_y
    
    # 扫描所有像素找连通域
    # 为了性能，先用粗粒度扫描
    step = max(1, min_size // 4)
    for y in range(0, h, step):
        for x in range(0, w, step):
            if binary[y, x] == 1 and not visited[y, x]:
                bounds = flood_fill_bounds(y, x)
                bw = bounds[2] - bounds[0]
                bh = bounds[3] - bounds[1]
                if bw >= min_size and bh >= min_size:
                    regions.append(bounds)
    
    # 合并重叠区域
    regions = merge_overlapping(regions, padding * 2)
    
    results = []
    for i, (x1, y1, x2, y2) in enumerate(regions):
        # 添加 padding
        x1 = max(0, x1 - padding)
        y1 = max(0, y1 - padding)
        x2 = min(w, x2 + padding)
        y2 = min(h, y2 + padding)
        
        crop = img.crop((x1, y1, x2, y2))
        name = f"{base_name}_obj{i:03d}.png"
        out_path = output_dir / name
        crop.save(out_path, "PNG")
        results.append(str(out_path))
    
    return results


def merge_overlapping(regions: list, margin: int = 4) -> list:
    """合并重叠或相邻的区域"""
    if not regions:
        return []
    
    # 按面积排序
    regions = sorted(regions, key=lambda r: (r[2]-r[0]) * (r[3]-r[1]), reverse=True)
    merged = []
    used = [False] * len(regions)
    
    for i in range(len(regions)):
        if used[i]:
            continue
        x1, y1, x2, y2 = regions[i]
        
        # 尝试合并所有与当前区域重叠的区域
        changed = True
        while changed:
            changed = False
            for j in range(i + 1, len(regions)):
                if used[j]:
                    continue
                jx1, jy1, jx2, jy2 = regions[j]
                # 检查是否重叠（含 margin）
                if (x1 - margin <= jx2 and x2 + margin >= jx1 and
                    y1 - margin <= jy2 and y2 + margin >= jy1):
                    x1 = min(x1, jx1)
                    y1 = min(y1, jy1)
                    x2 = max(x2, jx2)
                    y2 = max(y2, jy2)
                    used[j] = True
                    changed = True
        
        merged.append((x1, y1, x2, y2))
        used[i] = True
    
    return merged


def process_single_file(input_path: Path, args, output_dir: Path) -> list:
    """处理单个文件"""
    img = Image.open(input_path)
    base_name = input_path.stem
    
    # 去除背景色
    if args.remove_bg:
        bg_color = hex_to_rgb(args.remove_bg)
        img = remove_background(img, bg_color, args.bg_tolerance)
        print(f"  已去除背景色 {args.remove_bg}")
    
    # 切割
    if args.auto:
        results = auto_slice(img, output_dir, base_name, 
                           min_size=args.min_size, padding=args.padding)
        print(f"  自动检测到 {len(results)} 个独立物体")
    else:
        results = grid_slice(img, args.cols, args.rows, output_dir, base_name)
        print(f"  网格切割: {args.cols}x{args.rows} = {len(results)} 帧")
    
    return results


def main():
    parser = argparse.ArgumentParser(
        description="MarioTrickster Sprite Sheet Batch Slicer",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__
    )
    
    parser.add_argument("input", help="输入图片路径或文件夹路径")
    parser.add_argument("--output", "-o", help="输出目录（默认: ./sliced/）", default="./sliced")
    parser.add_argument("--cols", "-c", type=int, default=1, help="网格列数")
    parser.add_argument("--rows", "-r", type=int, default=1, help="网格行数")
    parser.add_argument("--auto", "-a", action="store_true", help="自动检测独立物体并分割")
    parser.add_argument("--remove-bg", help="去除背景色（十六进制，如 #ff00ff）")
    parser.add_argument("--bg-tolerance", type=int, default=30, help="背景色容差（默认30）")
    parser.add_argument("--min-size", type=int, default=16, help="自动模式下最小物体尺寸（像素）")
    parser.add_argument("--padding", type=int, default=2, help="裁切时的边距（像素）")
    
    args = parser.parse_args()
    
    input_path = Path(args.input)
    output_dir = Path(args.output)
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # 支持的图片格式
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
            print(f"处理: {f.name}")
            results = process_single_file(f, args, output_dir)
            all_results.extend(results)
        results = all_results
    else:
        print(f"路径不存在: {input_path}")
        sys.exit(1)
    
    print(f"\n完成！共输出 {len(results)} 个文件到 {output_dir}")
    print("下一步：将输出文件拖入 Unity 的 Assets/Art/ 目录，然后使用 Asset Import Pipeline 工具一键导入。")


if __name__ == "__main__":
    main()
