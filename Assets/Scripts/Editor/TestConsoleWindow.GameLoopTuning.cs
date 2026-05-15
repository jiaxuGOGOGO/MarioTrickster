using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public partial class TestConsoleWindow
{
    // ═══════════════════════════════════════════════════
    // Tab 5: Game Loop Tuning (对抗节奏实时调参)
    // ═══════════════════════════════════════════════════
    private void DrawGameLoopTuningTab()
    {
        EditorGUILayout.LabelField("Game Loop Tuning", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "所有对抗节奏参数统一写入 Assets/Resources/GameplayLoopConfig.asset。PlayMode 下拖动滑块会通过 GameplayMetrics Facade 实时生效；资源缺失时运行时代码仍回退到各组件默认值。",
            MessageType.Info);

        GameplayLoopConfigSO loadedConfig = EnsureGameplayLoopConfigAsset();
        if (loadedConfig != gameplayLoopConfig || gameplayLoopConfigSerialized == null)
        {
            gameplayLoopConfig = loadedConfig;
            gameplayLoopConfigSerialized = gameplayLoopConfig != null ? new SerializedObject(gameplayLoopConfig) : null;
            GameplayMetrics.SetActiveConfig(gameplayLoopConfig);
        }

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Source Asset", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        GameplayLoopConfigSO selectedConfig = (GameplayLoopConfigSO)EditorGUILayout.ObjectField(
            "Gameplay Loop Config", gameplayLoopConfig, typeof(GameplayLoopConfigSO), false);
        if (EditorGUI.EndChangeCheck())
        {
            gameplayLoopConfig = selectedConfig;
            gameplayLoopConfigSerialized = gameplayLoopConfig != null ? new SerializedObject(gameplayLoopConfig) : null;
            GameplayMetrics.SetActiveConfig(gameplayLoopConfig);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Ping Asset", GUILayout.Height(24)) && gameplayLoopConfig != null)
        {
            EditorGUIUtility.PingObject(gameplayLoopConfig);
            Selection.activeObject = gameplayLoopConfig;
        }
        if (GUILayout.Button("Refresh Facade", GUILayout.Height(24)))
        {
            GameplayMetrics.RefreshConfig();
            gameplayLoopConfig = GameplayMetrics.ActiveConfig;
            gameplayLoopConfigSerialized = gameplayLoopConfig != null ? new SerializedObject(gameplayLoopConfig) : null;
            SceneView.RepaintAll();
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        string status = GameplayMetrics.ActiveConfig != null
            ? $"Facade bound: {GameplayMetrics.ActiveConfig.name}"
            : "Facade not bound; runtime will use local fallback defaults.";
        EditorGUILayout.HelpBox(status, GameplayMetrics.ActiveConfig != null ? MessageType.None : MessageType.Warning);
        EditorGUILayout.EndVertical();

        if (gameplayLoopConfigSerialized == null)
        {
            EditorGUILayout.HelpBox("GameplayLoopConfigSO 不存在或未绑定，无法显示滑块。", MessageType.Warning);
            return;
        }

        EditorGUI.BeginChangeCheck();
        gameplayLoopConfigSerialized.Update();

        DrawGameplayLoopConfigSection("Energy System", new[]
        {
            "energyMaxEnergy", "energyStartEnergy", "energyDisguiseCost", "energyDisguiseDrainPerSecond",
            "energyBlendedDrainMultiplier", "energyControlCost", "energyRegenPerSecond",
            "energyDisguisedRegenMultiplier", "energyRegenDelayAfterControl", "energyLowEnergyThreshold"
        });

        DrawGameplayLoopConfigSection("Scan Ability", new[]
        {
            "scanRadius", "scanCooldown", "scanRevealDuration", "scanRevealGateBonusDuration",
            "scanPulseSpeed", "scanPulseLineWidth", "scanFlashFrequency", "scanRevealColor"
        });

        DrawGameplayLoopConfigSection("Trickster Possession Gate", new[]
        {
            "possessionRevealDuration", "possessionEscapeDuration"
        });

        DrawGameplayLoopConfigSection("Alarm Crisis Director", new[]
        {
            "alarmWarningDuration", "alarmScanSpeed", "alarmScanWidth", "alarmEvidenceAmplifyFactor",
            "alarmScanSuspicionBonus", "alarmTriggerTier", "alarmScanCooldown", "alarmLockdownForcesScan"
        });

        DrawGameplayLoopConfigSection("Route Budget Service", new[]
        {
            "routeAutoRecoveryTime", "routeMaxSimultaneousDegraded"
        });

        DrawGameplayLoopConfigSection("Trickster Heat Meter", new[]
        {
            "heatPerPossession", "heatPerActivation", "heatComboHeatFactor", "heatComboBreakHeatPerChain",
            "heatDecayPerSecond", "heatLockdownFallbackHeat", "heatLockdownCooldown", "heatToDecaySlowdown",
            "heatSuspiciousThreshold", "heatAlertThreshold", "heatLockdownThreshold"
        });

        DrawGameplayLoopConfigSection("Prop Combo Tracker", new[]
        {
            "comboWindow", "comboDifferentAnchorMultiplier", "comboDifferentPropTypeMultiplier",
            "comboSameAnchorMultiplier", "comboSamePropMultiplier", "comboSameAnchorSuspicionBonus", "comboBreakCooldown"
        });

        DrawGameplayLoopConfigSection("Interference Compensation Policy", new[]
        {
            "compensationRouteDegradeResidueBonus", "compensationRouteDegradeEvidenceBonus",
            "compensationPropActivateSuspicionBonus", "compensationProgressBoostDuration", "compensationProgressBoostMultiplier"
        });

        bool changed = gameplayLoopConfigSerialized.ApplyModifiedProperties();
        if (EditorGUI.EndChangeCheck() || changed)
        {
            EditorUtility.SetDirty(gameplayLoopConfig);
            GameplayMetrics.SetActiveConfig(gameplayLoopConfig);
            SceneView.RepaintAll();
            if (EditorApplication.isPlaying)
            {
                Repaint();
            }
        }
    }

    private GameplayLoopConfigSO EnsureGameplayLoopConfigAsset()
    {
        GameplayLoopConfigSO config = GameplayMetrics.ActiveConfig;
        if (config != null)
        {
            return config;
        }

        const string resourcesPath = "Assets/Resources";
        const string assetPath = "Assets/Resources/GameplayLoopConfig.asset";

        config = AssetDatabase.LoadAssetAtPath<GameplayLoopConfigSO>(assetPath);
        if (config != null)
        {
            GameplayMetrics.SetActiveConfig(config);
            return config;
        }

        if (!AssetDatabase.IsValidFolder(resourcesPath))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        config = CreateInstance<GameplayLoopConfigSO>();
        AssetDatabase.CreateAsset(config, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        GameplayMetrics.SetActiveConfig(config);
        return config;
    }

    private void DrawGameplayLoopConfigSection(string title, string[] propertyNames)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        for (int i = 0; i < propertyNames.Length; i++)
        {
            SerializedProperty property = gameplayLoopConfigSerialized.FindProperty(propertyNames[i]);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, true);
            }
            else
            {
                EditorGUILayout.HelpBox($"Missing property: {propertyNames[i]}", MessageType.Warning);
            }
        }

        EditorGUILayout.EndVertical();
    }
}
