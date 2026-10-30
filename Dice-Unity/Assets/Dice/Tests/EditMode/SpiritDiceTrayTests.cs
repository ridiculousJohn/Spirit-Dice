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
            IEnumerator flip = tray.Flip(1, 5, 0f);
            Assert.That(flip.MoveNext(), Is.False);
            // A timing that started long ago lands the roll on its first step.
            IEnumerator roll = tray.RollDie(3, 6, new SpiritRollTiming(0f, null, 0f, Time.unscaledTimeAsDouble - 10, 0));
            Assert.That(roll.MoveNext(), Is.False);
            Assert.That(tray.IsRolling, Is.False);
            AssertFaces(1, 5, 3, 6, 5);
        }

        /** Checks a running flip rejects new values and settles on its face when interrupted / fails if values can be replaced mid-animation or the interrupted die is left between faces. */
        [Test]
        public void RunningFlipRejectsNewValuesAndSettlesWhenInterrupted()
        {
            tray.SetValues(new DiceValues(1, 2, 3, 4, 5));
            IEnumerator flip = tray.Flip(4, 2, 10f);
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
