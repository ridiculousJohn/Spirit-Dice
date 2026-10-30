using System;
using System.Collections.Generic;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    /**
     * Scene lighting of a configured table on synthetic split room and table images, with two synthetic steady lights and
     * one prop piece: what an update shows and a release takes off, how a piece light follows its prop, how flares and
     * play-mode testing overrides combine with the ambient multiplier, and what a presentation reset ends.
     */
    public sealed class EncounterTableLightingTests
    {
        private const float Tolerance = .00001f;
        private SyntheticCharmAssets assets;
        private TablePieceAsset pieceAsset;
        private static readonly Vector2 AuthoredUvOffset = new Vector2(.01f, .02f);
        private static readonly Vector3 AuthoredReceiver = new Vector3(1, 2, 3);
        private readonly List<EncounterTableLighting> owners = new List<EncounterTableLighting>();
        private SyntheticUi ui;
        private RawImage room, table;
        private SpiritTablePieceView piece;
        private SceneLightingAsset lighting;

        [SetUp]
        public void SetUp()
        {
            ui = new SyntheticUi();
            var frame = ui.Rect(ui.Root, "Portrait");
            frame.anchorMin = frame.anchorMax = new Vector2(.5f, .5f);
            frame.sizeDelta = SpiritPortraitSafeArea.DefaultReferenceSize;
            // The room image shows the room texture above the table image, which shows the texture's lower slice.
            room = Image(frame, "Room", new Vector2(0, .4f), Vector2.one, new Rect(0, .4f, 1, .6f));
            table = Image(frame, "Table", Vector2.zero, new Vector2(1, .4f), new Rect(0, 0, 1, .4f));
            piece = ui.Add<SpiritTablePieceView>(frame, "Prop");
            assets = new SyntheticCharmAssets();
            pieceAsset = assets.Piece("piece.test.prop");
            SyntheticUi.Set(piece, "piece", pieceAsset);
            foreach (var rect in ui.Root.GetComponentsInChildren<RectTransform>(true)) rect.ForceUpdateRectTransforms();
            lighting = Lighting(.5f, .8f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var owner in owners) owner.Release();
            owners.Clear();
            ui.Dispose();
            assets.Dispose();
        }

        /**
         * Checks an update shows the table's sampled lights through one material shared by the room and table images and
         * follows live asset edits, and a release takes the lighting off and frees the material / fails if the images do not
         * share the sample, an edit is missed or a release leaves lighting behind.
         */
        [Test]
        public void UpdateShowsTheLightingAndReleaseTakesItOff()
        {
            var owner = Configure(Table(lighting));
            owner.Update();
            Material material = room.material;
            Assert.That(material.shader.name, Is.EqualTo("Dice/Scene Lighting"));
            Assert.That(table.material, Is.SameAs(material), "The split images share one compositor.");
            Assert.That(owner.State.Asset, Is.SameAs(lighting));
            Assert.That(owner.State.GetIntensity(0), Is.EqualTo(.5f).Within(Tolerance));
            lighting.Lights[0].Intensity = .57f;
            owner.Update();
            Assert.That(material.GetFloat("_LightIntensity0"), Is.EqualTo(.57f).Within(Tolerance), "An update missed an asset edit.");

            owner.Release();
            Assert.That(owner.State.Asset == null && owner.State.Count == 0, Is.True, "A release kept the sample.");
            Assert.That(room.material == room.defaultMaterial && table.material == table.defaultMaterial, Is.True);
            Assert.That(material == null, Is.True, "A release kept its compositor.");
        }

        /** Checks a table without scene lighting shows none and offers no lights to rules / fails if an unlit table binds a compositor or flare target. */
        [Test]
        public void AnUnlitTableShowsNoLighting()
        {
            var lit = Configure(Table(lighting));
            Assert.That(lit.RuleLights, Is.SameAs(lit));
            var unlit = Configure(Table(null));
            Assert.That(unlit.State.Asset, Is.Null);
            Assert.That(room.material == room.defaultMaterial && table.material == table.defaultMaterial, Is.True);
            Assert.That(unlit.LightCount, Is.Zero);
            Assert.That(unlit.RuleLights, Is.Null);
        }

        /**
         * Checks a light bound to a prop follows it through whichever split image shows the prop, goes dark while the prop
         * is hidden, and that a table without piece lights keeps every authored position / fails if the light ignores the
         * prop or its image slice, stays lit for a hidden prop, or an unbound light moves.
         */
        [Test]
        public void PieceLightsFollowTheirPropThroughTheSplitRoomTexture()
        {
            var reference = new Vector2(.25f, .3f);
            var bound = Configure(Table(lighting, new EncounterTablePieceLight
            {
                LightIndex = 0, Piece = pieceAsset, ReferencePosition = reference,
                ReceiverOffsetX = new Vector3(10, 0, 0), ReceiverOffsetY = new Vector3(0, 8, 0)
            }));
            Place(table, new Vector2(.25f, .1f));
            bound.Update();
            AssertPieceLight(bound.State, table, new Vector2(.25f, .1f), reference);
            Place(room, new Vector2(.75f, .9f));
            bound.Update();
            AssertPieceLight(bound.State, room, new Vector2(.75f, .9f), reference);
            piece.gameObject.SetActive(false);
            bound.Update();
            Assert.That(bound.State.GetIntensity(0), Is.Zero, "A hidden prop kept its light.");
            piece.gameObject.SetActive(true);

            var unbound = Configure(Table(lighting));
            unbound.Update();
            Assert.That(unbound.State.GetUvOffset(0), Is.EqualTo(AuthoredUvOffset));
            Assert.That(unbound.State.GetReceiverPosition(0), Is.EqualTo(AuthoredReceiver));
        }

        /**
         * Checks a held testing override replaces the ambient multiplier until released, for lights bound to a prop and
         * free lights alike, without changing the ambient activity's multiplier / fails if the ambient multiplier reaches a
         * held light, an override changes it, or a release does not restore it.
         */
        [TestCase(true)]
        [TestCase(false)]
        public void OverridesHoldInPlaceOfTheAmbientMultiplier(bool boundToProp)
        {
            var owner = Configure(boundToProp
                ? Table(lighting, new EncounterTablePieceLight { LightIndex = 0, Piece = pieceAsset })
                : Table(lighting));
            var ambient = (IAmbientLightingTarget)owner;
            ambient.SetLightIntensity(0, .25f);
            ambient.SetLightIntensity(1, .5f);
            owner.Update();
            AssertSampled(owner, .5f * .25f, .8f * .5f, "The ambient multipliers did not reach the sampled lights.");
            Assert.That(owner.GetTestLightOverride(0, out float free), Is.False);
            Assert.That(free, Is.EqualTo(.25f), "A free light did not report the ambient multiplier.");

            owner.SetTestLightOverride(0, 0f);
            owner.SetTestLightOverride(1, 1.5f);
            // The activity restoring full intensity must not end a held override.
            ambient.SetLightIntensity(0, 1f);
            ambient.SetLightIntensity(1, 1f);
            owner.Update();
            AssertSampled(owner, 0f, .8f * 1.5f, "A held override did not replace the ambient multiplier.");
            Assert.That(owner.GetTestLightOverride(0, out float held), Is.True);
            Assert.That(held, Is.EqualTo(0f));
            Assert.That(ambient.GetLightIntensity(0), Is.EqualTo(1f), "An override changed the activity's multiplier.");

            owner.ReleaseTestLightOverrides();
            owner.Update();
            AssertSampled(owner, .5f, .8f, "Releasing overrides did not restore the activity's multipliers.");
            Assert.That(owner.GetTestLightOverride(1, out float released), Is.False);
            Assert.That(released, Is.EqualTo(1f));
        }

        /** Checks overrides outside the table's lights or of invalid multipliers are rejected without holding a light / fails if a rejected value is held. */
        [Test]
        public void InvalidOverridesAreRejected()
        {
            var owner = Configure(Table(lighting));
            Assert.That(owner.LightCount, Is.EqualTo(2));
            owner.SetTestLightOverride(1, .75f);
            Assert.Throws<ArgumentOutOfRangeException>(() => owner.SetTestLightOverride(-1, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => owner.SetTestLightOverride(2, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => owner.GetTestLightOverride(-1, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => owner.GetTestLightOverride(2, out _));
            foreach (float invalid in new[] { -.1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => owner.SetTestLightOverride(1, invalid), invalid.ToString());
            Assert.That(owner.GetTestLightOverride(1, out float held), Is.True);
            Assert.That(held, Is.EqualTo(.75f), "A rejected value replaced the held override.");
            Assert.That(owner.GetTestLightOverride(0, out _), Is.False, "A rejected value held a light.");
        }

        /**
         * Checks a rule flare raises its light above the resting level and a presentation reset ends it and releases every
         * override, and that a newly configured table starts with none held / fails if a flare or override outlasts the
         * reset or a new table inherits one.
         */
        [Test]
        public void PresentationResetEndsFlaresAndReleasesOverrides()
        {
            var owner = Configure(Table(lighting));
            // A flare without a rise holds its peak at once.
            ((IRuleSceneLights)owner).Flare(0, 2f, 0f, 60f, 1f);
            owner.SetTestLightOverride(1, 0f);
            owner.Update();
            AssertSampled(owner, .5f * 2f, 0f, "The flare or override did not reach the sampled lights.");
            owner.ResetPresentation();
            owner.Update();
            AssertSampled(owner, .5f, .8f, "A presentation reset kept a flare or override.");
            Assert.That(owner.GetTestLightOverride(1, out _), Is.False);
            var next = Configure(Table(lighting));
            Assert.That(next.GetTestLightOverride(0, out _) || next.GetTestLightOverride(1, out _), Is.False);
        }

        /** Checks configuring fails for a piece light without its prop and a negative resting level / fails if such a table configures. */
        [Test]
        public void ConfiguringFailsForLightsWithoutTheirPropOrWithNegativeRest()
        {
            var missingProp = Table(lighting, new EncounterTablePieceLight { LightIndex = 0, Piece = assets.Piece("piece.missing") });
            Assert.Throws<InvalidOperationException>(() => Configure(missingProp));
            var negativeRest = Table(lighting);
            using (var serialized = new SerializedObject(negativeRest))
            {
                var levels = serialized.FindProperty("restingLightLevels");
                levels.arraySize = 1;
                levels.GetArrayElementAtIndex(0).floatValue = -1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Assert.Throws<InvalidOperationException>(() => Configure(negativeRest));
        }

        private EncounterTableLighting Configure(EncounterTablePresentationAsset configured)
        {
            var owner = new EncounterTableLighting(configured, new[] { piece }, room, table, null, null, null, null);
            owners.Add(owner);
            return owner;
        }

        private RawImage Image(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Rect uv)
        {
            var image = ui.Add<RawImage>(parent, name);
            image.rectTransform.anchorMin = anchorMin;
            image.rectTransform.anchorMax = anchorMax;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            image.uvRect = uv;
            return image;
        }

        private void Place(RawImage image, Vector2 normalized)
        {
            Rect rect = image.rectTransform.rect;
            piece.transform.position = image.rectTransform.TransformPoint(
                new Vector3(rect.xMin + rect.width * normalized.x, rect.yMin + rect.height * normalized.y, 0));
        }

        private static void AssertPieceLight(SceneLightingState sampled, RawImage image, Vector2 normalized, Vector2 reference)
        {
            Vector2 delta = image.uvRect.min + Vector2.Scale(image.uvRect.size, normalized) - reference;
            Assert.That((sampled.GetUvOffset(0) - (AuthoredUvOffset + delta)).magnitude, Is.LessThan(.001f),
                "The light did not follow the prop: " + sampled.GetUvOffset(0));
            Assert.That((sampled.GetReceiverPosition(0) - (AuthoredReceiver + new Vector3(10 * delta.x, 8 * delta.y, 0))).magnitude,
                Is.LessThan(.01f), "The receiver light did not follow the prop: " + sampled.GetReceiverPosition(0));
            Assert.That(sampled.GetIntensity(0), Is.GreaterThan(0f), "A resting prop dimmed its light.");
        }

        private static void AssertSampled(EncounterTableLighting owner, float first, float second, string message)
        {
            Assert.That(owner.State.GetIntensity(0), Is.EqualTo(first).Within(Tolerance), message);
            Assert.That(owner.State.GetIntensity(1), Is.EqualTo(second).Within(Tolerance), message);
        }

        private EncounterTablePresentationAsset Table(SceneLightingAsset sceneLighting, params EncounterTablePieceLight[] pieceLights)
        {
            var created = ui.Own(ScriptableObject.CreateInstance<EncounterTablePresentationAsset>());
            using var serialized = new SerializedObject(created);
            serialized.FindProperty("sceneLighting").objectReferenceValue = sceneLighting;
            var list = serialized.FindProperty("pieceLights");
            list.arraySize = pieceLights.Length;
            for (int i = 0; i < pieceLights.Length; ++i)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("LightIndex").intValue = pieceLights[i].LightIndex;
                element.FindPropertyRelative("Piece").objectReferenceValue = pieceLights[i].Piece;
                element.FindPropertyRelative("ReferencePosition").vector2Value = pieceLights[i].ReferencePosition;
                element.FindPropertyRelative("ReceiverOffsetX").vector3Value = pieceLights[i].ReceiverOffsetX;
                element.FindPropertyRelative("ReceiverOffsetY").vector3Value = pieceLights[i].ReceiverOffsetY;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return created;
        }

        /** Two steady lights of the given intensities over a flat room texture; the first at an authored offset and receiver. */
        private SceneLightingAsset Lighting(float first, float second)
        {
            var texture = ui.Own(new Texture2D(8, 8, TextureFormat.RGBA32, false));
            var contribution = ui.Own(new Texture2D(8, 8, TextureFormat.RGBA32, false));
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; ++i) pixels[i] = new Color(.1f, .12f, .14f);
            texture.SetPixels(pixels); texture.Apply();
            for (int i = 0; i < pixels.Length; ++i) pixels[i] = new Color(.2f, .15f, .08f);
            contribution.SetPixels(pixels); contribution.Apply();
            var asset = ui.Own(ScriptableObject.CreateInstance<SceneLightingAsset>());
            asset.Shader = TestShaders.Load(TestShaders.SceneLighting);
            asset.BaseTexture = texture;
            asset.Lights = new[]
            {
                new SceneLightLayer
                {
                    Contribution = contribution, Intensity = first, FlickerAmount = 0f, UvOffset = AuthoredUvOffset,
                    ReceiverPosition = AuthoredReceiver
                },
                new SceneLightLayer { Contribution = contribution, Intensity = second, FlickerAmount = 0f }
            };
            return asset;
        }
    }
}
