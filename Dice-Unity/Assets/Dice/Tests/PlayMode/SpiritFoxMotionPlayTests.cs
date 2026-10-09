using System;
using System.Collections;
using System.IO;
using Dice.App;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dice.Tests
{
    public sealed class SpiritFoxMotionPlayTests
    {
        [UnityTest]
        public IEnumerator IdleAndSpiritReactionRenderWithoutChangingEncounterState()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var fox = controller.GetComponentInChildren<SpiritFoxPortrait>();
            Assert.That(fox, Is.Not.Null);
            var snapshot = controller.Snapshot;
            fox.enabled = false;
            fox.ResetPose();
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_MOTION_ROOT");
            if (!string.IsNullOrEmpty(root))
            {
                Assert.That(Directory.Exists(root), Is.False, "Motion captures require a fresh directory.");
                Directory.CreateDirectory(root);
            }

            const int framesPerSecond = 24;
            int frame = 0;
            yield return CaptureMotion(fox, 12f, framesPerSecond, root, frame);
            frame += framesPerSecond * 12;
            Assert.That(fox.IsReacting, Is.False, "Idle gestures must remain separate from scored reactions.");
            fox.React();
            Assert.That(fox.IsReacting, Is.True);
            yield return CaptureMotion(fox, 2.25f, framesPerSecond, root, frame);
            Assert.That(fox.IsReacting, Is.False);
            Assert.That(controller.Snapshot, Is.SameAs(snapshot), "Host animation must not mutate the encounter.");

            var parts = fox.GetComponentsInChildren<SpiritPortraitImage>();
            Assert.That(parts.Length, Is.GreaterThan(2));
            foreach (var part in parts)
            {
                Assert.That(part.raycastTarget, Is.False, part.name);
                var mesh = part.canvasRenderer.GetMesh();
                Assert.That(mesh, Is.Not.Null, part.name);
                Assert.That(mesh.vertexCount, Is.GreaterThan(4), part.name + " has no deformable mesh.");
            }
            fox.enabled = true;
        }

        private static IEnumerator CaptureMotion(SpiritFoxPortrait fox, float seconds, int framesPerSecond,
            string root, int firstFrame)
        {
            int count = Mathf.RoundToInt(seconds * framesPerSecond);
            for (int frame = 0; frame < count; ++frame)
            {
                fox.Advance(1f / framesPerSecond);
                Canvas.ForceUpdateCanvases();
                if (string.IsNullOrEmpty(root))
                {
                    yield return null;
                    continue;
                }
                yield return new WaitForEndOfFrame();
                var pixels = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    string path = Path.Combine(root, (firstFrame + frame).ToString("D4") + ".jpg");
                    File.WriteAllBytes(path, pixels.EncodeToJPG(90));
                }
                finally { UnityEngine.Object.Destroy(pixels); }
            }
        }
    }
}
