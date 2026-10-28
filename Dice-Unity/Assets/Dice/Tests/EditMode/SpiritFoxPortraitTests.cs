using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    [Explicit("Art-generation check; run it by name while the fox art is being changed.")]
    [Category("ArtAuthoring")]
    public sealed class SpiritFoxPortraitTests
    {
        private GameObject host;
        private SpiritHostPortrait portrait;
        private RectTransform portraitRoot;
        private RectTransform physicalHead;
        private RectTransform spiritHead;
        private RectTransform body;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Spirit Fox Test", typeof(RectTransform));
            var parent = host.GetComponent<RectTransform>();
            parent.sizeDelta = new Vector2(540f, 960f);
            var presentation = AssetDatabase.LoadAssetAtPath<SpiritHostPresentationAsset>(
                "Assets/Dice/Content/Presentation/SpiritHouse/LanternKeeperHost.asset");
            Assert.That(presentation, Is.Not.Null);
            portrait = Object.Instantiate(presentation.PortraitPrefab, parent, false);
            portrait.Build(presentation);
            portraitRoot = portrait.GetLayer("Body").rectTransform.parent as RectTransform;
            physicalHead = portrait.GetLayer("Physical Front").rectTransform;
            spiritHead = portrait.GetLayer("Cyan Front").rectTransform;
            body = portrait.GetLayer("Body").rectTransform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        [Test]
        public void ScoredReactionMovesSpiritFirstThenPhysicalHeadCatchesUp()
        {
            Vector2 initialPhysical = physicalHead.anchoredPosition;
            Vector2 initialSpirit = spiritHead.anchoredPosition;
            portrait.React();
            portrait.Advance(0.1f);

            float earlyPhysicalTravel = Vector2.Distance(physicalHead.anchoredPosition, initialPhysical);
            float earlySpiritTravel = Vector2.Distance(spiritHead.anchoredPosition, initialSpirit);
            Assert.That(portrait.IsReacting, Is.True);
            Assert.That(earlySpiritTravel, Is.GreaterThan(earlyPhysicalTravel), "The spirit head moves first.");

            portrait.Advance(0.3f);
            Assert.That(Vector2.Distance(physicalHead.anchoredPosition, initialPhysical),
                Is.GreaterThan(earlyPhysicalTravel), "The physical head catches up.");
            Assert.That(portrait.IsReacting, Is.True);
        }

        [Test]
        public void ScoredReactionCompletesAndHeadsReturnToSharedIdlePose()
        {
            portrait.React();
            portrait.Advance(portrait.GestureDuration * 0.5f);
            Assert.That(portrait.IsReacting, Is.True);

            portrait.Advance(portrait.GestureDuration * 0.5f + 0.01f);
            Assert.That(portrait.IsReacting, Is.False);
            Assert.That(Vector2.Distance(physicalHead.anchoredPosition, spiritHead.anchoredPosition),
                Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(physicalHead.localRotation, spiritHead.localRotation),
                Is.LessThan(0.001f));
        }

        [TestCase(SpiritPresentationState.Victory)]
        [TestCase(SpiritPresentationState.Defeat)]
        [TestCase(SpiritPresentationState.Farewell)]
        public void OutcomeSpiritLeadsPhysicalHeadByTheAuthoredDelay(SpiritPresentationState state)
        {
            float duration = portrait.GetDuration(state);
            Assert.That(duration, Is.GreaterThan(0f), state + " requires an authored performance.");
            Vector2 initialPhysical = physicalHead.anchoredPosition;
            Vector2 initialSpirit = spiritHead.anchoredPosition;
            portrait.SetState(state, .1f / duration);
            float physicalTravel = Vector2.Distance(initialPhysical, physicalHead.anchoredPosition);
            float spectralTravel = Vector2.Distance(initialSpirit, spiritHead.anchoredPosition);
            Assert.That(spectralTravel, Is.GreaterThan(physicalTravel), "The spirit head leads.");
            Assert.That(portrait.GetLayer("Cyan Front").color.a, Is.GreaterThan(0f), "The leading spirit is visible.");
            Assert.That(portrait.GetLayer("Gold Front").color.a, Is.GreaterThan(0f), "The leading spirit is visible.");

            portrait.SetState(state, .4f / duration);
            Assert.That(Vector2.Distance(initialPhysical, physicalHead.anchoredPosition), Is.GreaterThan(physicalTravel),
                "The physical head follows after the delay.");
            Assert.That(portrait.State, Is.EqualTo(state));
        }

        [TestCase(SpiritPresentationState.Victory)]
        [TestCase(SpiritPresentationState.Defeat)]
        [TestCase(SpiritPresentationState.Farewell)]
        public void OutcomeReturnsToIdlePoseAndHoldsWhenAdvanced(SpiritPresentationState state)
        {
            portrait.Sample(portrait.GetDuration(state));
            Vector2 expectedPhysical = physicalHead.anchoredPosition;
            Quaternion expectedRotation = physicalHead.localRotation;
            Vector2 expectedBody = body.anchoredPosition;
            float expectedBreath = portrait.GetLayer("Body").Weights[0];

            portrait.SetState(state, .5f);
            Assert.That(Vector2.Distance(expectedPhysical, physicalHead.anchoredPosition), Is.GreaterThan(0f),
                "The outcome moves the head away from its idle pose.");
            Vector2 midpoint = physicalHead.anchoredPosition;
            portrait.Advance(10f);
            Assert.That(physicalHead.anchoredPosition, Is.EqualTo(midpoint));
            portrait.SetState(state, 1f);
            portrait.Advance(50f);

            Assert.That(portrait.State, Is.EqualTo(state));
            Assert.That(portrait.Progress, Is.EqualTo(1f));
            Assert.That(Vector2.Distance(physicalHead.anchoredPosition, expectedPhysical), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(physicalHead.localRotation, expectedRotation), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(physicalHead.anchoredPosition, spiritHead.anchoredPosition), Is.LessThan(.001f));
            Assert.That(portrait.GetLayer("Cyan Front").color.a, Is.Zero.Within(.0001f));
            Assert.That(portrait.GetLayer("Gold Front").color.a, Is.Zero.Within(.0001f));
            Assert.That(portrait.GetLayer("Physical Turn").color.a, Is.Zero.Within(.0001f));
            Assert.That(body.anchoredPosition, Is.EqualTo(expectedBody));
            Assert.That(portrait.GetLayer("Body").Weights[0], Is.EqualTo(expectedBreath).Within(.0001f));
        }

        [Test]
        public void VictoryNodsAndDefeatUsesAGentlerTilt()
        {
            portrait.SetState(SpiritPresentationState.Victory, .5f);
            float victoryY = physicalHead.anchoredPosition.y;
            Assert.That(Mathf.DeltaAngle(0f, physicalHead.localEulerAngles.z), Is.LessThan(0f), "Victory nods one way.");
            portrait.ResetPose();
            portrait.SetState(SpiritPresentationState.Defeat, .5f);
            Assert.That(Mathf.DeltaAngle(0f, physicalHead.localEulerAngles.z), Is.GreaterThan(0f), "Defeat tilts the other way.");
            Assert.That(physicalHead.anchoredPosition.y, Is.GreaterThan(victoryY), "The victory nod lowers the head below the defeat tilt.");
        }

        [Test]
        public void ScoredReactionOverridesAnOngoingIdleGesture()
        {
            portrait.Advance(9.3f);
            Assert.That(portrait.IsReacting, Is.False);

            portrait.React();
            portrait.Advance(0.1f);

            Assert.That(portrait.IsReacting, Is.True);
            Assert.That(Vector2.Distance(physicalHead.anchoredPosition, spiritHead.anchoredPosition),
                Is.GreaterThan(0f), "The reaction separates the spirit head from the physical head.");
        }

        [Test]
        public void BodyRootStaysPlantedThroughIdleAndScoredReaction()
        {
            Vector2 position = body.anchoredPosition;
            Quaternion rotation = body.localRotation;
            Vector3 scale = body.localScale;
            portrait.Advance(9.3f);
            AssertBodyPose(position, rotation, scale);

            portrait.React();
            foreach (float step in new[] { 0.1f, 0.3f, 0.8f, 0.4f, 0.3f })
            {
                portrait.Advance(step);
                AssertBodyPose(position, rotation, scale);
            }
        }

        [TestCase(540f, 960f)]
        [TestCase(443f, 900f)]
        [TestCase(960f, 540f)]
        public void TailsAndHeadsStayRegisteredWithTheirBodyAcrossPortraitSizes(float width, float height)
        {
            var parent = host.GetComponent<RectTransform>();
            parent.sizeDelta = new Vector2(width, height);
            portrait.Advance(0f);

            foreach (string objectName in new[] { "Cyan Tail", "Gold Tail" })
            {
                var tail = (RectTransform)portraitRoot.Find(objectName);
                Assert.That(tail.anchoredPosition, Is.EqualTo(body.anchoredPosition));
                Assert.That(tail.sizeDelta, Is.EqualTo(body.sizeDelta));
                Assert.That(tail.localScale, Is.EqualTo(body.localScale));
            }
            Assert.That(physicalHead.anchoredPosition, Is.EqualTo(spiritHead.anchoredPosition));
            Assert.That(physicalHead.sizeDelta, Is.EqualTo(spiritHead.sizeDelta));
            Assert.That(physicalHead.localScale, Is.EqualTo(spiritHead.localScale));
        }

        private void AssertBodyPose(Vector2 position, Quaternion rotation, Vector3 scale)
        {
            Assert.That(body.anchoredPosition, Is.EqualTo(position));
            Assert.That(body.localRotation, Is.EqualTo(rotation));
            Assert.That(body.localScale, Is.EqualTo(scale));
        }
    }
}
