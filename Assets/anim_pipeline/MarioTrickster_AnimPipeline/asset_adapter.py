"""
MarioTrickster 产出物适配器 (Asset Adapter)
=============================================
职责：
  将 anim_pipeline 的通用产出物（sprite_sheet.png / sprite_meta.json）
  按照 Art 仓库的接回定义（ART_BIBLE + PIPELINE_ALIGNMENT_PLAYBOOK）
  自动改名并复制到约定目录。

接回定义摘要（来自 PIPELINE_ALIGNMENT_AND_ART_LANDING_PLAYBOOK.md）：
  - 命名规则：{character}_{action}_sheet_v{version:02d}.png
  - 角色目录：Assets/Art/Characters/{Character}/
  - 敌人目录：Assets/Art/Enemies/{Enemy}/
  - 导入参数：PPU=32, Pivot=BottomCenter, Filter=Point
  - 元数据：同名 _meta.json 随行

使用方式：
  1. 被 run_pipeline.py 在后处理完成后自动调用
  2. 也可独立运行：python asset_adapter.py --output ./output --action run --character trickster
"""

import os
import sys
import json
import shutil
import re
from pathlib import Path


# ============================================================
# 目录约定
# ============================================================

# Art 仓库相对于本脚本的位置（通过 submodule 挂载）
# 主仓库结构：MarioTrickster/Assets/anim_pipeline/MarioTrickster_AnimPipeline/asset_adapter.py
# Art submodule：MarioTrickster/Assets/MarioTrickster-Art/Assets/Art/
_SCRIPT_DIR = Path(__file__).parent
_PROJECT_ROOT = _SCRIPT_DIR.parent.parent.parent  # MarioTrickster/
_ART_SUBMODULE = _PROJECT_ROOT / "Assets" / "MarioTrickster-Art"
_ART_ROOT = _ART_SUBMODULE / "Assets" / "Art"

# 如果 submodule 未初始化（空目录），回退到独立克隆的 Art 仓库
_ART_STANDALONE = _PROJECT_ROOT.parent / "MarioTrickster-Art"

# 角色类型 → 子目录映射
ASSET_TYPE_MAP = {
    "character": "Characters",
    "enemy": "Enemies",
    "environment": "Environment",
    "hazard": "Hazards",
    "vfx": "VFX",
    "prop": "Environment",  # 道具暂归环境
    "ui": "UI",
}


def _resolve_art_root():
    """解析 Art 仓库根目录，优先 submodule，回退独立仓库"""
    if _ART_ROOT.exists() and any(_ART_ROOT.iterdir()):
        return _ART_ROOT
    # submodule 可能未初始化，尝试独立仓库
    standalone = _ART_STANDALONE / "Assets" / "Art"
    if standalone.exists() and any(standalone.iterdir()):
        return standalone
    return None


def _find_next_version(target_dir, character, action):
    """
    扫描目标目录中已有的版本号，返回下一个版本号。
    命名格式：{character}_{action}_sheet_v{nn}.png
    """
    pattern = re.compile(
        rf"^{re.escape(character)}_{re.escape(action)}_sheet_v(\d+)\.png$",
        re.IGNORECASE
    )
    max_ver = 0
    if target_dir.exists():
        for f in target_dir.iterdir():
            m = pattern.match(f.name)
            if m:
                max_ver = max(max_ver, int(m.group(1)))
    return max_ver + 1


def adapt_output(output_dir, action, character="trickster", asset_type="character", dry_run=False):
    """
    将管线产出物适配到 Art 仓库约定目录。

    参数：
        output_dir:  管线输出目录（包含 sprite_sheet.png 和 sprite_meta.json）
        action:      动作类型（run, idle, jump 等）
        character:   角色名（默认 trickster）
        asset_type:  资产类型（character, enemy, environment 等）
        dry_run:     True 时只打印不实际复制

    返回：
        dict: {"sheet": 目标路径, "meta": 目标路径, "version": 版本号} 或 None
    """
    output_dir = Path(output_dir)
    sheet_src = output_dir / "sprite_sheet.png"
    meta_src = output_dir / "sprite_meta.json"

    if not sheet_src.exists():
        print(f"  [适配器] 未找到 sprite_sheet.png: {sheet_src}")
        return None

    # 解析 Art 仓库目录
    art_root = _resolve_art_root()
    if art_root is None:
        print("  [适配器] 未找到 Art 仓库目录（submodule 和独立仓库均不可用）")
        print(f"  [适配器] 产出物保留在原位: {sheet_src}")
        return None

    # 确定目标子目录
    type_dir = ASSET_TYPE_MAP.get(asset_type, "Characters")
    char_title = character.capitalize()
    target_dir = art_root / type_dir / char_title

    # 计算版本号
    version = _find_next_version(target_dir, character, action)

    # 构建目标文件名
    sheet_name = f"{character}_{action}_sheet_v{version:02d}.png"
    meta_name = f"{character}_{action}_sheet_v{version:02d}_meta.json"

    sheet_dst = target_dir / sheet_name
    meta_dst = target_dir / meta_name

    if dry_run:
        print(f"  [适配器][DRY RUN] {sheet_src.name} → {sheet_dst}")
        if meta_src.exists():
            print(f"  [适配器][DRY RUN] {meta_src.name} → {meta_dst}")
        return {"sheet": str(sheet_dst), "meta": str(meta_dst), "version": version}

    # 创建目录并复制
    target_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(str(sheet_src), str(sheet_dst))
    print(f"  [适配器] ✓ {sheet_name} → {target_dir}")

    if meta_src.exists():
        # 更新元数据中的文件名引用
        try:
            with open(meta_src, "r") as f:
                meta = json.load(f)
            meta["sprite_sheet"] = sheet_name
            meta["version"] = version
            meta["character"] = character
            meta["action"] = action
            meta["asset_type"] = asset_type
            meta["import_params"] = {
                "ppu": 32,
                "pivot": "BottomCenter (0.5, 0)",
                "filter": "Point",
                "slicer": "AI_SpriteSlicer"
            }
            with open(meta_dst, "w") as f:
                json.dump(meta, f, indent=2, ensure_ascii=False)
            print(f"  [适配器] ✓ {meta_name} → {target_dir}")
        except Exception as e:
            # 元数据写入失败不阻塞主流程
            shutil.copy2(str(meta_src), str(meta_dst))
            print(f"  [适配器] △ 元数据增强失败({e})，已原样复制")

    print(f"  [适配器] 版本: v{version:02d}")
    return {"sheet": str(sheet_dst), "meta": str(meta_dst), "version": version}


# ============================================================
# CLI 入口
# ============================================================

if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(
        description="MarioTrickster 产出物适配器 — 将管线输出适配到 Art 仓库",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
示例:
  python asset_adapter.py --output ./output --action run
  python asset_adapter.py --output ./output --action idle --character trickster --type character
  python asset_adapter.py --output ./output --action run --dry-run
        """
    )
    parser.add_argument("--output", type=str, required=True, help="管线输出目录")
    parser.add_argument("--action", type=str, required=True, help="动作类型 (run/idle/jump/...)")
    parser.add_argument("--character", type=str, default="trickster", help="角色名 (默认: trickster)")
    parser.add_argument("--type", type=str, default="character",
                        choices=list(ASSET_TYPE_MAP.keys()),
                        help="资产类型 (默认: character)")
    parser.add_argument("--dry-run", action="store_true", help="只打印不实际复制")

    args = parser.parse_args()
    result = adapt_output(args.output, args.action, args.character, args.type, args.dry_run)

    if result:
        print(f"\n  适配完成: {result['sheet']}")
    else:
        print("\n  适配未执行，请检查上方提示")
        sys.exit(1)
