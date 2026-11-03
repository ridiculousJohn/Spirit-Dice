using System;
using System.Collections;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dice.Tests
{
    public sealed class SpiritDiceTrayTests
    {
        private Scene preview;
        private TestTrayRig rig;
        private SpiritDiceTray tray;

        [SetUp]
        public void SetUp()
        {
            preview = EditorSceneManager.NewPreviewScene();
            rig = new TestTrayRig(preview);
            tray = rig.StartDice();
        }

        [TearDown]
        public void TearDown()
        {
            rig.Dispose();
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }

        /** Checks each requested face is the one turned up in every slot / fails if a face's rest rotation shows another face. */
        [Test]
        public void SetValuesTurnsEachRequestedFaceUp()
        {
            for (int face = 1; face <= 6; face++)
            {
                tray.SetValues(new DiceValues(face, face, face, face, face));
                for (int index = 0; index < DiceValues.Count; index++)
                    Assert.That(tray.GetVisibleFace(index), Is.EqualTo(face), "Face " + face + ", slot " + index);
            }
        }

        /** Checks a roll lands the masked dice on their new faces and leaves the others / fails if an unmasked die changes or a masked one keeps its face. */
        [Test]
        public void RollChangesOnlyTheMaskedDice()
        {
            tray.SetValues(new DiceValues(1, 2, 3, 4, 5));
            IEnumerator animation = tray.Roll(new DiceValues(6, 6, 6, 6, 6), (1 << 1) | (1 << 3), 0f);
            Assert.That(animation.MoveNext(), Is.False);
            Assert.That(tray.IsRolling, Is.False);
            AssertFaces(1, 6, 3, 6, 5);
        }

        /** Checks highlights show the union of the selection and scoring masks, limited to the dice / fails if either mask hides the other's highlights or bits beyond the dice are kept. */
        [Test]
        public void HighlightsFollowTheSelectionAndScoringMasks()
        {
            tray.SetSelection(1 << 0);
            tray.SetScoringHighlight((1 << 1) | (1 << 3));
            AssertHighlights((1 << 0) | (1 << 1) | (1 << 3));
            tray.SetScoringHighlight(0);
            AssertHighlights(1 << 0);
            tray.SetSelection(0);
            AssertHighlights(0);
            tray.SetScoringHighlight(-1);
            Assert.That(tray.ScoringHighlightMask, Is.EqualTo(DiceValues.AllMask));
            AssertHighlights(DiceValues.AllMask);
        }

        /** Checks release unbinds the tray and clears every highlight it set / fails if highlights or the shared texture survive release. */
        [Test]
        public void ReleaseClearsHighlights()
        {
            tray.SetSelection(1 << 2);
            tray.SetScoringHighlight(DiceValues.AllMask);
            tray.ReleasePresentation();
            Assert.That(tray.ScoringHighlightMask, Is.Zero);
            Assert.That(tray.Texture, Is.Null);
            AssertHighlights(0);
        }

        /** Checks ending a roll early settles its dice on their results while it blocks other changes during the roll / fails if an interrupted roll leaves dice mid-air or moves unmasked dice. */
        [Test]
        public void InterruptedRollSettlesItsDiceOnTheirResults()
        {
            tray.SetValues(new DiceValues(1, 2, 3, 4, 5));
            IEnumerator animation = tray.Roll(new DiceValues(6, 5, 4, 3, 2), 1 << 2);
            Assert.That(animation.MoveNext(), Is.True);
            Assert.That(tray.IsRolling, Is.True);
            ((IDisposable)animation).Dispose();
            Assert.That(tray.IsRolling, Is.False);
            AssertFaces(1, 2, 4, 4, 5);
        }

        /** Checks a flip and a single-die roll move only their target die / fails if either turns another die. */
        [Test]
        public void FlipAndSingleDieRollMoveOnlyTheirTarget()
        {
            tray.SetValues(new DiceValues(1, 2, 3, 4, 5));
            IEnumerator flip = tray.Flip(1 << 1, AllShowing(5), 0f);
            Assert.That(flip.MoveNext(), Is.False);
            IEnumerator roll = tray.Roll(AllShowing(6), 1 << 3, LandedTiming());
            Assert.That(roll.MoveNext(), Is.False);
            Assert.That(tray.IsRolling, Is.False);
            AssertFaces(1, 5, 3, 6, 5);
        }

        /** Checks one flip turns every masked die to its own face, neighbouring and opposite alike, and leaves the rest / fails if a masked die misses its face or an unmasked die moves. */
        [Test]
        public void AMultiDieFlipLandsEveryMaskedDieOnItsFace()
        {
            tray.SetValues(new DiceValues(1, 2, 3, 4, 5));
            // Bay 0 turns to its opposite face, bays 2 and 4 to neighbouring faces.
            IEnumerator flip = tray.Flip((1 << 0) | (1 << 2) | (1 << 4), new DiceValues(6, 1, 2, 1, 1), 0f);
            Assert.That(flip.MoveNext(), Is.False);
            Assert.That(tray.IsRolling, Is.False);
            AssertFaces(6, 2, 2, 4, 1);
        }

        /** Checks a roll on one die runs while another die is mid-flip and each lands on its own face / fails if the second animation is refused or either die misses its face. */
        [Test]
        public void AnimationsOnDifferentDiceRunTogetherAndEachLands()
        {
            tray.SetValues(new DiceValues(1, 2, 3, 4, 5));
            IEnumerator flip = tray.Flip(1 << 1, AllShowing(5), 10f);
            Assert.That(flip.MoveNext(), Is.True);
            IEnumerator roll = tray.Roll(AllShowing(6), (1 << 3) | (1 << 4), LandedTiming());
            Assert.That(roll.MoveNext(), Is.False, "The roll lands while the flip still runs.");
            Assert.That(tray.IsRolling, Is.True, "The flipping die still moves.");
            AssertFaces(1, 2, 3, 6, 6);
            ((IDisposable)flip).Dispose();
            Assert.That(tray.IsRolling, Is.False);
            AssertFaces(1, 5, 3, 6, 6);
        }

        /** Checks a roll or flip that takes a die another animation is moving throws, claims nothing and leaves the running animation to finish / fails if two animations share a die or the refused one holds a die. */
        [Test]
        public void AnAnimationOnAMovingDieThrows()
        {
            tray.SetValues(new DiceValues(1, 2, 3, 4, 5));
            IEnumerator flip = tray.Flip(1 << 2, AllShowing(4), 10f);
            Assert.That(flip.MoveNext(), Is.True);
            Assert.Throws<InvalidOperationException>(() => tray.Roll(AllShowing(6), (1 << 2) | (1 << 3)).MoveNext());
            Assert.Throws<InvalidOperationException>(() => tray.Flip(1 << 2, AllShowing(1), 10f).MoveNext());
            ((IDisposable)flip).Dispose();
            Assert.That(tray.IsRolling, Is.False, "The refused animations hold no die.");
            AssertFaces(1, 2, 4, 4, 5);
        }

        /** Checks RollEnded fires once, when the last of two overlapping animations ends / fails if it fires while a die still moves or more than once. */
        [Test]
        public void RollEndedFiresOnceWhenTheLastAnimationEnds()
        {
            int ended = 0;
            tray.RollEnded += () => ++ended;
            tray.SetValues(new DiceValues(1, 2, 3, 4, 5));
            IEnumerator first = tray.Flip(1 << 0, AllShowing(6), 10f);
            IEnumerator second = tray.Roll(AllShowing(6), 1 << 1, 10f);
            Assert.That(first.MoveNext(), Is.True);
            Assert.That(second.MoveNext(), Is.True);
            ((IDisposable)first).Dispose();
            Assert.That(ended, Is.Zero, "Another animation still moves a die.");
            Assert.That(tray.IsRolling, Is.True);
            ((IDisposable)second).Dispose();
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(tray.IsRolling, Is.False);
        }

        /** Checks a running flip rejects new values and settles on its face when interrupted / fails if values can be replaced mid-animation or the interrupted die is left between faces. */
        [Test]
        public void RunningFlipRejectsNewValuesAndSettlesWhenInterrupted()
        {
            tray.SetValues(new DiceValues(1, 2, 3, 4, 5));
            IEnumerator flip = tray.Flip(1 << 4, AllShowing(2), 10f);
            Assert.That(flip.MoveNext(), Is.True);
            Assert.Throws<InvalidOperationException>(() => tray.SetValues(new DiceValues(1, 1, 1, 1, 1)));
            ((IDisposable)flip).Dispose();
            Assert.That(tray.IsRolling, Is.False);
            AssertFaces(1, 2, 3, 4, 2);
        }

        /** Checks slot queries project each die's slot and its authored label anchor / fails if the hit rect misses its slot point or labels ignore a moved anchor. */
        [Test]
        public void SlotQueriesProjectTheDieSlotAndItsLabelAnchor()
        {
            tray.SetValues(new DiceValues(1, 2, 3, 4, 5));
            for (int index = 0; index < DiceValues.Count; ++index)
            {
                Rect slot = tray.GetSlotViewportRect(index);
                Assert.That(slot.width, Is.GreaterThan(0));
                Assert.That(slot.height, Is.GreaterThan(0));
                Assert.That(slot.Contains(tray.GetSlotViewportPosition(index)), Is.True, "Slot " + index);
                Transform anchor = rig.Geometry.DiceLabelAnchors[index];
                anchor.localPosition += new Vector3(.08f, 0, -.1f);
                Vector2 label = rig.Camera.WorldToViewportPoint(anchor.position);
                Assert.That(Vector2.Distance(tray.GetSlotLabelViewportPosition(index), label), Is.LessThan(.0001f), "Label " + index);
            }
        }

        private static DiceValues AllShowing(int face) => new DiceValues(face, face, face, face, face);

        /** A roll timing that started long ago, so the roll lands on its first step. */
        private static SpiritRollTiming LandedTiming() => new SpiritRollTiming(0f, null, 0f, Time.unscaledTimeAsDouble - 10, 0);

        private void AssertFaces(params int[] faces)
        {
            for (int index = 0; index < faces.Length; ++index)
                Assert.That(tray.GetVisibleFace(index), Is.EqualTo(faces[index]), "Slot " + index);
        }

        private void AssertHighlights(int mask)
        {
            for (int index = 0; index < DiceValues.Count; ++index)
            {
                bool highlighted = (mask & (1 << index)) != 0;
                Assert.That(rig.Outlines[index].activeSelf, Is.EqualTo(highlighted), "Outline " + index);
                Assert.That(rig.Frames[index].activeSelf, Is.EqualTo(highlighted), "Frame " + index);
            }
        }
    }
}
