using System;
using System.Collections;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class SpiritTrayCompositionPlayTests
    {
        [UnityTest]
        public IEnumerator AllThreeEncounterTablesCaptureGameplayAtBothPortraitWidths()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit(false);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = controller.GetComponent<SpiritEncounterView>();
            Assert.That(controller.JourneyCatalog.Entries.Count(value => !value.IsStoryOnly), Is.EqualTo(3));
            Camera previousCamera = null;
            RenderTexture previousTexture = null;
            foreach (var journey in controller.JourneyCatalog.Entries.Where(value => !value.IsStoryOnly))
            {
                controller.ReturnToInvestigation();
                ContentReady.SeedJourneyPrerequisites(controller, journey.Id);
                controller.SelectEncounter(journey.EncounterId);
                while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                    controller.AdvanceJourney();
                controller.AcceptEncounter();
                float deadline = Time.realtimeSinceStartup + 30;
                while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(controller.IsBusy, Is.False);
                yield return null;
                Assert.That(view.TrayScene.Presentation, Is.SameAs(journey.TablePresentation.TrayPresentation));
                Assert.That(view.DiceTray.RenderCamera, Is.SameAs(view.ModifierHistory.Renderer.RenderCamera));
                Assert.That(view.DiceTray.Texture, Is.SameAs(view.ModifierHistory.Renderer.Texture));
                Assert.That(view.TrayScene.RenderRoot.GetComponentsInChildren<Camera>().Length, Is.EqualTo(1));
                if (previousCamera != null || previousTexture != null)
                    Assert.Fail("Switching tables retained a previous shared camera or texture.");
                var faceNormals = new[] { Vector3.forward, Vector3.up, Vector3.right,
                    Vector3.left, Vector3.down, Vector3.back };
                for (int i = 0; i < DiceValues.Count; ++i)
                {
                    var slot = view.TrayScene.Geometry.DiceSlots[i];
                    var die = slot.Find("Spirit Dice Slot Content " + i + "/Spirit Die " + i);
                    Assert.That(die, Is.Not.Null, "Die " + i);
                    Vector3 resultNormal = die.TransformDirection(faceNormals[controller.Snapshot.CurrentDice[i] - 1]);
                    // The result faces upward; a shallow camera can favor a side face.
                    Assert.That(Vector3.Dot(resultNormal, slot.up), Is.GreaterThan(.999f), "Die " + i + " result face");
                    var camera = view.DiceTray.RenderCamera;
                    Vector3 towardCamera = camera.orthographic ? -camera.transform.forward
                        : (camera.transform.position - die.position).normalized;
                    Assert.That(Vector3.Dot(resultNormal, towardCamera), Is.GreaterThan(0f), "Die " + i + " result visibility");
                }
                var definition = new ModifierDefinition("test.capture.history", "test.capture.history", ModifierPhase.InitialRoll,
                    new AlwaysCondition(), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 1) });
                var encounter = new EncounterDefinition("test.capture.tray", new[] { new StageDefinition(1000000, 3, 3) },
                    startingModifiers: new[] { definition.Id });
                var catalog = new MechanicalCatalog("test.capture.tray", controller.Catalog.Hands,
                    new[] { encounter }, new[] { definition });
                var contribution = EncounterSession.CreateEncounter(catalog, encounter.Id, 60).Snapshot.ModifierContributions[0];
                view.ModifierHistory.Clear();
                for (int i = 0; i < 6; ++i) view.ModifierHistory.Add(contribution, definition, false);
                foreach (int width in new[] { 540, 443 })
                {
                    var portrait = (RectTransform)view.transform.Find("SpiritCanvas/SafeArea/Portrait");
                    portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                    portrait.sizeDelta = new Vector2(width, 960);
                    portrait.anchoredPosition = Vector2.zero;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    var trayImage = view.GetComponentsInChildren<RawImage>(true).Single(image => image.name == "DiceTrayImage");
                    Assert.That(trayImage.texture, Is.SameAs(view.TrayScene.Texture));
                    Assert.That(view.ModifierHistory.GetComponentsInChildren<RawImage>(true),
                        Is.EquivalentTo(new[] { trayImage }));
                    for (int index = 0; index < DiceValues.Count; ++index)
                    {
                        Rect bounds = view.DiceTray.GetSlotViewportRect(index);
                        var button = view.GetComponentsInChildren<Button>(true).Single(value => value.name == "DieButton" + (index + 1));
                        var hit = (RectTransform)button.transform;
                        Assert.That(Vector2.Distance(hit.anchorMin, bounds.min), Is.LessThan(.001f));
                        Assert.That(Vector2.Distance(hit.anchorMax, bounds.max), Is.LessThan(.001f));
                        Assert.That(hit.rect.width, Is.GreaterThan(0));
                        Assert.That(hit.rect.height, Is.GreaterThan(0));
                    }
                    yield return Capture(view, journey.EncounterId, width);
                }
                previousCamera = view.TrayScene.RenderCamera;
                previousTexture = view.TrayScene.Texture;
            }
            view.gameObject.SetActive(false);
            yield return null;
            Assert.That(view.TrayScene.RenderRoot.gameObject.activeInHierarchy, Is.False);
            view.gameObject.SetActive(true);
            yield return null;
            Assert.That(view.TrayScene.RenderRoot.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.DiceTray.Texture, Is.SameAs(view.ModifierHistory.Renderer.Texture));
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator Capture(SpiritEncounterView view, string encounterId, int width)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) yield break;
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "shared-tray-" + encounterId + "-" + width + "x960.png");
            Assert.That(File.Exists(path), Is.False, "Capture output must be fresh.");
            var canvas = view.transform.Find("SpiritCanvas").GetComponent<Canvas>();
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousPlane = canvas.planeDistance;
            var cameraRoot = new GameObject("Tray Capture Camera");
            var camera = cameraRoot.AddComponent<Camera>();
            var target = new RenderTexture(width, 960, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D readback = null;
            RenderTexture previousTarget = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                target.Create();
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.orthographic = true;
                camera.orthographicSize = 480;
                camera.nearClipPlane = .1f; camera.farClipPlane = 200;
                camera.cullingMask = ~(1 << SpiritDiceTray.RenderLayer);
                camera.backgroundColor = new Color(.04f, .055f, .05f, 1);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.allowHDR = camera.allowMSAA = false;
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                readback = new Texture2D(width, 960, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, width, 960), 0, 0);
                readback.Apply();
                Assert.That(readback.GetPixels32().Count(pixel => pixel.r > 45 || pixel.g > 45 || pixel.b > 45),
                    Is.GreaterThan(width * 960 / 4), "Gameplay artwork must fill the captured portrait.");
                File.WriteAllBytes(path, readback.EncodeToPNG());
                TestContext.WriteLine("Tray composition capture: " + path);
            }
            finally
            {
                canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = previousPlane;
                RenderTexture.active = previousTarget; GL.sRGBWrite = previousSrgb;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(cameraRoot);
                if (readback != null) UnityEngine.Object.Destroy(readback);
            }
        }
    }
}
