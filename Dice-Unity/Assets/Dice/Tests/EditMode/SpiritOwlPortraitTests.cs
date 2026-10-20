using System.Linq;
using System.Reflection;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class SpiritOwlPortraitTests
    {
        private GameObject host;
        private SpiritHostPortrait portrait;
        private SpiritHostPresentationAsset presentation;
        private static readonly Vector2 PerchPosition = new Vector2(.814f, .884f);
        private static readonly Vector2 TablePosition = new Vector2(.5f, .612f);

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Spirit Owl Test", typeof(RectTransform));
            var parent = host.GetComponent<RectTransform>();
            parent.sizeDelta = new Vector2(540f, 960f);
            presentation = AssetDatabase.LoadAssetAtPath<SpiritHostPresentationAsset>(
                "Assets/Dice/Content/Presentation/Watchtower/WatchtowerHost.asset");
            Assert.That(presentation, Is.Not.Null);
            portrait = presentation.Create(parent);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        [Test]
        public void ArrivalUsesSeparateFlightAndLandingArtworkBeforeFoldingAtTheTable()
        {
            portrait.SetState(SpiritPresentationState.Waiting, 0f);
            AssertContact("Physical Rest", PerchPosition);
            Assert.That(Picture("Physical Rest").color.a, Is.EqualTo(1f));
            AssertArtwork("Physical Rest");

            portrait.SetState(SpiritPresentationState.Arrival, .45f);
            Assert.That(Picture("Physical Arrival").color.a, Is.EqualTo(1f));
            AssertArtwork("Physical Arrival");
            Assert.That(Contact("Physical Arrival").x, Is.InRange(TablePosition.x, PerchPosition.x));

            portrait.SetState(SpiritPresentationState.Arrival, .81f);
            Assert.That(Picture("Physical Settle").color.a, Is.EqualTo(1f));
            AssertArtwork("Physical Settle");
            AssertContact("Physical Settle", TablePosition);

            portrait.SetState(SpiritPresentationState.Arrival, 1f);
            Assert.That(Picture("Physical Rest").color.a, Is.EqualTo(1f));
            Assert.That(Picture("Physical Arrival").color.a, Is.Zero);
            Assert.That(Picture("Physical Settle").color.a, Is.Zero);
            AssertContact("Physical Rest", TablePosition);
        }

        [Test]
        public void SpiritTurnsBeforePhysicalHeadOnArrivalIdleAndReaction()
        {
            portrait.SetState(SpiritPresentationState.Arrival, .055f);
            AssertSpiritLeads();
            portrait.SetState(SpiritPresentationState.Idle, 1f);
            portrait.Advance(3.8f);
            AssertSpiritLeads();
            portrait.ResetPose();
            portrait.React();
            portrait.Advance(.1f);
            Assert.That(portrait.IsReacting, Is.True);
            AssertSpiritLeads();
            portrait.Advance(.3f);
            Assert.That(HeadDisplacement("Physical Rest"), Is.GreaterThan(1f));
        }

        [TestCase(SpiritPresentationState.Victory, 1.5f)]
        [TestCase(SpiritPresentationState.Defeat, 1.2f)]
        [TestCase(SpiritPresentationState.Farewell, .5f)]
        public void OutcomeHeadMotionLeadsWithTheSpiritBeforeThePhysicalAcknowledgment(
            SpiritPresentationState state, float duration)
        {
            Assert.That(portrait.GetDuration(state), Is.EqualTo(duration));
            portrait.SetState(state, .1f / duration);
            Assert.That(Mathf.Abs(Picture("Spirit Rest").Weights[2]), Is.GreaterThan(.005f));
            Assert.That(Picture("Physical Rest").Weights[2], Is.Zero.Within(.0001f));
            Assert.That(Picture("Spirit Rest").color.a, Is.GreaterThan(.03f));
            portrait.SetState(state, .4f / duration);
            Assert.That(Mathf.Abs(Picture("Physical Rest").Weights[2]), Is.GreaterThan(.01f));
            Assert.That(Picture("Physical Rest").color.a, Is.EqualTo(1f));
            Assert.That(Picture("Physical Arrival").color.a, Is.Zero);
            Assert.That(Picture("Physical Settle").color.a, Is.Zero);
        }

        [TestCase(SpiritPresentationState.Victory)]
        [TestCase(SpiritPresentationState.Defeat)]
        [TestCase(SpiritPresentationState.Farewell)]
        public void OutcomeSamplingIsNormalizedDeterministicAndDoesNotAdvanceItself(SpiritPresentationState state)
        {
            portrait.SetState(state, .4f);
            Vector2 head = ArtworkPoint("Physical Rest", new Vector2(.53f, .85f));
            float alpha = Picture("Spirit Rest").color.a;
            portrait.Advance(10f);
            Assert.That(portrait.Progress, Is.EqualTo(.4f));
            Assert.That(ArtworkPoint("Physical Rest", new Vector2(.53f, .85f)), Is.EqualTo(head));
            Assert.That(Picture("Spirit Rest").color.a, Is.EqualTo(alpha));
            portrait.SetState(state, .9f);
            portrait.SetState(state, .4f);
            Assert.That(ArtworkPoint("Physical Rest", new Vector2(.53f, .85f)), Is.EqualTo(head));
            portrait.ResetPose();
            portrait.SetState(state, .4f);
            Assert.That(ArtworkPoint("Physical Rest", new Vector2(.53f, .85f)), Is.EqualTo(head));
        }

        [Test]
        public void OutcomeEntryPreservesIdleAndInterruptedReactionDeformation(
            [Values(SpiritPresentationState.Victory, SpiritPresentationState.Defeat, SpiritPresentationState.Farewell)]
            SpiritPresentationState state, [Values(false, true)] bool reacting)
        {
            portrait.Advance(3.85f);
            if (reacting)
            {
                portrait.React();
                portrait.Advance(.4f);
            }
            float[][] weights = presentation.Layers.Select(layer => Picture(layer.Id).Weights.ToArray()).ToArray();
            float[] alphas = presentation.Layers.Select(layer => Picture(layer.Id).color.a).ToArray();
            Vector2 head = ArtworkPoint("Physical Rest", new Vector2(.53f, .85f));

            portrait.SetState(state, 0f);

            for (int i = 0; i < presentation.Layers.Length; ++i)
            {
                var picture = Picture(presentation.Layers[i].Id);
                Assert.That(picture.color.a, Is.EqualTo(alphas[i]).Within(.0001f), picture.name);
                for (int channel = 0; channel < weights[i].Length; ++channel)
                    Assert.That(picture.Weights[channel], Is.EqualTo(weights[i][channel]).Within(.0001f), picture.name);
            }
            Assert.That(Vector2.Distance(ArtworkPoint("Physical Rest", new Vector2(.53f, .85f)), head), Is.LessThan(.001f));
            Assert.That(portrait.IsReacting, Is.False);
        }

        [TestCase(SpiritPresentationState.Victory)]
        [TestCase(SpiritPresentationState.Defeat)]
        [TestCase(SpiritPresentationState.Farewell)]
        public void EveryOutcomeKeepsThePhysicalFeetGroundedAndTheArtworkTransformsFixed(SpiritPresentationState state)
        {
            Vector2[] sizes = presentation.Layers.Select(layer => Picture(layer.Id).rectTransform.sizeDelta).ToArray();
            for (int sample = 0; sample <= 20; ++sample)
            {
                portrait.SetState(state, sample / 20f);
                for (int i = 0; i < presentation.Layers.Length; ++i)
                {
                    var picture = Picture(presentation.Layers[i].Id);
                    Assert.That(picture.rectTransform.sizeDelta, Is.EqualTo(sizes[i]), picture.name);
                    Assert.That(picture.rectTransform.localRotation, Is.EqualTo(Quaternion.identity), picture.name);
                    AssertContact(picture.name, TablePosition);
                }
            }
        }

        [TestCase(SpiritPresentationState.Victory)]
        [TestCase(SpiritPresentationState.Defeat)]
        [TestCase(SpiritPresentationState.Farewell)]
        public void OutcomeTerminalPoseMatchesIdleAndRemainsStable(SpiritPresentationState state)
        {
            portrait.Sample(portrait.GetDuration(state));
            float[][] weights = presentation.Layers.Select(layer => Picture(layer.Id).Weights.ToArray()).ToArray();
            float[] alphas = presentation.Layers.Select(layer => Picture(layer.Id).color.a).ToArray();
            portrait.SetState(state, .4f);
            portrait.SetState(state, 1f);
            portrait.Advance(20f);

            Assert.That(portrait.State, Is.EqualTo(state));
            Assert.That(portrait.Progress, Is.EqualTo(1f));
            for (int i = 0; i < presentation.Layers.Length; ++i)
            {
                var picture = Picture(presentation.Layers[i].Id);
                Assert.That(picture.color.a, Is.EqualTo(alphas[i]).Within(.0001f), picture.name);
                for (int channel = 0; channel < weights[i].Length; ++channel)
                    Assert.That(picture.Weights[channel], Is.EqualTo(weights[i][channel]).Within(.0001f), picture.name);
            }
            AssertContacts(TablePosition);
        }

        [Test]
        public void VictoryAcknowledgmentAndQuieterDefeatTiltInOppositeDirections()
        {
            portrait.SetState(SpiritPresentationState.Victory, .5f);
            float victory = Picture("Physical Rest").Weights[2];
            Assert.That(victory, Is.GreaterThan(.09f));
            portrait.ResetPose();
            portrait.SetState(SpiritPresentationState.Defeat, .5f);
            float defeat = Picture("Physical Rest").Weights[2];
            Assert.That(defeat, Is.LessThan(-.05f));
            Assert.That(Mathf.Abs(defeat), Is.LessThan(victory * .7f));
        }

        [Test]
        public void ArrivalIsDeterministicAfterSeekingResetAndSkipping()
        {
            portrait.SetState(SpiritPresentationState.Arrival, .45f);
            Vector2 contact = Contact("Physical Arrival");
            Vector2 flightSize = Picture("Physical Arrival").rectTransform.sizeDelta;
            Vector2 displacement = Picture("Physical Arrival").EvaluateDisplacement(new Vector2(.43f, .52f), flightSize);
            portrait.Advance(100f);
            Assert.That(Contact("Physical Arrival"), Is.EqualTo(contact));
            Assert.That(Picture("Physical Arrival").EvaluateDisplacement(new Vector2(.43f, .52f), flightSize),
                Is.EqualTo(displacement));
            portrait.SetState(SpiritPresentationState.Arrival, .9f);
            portrait.ResetPose();
            portrait.SetState(SpiritPresentationState.Arrival, .45f);
            Assert.That(Contact("Physical Arrival"), Is.EqualTo(contact));
            Assert.That(Picture("Physical Arrival").rectTransform.sizeDelta, Is.EqualTo(flightSize));
            Assert.That(Picture("Physical Arrival").EvaluateDisplacement(new Vector2(.43f, .52f), flightSize),
                Is.EqualTo(displacement));
            portrait.SetState(SpiritPresentationState.Idle, 1f);
            AssertContact("Physical Rest", TablePosition);
            Assert.That(Picture("Physical Rest").color.a, Is.EqualTo(1f));
            Assert.That(portrait.IsReacting, Is.False);
        }

        [Test]
        public void LandingAndReactionKeepEveryPhysicalFootContactPlanted()
        {
            foreach (float progress in new[] { .76f, .81f, .9f, .96f, 1f })
            {
                portrait.SetState(SpiritPresentationState.Arrival, progress);
                AssertContacts(TablePosition);
            }
            portrait.SetState(SpiritPresentationState.Idle, 1f);
            portrait.Advance(3.8f);
            AssertContacts(TablePosition);
            portrait.React();
            foreach (float step in new[] { .1f, .3f, .7f, .8f })
            {
                portrait.Advance(step);
                AssertContacts(TablePosition);
            }
            Assert.That(portrait.IsReacting, Is.False);
        }

        [TestCase(.24f, "Physical Rest", .5f, .80f, "Physical Arrival", .43f, .47f)]
        [TestCase(.735f, "Physical Arrival", .43f, .47f, "Physical Settle", .51f, .72f)]
        [TestCase(.90f, "Physical Settle", .51f, .72f, "Physical Rest", .5f, .80f)]
        public void PoseBlendsRegisterTheFaceAboveThePlantedBody(float progress, string first, float firstX,
            float firstY, string second, float secondX, float secondY)
        {
            portrait.SetState(SpiritPresentationState.Arrival, progress);
            Assert.That(Picture(first).color.a, Is.GreaterThan(.05f));
            Assert.That(Picture(second).color.a, Is.GreaterThan(.05f));
            Assert.That(Vector2.Distance(ArtworkPoint(first, new Vector2(firstX, firstY)),
                ArtworkPoint(second, new Vector2(secondX, secondY))), Is.LessThan(5f));
        }

        [Test]
        public void FoldingKeepsTheChestRegisteredAndRecoversFromASmallGroundedSettle()
        {
            portrait.SetState(SpiritPresentationState.Arrival, .90f);
            Assert.That(Vector2.Distance(ArtworkPoint("Physical Settle", new Vector2(.51f, .43f)),
                ArtworkPoint("Physical Rest", new Vector2(.5f, .52f))), Is.LessThan(5f));
            portrait.SetState(SpiritPresentationState.Arrival, .76f);
            float landingHead = ArtworkPoint("Physical Settle", new Vector2(.51f, .72f)).y;
            portrait.SetState(SpiritPresentationState.Arrival, .79f);
            float compressedHead = ArtworkPoint("Physical Settle", new Vector2(.51f, .72f)).y;
            Assert.That(landingHead - compressedHead, Is.InRange(3f, 6f));
            AssertContacts(TablePosition);
            portrait.SetState(SpiritPresentationState.Arrival, 1f);
            Assert.That(ArtworkPoint("Physical Rest", new Vector2(.5f, .80f)).y - compressedHead,
                Is.InRange(3f, 6f));
        }

        [TestCase(540f, 960f)]
        [TestCase(443f, 900f)]
        [TestCase(960f, 540f)]
        public void AuthoredContactsFollowTheRoomAcrossPortraitSizes(float width, float height)
        {
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            portrait.SetState(SpiritPresentationState.Waiting, 0f);
            AssertContact("Physical Rest", PerchPosition);
            portrait.SetState(SpiritPresentationState.Idle, 1f);
            AssertContacts(TablePosition);
        }

        [Test]
        public void WatcherHasAuthoredFeetToCrownHeightAtPerchAndTable()
        {
            portrait.SetState(SpiritPresentationState.Waiting, 0f);
            Assert.That(VisibleHeight(), Is.EqualTo(.062f).Within(.001f));
            portrait.SetState(SpiritPresentationState.Idle, 1f);
            Assert.That(VisibleHeight(), Is.EqualTo(.31f).Within(.001f));
        }

        [Test]
        public void FlightWingsFitInsideThePortraitThroughoutDeparture()
        {
            var parent = host.GetComponent<RectTransform>();
            for (int sample = 18; sample < 79; ++sample)
            {
                portrait.SetState(SpiritPresentationState.Arrival, sample / 100f);
                var picture = Picture("Physical Arrival");
                Vector3 left = parent.InverseTransformPoint(picture.rectTransform.TransformPoint(
                    new Vector3(picture.rectTransform.rect.xMin, 0f, 0f)));
                Vector3 right = parent.InverseTransformPoint(picture.rectTransform.TransformPoint(
                    new Vector3(picture.rectTransform.rect.xMax, 0f, 0f)));
                Assert.That(left.x, Is.GreaterThanOrEqualTo(parent.rect.xMin));
                Assert.That(right.x, Is.LessThanOrEqualTo(parent.rect.xMax));
                foreach (float edge in new[] { 0f, 1f })
                    Assert.That(ArtworkPoint("Physical Arrival", new Vector2(edge, 1f)).y,
                        Is.LessThanOrEqualTo(parent.rect.yMax));
            }
        }

        [Test]
        public void HeadDeformationPreservesFeetAndDoesNotRotateFlightWingTips()
        {
            var picture = Picture("Physical Arrival");
            var layer = presentation.Layers.Single(value => value.Id == picture.name);
            for (int channel = 0; channel < layer.Mesh.Shapes.Length; ++channel)
            {
                var weights = new float[layer.Mesh.Shapes.Length];
                weights[channel] = 1f;
                picture.SetWeights(weights);
                Vector2 size = new Vector2(450f, 300f);
                Assert.That(picture.EvaluateDisplacement(layer.Pivot, size).magnitude, Is.LessThan(.001f));
                if (channel == 1 || channel == 2)
                {
                    Assert.That(picture.EvaluateDisplacement(new Vector2(.1f, .85f), size).magnitude, Is.LessThan(.001f));
                    Assert.That(picture.EvaluateDisplacement(new Vector2(.9f, .75f), size).magnitude, Is.LessThan(.001f));
                }
            }
        }

        [Test]
        public void MeshKeepsTextureAlphaAndLocalizedSpiritFadeSeparate()
        {
            var picture = Picture("Spirit Rest");
            var layer = presentation.Layers.Single(value => value.Id == picture.name);
            portrait.React();
            portrait.Advance(.1f);
            Assert.That(picture.texture, Is.SameAs(layer.Texture));
            Assert.That(picture.raycastTarget, Is.False);
            using var vertices = new VertexHelper();
            typeof(SpiritPortraitImage).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(picture, new object[] { vertices });
            Assert.That(vertices.currentVertCount, Is.GreaterThan(100));
            UIVertex foot = default;
            vertices.PopulateUIVertex(ref foot, layer.Mesh.Columns / 2);
            Assert.That(foot.color.a, Is.Zero);
            UIVertex head = default;
            vertices.PopulateUIVertex(ref head, layer.Mesh.Rows * (layer.Mesh.Columns + 1) + layer.Mesh.Columns / 2);
            Assert.That(head.color.a, Is.GreaterThan(0));
        }

        [Test]
        public void HiddenResetCancelsReactionAndRefreshingVisibleHostPreservesIt()
        {
            portrait.React();
            portrait.Advance(.4f);
            float displacement = HeadDisplacement("Physical Rest");
            portrait.SetVisible(true);
            Assert.That(portrait.IsReacting, Is.True);
            Assert.That(HeadDisplacement("Physical Rest"), Is.EqualTo(displacement));
            portrait.SetVisible(false);
            portrait.React();
            portrait.Advance(100f);
            Assert.That(portrait.IsReacting, Is.False);
            Assert.That(portrait.State, Is.EqualTo(SpiritPresentationState.Idle));
            portrait.SetVisible(true);
            portrait.React();
            Assert.That(portrait.IsReacting, Is.True);
        }

        [TestCase(-1f, 0f)]
        [TestCase(2f, 1f)]
        [TestCase(float.NaN, 0f)]
        public void JourneyProgressCannotProduceInvalidPoses(float progress, float expected)
        {
            portrait.SetState(SpiritPresentationState.Arrival, progress);
            Assert.That(portrait.Progress, Is.EqualTo(expected));
            Assert.That(float.IsNaN(Contact("Physical Rest").x), Is.False);
            Assert.That(float.IsNaN(Contact("Physical Rest").y), Is.False);
        }

        private SpiritPortraitImage Picture(string name) => portrait.GetLayer(name);

        private void AssertArtwork(string name)
            => Assert.That(Picture(name).texture, Is.SameAs(presentation.Layers.Single(layer => layer.Id == name).Texture));

        private float HeadDisplacement(string name)
        {
            var picture = Picture(name);
            return picture.EvaluateDisplacement(new Vector2(.53f, .85f), picture.rectTransform.rect.size).magnitude;
        }

        private void AssertSpiritLeads()
        {
            Assert.That(HeadDisplacement("Spirit Rest"), Is.GreaterThan(.5f));
            Assert.That(HeadDisplacement("Spirit Rest"), Is.GreaterThan(HeadDisplacement("Physical Rest") * 3f));
        }

        private float VisibleHeight()
        {
            var rect = Picture("Physical Rest").rectTransform;
            var parent = host.GetComponent<RectTransform>();
            Vector3 feet = parent.InverseTransformPoint(rect.TransformPoint(Vector3.zero));
            Vector3 crown = parent.InverseTransformPoint(rect.TransformPoint(new Vector3(0f, rect.rect.yMax, 0f)));
            return (crown.y - feet.y) / parent.rect.height;
        }

        private Vector2 Contact(string name)
        {
            var picture = Picture(name);
            var rect = picture.rectTransform;
            Vector2 localContact = picture.EvaluateDisplacement(rect.pivot, rect.rect.size);
            Vector3 contact = host.transform.InverseTransformPoint(rect.TransformPoint(localContact));
            var parent = host.GetComponent<RectTransform>();
            return new Vector2((contact.x - parent.rect.xMin) / parent.rect.width,
                (contact.y - parent.rect.yMin) / parent.rect.height);
        }

        private Vector2 ArtworkPoint(string name, Vector2 uv)
        {
            var picture = Picture(name);
            var rect = picture.rectTransform;
            Vector2 point = Vector2.Scale(uv - rect.pivot, rect.rect.size)
                + picture.EvaluateDisplacement(uv, rect.rect.size);
            return host.transform.InverseTransformPoint(rect.TransformPoint(point));
        }

        private void AssertContact(string name, Vector2 position)
            => Assert.That(Vector2.Distance(Contact(name), position), Is.LessThan(.00001f), name);

        private void AssertContacts(Vector2 position)
        {
            foreach (string name in new[] { "Physical Rest", "Physical Arrival", "Physical Settle" })
            {
                if (Picture(name).color.a <= .001f) continue;
                AssertContact(name, position);
                Assert.That(Picture(name).rectTransform.localRotation, Is.EqualTo(Quaternion.identity));
            }
        }
    }
}
