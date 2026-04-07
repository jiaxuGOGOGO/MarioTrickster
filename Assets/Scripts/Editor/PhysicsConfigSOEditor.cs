#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

// ═══════════════════════════════════════════════════════════════════
// PhysicsConfigSOEditor — S52 自定义 Inspector，在底部显示实时推导值
//
// S53 升级：
//   1. 修改 SO 参数后自动刷新 Scene 视图，让 JumpArcVisualizer 实时重绘
//   2. 显示 PhysicsMetrics Facade 联动状态，确认"唯一真理源"已生效
//   3. 显示含 Coyote Time 的最大可跨越间隙
//
// S54 升级：
//   手感预设管理系统 (Preset Manager)
//   - Save Preset: 将当前滑块参数保存为 JSON 预设文件
//   - Load Preset: 从下拉菜单选择预设一键加载，所有滑块瞬间切换
//   - Delete Preset: 删除不需要的预设
//   - 预设存储在 Assets/PhysicsPresets/ 文件夹中，JSON 格式可读可编辑
//   - 支持 Undo（加载预设后可 Ctrl+Z 撤回）
//
// [AI防坑警告] 预设系统只操作 PhysicsConfigSO 的公开字段值，
// 不修改任何底层架构。加载预设等价于手动拖动所有滑块到目标值。
//
// 设计目的：
//   当主理人在 PlayMode 拖动手感滑块时，Inspector 底部实时显示
//   当前参数下的跳跃高度、水平距离等推导值，无需手动计算。
//   这是"所见即所得"调参体验的关键组件。
// ═══════════════════════════════════════════════════════════════════

[CustomEditor(typeof(PhysicsConfigSO))]
public class PhysicsConfigSOEditor : Editor
{
    // ═══════════════════════════════════════════════════
    // 预设管理状态
    // ═══════════════════════════════════════════════════

    private const string PRESETS_FOLDER = "Assets/PhysicsPresets";
    private string _newPresetName = "";
    private int _selectedPresetIndex = 0;
    private string[] _presetNames = new string[0];
    private bool _presetsNeedRefresh = true;

    // ═══════════════════════════════════════════════════
    // JSON 序列化数据结构
    // ═══════════════════════════════════════════════════

    [System.Serializable]
    private class PresetData
    {
        // 移动
        public float maxSpeed;
        public float acceleration;
        public float groundDeceleration;
        public float airDeceleration;
        public float groundingForce;
        // 跳跃
        public float jumpPower;
        public float maxFallSpeed;
        public float fallAcceleration;
        public float jumpEndEarlyGravityModifier;
        public float coyoteTime;
        public float jumpBuffer;
        // 顶点
        public float apexThreshold;
        public float apexGravityMultiplier;
        // 弹射
        public float airFriction;
        public float bounceAirAcceleration;
        // 击退
        public float knockbackStunDuration;
        // 地面检测
        public float grounderDistance;
        // 元数据
        public string description;
        public string savedAt;
    }

    // ═══════════════════════════════════════════════════
    // Inspector 绘制
    // ═══════════════════════════════════════════════════

    public override void OnInspectorGUI()
    {
        // 检测参数变化
        EditorGUI.BeginChangeCheck();

        // 绘制默认 Inspector（所有 [Range] 滑块）
        DrawDefaultInspector();

        // S53: 参数变化时刷新 Scene 视图，让 JumpArcVisualizer 实时重绘
        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }

        PhysicsConfigSO config = (PhysicsConfigSO)target;

        // ── 实时推导值 ──
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("实时推导值 (Derived Values)", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(true); // 只读

        EditorGUILayout.FloatField("最大跳跃高度 (格)", config.DerivedMaxJumpHeight);
        EditorGUILayout.FloatField("满速平跳距离 (格)", config.DerivedMaxJumpDistance);
        EditorGUILayout.FloatField("短跳最低高度 (格)", config.DerivedMinJumpHeight);
        EditorGUILayout.FloatField("Coyote 额外距离 (格)", config.DerivedCoyoteBonusDistance);
        // S53: 显示含 Coyote 的最大可跨越间隙
        EditorGUILayout.FloatField("最大可跨越间隙 (格)",
            config.DerivedMaxJumpDistance + config.DerivedCoyoteBonusDistance);

        EditorGUI.EndDisabledGroup();

        // ── S54: 手感预设管理 ──
        EditorGUILayout.Space(15);
        DrawPresetManager(config);

        // ── S53: Facade 联动状态 ──
        EditorGUILayout.Space(5);
        bool isFacadeActive = PhysicsMetrics.ActiveConfig == config;
        if (isFacadeActive)
        {
            EditorGUILayout.HelpBox(
                "S53 唯一真理源已生效：此 SO 的推导值正在驱动 PhysicsMetrics、" +
                "验证器、JumpArcVisualizer 等全项目组件。拖动滑块即可全局同步。",
                MessageType.Info);
        }
        else if (PhysicsMetrics.ActiveConfig != null)
        {
            EditorGUILayout.HelpBox(
                "注意：PhysicsMetrics 当前绑定了另一个 PhysicsConfigSO 实例。" +
                "此 SO 的修改不会影响全局度量。",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "提示：将此 SO 放入 Resources 文件夹并命名为 PhysicsConfig，" +
                "即可自动绑定为 PhysicsMetrics 的唯一真理源。",
                MessageType.Info);
        }

        // 安全警告
        if (config.DerivedMaxJumpHeight < 1.5f)
        {
            EditorGUILayout.HelpBox(
                "警告：当前跳跃高度不足 1.5 格，可能无法跨越标准高台！",
                MessageType.Warning);
        }

        if (config.DerivedMaxJumpDistance < 3f)
        {
            EditorGUILayout.HelpBox(
                "警告：当前平跳距离不足 3 格，可能无法跨越标准间隙！",
                MessageType.Warning);
        }
    }

    // ═══════════════════════════════════════════════════
    // 预设管理 UI
    // ═══════════════════════════════════════════════════

    private void DrawPresetManager(PhysicsConfigSO config)
    {
        // 标题栏
        EditorGUILayout.LabelField("🎮 手感预设管理 (Preset Manager)", EditorStyles.boldLabel);

        // 刷新预设列表
        if (_presetsNeedRefresh)
        {
            RefreshPresetList();
            _presetsNeedRefresh = false;
        }

        // ── 保存预设 ──
        EditorGUILayout.BeginHorizontal();
        _newPresetName = EditorGUILayout.TextField("预设名称", _newPresetName);
        if (GUILayout.Button("💾 保存", GUILayout.Width(60)))
        {
            if (string.IsNullOrWhiteSpace(_newPresetName))
            {
                EditorUtility.DisplayDialog("保存失败", "请输入预设名称。", "确定");
            }
            else
            {
                SavePreset(config, _newPresetName.Trim());
                _newPresetName = "";
                _presetsNeedRefresh = true;
            }
        }
        EditorGUILayout.EndHorizontal();

        // ── 加载/删除预设 ──
        if (_presetNames.Length > 0)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();

            // 下拉菜单
            _selectedPresetIndex = EditorGUILayout.Popup("已保存预设", _selectedPresetIndex, _presetNames);

            // 加载按钮
            if (GUILayout.Button("📂 加载", GUILayout.Width(60)))
            {
                if (_selectedPresetIndex >= 0 && _selectedPresetIndex < _presetNames.Length)
                {
                    LoadPreset(config, _presetNames[_selectedPresetIndex]);
                }
            }

            // 删除按钮
            if (GUILayout.Button("🗑", GUILayout.Width(30)))
            {
                if (_selectedPresetIndex >= 0 && _selectedPresetIndex < _presetNames.Length)
                {
                    string name = _presetNames[_selectedPresetIndex];
                    if (EditorUtility.DisplayDialog("删除预设",
                        $"确定要删除预设 \"{name}\" 吗？此操作不可撤销。", "删除", "取消"))
                    {
                        DeletePreset(name);
                        _presetsNeedRefresh = true;
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            // 显示选中预设的描述信息
            if (_selectedPresetIndex >= 0 && _selectedPresetIndex < _presetNames.Length)
            {
                string desc = GetPresetDescription(_presetNames[_selectedPresetIndex]);
                if (!string.IsNullOrEmpty(desc))
                {
                    EditorGUILayout.HelpBox(desc, MessageType.None);
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "暂无已保存的预设。输入名称（如"轻盈版"、"重手感"）后点击保存。",
                MessageType.None);
        }

        // 刷新按钮
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("🔄 刷新列表", GUILayout.Width(80)))
        {
            _presetsNeedRefresh = true;
        }
        EditorGUILayout.EndHorizontal();
    }

    // ═══════════════════════════════════════════════════
    // 预设 IO 操作
    // ═══════════════════════════════════════════════════

    private void RefreshPresetList()
    {
        if (!Directory.Exists(PRESETS_FOLDER))
        {
            _presetNames = new string[0];
            _selectedPresetIndex = 0;
            return;
        }

        _presetNames = Directory.GetFiles(PRESETS_FOLDER, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(n => n)
            .ToArray();

        if (_selectedPresetIndex >= _presetNames.Length)
            _selectedPresetIndex = Mathf.Max(0, _presetNames.Length - 1);
    }

    private void SavePreset(PhysicsConfigSO config, string presetName)
    {
        // 确保文件夹存在
        if (!Directory.Exists(PRESETS_FOLDER))
        {
            Directory.CreateDirectory(PRESETS_FOLDER);
        }

        // 构建预设数据
        PresetData data = new PresetData
        {
            // 移动
            maxSpeed = config.maxSpeed,
            acceleration = config.acceleration,
            groundDeceleration = config.groundDeceleration,
            airDeceleration = config.airDeceleration,
            groundingForce = config.groundingForce,
            // 跳跃
            jumpPower = config.jumpPower,
            maxFallSpeed = config.maxFallSpeed,
            fallAcceleration = config.fallAcceleration,
            jumpEndEarlyGravityModifier = config.jumpEndEarlyGravityModifier,
            coyoteTime = config.coyoteTime,
            jumpBuffer = config.jumpBuffer,
            // 顶点
            apexThreshold = config.apexThreshold,
            apexGravityMultiplier = config.apexGravityMultiplier,
            // 弹射
            airFriction = config.airFriction,
            bounceAirAcceleration = config.bounceAirAcceleration,
            // 击退
            knockbackStunDuration = config.knockbackStunDuration,
            // 地面检测
            grounderDistance = config.grounderDistance,
            // 元数据
            description = $"跳高{config.DerivedMaxJumpHeight:F1}格 | " +
                          $"跳远{config.DerivedMaxJumpDistance:F1}格 | " +
                          $"速度{config.maxSpeed:F0}",
            savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        string json = JsonUtility.ToJson(data, true);
        string path = Path.Combine(PRESETS_FOLDER, presetName + ".json");

        // 检查同名覆盖
        if (File.Exists(path))
        {
            if (!EditorUtility.DisplayDialog("覆盖确认",
                $"预设 \"{presetName}\" 已存在，是否覆盖？", "覆盖", "取消"))
            {
                return;
            }
        }

        File.WriteAllText(path, json);
        AssetDatabase.Refresh();

        Debug.Log($"[PhysicsPreset] 已保存预设: {presetName} " +
                  $"(跳高{data.description})");
    }

    private void LoadPreset(PhysicsConfigSO config, string presetName)
    {
        string path = Path.Combine(PRESETS_FOLDER, presetName + ".json");
        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog("加载失败", $"预设文件不存在: {path}", "确定");
            return;
        }

        string json = File.ReadAllText(path);
        PresetData data = JsonUtility.FromJson<PresetData>(json);

        // 支持 Undo
        Undo.RecordObject(config, $"Load Preset: {presetName}");

        // 应用所有参数
        config.maxSpeed = data.maxSpeed;
        config.acceleration = data.acceleration;
        config.groundDeceleration = data.groundDeceleration;
        config.airDeceleration = data.airDeceleration;
        config.groundingForce = data.groundingForce;

        config.jumpPower = data.jumpPower;
        config.maxFallSpeed = data.maxFallSpeed;
        config.fallAcceleration = data.fallAcceleration;
        config.jumpEndEarlyGravityModifier = data.jumpEndEarlyGravityModifier;
        config.coyoteTime = data.coyoteTime;
        config.jumpBuffer = data.jumpBuffer;

        config.apexThreshold = data.apexThreshold;
        config.apexGravityMultiplier = data.apexGravityMultiplier;

        config.airFriction = data.airFriction;
        config.bounceAirAcceleration = data.bounceAirAcceleration;

        config.knockbackStunDuration = data.knockbackStunDuration;
        config.grounderDistance = data.grounderDistance;

        // 标记为脏（确保保存）
        EditorUtility.SetDirty(config);
        SceneView.RepaintAll();

        Debug.Log($"[PhysicsPreset] 已加载预设: {presetName} " +
                  $"(保存于 {data.savedAt})");
    }

    private void DeletePreset(string presetName)
    {
        string path = Path.Combine(PRESETS_FOLDER, presetName + ".json");
        if (File.Exists(path))
        {
            File.Delete(path);
            // 同时删除 .meta 文件
            string metaPath = path + ".meta";
            if (File.Exists(metaPath))
                File.Delete(metaPath);

            AssetDatabase.Refresh();
            Debug.Log($"[PhysicsPreset] 已删除预设: {presetName}");
        }
    }

    private string GetPresetDescription(string presetName)
    {
        string path = Path.Combine(PRESETS_FOLDER, presetName + ".json");
        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            PresetData data = JsonUtility.FromJson<PresetData>(json);
            return $"{data.description}  (保存于 {data.savedAt})";
        }
        catch
        {
            return null;
        }
    }
}
#endif
