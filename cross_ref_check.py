#!/usr/bin/env python3
"""
跨文件引用一致性检查

验证：
1. 测试脚本中引用的类/方法在主脚本中是否存在
2. TestSceneBuilder 中引用的组件类是否存在
3. 公共方法调用签名是否匹配
"""

import os
import re
import sys

def extract_classes(filepath):
    """提取文件中定义的所有类名"""
    classes = []
    with open(filepath, 'r', encoding='utf-8-sig') as f:
        for line in f:
            match = re.match(r'\s*(?:public|private|protected|internal)?\s*(?:abstract|sealed|static|partial)?\s*(?:class|struct|enum|interface)\s+(\w+)', line)
            if match:
                classes.append(match.group(1))
    return classes

def extract_public_methods(filepath, class_name=None):
    """提取文件中定义的公共方法"""
    methods = []
    with open(filepath, 'r', encoding='utf-8-sig') as f:
        for line in f:
            match = re.match(r'\s*public\s+(?:static\s+)?(?:virtual\s+)?(?:override\s+)?(?:[\w<>\[\]]+)\s+(\w+)\s*\(', line)
            if match:
                methods.append(match.group(1))
    return methods

def main():
    project_root = os.path.dirname(os.path.abspath(__file__))
    
    # 收集所有类定义
    all_classes = {}
    cs_files = []
    for root, dirs, files in os.walk(os.path.join(project_root, 'Assets')):
        for f in files:
            if f.endswith('.cs'):
                filepath = os.path.join(root, f)
                cs_files.append(filepath)
                classes = extract_classes(filepath)
                for c in classes:
                    all_classes[c] = os.path.relpath(filepath, project_root)
    
    print("=" * 60)
    print("MarioTrickster 跨文件引用一致性检查")
    print("=" * 60)
    
    # 1. 检查 TestSceneBuilder 中引用的类
    print("\n--- TestSceneBuilder 组件引用检查 ---")
    builder_path = os.path.join(project_root, 'Assets/Scripts/Editor/TestSceneBuilder.cs')
    with open(builder_path, 'r', encoding='utf-8-sig') as f:
        builder_content = f.read()
    
    # 提取 AddComponent<T>() 调用
    components_used = re.findall(r'AddComponent<(\w+)>', builder_content)
    components_used = list(set(components_used))
    
    errors = 0
    for comp in sorted(components_used):
        if comp in all_classes:
            print(f"  ✅ {comp} → {all_classes[comp]}")
        elif comp in ['SpriteRenderer', 'BoxCollider2D', 'Rigidbody2D', 'Camera']:
            print(f"  ✅ {comp} → Unity 内置组件")
        else:
            print(f"  ❌ {comp} → 未找到定义！")
            errors += 1
    
    # 2. 检查 EditMode 测试中引用的类
    print("\n--- EditMode 测试引用检查 ---")
    edit_test_path = os.path.join(project_root, 'Assets/Tests/EditMode/ComponentSetupTests.cs')
    with open(edit_test_path, 'r', encoding='utf-8-sig') as f:
        edit_content = f.read()
    
    edit_components = re.findall(r'AddComponent<(\w+)>', edit_content)
    edit_components = list(set(edit_components))
    
    for comp in sorted(edit_components):
        if comp in all_classes:
            print(f"  ✅ {comp} → {all_classes[comp]}")
        elif comp in ['SpriteRenderer', 'BoxCollider2D', 'Rigidbody2D']:
            print(f"  ✅ {comp} → Unity 内置组件")
        else:
            print(f"  ❌ {comp} → 未找到定义！")
            errors += 1
    
    # 3. 检查 PlayMode 测试中引用的类
    print("\n--- PlayMode 测试引用检查 ---")
    play_test_path = os.path.join(project_root, 'Assets/Tests/PlayMode/GameplayTests.cs')
    with open(play_test_path, 'r', encoding='utf-8-sig') as f:
        play_content = f.read()
    
    play_components = re.findall(r'AddComponent<(\w+)>', play_content)
    play_components = list(set(play_components))
    
    for comp in sorted(play_components):
        if comp in all_classes:
            print(f"  ✅ {comp} → {all_classes[comp]}")
        elif comp in ['SpriteRenderer', 'BoxCollider2D', 'Rigidbody2D']:
            print(f"  ✅ {comp} → Unity 内置组件")
        else:
            print(f"  ❌ {comp} → 未找到定义！")
            errors += 1
    
    # 4. 检查关键方法调用
    print("\n--- 关键方法调用检查 ---")
    
    # 检查 MarioController 的公共方法
    mario_path = os.path.join(project_root, 'Assets/Scripts/Player/MarioController.cs')
    mario_methods = extract_public_methods(mario_path)
    
    key_methods = {
        'MarioController': ['SetMoveInput', 'OnJumpPressed', 'OnJumpReleased', 'SetPlatformVelocity', 'Die', 'Bounce'],
        'TricksterController': ['SetMoveInput', 'OnJumpPressed', 'OnJumpReleased', 'SetPlatformVelocity', 'OnDisguisePressed', 'OnAbilityPressed', 'OnSwitchDisguise'],
        'PlayerHealth': ['TakeDamage', 'Heal', 'ResetHealth'],
        'DisguiseSystem': ['Disguise', 'Undisguise', 'ToggleDisguise', 'GetDebugStatus', 'NextDisguise', 'PreviousDisguise'],
        'InputManager': ['SetMarioController', 'SetTricksterController', 'EnableAllInput', 'DisableAllInput'],
        'GameManager': ['StartGame', 'EndRound', 'OnMarioReachedGoal', 'TogglePause', 'RestartLevel', 'ResetRound'],
    }
    
    for class_name, methods in key_methods.items():
        if class_name not in all_classes:
            print(f"  ❌ 类 {class_name} 未找到！")
            errors += 1
            continue
        
        class_file = os.path.join(project_root, all_classes[class_name])
        class_methods = extract_public_methods(class_file)
        
        for method in methods:
            if method in class_methods:
                print(f"  ✅ {class_name}.{method}()")
            else:
                print(f"  ❌ {class_name}.{method}() → 方法未找到！")
                errors += 1
    
    # 5. 检查公共属性
    print("\n--- 关键公共属性检查 ---")
    key_properties = {
        'MarioController': ['IsMoving', 'IsGrounded', 'IsFacingRight'],
        'TricksterController': ['IsDisguised'],
        'PlayerHealth': ['CurrentHealth', 'MaxHealth', 'IsInvincible'],
        'DisguiseSystem': ['IsDisguised', 'IsFullyBlended', 'CooldownRemaining', 'CooldownProgress'],
        'GameManager': ['CurrentState', 'GameTimer', 'MarioWins', 'TricksterWins', 'CurrentRound'],
    }
    
    for class_name, props in key_properties.items():
        if class_name not in all_classes:
            continue
        class_file = os.path.join(project_root, all_classes[class_name])
        with open(class_file, 'r', encoding='utf-8-sig') as f:
            content = f.read()
        for prop in props:
            if re.search(rf'public\s+\w+\s+{prop}\s', content):
                print(f"  ✅ {class_name}.{prop}")
            else:
                print(f"  ❌ {class_name}.{prop} → 属性未找到！")
                errors += 1
    
    print("\n" + "=" * 60)
    if errors == 0:
        print("✅ 所有跨文件引用检查通过！")
    else:
        print(f"❌ 发现 {errors} 个引用错误")
    print("=" * 60)
    
    return 0 if errors == 0 else 1

if __name__ == '__main__':
    sys.exit(main())
