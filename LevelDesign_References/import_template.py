#!/usr/bin/env python3
"""
MarioTrickster 关卡模板一键导入工具 v1.0
=========================================
用法:
  python3 LevelDesign_References/import_template.py ai_reply.md
  python3 LevelDesign_References/import_template.py ai_reply.md --source "B站某视频截图"
  python3 LevelDesign_References/import_template.py ai_reply.md --no-push

功能（自动完成路线 B 第三步的全部操作）:
  1. 从网页 AI 回复文件中提取 ASCII 矩阵 + 动态参数 + 缺失机制 + 防重指纹
  2. 追加到 ASCII_Templates.md（自动编号）
  3. 运行物理验证器，致命错误时中止并提示修复
  4. 更新 TEMPLATE_REGISTRY.md（完整登记表 + 快速复制区 + 已探索灵感来源）
  5. git add + commit + push

输入文件格式要求:
  网页 AI 的回复应包含以下 4 个模块（路线 B 提示词已要求）：
  【动态参数建议】 【ASCII 矩阵代码块】 【缺失机制分析】 【一键防重登记指纹】
"""

import sys
import re
import os
import subprocess
from pathlib import Path
from datetime import datetime

SCRIPT_DIR = Path(__file__).parent
TEMPLATES_MD = SCRIPT_DIR / "ASCII_Templates.md"
REGISTRY_MD = SCRIPT_DIR / "TEMPLATE_REGISTRY.md"
VALIDATOR = SCRIPT_DIR / "validate_ascii_template.py"


def die(msg):
    print(f"\n❌ 错误: {msg}")
    sys.exit(1)


def info(msg):
    print(f"  ℹ️  {msg}")


def success(msg):
    print(f"  ✅ {msg}")


def parse_ai_reply(text):
    """从 AI 回复中提取 4 个模块"""
    result = {}

    # 提取 ASCII 矩阵（代码块内）
    ascii_blocks = re.findall(r'```\n?([\.\#\=\-W\^~PBCc>EeFHoMG\n]+?)```', text)
    if not ascii_blocks:
        # 尝试更宽松的匹配
        ascii_blocks = re.findall(r'```(?:text)?\n?(.*?)```', text, re.DOTALL)
        # 过滤只保留看起来像 ASCII 关卡的块
        ascii_blocks = [b.strip() for b in ascii_blocks
                       if re.search(r'[#=\-\.]{5,}', b) and ('M' in b or 'G' in b)]
    if not ascii_blocks:
        die("未找到 ASCII 矩阵代码块。请确保 AI 回复中包含用 ``` 包裹的 ASCII 关卡。")
    result['ascii'] = ascii_blocks[0].strip()

    # 提取动态参数建议
    dyn_match = re.search(r'【动态参数建议】[：:]?\s*\n(.*?)(?=\n【|\n===|\n---|\Z)', text, re.DOTALL)
    result['dynamic_params'] = dyn_match.group(1).strip() if dyn_match else ""

    # 提取缺失机制分析
    missing_match = re.search(r'【缺失机制分析】[：:]?\s*\n(.*?)(?=\n【|\n===|\n---|\Z)', text, re.DOTALL)
    result['missing_mechanics'] = missing_match.group(1).strip() if missing_match else ""

    # 提取防重指纹
    fingerprint_match = re.search(r'【一键防重登记指纹】[：:]?\s*\n(.*?)(?=\n【|\n===|\n---|\Z)', text, re.DOTALL)
    if fingerprint_match:
        fp_text = fingerprint_match.group(1).strip()
        result['fingerprint'] = fp_text
    else:
        result['fingerprint'] = ""

    return result


def get_next_template_number():
    """从 ASCII_Templates.md 中获取下一个模板编号"""
    content = TEMPLATES_MD.read_text(encoding='utf-8')
    numbers = re.findall(r'##\s*模板\s*(\d+)', content)
    if numbers:
        return max(int(n) for n in numbers) + 1
    return 1


def parse_fingerprint(fp_text, t_num):
    """解析防重指纹，提取博弈摘要、元素、难度等"""
    result = {
        'summary': '',
        'elements': '',
        'difficulty': '中',
        'size': '',
    }

    # 尝试解析格式: T编号_[机制A] + [机制B]（核心博弈体验）
    # 或者更宽松的格式
    fp_clean = re.sub(r'^T\d+[_\s]*', '', fp_text.split('\n')[0].strip())
    result['summary'] = fp_clean

    # 提取难度
    diff_match = re.search(r'\[难度[：:]?\s*(易|中|难|极难)\]', fp_text)
    if diff_match:
        result['difficulty'] = diff_match.group(1)

    return result


def append_to_templates(parsed, t_num, source=""):
    """追加新模板到 ASCII_Templates.md"""
    ascii_text = parsed['ascii']
    lines = ascii_text.strip().split('\n')
    height = len(lines)
    width = max(len(l) for l in lines) if lines else 0

    # 构建模板标题（从指纹中提取）
    title = parsed['fingerprint'].split('\n')[0].strip() if parsed['fingerprint'] else f"模板 {t_num}"
    title = re.sub(r'^T\d+[_\s]*', '', title)  # 去掉 T编号前缀

    section = f"""

---

## 模板 {t_num}：{title}

**灵感来源**：{source if source else "网页 AI 视觉拆解"}

"""

    if parsed['dynamic_params']:
        section += f"""**动态参数建议**：

{parsed['dynamic_params']}

"""

    section += f"""```
{ascii_text}
```

**尺寸**：{width} x {height}

"""

    if parsed['missing_mechanics']:
        section += f"""**缺失机制分析**：

{parsed['missing_mechanics']}
"""

    with open(TEMPLATES_MD, 'a', encoding='utf-8') as f:
        f.write(section)

    return width, height


def run_validator():
    """运行物理验证器"""
    print("\n" + "=" * 50)
    print("🔍 运行物理验证器...")
    print("=" * 50)

    result = subprocess.run(
        [sys.executable, str(VALIDATOR)],
        capture_output=True, text=True
    )
    print(result.stdout)
    if result.stderr:
        print(result.stderr)

    return result.returncode == 0


def update_registry(parsed, t_num, width, height, source=""):
    """更新 TEMPLATE_REGISTRY.md"""
    content = REGISTRY_MD.read_text(encoding='utf-8')
    fp_info = parse_fingerprint(parsed['fingerprint'], t_num)
    today = datetime.now().strftime('%Y-%m-%d')
    source_text = source if source else "网页 AI 视觉拆解"

    # 提取 ASCII 中使用的元素字符
    ascii_text = parsed['ascii']
    used_chars = set()
    element_map = {
        '#': '#', '=': '=', '-': '-', 'W': 'W', '^': '^', '~': '~',
        'P': 'P', 'B': 'B', 'C': 'C', '>': '>', 'E': 'E', 'e': 'e',
        'F': 'F', 'H': 'H', 'o': 'o', 'M': 'M', 'G': 'G'
    }
    for ch in ascii_text:
        if ch in element_map:
            used_chars.add(f'`{ch}`')
    elements_str = ' '.join(sorted(used_chars))

    summary = fp_info['summary']
    difficulty = fp_info['difficulty']

    # 1. 更新完整登记表 —— 在最后一行 T 记录后追加
    table_pattern = r'(\| T\d+ \|[^\n]+\n)(?=\n---)'
    table_rows = list(re.finditer(table_pattern, content))
    if table_rows:
        last_row = table_rows[-1]
        insert_pos = last_row.end()
        new_row = f"| T{t_num:02d} | {summary} | {elements_str} | {width}x{height} | {difficulty} | {source_text} | {today} |\n"
        content = content[:insert_pos] + new_row + content[insert_pos:]
    else:
        info("⚠️ 未找到完整登记表的插入位置，请手动添加")

    # 2. 更新快速复制区 —— 在 ``` 结束前追加
    quick_copy_pattern = r'(我已有以下博弈组合.*?)(```)'
    qc_match = re.search(quick_copy_pattern, content, re.DOTALL)
    if qc_match:
        # 找到最后一个编号
        existing_nums = re.findall(r'^(\d+)\. ', qc_match.group(1), re.MULTILINE)
        next_num = max(int(n) for n in existing_nums) + 1 if existing_nums else t_num
        new_line = f"{next_num}. {summary}\n"
        insert_pos = qc_match.start(2)
        content = content[:insert_pos] + new_line + content[insert_pos:]
    else:
        info("⚠️ 未找到快速复制区的插入位置，请手动添加")

    # 3. 更新已探索灵感来源 —— 在表格最后一行后追加
    source_table_pattern = r'(\| [^\n]+ \| T\d+ \|\n)(?=\n>)'
    source_rows = list(re.finditer(source_table_pattern, content))
    if source_rows and source_text:
        last_source = source_rows[-1]
        # 简单地把来源拆分为游戏名和具体关卡
        new_source_row = f"| {source_text} | 视觉拆解 | T{t_num:02d} |\n"
        content = content[:last_source.end()] + new_source_row + content[last_source.end():]

    REGISTRY_MD.write_text(content, encoding='utf-8')
    return True


def git_commit_push(t_num, no_push=False):
    """git add + commit + push"""
    repo_root = SCRIPT_DIR.parent
    msg = f"feat(Level): 导入模板 T{t_num:02d} (视觉拆解新关卡)"

    subprocess.run(['git', 'add', '.'], cwd=repo_root, check=True)
    subprocess.run(['git', 'commit', '-m', msg], cwd=repo_root, check=True)

    if no_push:
        info("--no-push 模式，跳过 push")
    else:
        result = subprocess.run(['git', 'push'], cwd=repo_root, capture_output=True, text=True)
        if result.returncode != 0:
            print(result.stderr)
            die("git push 失败")
        success("已推送到远程仓库")


def main():
    import argparse
    parser = argparse.ArgumentParser(
        description='MarioTrickster 关卡模板一键导入工具',
        epilog='示例: python3 import_template.py ai_reply.md --source "Celeste B面截图"'
    )
    parser.add_argument('input_file', help='网页 AI 回复的文本文件（Markdown 格式）')
    parser.add_argument('--source', default='', help='灵感来源描述（如 "B站某视频截图"）')
    parser.add_argument('--no-push', action='store_true', help='只 commit 不 push（调试用）')
    parser.add_argument('--dry-run', action='store_true', help='只解析不写入（预览模式）')

    args = parser.parse_args()

    input_path = Path(args.input_file)
    if not input_path.exists():
        die(f"文件不存在: {input_path}")

    print("=" * 50)
    print("🚀 MarioTrickster 模板一键导入工具 v1.0")
    print("=" * 50)

    # Step 1: 解析 AI 回复
    print("\n📖 步骤 1/5: 解析 AI 回复...")
    text = input_path.read_text(encoding='utf-8')
    parsed = parse_ai_reply(text)

    ascii_lines = parsed['ascii'].split('\n')
    info(f"ASCII 矩阵: {max(len(l) for l in ascii_lines)}x{len(ascii_lines)}")
    info(f"动态参数: {'有' if parsed['dynamic_params'] else '无'}")
    info(f"缺失机制: {'有' if parsed['missing_mechanics'] else '无'}")
    info(f"防重指纹: {parsed['fingerprint'][:60]}..." if len(parsed['fingerprint']) > 60 else f"防重指纹: {parsed['fingerprint']}")

    t_num = get_next_template_number()
    info(f"将分配编号: T{t_num:02d}")

    if args.dry_run:
        print("\n🔍 预览模式，不写入任何文件。")
        print(f"\n--- ASCII 矩阵预览 ---\n{parsed['ascii']}\n---")
        return

    # Step 2: 追加到 ASCII_Templates.md
    print(f"\n📝 步骤 2/5: 追加 T{t_num:02d} 到 ASCII_Templates.md...")
    width, height = append_to_templates(parsed, t_num, args.source)
    success(f"模板 T{t_num:02d} ({width}x{height}) 已追加")

    # Step 3: 运行物理验证器
    print(f"\n🔬 步骤 3/5: 物理验证...")
    if not run_validator():
        print("\n" + "=" * 50)
        print("⛔ 验证失败！请修复 ASCII_Templates.md 中的致命错误后重新运行。")
        print("   常见修复：")
        print("   · 敌人在平台下方 → 移到平台上方行")
        print("   · 行宽不一致 → 补齐短行末尾的 '.'")
        print("   · 包含空格 → 替换为 '.'")
        print(f"\n   修复后重新运行: python3 {__file__} {args.input_file}")
        print("=" * 50)
        # 回滚 Templates 的修改
        subprocess.run(['git', 'checkout', '--', str(TEMPLATES_MD)],
                      cwd=SCRIPT_DIR.parent)
        info("已回滚 ASCII_Templates.md 的修改")
        sys.exit(1)
    success("物理验证通过")

    # Step 4: 更新 TEMPLATE_REGISTRY.md
    print(f"\n📋 步骤 4/5: 更新登记簿...")
    update_registry(parsed, t_num, width, height, args.source)
    success("完整登记表 + 快速复制区 + 已探索灵感来源 已更新")

    # Step 5: git commit + push
    print(f"\n🚀 步骤 5/5: 提交并推送...")
    git_commit_push(t_num, args.no_push)

    # 总结
    print("\n" + "=" * 50)
    print(f"🎉 模板 T{t_num:02d} 导入完成！")
    print("=" * 50)
    print(f"  编号: T{t_num:02d}")
    print(f"  尺寸: {width}x{height}")
    print(f"  来源: {args.source or '网页 AI 视觉拆解'}")
    print(f"  指纹: {parsed['fingerprint'][:80]}")
    print(f"\n  下一步: 在 Unity 中 Pull → Level Studio 粘贴 → Build → Play 测试")


if __name__ == '__main__':
    main()
