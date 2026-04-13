from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw

import run_pipeline
import blender_render_drive_video as brdv

base = Path('/home/ubuntu/MarioTrickster/Assets/anim_pipeline/MarioTrickster_AnimPipeline/.tmp_s106_smoke')
base.mkdir(parents=True, exist_ok=True)

# 构造一个带左下角“签名块”的参考图
ref_path = base / 'ref.png'
img = Image.new('RGBA', (128, 192), (30, 30, 30, 255))
draw = ImageDraw.Draw(img)
draw.rectangle((36, 28, 92, 172), fill=(190, 190, 190, 255))
draw.rectangle((0, 160, 40, 191), fill=(255, 20, 20, 255))
img.save(ref_path)
clean_ref = run_pipeline.preprocess_reference_image(ref_path, base)
assert clean_ref.exists(), 'cleaned reference image missing'

# 构造两帧：一帧正常，一帧极小主体坏帧
frames_dir = base / 'frames'
frames_dir.mkdir(exist_ok=True)
frame_good = frames_dir / 'frame_0001.png'
frame_bad = frames_dir / 'frame_0002.png'
img_good = Image.new('RGBA', (128, 192), (20, 20, 20, 255))
draw_good = ImageDraw.Draw(img_good)
draw_good.rectangle((40, 20, 88, 180), fill=(220, 220, 220, 255))
img_good.save(frame_good)
img_bad = Image.new('RGBA', (128, 192), (20, 20, 20, 255))
draw_bad = ImageDraw.Draw(img_bad)
draw_bad.rectangle((60, 90, 64, 100), fill=(220, 220, 220, 255))
img_bad.save(frame_bad)

nobg_dir = base / 'nobg'
results, frame_records = run_pipeline.remove_background([frame_good, frame_bad], nobg_dir, str(clean_ref))
assert len(results) == 2, 'remove_background result count mismatch'
assert len(frame_records) == 2, 'frame_records count mismatch'
assert any(r.get('is_bad') for r in frame_records), 'bad frame was not detected'

pixel_dir = base / 'pixel'
pixelized = run_pipeline.pixelize_frames(results, pixel_dir, pixel_size=2, palette_colors=8)
run_pipeline._apply_reference_color_match_to_frames(pixelized, str(clean_ref), frame_records=frame_records)

sheet_path = base / 'sheet.png'
meta_path = base / 'sheet.json'
final_rgb_path = base / 'final_rgb.png'
run_pipeline.assemble_sprite_sheet(pixelized, sheet_path, cols=2, metadata_path=meta_path, frame_records=frame_records, fps=16)
run_pipeline.apply_reference_color_match(sheet_path, str(clean_ref), final_rgb_path)
assert sheet_path.exists() and meta_path.exists() and final_rgb_path.exists(), 'final artifacts missing'
meta = json.loads(meta_path.read_text())
assert meta['pivot'] == {'x': 0.5, 'y': 0.0}, 'pivot metadata mismatch'
assert meta['frames'][1]['is_bad'] is True, 'bad frame metadata mismatch'

args = brdv.get_args if False else None
preset = {'blender_render_settings': {'resolution': [480, 480], 'fps': 16}}
class DummyArgs:
    width = None
    height = None
    fps = None
    proxy_mode = 'auto'
    ortho_padding = 1.6
resolved = brdv.resolve_render_settings(DummyArgs(), preset, 'run')
assert resolved['ortho_padding'] == 1.6, 'blender padding mismatch'

print('S106 smoke test passed')
