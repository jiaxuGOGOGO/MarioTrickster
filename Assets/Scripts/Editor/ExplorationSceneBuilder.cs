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
        var tunnelErrors = MechanismExplorationPlan.TunnelPlanIssues(scenario);
        if (tunnelErrors.Length > 0) { structuralFailure = true; return string.Join("\n", tunnelErrors); }
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
        ConfigureTunnelNetwork(root, scenario);
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
        if (scenario.duelVersion == 2)
        {
            var lower = scenario.routes.First(r => r.id == "lower");
            float middle = (lower.minX + lower.maxX) * 0.5f;
            AddCavernPresentation(root, scenario);
            AddRouteSign(root, new Vector2(middle, 12), "CAVERN / SURFACE: TWO WAYS HOME\nRead a warning - scan or retreat - choose your return");
            AddRouteSign(root, new Vector2(lower.minX - 8, 8), "SURFACE: climb the lookouts\nCAVERN: enter below the earth roof");
            AddRouteSign(root, new Vector2(middle, 3), "VENT SHAFT\nJump up / S + Jump drops through ledges");
            AddRouteSign(root, new Vector2(lower.maxX + 11, 4), "TAKE LOOT\nReturn by either level");
            AddRouteSign(root, new Vector2(3, 4), "ESCAPE <\nQ scans; FLASH = warning / ORANGE = solid");
        }
        else if (scenario.duelVersion >= 1)
        {
            float end = scenario.routes.First(r => r.id == "upper").maxX;
            AddRouteSign(root, new Vector2((13 + end) * 0.5f, 8), "SURFACE / UNDERPASS DUEL\nDisguise - control props - scan - relocate");
            AddRouteSign(root, new Vector2(7, 6), "SURFACE: climb / detour\nUNDERPASS: short route / ambush risk");
            AddRouteSign(root, new Vector2(end + 9, 3), "TAKE LOOT\nReturn LEFT to escape");
            AddRouteSign(root, new Vector2(3, 3), "ESCAPE\nQ: counter a visible warning");
        }
        else if (scenario.version >= 3 && !string.IsNullOrEmpty(scenario.experience))
        {
            if (scenario.tunnelVersion >= 1)
                AddRouteSign(root, new Vector2(22, 9), "TUNNEL DUEL: FEINT / RELOCATE / RETURN\nTrickster: direction input while possessed to transfer\nRunner: Q counter / upper detour / take loot and return");
            AddRouteSign(root, new Vector2(8, 5.5f), "CHOOSE: UPPER / LOWER\nUpper: more jumps, avoid the lower ambush");
            AddRouteSign(root, new Vector2(23, 7f), "LOWER: shorter, but exposed\nYellow warning: Q scan / retreat / wait for recovery");
            AddRouteSign(root, new Vector2(38, 3f), scenario.lootEscape ? "TAKE LOOT\nReturn LEFT: choose your route again" : "EXIT >");
            AddRouteSign(root, new Vector2(4, 3f), scenario.lootEscape ? "ESCAPE <\nBring the loot back here" : "Q scan uses a cooldown\nSave it or reveal early?");
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

    public static void AddCavernPresentation(GameObject root, MechanismExplorationPlan.Scenario scenario)
    {
        if (scenario.duelVersion != 2) return;
        foreach (var wall in root.GetComponentsInChildren<FakeWall>(true))
        { wall.ShowPublicWallCue = true; EditorUtility.SetDirty(wall); }
        var tiles = scenario.ascii.Split('\n').Reverse().ToArray();
        var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        Sprite soil = null;
        foreach (var renderer in renderers)
        {
            int x = Mathf.RoundToInt(renderer.transform.position.x), y = Mathf.RoundToInt(renderer.transform.position.y);
            if (y < 0 || y >= tiles.Length || x < 0 || x >= tiles[y].Length || tiles[y][x] != '#') continue;
            renderer.color = y >= 5 ? new Color(0.43f, 0.30f, 0.19f) : new Color(0.28f, 0.24f, 0.20f);
            if (renderer.sprite != null) soil = renderer.sprite;
        }
        if (soil == null || soil.bounds.size.x <= 0 || soil.bounds.size.y <= 0) return;
        var lane = scenario.routes.First(r => r.id == "lower");
        var backdrop = new GameObject("Cavern_Backdrop_VisualOnly");
        backdrop.transform.SetParent(root.transform);
        backdrop.transform.position = new Vector3((lane.minX + lane.maxX) * 0.5f, 2.5f, 0);
        backdrop.transform.localScale = new Vector3((lane.maxX - lane.minX + 1) / soil.bounds.size.x, 5f / soil.bounds.size.y, 1);
        var sprite = backdrop.AddComponent<SpriteRenderer>(); sprite.sprite = soil;
        sprite.color = new Color(0.10f, 0.12f, 0.15f); sprite.sortingOrder = -50;
        // Static location art only: no collider, hidden opponent markers, input or gameplay effects.
    }

    public static void ConfigureTunnelNetwork(GameObject root, MechanismExplorationPlan.Scenario scenario)
    {
        if (scenario.tunnelVersion < 1) return;
        var errors = MechanismExplorationPlan.TunnelPlanIssues(scenario);
        if (errors.Length > 0) throw new InvalidOperationException(string.Join("; ", errors));
        var anchors = root.GetComponentsInChildren<PossessionAnchor>(true);
        Func<MechanismExplorationPlan.Point, PossessionAnchor> resolve = p => {
            var matches = anchors.Where(a => Vector2.Distance(a.transform.position, new Vector2(p.x, p.y)) < 0.05f).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException($"Tunnel endpoint ({p.x},{p.y}) needs exactly one generated anchor");
            return matches[0];
        };
        foreach (var anchor in anchors) anchor.connectedUnderlineNodes.Clear();
        foreach (var link in scenario.tunnelLinks)
        {
            var from = resolve(link.from); var to = resolve(link.to);
            from.connectedUnderlineNodes.Add(to); to.underlineTransitTime = link.seconds;
            EditorUtility.SetDirty(from); EditorUtility.SetDirty(to);
        }
    }

    private static void AddRouteSign(GameObject root, Vector2 position, string text)
    {
        // Passive world text only: no collider, no AI evidence and no hidden-state disclosure.
        var sign = new GameObject("Experience_RouteHint");
        sign.transform.SetParent(root.transform);
        sign.transform.position = position;
        var label = sign.AddComponent<TextMesh>();
        label.text = text; label.fontSize = 48; label.characterSize = 0.075f;
        label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
        label.color = Color.white;
    }
}
