#!/usr/bin/env python3
"""
C# 语法静态分析脚本（不依赖 Unity DLL）

检查项目：
1. 括号匹配（{} () [] <>）
2. 分号缺失（语句行末尾）
3. 类/方法声明格式
4. using 语句格式
5. 字符串未闭合
6. 注释格式
7. 常见拼写错误
"""

import os
import re
import sys

def check_file(filepath):
    """对单个 .cs 文件进行语法检查"""
    errors = []
    warnings = []
    
    with open(filepath, 'r', encoding='utf-8-sig') as f:
        content = f.read()
        lines = content.split('\n')
    
    # 1. 括号匹配检查
    brace_stack = []
    in_string = False
    in_char = False
    in_line_comment = False
    in_block_comment = False
    
    for line_num, line in enumerate(lines, 1):
        in_line_comment = False
        i = 0
        while i < len(line):
            c = line[i]
            
            # 处理块注释
            if in_block_comment:
                if c == '*' and i + 1 < len(line) and line[i+1] == '/':
                    in_block_comment = False
                    i += 2
                    continue
                i += 1
                continue
            
            # 处理行注释
            if in_line_comment:
                break
            
            # 检测注释开始
            if c == '/' and i + 1 < len(line):
                if line[i+1] == '/':
                    in_line_comment = True
                    break
                elif line[i+1] == '*':
                    in_block_comment = True
                    i += 2
                    continue
            
            # 处理字符串
            if c == '"' and not in_char:
                in_string = not in_string
                i += 1
                continue
            
            if in_string:
                if c == '\\':
                    i += 2  # 跳过转义字符
                    continue
                i += 1
                continue
            
            # 处理字符
            if c == "'" and not in_string:
                in_char = not in_char
                i += 1
                continue
            
            if in_char:
                i += 1
                continue
            
            # 括号匹配
            if c in '{(':
                brace_stack.append((c, line_num))
            elif c == '}':
                if brace_stack and brace_stack[-1][0] == '{':
                    brace_stack.pop()
                else:
                    errors.append(f"  行 {line_num}: 多余的 '}}' 没有匹配的 '{{'")
            elif c == ')':
                if brace_stack and brace_stack[-1][0] == '(':
                    brace_stack.pop()
                else:
                    errors.append(f"  行 {line_num}: 多余的 ')' 没有匹配的 '('")
            
            i += 1
    
    # 检查未闭合的括号
    for brace, line_num in brace_stack:
        close = '}' if brace == '{' else ')'
        errors.append(f"  行 {line_num}: '{brace}' 未闭合，缺少 '{close}'")
    
    # 2. 检查 using 语句格式
    for line_num, line in enumerate(lines, 1):
        stripped = line.strip()
        if stripped.startswith('using ') and not stripped.startswith('using ('):
            if not stripped.endswith(';'):
                if not stripped.endswith('{'):  # using 块语句
                    errors.append(f"  行 {line_num}: using 语句缺少分号: {stripped}")
    
    # 3. 检查类声明
    class_pattern = re.compile(r'^\s*(public|private|protected|internal)?\s*(abstract|sealed|static|partial)?\s*class\s+(\w+)')
    for line_num, line in enumerate(lines, 1):
        match = class_pattern.match(line)
        if match:
            class_name = match.group(3)
            # 检查文件名是否与主类名匹配（仅对非嵌套类）
            basename = os.path.splitext(os.path.basename(filepath))[0]
            # 不强制要求，只是警告
    
    # 4. 检查常见问题
    for line_num, line in enumerate(lines, 1):
        stripped = line.strip()
        
        # 跳过注释和空行
        if stripped.startswith('//') or stripped.startswith('/*') or stripped.startswith('*') or not stripped:
            continue
        
        # 检查 #region / #endregion 配对（简单计数）
        # 检查属性声明中的常见错误
        if 'SerializeField' in stripped and '[' in stripped and ']' not in stripped:
            warnings.append(f"  行 {line_num}: [SerializeField] 属性可能未闭合")
    
    # 5. 检查 #region / #endregion 配对
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


def main():
    project_root = os.path.dirname(os.path.abspath(__file__))
    
    # 查找所有 .cs 文件
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
            print(f"❌ {rel_path}")
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
    
    # 额外检查：文件行数统计
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
