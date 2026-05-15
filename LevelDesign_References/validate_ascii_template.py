#!/usr/bin/env python3
"""
MarioTrickster ASCII 模板物理验证器 v1.0
========================================
用法:
  python3 validate_ascii_template.py                  # 从 ASCII_Templates.md 提取并验证所有模板
  python3 validate_ascii_template.py --text "..."     # 验证单个 ASCII 文本（粘贴到引号内）
  python3 validate_ascii_template.py --file input.txt # 验证文件中的 ASCII 文本

物理常量来源: Assets/Scripts/LevelDesign/PhysicsMetrics.cs
验证项:
  [P1] 行宽一致性 — 所有行必须等宽（矩形网格）
  [P2] 起点/终点存在性 — 必须有 M 和 G
  [P3] 禁止空格 — 只允许 . 作为空气
  [P4] 字符合法性 — 只允许字典中的字符
  [P5] 平台最小宽度 — 作为主要站立面的平台 >= 2 格（建议 >= 3 格）
  [P6] 敌人支撑 — 敌人(e/E)下方必须有可站立表面
  [P7] 跳跃可达性 — 相邻平台间距不超过物理极限
  [P8] 起点可达性 — Mario 脚下有地面或平台
  [P9] 终点可达性 — Goal 可从某个平台到达
"""

import sys
import re
import os
from pathlib import Path
from collections import defaultdict

# ─── 物理常量（与 PhysicsMetrics.cs 同步） ───
MAX_JUMP_HEIGHT = 2.5       # 原地起跳绝对上限
MAX_JUMP_DISTANCE = 4.5     # 满速平跳最远距离
SAFE_GAP = 4                # 水平安全间隙
SAFE_HEIGHT = 2             # 垂直安全高台
COYOTE_BONUS = 1.35         # Coyote time 额外距离
PLAYER_WIDTH = 0.8          # 角色碰撞体宽度

# ─── 合法字符字典 ───
LEGAL_CHARS = set('#=-W^~PBCc>EeFHoMG.[]')

# ─── 可站立表面字符 ───
SURFACE_CHARS = set('#=-BC')

# ─── 敌人字符 ───
ENEMY_CHARS = set('eE')


class ValidationResult:
    def __init__(self, template_name="unnamed"):
        self.name = template_name
        self.errors = []    # 致命：必须修复
        self.warnings = []  # 建议：可以忽略但推荐修复
        self.info = []      # 信息

    def error(self, code, msg):
        self.errors.append(f"[{code}] {msg}")

    def warn(self, code, msg):
        self.warnings.append(f"[{code}] {msg}")

    def add_info(self, msg):
        self.info.append(msg)

    @property
    def passed(self):
        return len(self.errors) == 0

    def summary(self):
        status = "PASS" if self.passed else "FAIL"
        icon = "\u2705" if self.passed else "\u274c"
        lines = [f"\n{'='*60}",
                 f"{icon} {self.name}: {status}",
                 f"{'='*60}"]
        if self.info:
            for i in self.info:
                lines.append(f"  \u2139\ufe0f  {i}")
        if self.errors:
            lines.append(f"\n  \u274c 致命错误 ({len(self.errors)}):")
            for e in self.errors:
                lines.append(f"    {e}")
        if self.warnings:
            lines.append(f"\n  \u26a0\ufe0f  警告 ({len(self.warnings)}):")
            for w in self.warnings:
                lines.append(f"    {w}")
        if self.passed and not self.warnings:
            lines.append("  所有检查通过，模板物理可行。")
        return "\n".join(lines)


def merge_horizontal_segments(positions_by_row):
    """将同行连续位置合并为段 {row: [(start_col, end_col, worldY, width), ...]}"""
    segments = []
    for row, cols_and_wy in sorted(positions_by_row.items()):
        cols_and_wy.sort(key=lambda x: x[0])
        start_col, start_wy = cols_and_wy[0]
        prev_col = start_col
        for col, wy in cols_and_wy[1:]:
            if col == prev_col + 1:
                prev_col = col
            else:
                segments.append({
                    'start': start_col, 'end': prev_col,
                    'worldY': start_wy, 'width': prev_col - start_col + 1,
                    'row': row
                })
                start_col, start_wy = col, wy
                prev_col = col
        segments.append({
            'start': start_col, 'end': prev_col,
            'worldY': start_wy, 'width': prev_col - start_col + 1,
            'row': row
        })
    return segments


def validate_template(ascii_text, name="unnamed"):
    """验证单个 ASCII 模板，返回 ValidationResult"""
    result = ValidationResult(name)
    lines = ascii_text.strip().split('\n')
    height = len(lines)

    if height == 0:
        result.error("P0", "模板为空")
        return result

    # ─── P1: 行宽一致性 ───
    lengths = [len(l) for l in lines]
    max_width = max(lengths)
    min_width = min(lengths)
    if max_width != min_width:
        result.error("P1", f"行宽不一致: 最短 {min_width}, 最长 {max_width}")
        for i, l in enumerate(lines):
            if len(l) != max_width:
                result.error("P1", f"  第 {i+1} 行: 长度 {len(l)} (应为 {max_width}, 差 {max_width - len(l)} 字符)")
        # 补齐以继续检查
        lines = [l.ljust(max_width, '.') for l in lines]

    width = max_width
    result.add_info(f"尺寸: {width} x {height}")

    # ─── P3: 禁止空格 ───
    for i, line in enumerate(lines):
        if ' ' in line:
            spaces = [j for j, c in enumerate(line) if c == ' ']
            result.error("P3", f"第 {i+1} 行包含空格 (列 {spaces[:5]}{'...' if len(spaces)>5 else ''})，请用 '.' 替换")

    # ─── P4: 字符合法性 ───
    for i, line in enumerate(lines):
        for j, ch in enumerate(line):
            if ch not in LEGAL_CHARS:
                result.error("P4", f"第 {i+1} 行第 {j+1} 列: 非法字符 '{ch}'")

    # ─── 解析元素位置 ───
    mario_pos = None
    goal_pos = None
    surface_by_row = defaultdict(list)  # row -> [(col, worldY)]
    platform_by_row = defaultdict(list)  # 仅 = 字符
    enemies = []  # [(col, worldY, row, char)]

    for row, line in enumerate(lines):
        worldY = height - 1 - row
        for col, ch in enumerate(line):
            if ch == 'M':
                mario_pos = (col, worldY, row)
            elif ch == 'G':
                goal_pos = (col, worldY, row)
            if ch in SURFACE_CHARS:
                surface_by_row[row].append((col, worldY))
            if ch == '=':
                platform_by_row[row].append((col, worldY))
            if ch in ENEMY_CHARS:
                enemies.append((col, worldY, row, ch))

    # ─── P2: 起点/终点存在性 ───
    if mario_pos is None:
        result.error("P2", "缺少 Mario 起点 (M)")
    if goal_pos is None:
        result.error("P2", "缺少终点 (G)")

    # ─── 合并表面段 ───
    all_surface_segs = merge_horizontal_segments(surface_by_row)
    platform_segs = merge_horizontal_segments(platform_by_row)

    # ─── P5: 平台最小宽度 ───
    # 区分"主要站立面"和"挑战性小平台"
    # 如果平台是关卡中唯一的站立面（如 T02），1 格宽是致命的
    # 如果平台是可选的挑战路线（如 T01 的地刺深渊中间），1 格宽是设计意图
    single_width_platforms = [s for s in platform_segs if s['width'] == 1]
    double_width_platforms = [s for s in platform_segs if s['width'] == 2]

    if single_width_platforms:
        for seg in single_width_platforms:
            result.warn("P5", f"平台 (col={seg['start']}, 行{seg['row']+1}, wY={seg['worldY']}) 仅 1 格宽 — 角色碰撞体宽 {PLAYER_WIDTH} 格，几乎无法站稳。如果这是主要站立面请加宽到 3+ 格；如果是故意的精确跳跃挑战可忽略。")

    # ─── P6: 敌人支撑 ───
    for col, wy, row, ch in enemies:
        found_support = False
        support_distance = 0
        for dy in range(1, 8):
            check_wy = wy - dy
            if check_wy < 0:
                break
            for seg in all_surface_segs:
                if seg['start'] <= col <= seg['end'] and seg['worldY'] == check_wy:
                    found_support = True
                    support_distance = dy
                    break
            if found_support:
                break

        char_name = "简单敌人(e)" if ch == 'e' else "弹跳怪(E)"
        if not found_support:
            result.error("P6", f"{char_name} (col={col}, 行{row+1}) 下方无任何支撑面，会掉入虚空")
        elif support_distance > 3:
            result.warn("P6", f"{char_name} (col={col}, 行{row+1}) 距最近支撑面 {support_distance} 格，掉落距离较大")
        elif support_distance == 1:
            pass  # 完美：直接在平台上方
        # distance 2-3 也可以接受（会掉落到平台上）

    # ─── P7: 跳跃可达性（简化版：检查相邻高度层的平台间距） ───
    # 按 worldY 分组
    by_height = defaultdict(list)
    for seg in all_surface_segs:
        by_height[seg['worldY']].append(seg)

    sorted_heights = sorted(by_height.keys())
    for i in range(len(sorted_heights) - 1):
        lower_y = sorted_heights[i]
        upper_y = sorted_heights[i + 1]
        vert_diff = upper_y - lower_y

        # 只检查垂直差在跳跃范围内的层（超过的不算相邻）
        if vert_diff > MAX_JUMP_HEIGHT + 1:
            continue

        if vert_diff > MAX_JUMP_HEIGHT:
            # 检查是否有中间平台可以中转
            has_intermediate = False
            for h in sorted_heights:
                if lower_y < h < upper_y:
                    has_intermediate = True
                    break
            if not has_intermediate:
                # 检查这两层之间是否真的需要跳（可能是不同区域）
                lower_segs = by_height[lower_y]
                upper_segs = by_height[upper_y]
                for ls in lower_segs:
                    for us in upper_segs:
                        # 计算水平重叠或间隙
                        h_gap = max(0, max(us['start'] - ls['end'], ls['start'] - us['end']) - 1)
                        if h_gap <= SAFE_GAP + 2:  # 在合理水平范围内才算需要跳
                            result.warn("P7", f"wY={lower_y} → wY={upper_y}: 垂直差 {vert_diff} 格超过极限跳高 {MAX_JUMP_HEIGHT} 格")
                            break

    # ─── P8: Mario 起点支撑 ───
    # Mario 可以从高处掉落到下方任意支撑面（重力会把他带到那里）
    if mario_pos:
        m_col, m_wy, m_row = mario_pos
        found = False
        # 搜索 Mario 正下方整个垂直列，找任何支撑面
        for dy in range(1, height + 1):
            check_wy = m_wy - dy
            if check_wy < 0:
                break
            for seg in all_surface_segs:
                if seg['start'] <= m_col <= seg['end'] and seg['worldY'] == check_wy:
                    found = True
                    if dy > 5:
                        result.warn("P8", f"Mario (col={m_col}, 行{m_row+1}) 距最近支撑面 {dy} 格，掉落距离较大")
                    break
            if found:
                break
        # 也检查 Mario 是否在最底行
        if not found and m_row == height - 1:
            found = True
        if not found:
            result.error("P8", f"Mario (col={m_col}, 行{m_row+1}) 下方无任何地面/平台支撑，会掉入虚空")

    # ─── P9: 终点可达性 ───
    if goal_pos:
        g_col, g_wy, g_row = goal_pos
        found = False
        for dy in range(0, 4):
            check_wy = g_wy - dy - 1
            if check_wy < 0:
                if g_row == height - 1:
                    found = True
                break
            for seg in all_surface_segs:
                if seg['start'] <= g_col <= seg['end'] and seg['worldY'] == check_wy:
                    found = True
                    break
            if found:
                break
        if not found:
            result.warn("P9", f"终点 G (col={g_col}, 行{g_row+1}) 下方无明确支撑面 — 可能需要跳跃到达（如果是设计意图可忽略）")

    return result


def extract_templates_from_md(md_path):
    """从 ASCII_Templates.md 提取所有模板"""
    with open(md_path, 'r', encoding='utf-8') as f:
        content = f.read()

    templates = []
    # 匹配模式: ## 模板 N：标题 ... ``` ... ```
    pattern = r'##\s*模板\s*(\d+)[：:]\s*(.+?)(?:\n|\r\n).*?```\n(.*?)```'
    for match in re.finditer(pattern, content, re.DOTALL):
        num = match.group(1)
        title = match.group(2).strip()
        ascii_text = match.group(3).strip()
        templates.append((f"T{num.zfill(2)}: {title}", ascii_text))

    return templates


def main():
    if len(sys.argv) > 1 and sys.argv[1] == '--text':
        # 验证命令行传入的文本
        text = sys.argv[2] if len(sys.argv) > 2 else sys.stdin.read()
        result = validate_template(text, "命令行输入")
        print(result.summary())
        sys.exit(0 if result.passed else 1)

    elif len(sys.argv) > 1 and sys.argv[1] == '--file':
        # 验证文件中的文本
        filepath = sys.argv[2]
        with open(filepath, 'r', encoding='utf-8') as f:
            text = f.read()
        result = validate_template(text, filepath)
        print(result.summary())
        sys.exit(0 if result.passed else 1)

    else:
        # 默认：从 ASCII_Templates.md 提取并验证所有模板
        script_dir = Path(__file__).parent
        md_path = script_dir / "ASCII_Templates.md"
        if not md_path.exists():
            print(f"错误: 找不到 {md_path}")
            sys.exit(1)

        templates = extract_templates_from_md(md_path)
        if not templates:
            print("错误: 未从 ASCII_Templates.md 中提取到任何模板")
            sys.exit(1)

        print("=" * 60)
        print(f"\U0001f50d MarioTrickster ASCII 模板物理验证器 v1.0")
        print(f"   来源: {md_path}")
        print(f"   模板数: {len(templates)}")
        print("=" * 60)

        all_passed = True
        total_errors = 0
        total_warnings = 0

        for name, text in templates:
            result = validate_template(text, name)
            print(result.summary())
            if not result.passed:
                all_passed = False
            total_errors += len(result.errors)
            total_warnings += len(result.warnings)

        # 总结
        print(f"\n{'='*60}")
        print(f"\U0001f4ca 验证总结")
        print(f"{'='*60}")
        print(f"  模板总数: {len(templates)}")
        print(f"  致命错误: {total_errors}")
        print(f"  警告: {total_warnings}")
        status = "\u2705 全部通过" if all_passed else f"\u274c {sum(1 for n,t in templates if not validate_template(t,n).passed)} 个模板有致命错误"
        print(f"  总体状态: {status}")

        sys.exit(0 if all_passed else 1)


if __name__ == '__main__':
    main()
