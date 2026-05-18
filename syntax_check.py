#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
C# 语法静态分析脚本（不依赖 Unity DLL / dotnet / mono）

设计目标：
    在没有 Unity / dotnet / mono 的纯 Python 沙盒里，对 .cs 源码做"
    与真实编译器一致"的轻量校验，避免 token 扫描器把合法语法误判
    为括号不平衡。

历史问题（已修复）：
    旧版只识别普通字符串 "..."，对以下三类 C# 语法会错位：
        1. verbatim 字符串            @"..."
           ——其中 ""  仅表示一个 "，且不处理 \  转义
        2. 内插+verbatim 字符串       $@"...{{...}}..."（或 @$"...")
           ——同时具备 verbatim 规则与 {{ }} 转义规则
        3. 字符字面量含转义反斜杠     '\\' '\"' 等
           ——旧脚本把 ' 当作字符串切换符
    任意一处错位都会让后续 { } ( ) 计数错乱，最终在文件末尾误报
    "多余的 '}' 没有匹配的 '{'"。本脚本按 C# 语言规范精细实现。

检查项目：
    1. 括号匹配（{} () []）：跨字符串/注释正确忽略
    2. using 语句缺少分号
    3. #region / #endregion 配对
    4. 文件行数统计

使用：
    python3 syntax_check.py
    退出码：0 表示全部通过，1 表示发现错误
"""

import os
import re
import sys


# ════════════════════════════════════════════════════════════════════
# 鲁棒的 C# token 扫描器：跨字符串/注释做括号匹配
# ════════════════════════════════════════════════════════════════════

def _scan_brackets(src):
    """
    线性扫描 C# 源码，正确处理：
      - // 行注释 与 /* */ 块注释
      - 普通字符串 "..."（含 \\ 转义）
      - verbatim 字符串 @"..."（"" 转义为 "）
      - 内插字符串 $"..."
      - 内插+verbatim 字符串 $@"..." / @$"..."
      - 字符字面量 '...'（含 \\ 与 \\u 转义）

    返回：错误信息列表（空表示括号匹配通过）。
    """
    n = len(src)
    i = 0
    line = 1
    stack = []      # 元素：(bracket_char, line)
    errors = []

    def push(ch, ln):
        stack.append((ch, ln))

    def pop(open_ch, close_ch, ln):
        if not stack:
            errors.append(f"  行 {ln}: 多余的 '{close_ch}' 没有匹配的 '{open_ch}'")
            return
        top, top_ln = stack[-1]
        if top == open_ch:
            stack.pop()
        else:
            errors.append(
                f"  行 {ln}: '{close_ch}' 与栈顶 '{top}' (行 {top_ln}) 不匹配"
            )

    while i < n:
        c = src[i]

        # 换行计数（先做，避免下面 continue 漏掉）
        if c == '\n':
            line += 1
            i += 1
            continue

        # ── 行注释 ──
        if c == '/' and i + 1 < n and src[i + 1] == '/':
            while i < n and src[i] != '\n':
                i += 1
            continue

        # ── 块注释 ──
        if c == '/' and i + 1 < n and src[i + 1] == '*':
            i += 2
            while i + 1 < n and not (src[i] == '*' and src[i + 1] == '/'):
                if src[i] == '\n':
                    line += 1
                i += 1
            i += 2
            continue

        # ── 内插+verbatim 字符串：$@"..." 或 @$"..." ──
        if (c == '$' and i + 2 < n and src[i + 1] == '@' and src[i + 2] == '"') or \
           (c == '@' and i + 2 < n and src[i + 1] == '$' and src[i + 2] == '"'):
            i += 3
            depth = 0  # 字符串内 { ... } 的局部嵌套深度
            while i < n:
                ch = src[i]
                if ch == '"' and depth == 0:
                    if i + 1 < n and src[i + 1] == '"':
                        i += 2
                        continue
                    i += 1
                    break
                if ch == '{' and depth == 0:
                    if i + 1 < n and src[i + 1] == '{':
                        i += 2
                        continue
                    depth += 1
                    i += 1
                    continue
                if ch == '}' and depth > 0:
                    if i + 1 < n and src[i + 1] == '}':
                        i += 2
                        continue
                    depth -= 1
                    i += 1
                    continue
                if ch == '\n':
                    line += 1
                i += 1
            continue

        # ── 内插字符串：$"..." ──
        if c == '$' and i + 1 < n and src[i + 1] == '"':
            i += 2
            depth = 0
            while i < n:
                ch = src[i]
                if ch == '\\' and depth == 0 and i + 1 < n:
                    if src[i + 1] == '\n':
                        line += 1
                    i += 2
                    continue
                if ch == '"' and depth == 0:
                    i += 1
                    break
                if ch == '{' and depth == 0:
                    if i + 1 < n and src[i + 1] == '{':
                        i += 2
                        continue
                    depth += 1
                    i += 1
                    continue
                if ch == '}' and depth > 0:
                    if i + 1 < n and src[i + 1] == '}':
                        i += 2
                        continue
                    depth -= 1
                    i += 1
                    continue
                if ch == '\n':
                    line += 1
                i += 1
            continue

        # ── verbatim 字符串：@"..." ──
        if c == '@' and i + 1 < n and src[i + 1] == '"':
            i += 2
            while i < n:
                ch = src[i]
                if ch == '"':
                    if i + 1 < n and src[i + 1] == '"':
                        i += 2
                        continue
                    i += 1
                    break
                if ch == '\n':
                    line += 1
                i += 1
            continue

        # ── 普通字符串：" ... " ──
        if c == '"':
            i += 1
            while i < n:
                ch = src[i]
                if ch == '\\' and i + 1 < n:
                    if src[i + 1] == '\n':
                        line += 1
                    i += 2
                    continue
                if ch == '"':
                    i += 1
                    break
                if ch == '\n':
                    line += 1
                i += 1
            continue

        # ── 字符字面量：'x' / '\x' / '\u00XX' ──
        if c == "'":
            i += 1
            if i < n and src[i] == '\\':
                i += 1
                while i < n and src[i] != "'":
                    if src[i] == '\n':
                        line += 1
                    i += 1
                if i < n:
                    i += 1  # consume closing '
            else:
                if i < n:
                    if src[i] == '\n':
                        line += 1
                    i += 1
                if i < n and src[i] == "'":
                    i += 1
            continue

        # ── 括号匹配（仅追踪 ()[]{}）──
        if c == '{':
            push('{', line); i += 1; continue
        if c == '}':
            pop('{', '}', line); i += 1; continue
        if c == '(':
            push('(', line); i += 1; continue
        if c == ')':
            pop('(', ')', line); i += 1; continue
        if c == '[':
            push('[', line); i += 1; continue
        if c == ']':
            pop('[', ']', line); i += 1; continue

        i += 1

    for ch, ln in stack:
        close = {'{': '}', '(': ')', '[': ']'}[ch]
        errors.append(f"  行 {ln}: '{ch}' 未闭合，缺少 '{close}'")
    return errors


# ════════════════════════════════════════════════════════════════════
# 单文件检查
# ════════════════════════════════════════════════════════════════════

def check_file(filepath):
    """对单个 .cs 文件做轻量静态检查。"""
    errors = []
    warnings = []

    with open(filepath, 'r', encoding='utf-8-sig') as f:
        content = f.read()
        lines = content.split('\n')

    # 1. 括号匹配（鲁棒扫描）
    errors.extend(_scan_brackets(content))

    # 2. using 语句缺少分号（行级粗筛，跳过 using 块语句）
    for line_num, line in enumerate(lines, 1):
        stripped = line.strip()
        if stripped.startswith('using ') and not stripped.startswith('using ('):
            if not stripped.endswith(';') and not stripped.endswith('{'):
                errors.append(f"  行 {line_num}: using 语句缺少分号: {stripped}")

    # 3. #region / #endregion 配对
    region_count = 0
    for line in lines:
        stripped = line.strip()
        if stripped.startswith('#region'):
            region_count += 1
        elif stripped.startswith('#endregion'):
            region_count -= 1
    if region_count > 0:
        warnings.append(f"  有 {region_count} 个 #region 未闭合")
    elif region_count < 0:
        warnings.append(f"  有 {-region_count} 个多余的 #endregion")

    return errors, warnings


# ════════════════════════════════════════════════════════════════════
# 主入口
# ════════════════════════════════════════════════════════════════════

def main():
    project_root = os.path.dirname(os.path.abspath(__file__))

    cs_files = []
    for root, dirs, files in os.walk(os.path.join(project_root, 'Assets')):
        for f in files:
            if f.endswith('.cs'):
                cs_files.append(os.path.join(root, f))
    cs_files.sort()

    total_errors = 0
    total_warnings = 0
    total_files = len(cs_files)
    passed_files = 0

    print("=" * 60)
    print("MarioTrickster C# 语法静态分析")
    print("=" * 60)
    print(f"扫描 {total_files} 个 .cs 文件...\n")

    for filepath in cs_files:
        rel_path = os.path.relpath(filepath, project_root)
        errors, warnings = check_file(filepath)

        if errors or warnings:
            print(f"❌ {rel_path}" if errors else f"⚠️  {rel_path}")
            for e in errors:
                print(f"   ERROR: {e}")
                total_errors += 1
            for w in warnings:
                print(f"   WARN:  {w}")
                total_warnings += 1
        else:
            print(f"✅ {rel_path}")
            passed_files += 1

    print("\n" + "=" * 60)
    print(f"结果: {passed_files}/{total_files} 文件通过")
    print(f"  错误: {total_errors}")
    print(f"  警告: {total_warnings}")
    print("=" * 60)

    # 行数统计
    print("\n文件行数统计:")
    print("-" * 50)
    total_lines = 0
    for filepath in cs_files:
        with open(filepath, 'r', encoding='utf-8-sig') as f:
            line_count = len(f.readlines())
        rel_path = os.path.relpath(filepath, project_root)
        print(f"  {line_count:>5} 行  {rel_path}")
        total_lines += line_count
    print("-" * 50)
    print(f"  {total_lines:>5} 行  总计 ({total_files} 个文件)")

    return 0 if total_errors == 0 else 1


if __name__ == '__main__':
    sys.exit(main())
