using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Only builds isolated rehearsal scenes. The coordinator owns saving/restoring user scenes.</summary>
public static class ExplorationSceneBuilder
{
    public static string Validate(MechanismExplorationPlan.Scenario scenario, out bool structuralFailure)
    {
        structuralFailure = !LevelStudioDocument.TryParse(scenario.ascii, out var doc, out string error);
        if (structuralFailure) return error;
        error = doc.PlayReadiness();
        if (!string.IsNullOrEmpty(error)) { structuralFailure = true; return error; }
        var l1 = AsciiLevelValidator.ValidateTemplate(doc.Grid);
        var l2 = LevelReachabilityAnalyzer.Analyze(doc.Grid);
        // Static reachability is only a heuristic, especially for dynamic traversal.
        // Keep the warning in the report and still physically test structurally valid rooms.
        return "L1 errors=" + l1.errors.Count + ", warnings=" + l1.warnings.Count +
            "; L2 reachable=" + l2.IsReachable + "\n" + string.Join("\n", l1.errors.Concat(l1.warnings));
    }

    public static GameObject Build(MechanismExplorationPlan.Scenario scenario, float timeLimit, bool instrument)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Build only in edit mode");
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var root = AsciiLevelGenerator.GenerateFromTemplate(scenario.ascii, true, false);
        if (root == null) throw new InvalidOperationException("Generator returned no root");
        PlayableEnvironmentBuilder.EnsurePlayableEnvironment(root);
        GameplayLoopSceneBootstrapper.EnsureGameplayLoopServices(root);
        if (scenario.lootEscape) GameplayLoopSceneBootstrapper.EnsureCombatRoomSemantics(root);
        var mario = Object.FindObjectOfType<MarioController>();
        if (mario != null && mario.GetComponent<MarioCounterplayProbe>() == null) Undo.AddComponent<MarioCounterplayProbe>(mario.gameObject);
        var gm = Object.FindObjectOfType<GameManager>();
        var config = new SerializedObject(gm);
        config.FindProperty("useTimer").boolValue = true;
        // Observer owns test budget. Keep timeout classification separate from game victory.
        config.FindProperty("levelTimeLimit").floatValue = timeLimit + 5f;
        config.ApplyModifiedPropertiesWithoutUndo();
        if (Camera.main != null) Camera.main.orthographic = true;
        foreach (var passage in root.GetComponentsInChildren<HiddenPassage>(true))
        {
            var exit = new GameObject("Exploration_PassageExit");
            exit.transform.SetParent(root.transform);
            exit.transform.position = new Vector3(passage.transform.position.x + 3f, 1f, 0f);
            var so = new SerializedObject(passage);
            so.FindProperty("exitPoint").objectReferenceValue = exit.transform;
            so.FindProperty("visibility").enumValueIndex = (int)PassageVisibility.HintWhenNear;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if (instrument)
        {
            var components = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (char c in scenario.mechanisms)
            {
                var entry = AsciiElementRegistry.GetDefault().GetEntry(c);
                if (entry == null) throw new InvalidOperationException("Missing registry entry: " + c);
                // Match actual component identity, not translated object names or merely the ASCII character.
                foreach (var target in components.Where(m => m != null &&
                    ((entry.componentTypeNames ?? Array.Empty<string>()).Contains(m.GetType().Name) ||
                     (c == 'o' && m is LootObjective))).Select(m => m.gameObject).Distinct())
                {
                    var probe = target.GetComponent<ExplorationContactProbe>() ?? target.AddComponent<ExplorationContactProbe>();
                    probe.mechanism = c.ToString();
                }
            }
        }
        return root;
    }
}
