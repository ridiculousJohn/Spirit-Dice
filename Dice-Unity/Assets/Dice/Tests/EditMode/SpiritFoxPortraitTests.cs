using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
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
            portrait = host.AddComponent<SpiritHostPortrait>();
            var presentation = AssetDatabase.LoadAssetAtPath<SpiritHostPresentationAsset>(
                "Assets/Dice/Content/Presentation/SpiritHouse/LanternKeeperHost.asset");
            Assert.That(presentation, Is.Not.Null);
            portrait.Build(parent, presentation);
            portraitRoot = (RectTransform)parent.Find("Spirit Portrait");
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
            Assert.That(earlySpiritTravel, Is.GreaterThan(earlyPhysicalTravel * 3f));

            portrait.Advance(0.3f);
            Assert.That(Vector2.Distance(physicalHead.anchoredPosition, initialPhysical),
                Is.GreaterThan(earlyPhysicalTravel * 3f));
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

        [TestCase(SpiritPresentationState.Victory, 1.4f)]
        [TestCase(SpiritPresentationState.Defeat, 1.2f)]
        [TestCase(SpiritPresentationState.Farewell, .5f)]
        public void OutcomeSpiritLeadsPhysicalHeadByTheAuthoredDelay(SpiritPresentationState state, float duration)
        {
            Vector2 initialPhysical = physicalHead.anchoredPosition;
            Vector2 initialSpirit = spiritHead.anchoredPosition;
            portrait.SetState(state, .1f / duration);
            float physicalTravel = Vector2.Distance(initialPhysical, physicalHead.anchoredPosition);
            float spectralTravel = Vector2.Distance(initialSpirit, spiritHead.anchoredPosition);
            Assert.That(portrait.GetDuration(state), Is.EqualTo(duration));
            Assert.That(spectralTravel, Is.GreaterThan(physicalTravel + .1f));
            Assert.That(portrait.GetLayer("Cyan Front").color.a, Is.GreaterThan(.01f));
            Assert.That(portrait.GetLayer("Gold Front").color.a, Is.GreaterThan(.005f));

            portrait.SetState(state, .4f / duration);
            Assert.That(Vector2.Distance(initialPhysical, physicalHead.anchoredPosition), Is.GreaterThan(.3f));
            Assert.That(portrait.State, Is.EqualTo(state));
        }

        [TestCase(SpiritPresentationState.Victory)]
        [TestCase(SpiritPresentationState.Defeat)]
        [TestCase(SpiritPresentationState.Farewell)]
        public void OutcomeEntryPreservesAnInterruptedScoredReaction(SpiritPresentationState state)
        {
            portrait.React();
            portrait.Advance(.4f);
            Vector2 physicalPose = physicalHead.anchoredPosition;
            Vector2 spectralPose = spiritHead.anchoredPosition;
            Quaternion physicalRotation = physicalHead.localRotation;
            float alpha = portrait.GetLayer("Cyan Front").color.a;
            float tail = portrait.GetLayer("Body").Weights[1];
            float breath = portrait.GetLayer("Body").Weights[0];

            portrait.SetState(state, 0f);

            Assert.That(Vector2.Distance(physicalHead.anchoredPosition, physicalPose), Is.LessThan(.0001f));
            Assert.That(Vector2.Distance(spiritHead.anchoredPosition, spectralPose), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(physicalHead.localRotation, physicalRotation), Is.LessThan(.001f));
            Assert.That(portrait.GetLayer("Cyan Front").color.a, Is.EqualTo(alpha).Within(.0001f));
            Assert.That(portrait.GetLayer("Body").Weights[1], Is.EqualTo(tail).Within(.0001f));
            Assert.That(portrait.GetLayer("Body").Weights[0], Is.EqualTo(breath).Within(.0001f));
            Assert.That(portrait.IsReacting, Is.False);
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
            Assert.That(Vector2.Distance(expectedPhysical, physicalHead.anchoredPosition), Is.GreaterThan(.2f));
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
            Assert.That(Mathf.DeltaAngle(0f, physicalHead.localEulerAngles.z), Is.LessThan(-1f));
            portrait.ResetPose();
            portrait.SetState(SpiritPresentationState.Defeat, .5f);
            Assert.That(Mathf.DeltaAngle(0f, physicalHead.localEulerAngles.z), Is.GreaterThan(2f));
            Assert.That(physicalHead.anchoredPosition.y, Is.GreaterThan(victoryY + 2f));
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
                Is.GreaterThan(1f));
        }

        [Test]
        public void AnotherScoredHandRestartsTheReactionDuration()
        {
            portrait.React();
            portrait.Advance(portrait.GestureDuration * 0.8f);
            portrait.React();
            portrait.Advance(portrait.GestureDuration * 0.4f);

            Assert.That(portrait.IsReacting, Is.True);
            portrait.Advance(portrait.GestureDuration * 0.6f + 0.01f);
            Assert.That(portrait.IsReacting, Is.False);
        }

        [Test]
        public void RefreshingVisiblePortraitPreservesTheScoredReaction()
        {
            portrait.React();
            portrait.Advance(0.4f);
            Vector2 physicalPose = physicalHead.anchoredPosition;
            Vector2 spiritPose = spiritHead.anchoredPosition;

            portrait.SetVisible(true);

            Assert.That(portrait.IsReacting, Is.True);
            Assert.That(physicalHead.anchoredPosition, Is.EqualTo(physicalPose));
            Assert.That(spiritHead.anchoredPosition, Is.EqualTo(spiritPose));
        }

        [Test]
        public void ResetPoseClearsAnActiveReactionAndRestoresRestPose()
        {
            Vector2 initialPhysical = physicalHead.anchoredPosition;
            Vector2 initialSpirit = spiritHead.anchoredPosition;
            Quaternion initialRotation = physicalHead.localRotation;
            portrait.React();
            portrait.Advance(0.4f);

            portrait.ResetPose();

            Assert.That(portrait.IsReacting, Is.False);
            Assert.That(portraitRoot.gameObject.activeSelf, Is.True);
            Assert.That(physicalHead.anchoredPosition, Is.EqualTo(initialPhysical));
            Assert.That(spiritHead.anchoredPosition, Is.EqualTo(initialSpirit));
            Assert.That(physicalHead.localRotation, Is.EqualTo(initialRotation));
            Assert.That(spiritHead.localRotation, Is.EqualTo(initialRotation));
        }

        [Test]
        public void HiddenResetClearsAnimationAndRemainsStillUntilShown()
        {
            Vector2 initialPhysical = physicalHead.anchoredPosition;
            Vector2 initialSpirit = spiritHead.anchoredPosition;
            Quaternion initialRotation = physicalHead.localRotation;
            portrait.React();
            portrait.Advance(0.4f);
            portrait.SetVisible(false);
            portrait.ResetPose();

            Assert.That(portrait.IsReacting, Is.False);
            Assert.That(portraitRoot.gameObject.activeSelf, Is.False);
            Assert.That(physicalHead.anchoredPosition, Is.EqualTo(initialPhysical));
            Assert.That(spiritHead.anchoredPosition, Is.EqualTo(initialSpirit));
            Assert.That(physicalHead.localRotation, Is.EqualTo(initialRotation));

            portrait.React();
            portrait.Advance(100f);
            Assert.That(portrait.IsReacting, Is.False);
            Assert.That(physicalHead.anchoredPosition, Is.EqualTo(initialPhysical));
            Assert.That(spiritHead.anchoredPosition, Is.EqualTo(initialSpirit));

            portrait.SetVisible(true);
            portrait.React();
            portrait.Advance(0.1f);
            Assert.That(portrait.IsReacting, Is.True);
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
        public void PortraitScalesEveryLayerAroundTheRoomTableContact(float width, float height)
        {
            var parent = host.GetComponent<RectTransform>();
            parent.sizeDelta = new Vector2(width, height);
            portrait.Advance(0f);

            Assert.That(portraitRoot.localScale.x / (width / 540f), Is.EqualTo(1.16f).Within(0.0001f));
            Assert.That(portraitRoot.localScale.y / (height / 960f), Is.EqualTo(1.16f).Within(0.0001f));
            Vector3 contact = parent.InverseTransformPoint(portraitRoot.TransformPoint(new Vector3(270f, 624f, 0f)));
            Assert.That(contact.x, Is.EqualTo(parent.rect.xMin + width * .5f).Within(0.001f));
            Assert.That(contact.y, Is.EqualTo(parent.rect.yMin + height * .62f).Within(0.001f));

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
