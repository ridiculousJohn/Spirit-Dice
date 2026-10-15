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
    public sealed class SpiritModifierCategoryPlayTests
    {
        private static readonly string[] Ids =
        {
            "modifier.play_hand.two_pair.base_score",
            "modifier.play_hand.two_pair.multiplier",
            "modifier.play_hand.two_pair.combined",
            "modifier.play_hand.two_pair.rerolls"
        };

        [UnityTest]
        public IEnumerator AuthoredFamilySpritesRenderOnBoneAcrossEncounterTables()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit(false);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = controller.GetComponent<SpiritEncounterView>();
            var standard = controller.Catalog;
            var families = standard.Modifiers.GroupBy(value => value.FamilyId)
                .Select(group => group.First()).OrderBy(value => value.FamilyId).ToArray();
            var activated = families.Select(value => new ModifierDefinition(value.Id, value.FamilyId,
                ModifierPhase.InitialRoll, new AlwaysCondition(),
                new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 1) })).ToArray();
            var sample = new EncounterDefinition("artwork.samples", new[] { new StageDefinition(1000000, 3, 3) },
                startingModifiers: activated.Select(value => value.Id));
            var sampleCatalog = new MechanicalCatalog("artwork.samples", standard.Hands, new[] { sample }, activated);
            var contributions = EncounterSession.CreateEncounter(sampleCatalog, sample.Id, 60).Snapshot.ModifierContributions;
            int tableIndex = 0;
            foreach (var journey in controller.JourneyCatalog.Entries)
            {
                var encounter = new EncounterDefinition(journey.EncounterId, new[] { new StageDefinition(1000000, 3, 3) });
                controller.Initialize(new MechanicalCatalog("artwork.table", standard.Hands,
                    new[] { encounter }, standard.Modifiers), encounter.Id, 60, ContentReady.SpiritSavePath());
                Resize(view, 443);
                yield return null;
                int count = tableIndex++ == 0 ? families.Length : 12;
                for (int first = 0; first < count; first += 6)
                {
                    view.ModifierHistory.Clear();
                    for (int index = first; index < Math.Min(first + 6, count); ++index)
                    {
                        var definition = families[index];
                        var sprite = SpiritModifierIcons.GetSprite(definition);
                        Assert.That(sprite, Is.Not.Null, definition.Id);
                        view.ModifierHistory.Add(contributions[index], definition, false);
                        var material = view.ModifierHistory.Renderer.GetDieRenderer(index - first).sharedMaterial;
                        Assert.That(material.GetTexture("_FaceIcon"), Is.SameAs(sprite.texture));
                    }
                    yield return null;
                    yield return Capture(view, "families-" + journey.EncounterId + "-" + first, 443);
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FourTwinKnotsCategoriesRemainDistinctAcrossOwnedCardsDetailsAndThreeTables()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit(false);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = controller.GetComponent<SpiritEncounterView>();
            var standard = controller.Catalog;
            var definitions = Ids.Select(standard.GetModifier).ToArray();
            Assert.That(definitions.Select(value => value.FamilyId).Distinct().Count(), Is.EqualTo(1));
            Assert.That(definitions.Select(SpiritModifierIcons.GetSprite).Distinct().Count(), Is.EqualTo(1));
            Assert.That(SpiritModifierIcons.GetSprite(definitions[0]), Is.Not.Null);
            Assert.That(definitions.Select(SpiritModifierIcons.GetCategorySprite).Distinct().Count(), Is.EqualTo(4));
            Assert.That(definitions.Select(SpiritModifierIcons.GetEffectColor).Distinct().Count(), Is.EqualTo(4));
            Assert.That(controller.JourneyCatalog.Entries.Count, Is.EqualTo(3));

            int tableIndex = 0;
            foreach (var journey in controller.JourneyCatalog.Entries)
            {
                var encounter = new EncounterDefinition(journey.EncounterId,
                    new[] { new StageDefinition(1000000000, 3, 3) }, startingModifiers: Ids);
                var catalog = new MechanicalCatalog("category-tables.v1", standard.Hands, new[] { encounter }, definitions);
                ulong seed = FindTwoPairSeed(catalog, encounter.Id);
                controller.Initialize(catalog, encounter.Id, seed, ContentReady.SpiritSavePath());
                yield return null;
                Assert.That(controller.Snapshot.Modifiers.Count, Is.EqualTo(4));
                Assert.That(view.TrayScene.Presentation, Is.SameAs(journey.TablePresentation.TrayPresentation));
                Assert.That(view.ModifierHistory.Count, Is.EqualTo(4));
                for (int index = 0; index < definitions.Length; ++index)
                {
                    var material = view.ModifierHistory.Renderer.GetDieRenderer(index).sharedMaterial;
                    Assert.That(material.GetTexture("_FaceIcon"), Is.SameAs(SpiritModifierIcons.GetSprite(definitions[index]).texture));
                    Assert.That(Vector4.Distance(material.GetColor("_FaceIconColor"), SpiritModifierIcons.GetInkColor(definitions[index])), Is.LessThan(.00001f));
                    Assert.That(material.GetFloat("_ShowCategory"), Is.EqualTo(1));
                    Assert.That(material.GetTexture("_CategoryIcon"),
                        Is.SameAs(SpiritModifierIcons.GetCategorySprite(definitions[index]).texture));
                }
                foreach (int width in new[] { 540, 443 })
                {
                    Resize(view, width);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    yield return Capture(view, "ready-" + journey.EncounterId, width);
                    if (tableIndex != 0) continue;
                    view.ShowCharms();
                    yield return null;
                    for (int index = 0; index < definitions.Length; ++index)
                    {
                        var definition = definitions[index];
                        var card = Find<Button>(view, "OwnedCharm" + (index + 1));
                        AssertFamilyAndCategory(card, "CharmIcon", "CharmCategory", definition);
                        Assert.That(Find<Text>(card, "Effects").color, Is.EqualTo(SpiritModifierIcons.GetEffectColor(definition)));
                        var category = Find<SpiritModifierCategoryBadge>(card, "CharmCategory");
                        var status = card.GetComponentInChildren<SpiritCharmStatusBadge>();
                        Assert.That(status, Is.Not.Null);
                        Assert.That(BoundsIn(card.transform, category.Glyph.rectTransform)
                            .Overlaps(BoundsIn(card.transform, (RectTransform)status.transform)), Is.False,
                            "The category mark must leave the status badge independent.");
                        card.onClick.Invoke();
                        Canvas.ForceUpdateCanvases();
                        AssertFamilyAndCategory(view, "CharmDetailIcon", "CharmDetailCategory", definition);
                        Assert.That(Find<Text>(view, "CharmDetailBody").text, Does.Contain(
                            "<color=#" + ColorUtility.ToHtmlStringRGB(SpiritModifierIcons.GetEffectColor(definition)) + ">" +
                            SpiritModifierPresentation.GetEffectText(definition) + "</color>"));
                        Assert.That(Find<SpiritCharmSelectionGraphic>(card, "SelectionOutline").gameObject.activeSelf, Is.True);
                        yield return Capture(view, "owned-" + index, width);
                    }
                    view.ShowTable();
                    yield return null;
                }
                ++tableIndex;
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RewardCardsKeepCategoryMarksWhenSelectedAndResetForHandUpgrades()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = controller.GetComponent<SpiritEncounterView>();
            var portrait = (RectTransform)view.transform.Find("SpiritCanvas/SafeArea/Portrait");
            var overlay = new GameObject("Category Reward Comparison", typeof(RectTransform), typeof(Image));
            var overlayRect = (RectTransform)overlay.transform;
            overlayRect.SetParent(portrait, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color32(20, 32, 27, 255);
            var cards = Enumerable.Range(0, 3).Select(index =>
                SpiritRewardCard.Create("CategoryReward" + index, overlay.transform, (offer, option) => { })).ToArray();
            foreach (int width in new[] { 540, 443 })
            {
                Resize(view, width);
                for (int pass = 0; pass < 2; ++pass)
                {
                    for (int index = 0; index < cards.Length; ++index)
                    {
                        var card = cards[index];
                        var rect = (RectTransform)card.transform;
                        rect.anchorMin = new Vector2(.04f, 1);
                        rect.anchorMax = new Vector2(.96f, 1);
                        rect.pivot = new Vector2(.5f, 1);
                        rect.offsetMin = new Vector2(0, -160 - index * 180 - 150);
                        rect.offsetMax = new Vector2(0, -160 - index * 180);
                        var definition = controller.Catalog.GetModifier(Ids[(index + pass * 2) % Ids.Length]);
                        card.RenderModifier(definition, 1, index + 1);
                        card.SetModifierSelected(index == 0);
                        AssertFamilyAndCategory(card, "CharmIcon", "CharmCategory", definition);
                        Assert.That(Find<Text>(card, "Effects").color, Is.EqualTo(SpiritModifierIcons.GetEffectColor(definition)));
                        Assert.That(card.transform.Find("UpgradeSelectionMarker").gameObject.activeSelf, Is.EqualTo(index == 0));
                    }
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    foreach (var card in cards)
                    {
                        var category = Find<SpiritModifierCategoryBadge>(card, "CharmCategory");
                        AssertInside((RectTransform)card.transform, category.Glyph.rectTransform);
                        Assert.That(BoundsIn(card.transform, category.Glyph.rectTransform)
                            .Overlaps(BoundsIn(card.transform, Find<Text>(card, "Condition").rectTransform)), Is.False);
                    }
                    yield return Capture(view, "rewards-" + pass, width);
                }
                var reused = cards[0];
                reused.RenderHandUpgrade(controller.Catalog.Hands[0], 0, 2, 1);
                Assert.That(Find<SpiritModifierCategoryBadge>(reused, "CharmCategory").gameObject.activeSelf, Is.False);
                Assert.That(Find<Image>(reused, "CharmIcon").gameObject.activeSelf, Is.False);
                Assert.That(Find<SpiritModifierSigilGraphic>(reused, "CharmSigil").gameObject.activeSelf, Is.False);
                Assert.That(Find<Text>(reused, "Effects").color, Is.EqualTo(reused.GetComponentInParent<SpiritUiContext>().Theme.amber));
                reused.RenderModifier(controller.Catalog.GetModifier(Ids[3]), 3, 1);
                AssertFamilyAndCategory(reused, "CharmIcon", "CharmCategory", controller.Catalog.GetModifier(Ids[3]));
            }
            UnityEngine.Object.Destroy(overlay);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BothActionCuesShowCategoriesAndClearTheirStyleForHostAndUnknownArtwork()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = controller.GetComponent<SpiritEncounterView>();
            var cue = view.GetComponent<SpiritActionModifierCue>();
            var theme = view.GetComponentInChildren<SpiritUiContext>().Theme;
            foreach (int width in new[] { 540, 443 })
            {
                Resize(view, width);
                for (int index = 0; index < Ids.Length; ++index)
                {
                    var definition = controller.Catalog.GetModifier(Ids[index]);
                    cue.Show(definition, 5, true, true);
                    view.RerollButton.interactable = view.PlayButton.interactable = false;
                    yield return new WaitForSecondsRealtime(.15f);
                    foreach (var name in new[] { "RerollModifierCue", "PlayHandModifierCue" })
                    {
                        var group = Find<CanvasGroup>(view, name);
                        Assert.That(group.gameObject.activeInHierarchy, Is.True);
                        AssertFamilyAndCategory(group, "Artwork", "CharmCategory", definition);
                        AssertCueTint(group);
                    }
                    foreach (var button in new[] { view.RerollButton, view.PlayButton })
                        Assert.That(button.GetComponentInChildren<Text>().canvasRenderer.GetColor(),
                            Is.EqualTo(button.colors.disabledColor), "The underlying label keeps its disabled tint.");
                    // Selection refresh recaptures button content while the transient cue is still visible.
                    controller.ToggleDie(0);
                    view.RerollButton.interactable = view.PlayButton.interactable = true;
                    yield return new WaitForSecondsRealtime(.15f);
                    foreach (var name in new[] { "RerollModifierCue", "PlayHandModifierCue" })
                        AssertCueTint(Find<CanvasGroup>(view, name));
                    view.RerollButton.interactable = view.PlayButton.interactable = false;
                    yield return new WaitForSecondsRealtime(.15f);
                    foreach (var button in new[] { view.RerollButton, view.PlayButton })
                    {
                        var group = Find<CanvasGroup>(button,
                            button == view.RerollButton ? "RerollModifierCue" : "PlayHandModifierCue");
                        AssertCueTint(group);
                        Assert.That(button.GetComponentInChildren<Text>().GetComponent<CanvasGroup>().alpha + group.alpha,
                            Is.EqualTo(1).Within(.001f), "Labels must continue crossfading independently of tint.");
                    }
                    yield return Capture(view, "actions-" + index, width);
                    cue.ShowHost(SpiritModifierIcons.GetSprite(definition), ModifierPhase.PlayHand, 5);
                    Assert.That(cue.IsRerollVisible, Is.False);
                    var host = Find<CanvasGroup>(view, "PlayHandModifierCue");
                    Assert.That(Find<Image>(host, "Artwork").color, Is.EqualTo(theme.cream));
                    Assert.That(Find<SpiritModifierCategoryBadge>(host, "CharmCategory").gameObject.activeSelf, Is.False);
                    cue.Show(SpiritModifierIcons.GetSprite(definition), ModifierPhase.PlayerReroll, 5);
                    var unknown = Find<CanvasGroup>(view, "RerollModifierCue");
                    Assert.That(Find<Image>(unknown, "Artwork").color, Is.EqualTo(theme.cream));
                    Assert.That(Find<SpiritModifierCategoryBadge>(unknown, "CharmCategory").Glyph.sprite, Is.Null);
                    cue.Show(definition, 5, true, true);
                    cue.Clear();
                    Assert.That(cue.IsVisible, Is.False);
                    foreach (var name in new[] { "RerollModifierCue", "PlayHandModifierCue" })
                        Assert.That(Find<SpiritModifierCategoryBadge>(Find<CanvasGroup>(view, name), "CharmCategory")
                            .gameObject.activeSelf, Is.False);
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static ulong FindTwoPairSeed(MechanicalCatalog catalog, string encounterId)
        {
            for (ulong seed = 1; seed <= 2000; ++seed)
                if (EncounterSession.CreateEncounter(catalog, encounterId, seed).PreviewHandResolution()
                    .Events.OfType<ModifierActivatedEvent>().Count() == 4) return seed;
            throw new AssertionException("No two-pair seed activated all four categories.");
        }

        private static void AssertCueTint(CanvasGroup cue)
        {
            foreach (var graphic in cue.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.canvasRenderer.GetColor(), Is.EqualTo(Color.white),
                    cue.name + "/" + graphic.name + " must retain its full category color through button transitions.");
        }

        private static void AssertFamilyAndCategory(Component root, string iconName, string categoryName,
            ModifierDefinition definition)
        {
            var icon = Find<Image>(root, iconName);
            var category = Find<SpiritModifierCategoryBadge>(root, categoryName);
            Assert.That(icon.sprite, Is.SameAs(SpiritModifierIcons.GetSprite(definition)));
            Assert.That(icon.color, Is.EqualTo(SpiritModifierIcons.GetEffectColor(definition)));
            Assert.That(category.gameObject.activeInHierarchy, Is.True);
            Assert.That(category.Accent.color, Is.EqualTo(SpiritModifierIcons.GetEffectColor(definition)));
            Assert.That(category.Glyph.sprite, Is.SameAs(SpiritModifierIcons.GetCategorySprite(definition)));
            Assert.That(category.Glyph.color, Is.EqualTo(Color.white));
            Assert.That(category.Glyph.raycastTarget, Is.False);
        }

        private static void Resize(SpiritEncounterView view, int width)
        {
            var portrait = (RectTransform)view.transform.Find("SpiritCanvas/SafeArea/Portrait");
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(width, 960);
            portrait.anchoredPosition = Vector2.zero;
        }

        private static Rect BoundsIn(Transform parent, RectTransform rect)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, rect);
            return new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        }

        private static void AssertInside(RectTransform parent, RectTransform child)
        {
            Rect bounds = BoundsIn(parent, child);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(parent.rect.xMin));
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(parent.rect.yMin));
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(parent.rect.xMax));
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(parent.rect.yMax));
        }

        private static T Find<T>(Component root, string name) where T : Component
        {
            var value = root.GetComponentsInChildren<T>(true).FirstOrDefault(candidate => candidate.name == name);
            Assert.That(value, Is.Not.Null, name);
            return value;
        }

        private static IEnumerator Capture(SpiritEncounterView view, string name, int width)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) yield break;
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "category-" + name + "-" + width + "x960.png");
            Assert.That(File.Exists(path), Is.False, "Capture output must be fresh.");
            var canvas = view.transform.Find("SpiritCanvas").GetComponent<Canvas>();
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousPlane = canvas.planeDistance;
            var cameraRoot = new GameObject("Category Capture Camera");
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
                    Is.GreaterThan(width * 960 / 100), "The portrait capture must contain rendered content.");
                File.WriteAllBytes(path, readback.EncodeToPNG());
                TestContext.WriteLine("Modifier category capture: " + path);
            }
            finally
            {
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousPlane;
                RenderTexture.active = previousTarget;
                GL.sRGBWrite = previousSrgb;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(cameraRoot);
                if (readback != null) UnityEngine.Object.Destroy(readback);
            }
        }
    }
}
