import os
import config_manager

def main():
    print("=" * 60)
    print("  MarioTrickster CLIP Vision 模型重命名工具")
    print("=" * 60)

    config = config_manager.load_config()
    comfyui_root = config.get("comfyui_root", "")
    
    if not comfyui_root or not os.path.exists(comfyui_root):
        print(f"[错误] ComfyUI 根目录不存在: {comfyui_root}")
        print("请先在 pipeline_config.json 中配置正确的 comfyui_root 路径。")
        return

    clip_vision_dir = os.path.join(comfyui_root, "models", "clip_vision")
    if not os.path.exists(clip_vision_dir):
        print(f"[错误] clip_vision 目录不存在: {clip_vision_dir}")
        return

    target_name = "clip_vision_h.safetensors"
    target_path = os.path.join(clip_vision_dir, target_name)
    
    if os.path.exists(target_path):
        print(f"[提示] 目标文件已存在，无需重命名: {target_path}")
        return

    # 查找可能的旧文件名
    old_names = [
        "CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors",
        "clip_vision_h_old.safetensors"
    ]
    
    renamed = False
    for old_name in old_names:
        old_path = os.path.join(clip_vision_dir, old_name)
        if os.path.exists(old_path):
            try:
                os.rename(old_path, target_path)
                print(f"[成功] 已将 {old_name} 重命名为 {target_name}")
                renamed = True
                break
            except Exception as e:
                print(f"[错误] 重命名失败: {e}")
                return
                
    if not renamed:
        print(f"[提示] 未找到需要重命名的旧文件。请确保你已下载 CLIP Vision 模型到: {clip_vision_dir}")

if __name__ == "__main__":
    main()
