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
