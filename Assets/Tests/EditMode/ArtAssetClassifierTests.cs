#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

public class ArtAssetClassifierTests
{
    [Test]
    public void Classify_PlayerStateSprites_ReturnsStateDrivenCharacter()
    {
        Sprite[] sprites =
        {
            MakeSprite("hero_idle_00"),
            MakeSprite("hero_idle_01"),
            MakeSprite("hero_run_00"),
            MakeSprite("hero_run_01"),
            MakeSprite("hero_jump_00"),
            MakeSprite("hero_fall_00")
        };

        GameObject go = new GameObject("MarioRoot");
        try
        {
            var result = ArtAssetClassifier.Classify(go, sprites, 0);

            Assert.AreEqual(ArtAssetClassifier.AssetRole.Character, result.role);
            Assert.AreEqual(ArtAssetClassifier.AnimationMode.StateDriven, result.animationMode);
            Assert.AreEqual(ArtAssetClassifier.RuntimeBehavior.PlayerStateDriven, result.runtimeBehavior);
            Assert.IsTrue(result.stateFrames.ContainsKey(SpriteStateAnimator.MotionState.Idle));
            Assert.IsTrue(result.stateFrames.ContainsKey(SpriteStateAnimator.MotionState.Run));
            Assert.IsTrue(result.stateFrames.ContainsKey(SpriteStateAnimator.MotionState.Jump));
            Assert.IsTrue(result.stateFrames.ContainsKey(SpriteStateAnimator.MotionState.Fall));
        }
        finally
        {
            foreach (Sprite sprite in sprites) Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Classify_BackgroundSprites_ReturnsBackgroundLoop()
    {
        Sprite[] sprites =
        {
            MakeSprite("forest_background_00"),
            MakeSprite("forest_background_01")
        };

        try
        {
            var result = ArtAssetClassifier.Classify(null, sprites, -1);

            Assert.AreEqual(ArtAssetClassifier.AssetRole.Background, result.role);
            Assert.AreEqual(ArtAssetClassifier.RuntimeBehavior.BackgroundLayer, result.runtimeBehavior);
            Assert.AreEqual(ArtAssetClassifier.AnimationMode.Loop, result.animationMode);
        }
        finally
        {
            foreach (Sprite sprite in sprites) Object.DestroyImmediate(sprite);
        }
    }


    [Test]
    public void BuildStateGroups_CommonLooseFrameNames_SortsAndGroupsStates()
    {
        Sprite[] sprites =
        {
            MakeSprite("Idle0"),
            MakeSprite("run_10"),
            MakeSprite("run_02"),
            MakeSprite("Jump01"),
            MakeSprite("falling-03")
        };

        try
        {
            var result = ArtAssetClassifier.Classify(null, sprites, -1);

            Assert.AreEqual(ArtAssetClassifier.AssetRole.Character, result.role);
            Assert.AreEqual(ArtAssetClassifier.AnimationMode.StateDriven, result.animationMode);
            Assert.AreEqual("idle,run,jump,fall", result.StateSummary);
            Assert.AreEqual("run_02", result.stateFrames[SpriteStateAnimator.MotionState.Run][0].name);
            Assert.AreEqual("run_10", result.stateFrames[SpriteStateAnimator.MotionState.Run][1].name);
        }
        finally
        {
            foreach (Sprite sprite in sprites) Object.DestroyImmediate(sprite.texture);
            foreach (Sprite sprite in sprites) Object.DestroyImmediate(sprite);
        }
    }

    [Test]
    public void Classify_DestroyedConnectionSprites_DoesNotTriggerStateDrivenCharacter()
    {
        Sprite[] sprites =
        {
            MakeSprite("connection_destroyed_00"),
            MakeSprite("connection_destroyed_01"),
            MakeSprite("wire_destroyed_02")
        };

        try
        {
            var result = ArtAssetClassifier.Classify(null, sprites, -1);

            Assert.AreNotEqual(ArtAssetClassifier.AssetRole.Character, result.role);
            Assert.AreEqual(ArtAssetClassifier.AnimationMode.Loop, result.animationMode);
            Assert.IsFalse(result.IsStateDriven);
            Assert.AreEqual(0, result.stateFrames.Count);
        }
        finally
        {
            foreach (Sprite sprite in sprites) Object.DestroyImmediate(sprite);
        }
    }

    [Test]
    public void Classify_OnlyOneStateGroupWithoutPlayerTarget_FallsBackToLoopAnimator()
    {
        Sprite[] sprites =
        {
            MakeSprite("hero_idle_00"),
            MakeSprite("hero_idle_01"),
            MakeSprite("hero_idle_02")
        };

        try
        {
            var result = ArtAssetClassifier.Classify(null, sprites, -1);

            Assert.AreEqual(ArtAssetClassifier.AssetRole.Character, result.role);
            Assert.AreEqual(ArtAssetClassifier.AnimationMode.Loop, result.animationMode);
            Assert.IsFalse(result.IsStateDriven);
            Assert.IsTrue(result.stateFrames.ContainsKey(SpriteStateAnimator.MotionState.Idle));
            Assert.AreEqual(1, result.stateFrames.Count);
        }
        finally
        {
            foreach (Sprite sprite in sprites) Object.DestroyImmediate(sprite);
        }
    }

    [Test]
    public void Classify_SingleRunGroupAppliedToPlayerTarget_ReturnsStateDriven()
    {
        Sprite[] sprites =
        {
            MakeSprite("commercial_hero_run_00"),
            MakeSprite("commercial_hero_run_01"),
            MakeSprite("commercial_hero_run_02")
        };
        GameObject go = new GameObject("MarioRoot");

        try
        {
            var result = ArtAssetClassifier.Classify(go, sprites, -1);

            Assert.AreEqual(ArtAssetClassifier.AssetRole.Character, result.role);
            Assert.AreEqual(ArtAssetClassifier.RuntimeBehavior.PlayerStateDriven, result.runtimeBehavior);
            Assert.AreEqual(ArtAssetClassifier.AnimationMode.StateDriven, result.animationMode);
            Assert.IsTrue(result.IsStateDriven);
            Assert.IsTrue(result.stateFrames.ContainsKey(SpriteStateAnimator.MotionState.Run));
            Assert.AreEqual(1, result.stateFrames.Count);
        }
        finally
        {
            foreach (Sprite sprite in sprites) Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ApplyToMarker_WritesUnifiedMetadataWithoutBreakingLegacyFields()
    {
        GameObject go = new GameObject("MarkerHost");
        try
        {
            ImportedAssetMarker marker = go.AddComponent<ImportedAssetMarker>();
            var classification = new ArtAssetClassifier.Classification
            {
                role = ArtAssetClassifier.AssetRole.Collectible,
                animationMode = ArtAssetClassifier.AnimationMode.Loop,
                runtimeBehavior = ArtAssetClassifier.RuntimeBehavior.PickupConsume,
                confidence = 0.8f,
                notes = "test metadata"
            };

            ArtAssetClassifier.ApplyToMarker(marker, classification);

            Assert.AreEqual("Collectible", marker.assetRole);
            Assert.AreEqual("Loop", marker.animationMode);
            Assert.AreEqual("PickupConsume", marker.runtimeBehavior);
            Assert.AreEqual(0.8f, marker.classificationConfidence, 0.001f);
            Assert.AreEqual("test metadata", marker.classificationNotes);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static Sprite MakeSprite(string name)
    {
        Texture2D texture = new Texture2D(2, 2);
        texture.name = name + "_texture";
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 32f);
        sprite.name = name;
        return sprite;
    }
}
#endif
