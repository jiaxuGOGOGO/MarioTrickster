#!/usr/bin/env python3
"""Lightweight static checks for art-pipeline editor/runtime changes.

This does not replace Unity EditMode tests. It catches common merge mistakes in the
sandbox where the Unity editor binary is unavailable.
"""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
FILES = [
    "Assets/Scripts/Core/SpriteStateAnimator.cs",
    "Assets/Scripts/Core/ImportedAssetMarker.cs",
    "Assets/Scripts/Player/MarioController.cs",
    "Assets/Scripts/Editor/ArtAssetClassifier.cs",
    "Assets/Scripts/Editor/AssetApplyToSelected.cs",
    "Assets/Scripts/Editor/AssetImportPipeline.cs",
    "Assets/SpriteEffectFactory/Editor/SEF_QuickApply.cs",
    "Assets/Tests/EditMode/ArtAssetClassifierTests.cs",
]
REQUIRED_SYMBOLS = {
    "Assets/Scripts/Core/SpriteStateAnimator.cs": ["class SpriteStateAnimator", "MotionState", "MarioController"],
    "Assets/Scripts/Editor/ArtAssetClassifier.cs": ["class ArtAssetClassifier", "BuildStateGroups", "ApplyToMarker"],
    "Assets/SpriteEffectFactory/Editor/SEF_QuickApply.cs": ["ClearControllerAndRendererState", "RestoreSelectedToDefault", "visualEffectLocked"],
    "Assets/Scripts/Editor/AssetApplyToSelected.cs": ["ResolveBehaviorTemplateFromClassification", "ConfigureSpriteStateAnimator"],
}


def fail(message: str) -> None:
    print(f"[static-art-pipeline-check] FAIL: {message}")
    sys.exit(1)

for rel in FILES:
    path = ROOT / rel
    if not path.exists():
        fail(f"missing file: {rel}")
    text = path.read_text(encoding="utf-8")
    if text.count("{") != text.count("}"):
        fail(f"unbalanced braces: {rel}")
    if text.count("(") != text.count(")"):
        fail(f"unbalanced parentheses: {rel}")
    if text.count("[") != text.count("]"):
        fail(f"unbalanced brackets: {rel}")
    for symbol in REQUIRED_SYMBOLS.get(rel, []):
        if symbol not in text:
            fail(f"missing symbol {symbol!r} in {rel}")

print("[static-art-pipeline-check] OK")
