using System.Linq;
using System.Reflection;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    [Explicit("Art-generation check; run it by name while the owl art is being changed.")]
    [Category("ArtAuthoring")]
    public sealed class SpiritOwlPortraitTests
    {
        private GameObject host;
        private SpiritHostPortrait portrait;
        private SpiritHostPresentationAsset presentation;
        // Normalized room positions of the physical feet at rest on the table and while waiting on the perch.
        private Vector2 table, perch;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Spirit Owl Test", typeof(RectTransform));
            var parent = host.GetComponent<RectTransform>();
            parent.sizeDelta = new Vector2(540f, 960f);
            presentation = AssetDatabase.LoadAssetAtPath<SpiritHostPresentationAsset>(
                "Assets/Dice/Content/Presentation/Watchtower/WatchtowerHost.asset");
            Assert.That(presentation, Is.Not.Null);
            portrait = Object.Instantiate(presentation.PortraitPrefab, parent, false);
            portrait.Build(presentation);
            table = Contact("Physical Rest");
            portrait.SetState(SpiritPresentationState.Waiting, 0f);
            perch = Contact("Physical Rest");
            portrait.ResetPose();
            Assert.That(Vector2.Distance(perch, table), Is.GreaterThan(.01f), "The watcher waits away from the table.");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        [Test]
        public void ArrivalUsesSeparateFlightAndLandingArtworkBeforeFoldingAtTheTable()
        {
            portrait.SetState(SpiritPresentationState.Waiting, 0f);
            Assert.That(Picture("Physical Rest").color.a, Is.EqualTo(1f));
            AssertArtwork("Physical Rest");

            portrait.SetState(SpiritPresentationState.Arrival, .45f);
            Assert.That(Picture("Physical Arrival").color.a, Is.EqualTo(1f));
            AssertArtwork("Physical Arrival");
            Assert.That(Contact("Physical Arrival").x, Is.InRange(Mathf.Min(table.x, perch.x), Mathf.Max(table.x, perch.x)),
                "The flight crosses between the perch and the table.");

            portrait.SetState(SpiritPresentationState.Arrival, .81f);
            Assert.That(Picture("Physical Settle").color.a, Is.EqualTo(1f));
            AssertArtwork("Physical Settle");
            AssertContact("Physical Settle", table);

            portrait.SetState(SpiritPresentationState.Arrival, 1f);
            Assert.That(Picture("Physical Rest").color.a, Is.EqualTo(1f));
            Assert.That(Picture("Physical Arrival").color.a, Is.Zero);
            Assert.That(Picture("Physical Settle").color.a, Is.Zero);
            AssertContact("Physical Rest", table);
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
            Assert.That(HeadDisplacement("Physical Rest"), Is.GreaterThan(0f), "The physical head follows the spirit.");
        }

        [TestCase(SpiritPresentationState.Victory)]
        [TestCase(SpiritPresentationState.Defeat)]
        [TestCase(SpiritPresentationState.Farewell)]
        public void OutcomeHeadMotionLeadsWithTheSpiritBeforeThePhysicalAcknowledgment(SpiritPresentationState state)
        {
            float duration = portrait.GetDuration(state);
            Assert.That(duration, Is.GreaterThan(0f), state + " requires an authored performance.");
            portrait.SetState(state, .1f / duration);
            Assert.That(Mathf.Abs(Picture("Spirit Rest").Weights[2]), Is.GreaterThan(0f), "The spirit turns first.");
            Assert.That(Picture("Physical Rest").Weights[2], Is.Zero.Within(.0001f), "The physical head waits for the spirit.");
            Assert.That(Picture("Spirit Rest").color.a, Is.GreaterThan(0f), "The leading spirit is visible.");
            portrait.SetState(state, .4f / duration);
            Assert.That(Mathf.Abs(Picture("Physical Rest").Weights[2]), Is.GreaterThan(.0001f), "The physical head acknowledges.");
            Assert.That(Picture("Physical Rest").color.a, Is.EqualTo(1f));
            Assert.That(Picture("Physical Arrival").color.a, Is.Zero);
            Assert.That(Picture("Physical Settle").color.a, Is.Zero);
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
                    AssertContact(picture.name, table);
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
            AssertContacts(table);
        }

        [Test]
        public void VictoryAcknowledgmentAndQuieterDefeatTiltInOppositeDirections()
        {
            portrait.SetState(SpiritPresentationState.Victory, .5f);
            float victory = Picture("Physical Rest").Weights[2];
            Assert.That(victory, Is.GreaterThan(0f), "Victory tilts one way.");
            portrait.ResetPose();
            portrait.SetState(SpiritPresentationState.Defeat, .5f);
            float defeat = Picture("Physical Rest").Weights[2];
            Assert.That(defeat, Is.LessThan(0f), "Defeat tilts the other way.");
            Assert.That(Mathf.Abs(defeat), Is.LessThan(victory), "Defeat tilts more quietly than the victory acknowledgment.");
        }

        [Test]
        public void LandingAndReactionKeepEveryPhysicalFootContactPlanted()
        {
            foreach (float progress in new[] { .76f, .81f, .9f, .96f, 1f })
            {
                portrait.SetState(SpiritPresentationState.Arrival, progress);
                AssertContacts(table);
            }
            portrait.SetState(SpiritPresentationState.Idle, 1f);
            portrait.Advance(3.8f);
            AssertContacts(table);
            portrait.React();
            foreach (float step in new[] { .1f, .3f, .7f, .8f })
            {
                portrait.Advance(step);
                AssertContacts(table);
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
        public void FoldingKeepsTheChestRegisteredAndRecoversFromAGroundedSettle()
        {
            portrait.SetState(SpiritPresentationState.Arrival, .90f);
            Assert.That(Vector2.Distance(ArtworkPoint("Physical Settle", new Vector2(.51f, .43f)),
                ArtworkPoint("Physical Rest", new Vector2(.5f, .52f))), Is.LessThan(5f));
            portrait.SetState(SpiritPresentationState.Arrival, .76f);
            float landingHead = ArtworkPoint("Physical Settle", new Vector2(.51f, .72f)).y;
            portrait.SetState(SpiritPresentationState.Arrival, .79f);
            float compressedHead = ArtworkPoint("Physical Settle", new Vector2(.51f, .72f)).y;
            Assert.That(compressedHead, Is.LessThan(landingHead), "The landing settles the head down.");
            AssertContacts(table);
            portrait.SetState(SpiritPresentationState.Arrival, 1f);
            Assert.That(ArtworkPoint("Physical Rest", new Vector2(.5f, .80f)).y, Is.GreaterThan(compressedHead),
                "The folded watcher recovers from the settle.");
        }

        [Test]
        public void WatcherAppearsSmallerOnTheDistantPerchThanAtTheTable()
        {
            portrait.SetState(SpiritPresentationState.Waiting, 0f);
            float perchHeight = VisibleHeight();
            portrait.SetState(SpiritPresentationState.Idle, 1f);
            Assert.That(perchHeight, Is.GreaterThan(0f));
            Assert.That(perchHeight, Is.LessThan(VisibleHeight()), "The distant perch shows the watcher smaller than at the table.");
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

        private SpiritPortraitImage Picture(string name) => portrait.GetLayer(name);

        private void AssertArtwork(string name)
            => Assert.That(Picture(name).texture, Is.SameAs(presentation.Layers.Single(layer => layer.Id == name).Texture));

        private float HeadDisplacement(string name)
        {
            var picture = Picture(name);
            return picture.EvaluateDisplacement(new Vector2(.53f, .85f), picture.rectTransform.rect.size).magnitude;
        }

        private void AssertSpiritLeads()
            => Assert.That(HeadDisplacement("Spirit Rest"), Is.GreaterThan(HeadDisplacement("Physical Rest")),
                "The spirit head turns ahead of the physical head.");

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
