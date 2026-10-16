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
    public sealed class SpiritCollectionPlayTests
    {
        [UnityTest]
        public IEnumerator BoardAndGameplayMenuBrowseProfileCollectionWithoutChangingProgress()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit(false);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = controller.GetComponent<SpiritEncounterView>();
            var collection = view.GetComponent<SpiritCollectionView>();
            Assert.That(controller.ProfileUnlockedModifierIds, Is.Empty);
            var permanent = controller.Catalog.Modifiers.First(value => value.Availability.Access == ModifierAccess.PermanentUnlock &&
                value.RewardEligible && !controller.Catalog.Modifiers.Any(other => other.FamilyId == value.FamilyId &&
                    other.RewardEligible && other.Availability.Access == ModifierAccess.Starter) &&
                controller.Catalog.Encounters.Any(encounter => encounter.UnlockRewardModifierIds.Contains(value.Id)));
            foreach (int width in new[] { 443, 540 })
            {
                Resize(view, width);
                Find<Button>(view, "InvestigationCollectionButton").onClick.Invoke();
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(collection.IsVisible, Is.True);
                Assert.That(collection.Filter, Is.EqualTo(SpiritCollectionFilter.Owned));
                Assert.That(collection.Rows.Select(row => row.FamilyId), Is.EquivalentTo(controller.Catalog.Modifiers
                    .Where(value => value.RewardEligible && value.Availability.Access == ModifierAccess.Starter)
                    .Select(value => value.FamilyId).Distinct()));
                Assert.That(Find<Text>(collection, "CollectionSummary").text, Does.StartWith("Collected 0 / "));
                AssertCompleteText(Find<Text>(collection, "CollectionFilterButtonLabel"));
                yield return Capture(view, "owned-default", width);
                Find<Button>(collection, "CollectionFilterButton").onClick.Invoke();
                Assert.That(collection.Filter, Is.EqualTo(SpiritCollectionFilter.Discovered));
                Find<Button>(collection, "CollectionFilterButton").onClick.Invoke();
                Assert.That(collection.Filter, Is.EqualTo(SpiritCollectionFilter.All));
                Assert.That(collection.Rows.Count, Is.EqualTo(controller.Catalog.Modifiers
                    .Where(value => value.RewardEligible && value.Availability.Access != ModifierAccess.EncounterExclusive)
                    .Select(value => value.FamilyId).Distinct().Count()));
                var row = collection.Rows.Single(value => value.FamilyId == permanent.FamilyId);
                Assert.That(row.IsUndiscovered, Is.True);
                Assert.That(row.GetComponentsInChildren<Button>(), Is.Empty);
                Assert.That(Find<Text>(row, "FamilyCondition").text, Is.EqualTo("Locked"));
                Assert.That(Find<Text>(row, "FamilyName").text, Is.EqualTo("Undiscovered family"));
                Assert.That(collection.SelectedModifierId, Is.Not.EqualTo(permanent.Id));
                ScrollToRow(Find<ScrollRect>(collection, "CollectionFamilies"), (RectTransform)row.transform);
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (var family in collection.Rows)
                    foreach (var text in family.GetComponentsInChildren<Text>()) AssertCompleteText(text);
                AssertCompleteText(Find<Text>(collection, "CollectionSummary"));
                AssertCompleteText(Find<Text>(collection, "CollectionDetailTitle"));
                AssertCompleteText(Find<Text>(collection, "CollectionFilterButtonLabel"));
                yield return Capture(view, "all-undiscovered", width);
                Find<Button>(view, "CloseCollectionButton").onClick.Invoke();
                Assert.That(collection.IsVisible, Is.False);
                Assert.That(controller.Snapshot, Is.Null);
                Assert.That(controller.ProfileUnlockedModifierIds, Is.Empty);
            }

            var profile = new PlayerProfile();
            profile.UnlockModifier(permanent.Id);
            PlayerProfileFileStore.Save(controller.ProfilePath, profile);
            Assert.That(controller.InitializeJourney(controller.Catalog, controller.JourneyCatalog, controller.SavePath), Is.True);
            var journey = controller.JourneyCatalog.Entries.First();
            controller.SelectEncounter(journey.EncounterId);
            AdvanceToInvitation(controller);
            controller.AcceptEncounter();
            yield return Wait(controller);
            string persistedProfile = File.ReadAllText(controller.ProfilePath);
            foreach (int width in new[] { 443, 540 })
            {
                Resize(view, width);
                Find<Button>(view, "MenuButton").onClick.Invoke();
                Find<Button>(view, "CollectionButton").onClick.Invoke();
                yield return null;
                Assert.That(collection.IsVisible, Is.True);
                Assert.That(collection.Filter, Is.EqualTo(SpiritCollectionFilter.Owned));
                Assert.That(Find<Text>(collection, "CollectionSummary").text, Does.StartWith("Collected 1 / "));
                var row = collection.Rows.Single(value => value.FamilyId == permanent.FamilyId);
                var unlockedButton = row.GetComponentsInChildren<Button>().Single(button =>
                    Find<Text>(button, "VariantState").text == "Unlocked");
                unlockedButton.onClick.Invoke();
                Assert.That(collection.SelectedModifierId, Is.EqualTo(permanent.Id));
                Assert.That(unlockedButton.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
                Assert.That(Find<Text>(collection, "CollectionDetailTitle").text, Does.EndWith(" - Unlocked"));
                ScrollToRow(Find<ScrollRect>(collection, "CollectionFamilies"), (RectTransform)row.transform);
                yield return null;
                yield return Capture(view, "unlocked-menu", width);
                Find<Button>(collection, "CollectionFilterButton").onClick.Invoke();
                Assert.That(collection.Filter, Is.EqualTo(SpiritCollectionFilter.Discovered));
                Assert.That(collection.Rows.All(family => !family.IsUndiscovered), Is.True);
                Assert.That(collection.SelectedModifierId, Is.EqualTo(permanent.Id));
                Canvas.ForceUpdateCanvases();
                AssertCompleteText(Find<Text>(collection, "CollectionFilterButtonLabel"));
                yield return Capture(view, "discovered-menu", width);
                Find<Button>(view, "CloseCollectionButton").onClick.Invoke();
                Assert.That(File.ReadAllText(controller.ProfilePath), Is.EqualTo(persistedProfile));
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MothRunCharmsFollowTheInvitationAndBeginOnlyAfterExplicitAcceptance()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit(false);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = controller.GetComponent<SpiritEncounterView>();
            var journey = controller.JourneyCatalog.Entries.Single(value =>
                controller.Catalog.GetEncounter(value.EncounterId).EncounterModifierIds.Count > 0);
            var encounter = controller.Catalog.GetEncounter(journey.EncounterId);
            foreach (int width in new[] { 443, 540 })
            {
                Resize(view, width);
                controller.SelectEncounter(journey.EncounterId);
                AdvanceToInvitation(controller);
                yield return null;
                Assert.That(Find<RectTransform>(view, "EncounterCharmsIntroduction").gameObject.activeInHierarchy, Is.False);
                Find<Button>(view, "JourneyAcceptButton").onClick.Invoke();
                yield return null;
                var introduction = Find<RectTransform>(view, "EncounterCharmsIntroduction");
                Assert.That(introduction.gameObject.activeInHierarchy, Is.True);
                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
                Assert.That(controller.Snapshot, Is.Null);
                var rows = introduction.GetComponentsInChildren<SpiritModifierFamilyRow>();
                Assert.That(rows.Length, Is.EqualTo(encounter.EncounterModifierIds.Select(id => controller.Catalog.GetModifier(id).FamilyId).Distinct().Count()));
                Assert.That(Find<Text>(introduction, "EncounterCharmsScope").text, Does.Contain("can appear in charm choices")
                    .And.Contain("not added to your permanent collection"));
                Canvas.ForceUpdateCanvases();
                foreach (var text in introduction.GetComponentsInChildren<Text>()) AssertCompleteText(text);
                yield return Capture(view, "moth-introduction", width);
                Find<Button>(view, "BackToInvitationButton").onClick.Invoke();
                Assert.That(introduction.gameObject.activeInHierarchy, Is.False);
                Find<Button>(view, "JourneyAcceptButton").onClick.Invoke();
                Assert.That(introduction.gameObject.activeInHierarchy, Is.True);
                controller.ReturnToInvestigation();
                Assert.That(introduction.gameObject.activeInHierarchy, Is.False);
            }
            controller.SelectEncounter(journey.EncounterId);
            AdvanceToInvitation(controller);
            Find<Button>(view, "JourneyAcceptButton").onClick.Invoke();
            Find<Button>(view, "BeginEncounterButton").onClick.Invoke();
            yield return Wait(controller);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.Snapshot, Is.Not.Null);
            Assert.That(controller.Snapshot.Modifiers.Select(value => value.DefinitionId)
                .Intersect(encounter.EncounterModifierIds), Is.Empty, "Run charm additions enter the pool without being equipped.");
            Assert.That(controller.ProfileUnlockedModifierIds, Is.Empty);
            controller.ReturnToInvestigation();
            var ordinary = controller.JourneyCatalog.Entries.First(value =>
                controller.Catalog.GetEncounter(value.EncounterId).EncounterModifierIds.Count == 0);
            controller.SelectEncounter(ordinary.EncounterId);
            AdvanceToInvitation(controller);
            Find<Button>(view, "JourneyAcceptButton").onClick.Invoke();
            yield return Wait(controller);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(ordinary.EncounterId));
            LogAssert.NoUnexpectedReceived();
        }

        private static void AdvanceToInvitation(SpiritEncounterController controller)
        {
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();
        }

        private static IEnumerator Wait(SpiritEncounterController controller)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False, controller.StatusMessage);
        }

        private static void Resize(SpiritEncounterView view, int width)
        {
            var portrait = (RectTransform)view.transform.Find("SpiritCanvas/SafeArea/Portrait");
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(width, 960);
            portrait.anchoredPosition = Vector2.zero;
        }

        private static void ScrollToRow(ScrollRect scroll, RectTransform row)
        {
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content, row);
            scroll.StopMovement();
            scroll.content.anchoredPosition = new Vector2(0, Mathf.Max(0, -bounds.max.y));
        }

        private static void AssertCompleteText(Text label)
        {
            var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            var rendered = label.cachedTextGenerator;
            Assert.That(rendered.Populate(label.text, settings), Is.True, label.name);
            settings.fontSize = label.resizeTextForBestFit ? rendered.fontSizeUsedForBestFit : label.fontSize;
            settings.resizeTextForBestFit = false;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            using (var complete = new TextGenerator())
            {
                Assert.That(complete.Populate(label.text, settings), Is.True, label.name);
                Assert.That(rendered.vertexCount, Is.EqualTo(complete.vertexCount), label.name + " truncates text.");
            }
        }

        private static T Find<T>(Component parent, string name) where T : Component
            => parent.GetComponentsInChildren<T>(true).Single(component => component.name == name);

        private static IEnumerator Capture(SpiritEncounterView view, string name, int width)
        {
            string output = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(output)) yield break;
            Directory.CreateDirectory(output);
            string path = Path.Combine(output, "collection-" + name + "-" + width + "x960.png");
            Assert.That(File.Exists(path), Is.False, "Capture output must be fresh.");
            var canvas = view.transform.Find("SpiritCanvas").GetComponent<Canvas>();
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousPlane = canvas.planeDistance;
            var cameraRoot = new GameObject("Collection Capture Camera");
            var camera = cameraRoot.AddComponent<Camera>();
            var target = new RenderTexture(width, 960, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D readback = null;
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                target.Create();
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.orthographic = true;
                camera.orthographicSize = 480;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 200;
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
                    Is.GreaterThan(width * 960 / 100));
                File.WriteAllBytes(path, readback.EncodeToPNG());
                TestContext.WriteLine("Collection capture: " + path);
            }
            finally
            {
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousPlane;
                RenderTexture.active = previousTarget;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(cameraRoot);
                if (readback != null) UnityEngine.Object.Destroy(readback);
            }
        }
    }
}
