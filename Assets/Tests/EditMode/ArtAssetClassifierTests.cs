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
