using System;
using System.Collections;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class SpiritDiceTrayTests
    {
        private GameObject host;
        private SpiritDiceTray tray;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Spirit Dice Test");
            tray = host.AddComponent<SpiritDiceTray>();
            tray.Configure(AssetDatabase.LoadAssetAtPath<SpiritTrayPresentationAsset>(
                "Assets/Dice/Presentation/Shared/SpiritTrayPresentation.asset"));
            tray.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
        }

        [Test]
        public void EachEngineFaceIsTheDominantCameraFacingFaceInEverySlot()
        {
            for (int face = 1; face <= 6; face++)
            {
                tray.SetValues(new DiceValues(face, face, face, face, face));
                for (int index = 0; index < DiceValues.Count; index++)
                    Assert.That(tray.GetVisibleFace(index), Is.EqualTo(face), "Slot " + index);
            }
        }

        [Test]
        public void RollChangesOnlyRequestedDice()
        {
            tray.SetValues(new DiceValues(1, 2, 3, 4, 5));
            IEnumerator animation = tray.Roll(new DiceValues(6, 6, 6, 6, 6), (1 << 1) | (1 << 3), 0f);
            Assert.That(animation.MoveNext(), Is.False);
            Assert.That(tray.IsRolling, Is.False);
            Assert.That(tray.GetVisibleFace(0), Is.EqualTo(1));
            Assert.That(tray.GetVisibleFace(1), Is.EqualTo(6));
            Assert.That(tray.GetVisibleFace(2), Is.EqualTo(3));
            Assert.That(tray.GetVisibleFace(3), Is.EqualTo(6));
            Assert.That(tray.GetVisibleFace(4), Is.EqualTo(5));
        }

        [Test]
        public void ScoringHighlightsAndRerollSelectionRemainIndependent()
        {
            tray.SetSelection(1 << 0);
            tray.SetScoringHighlight((1 << 1) | (1 << 3));
            AssertHighlightVisuals((1 << 0) | (1 << 1) | (1 << 3));

            tray.SetScoringHighlight(0);
            AssertHighlightVisuals(1 << 0);

            tray.SetScoringHighlight((1 << 1) | (1 << 3));
            tray.SetSelection(0);
            Assert.That(tray.ScoringHighlightMask, Is.EqualTo((1 << 1) | (1 << 3)));
            AssertHighlightVisuals((1 << 1) | (1 << 3));
            tray.SetScoringHighlight(0);
            AssertHighlightVisuals(0);
        }

        [Test]
        public void ScoringHighlightsUseOnlyDiceBitsAndClearOnRelease()
        {
            tray.SetScoringHighlight(-1);
            Assert.That(tray.ScoringHighlightMask, Is.EqualTo(DiceValues.AllMask));
            AssertHighlightVisuals(DiceValues.AllMask);
            tray.SetSelection(1 << 2);
            tray.ReleasePresentation();
            Assert.That(tray.ScoringHighlightMask, Is.Zero);
            tray.Initialize();
            AssertHighlightVisuals(0);
        }

        [Test]
        public void ClearingScoringFeedbackDoesNotRecreateReleasedRenderObjects()
        {
            tray.SetScoringHighlight(DiceValues.AllMask);
            tray.ReleasePresentation();
            tray.SetScoringHighlight(0);
            Assert.That(tray.ScoringHighlightMask, Is.Zero);
            Assert.That(tray.RenderRoot == null, Is.True);
        }

        private void AssertHighlightVisuals(int mask)
        {
            for (int index = 0; index < DiceValues.Count; ++index)
            {
                bool highlighted = (mask & (1 << index)) != 0;
                Transform die = tray.RenderRoot.Find("Spirit Die " + index);
                Assert.That(die.Find("Die " + index + " Selection Outline").gameObject.activeSelf,
                    Is.EqualTo(highlighted), "Outline " + index);
                Assert.That(tray.RenderRoot.Find("Bay " + index + " Selection Frame").gameObject.activeSelf,
                    Is.EqualTo(highlighted), "Frame " + index);
            }
        }

        [Test]
        public void FinishingAnInterruptedAnimationSettlesSelectedDice()
        {
            tray.SetValues(new DiceValues(1, 2, 3, 4, 5));
            IEnumerator animation = tray.Roll(new DiceValues(6, 5, 4, 3, 2), 1 << 2);
            Assert.That(animation.MoveNext(), Is.True);
            Assert.That(tray.IsRolling, Is.True);
            Assert.That(tray.GetVisibleFace(0), Is.EqualTo(1));
            Assert.That(tray.GetVisibleFace(4), Is.EqualTo(5));
            ((IDisposable)animation).Dispose();
            Assert.That(tray.IsRolling, Is.False);
            Assert.That(tray.GetVisibleFace(2), Is.EqualTo(4));
        }
    }
}
