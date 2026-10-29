using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dice.Tests
{
    internal static class ContentReady
    {
        /** Real seconds a test waits for a presentation step, such as routing or an opening roll, before it fails. */
        internal const float Timeout = 30;
        /** Real seconds a test waits for a step that plays authored host motion or a whole sequence before it fails. */
        internal const float SequenceTimeout = 45;
        /** Real seconds a newly shown button may stay behind another graphic before a click on it fails. */
        private const float RaycastDepthTimeout = 1;
        /** The fixed seed the fixtures play their encounters from. */
        internal const ulong Seed = 60;
        /** Seeds searched for an opening whose rule draws produce the event a rule playback test needs. */
        internal const ulong RuleSeedSearchLimit = 4000;
        /** Player rerolls take the two leftmost dice, so a rule acting on kept dice targets one of the other three. */
        internal const int RerollMask = 0b00011;

        /** Statuses that report a failed startup or campaign opening. */
        private static readonly SpiritStatusKind[] StartupFailures =
        {
            SpiritStatusKind.ConfigurationFailed, SpiritStatusKind.ApplicationFailed, SpiritStatusKind.ContentFailed,
            SpiritStatusKind.EncounterInitializationFailed, SpiritStatusKind.MainMenuFailed, SpiritStatusKind.SaveSlotsFailed,
            SpiritStatusKind.InvestigationFailed, SpiritStatusKind.ProfileProblem, SpiritStatusKind.StartFailed,
            SpiritStatusKind.LoadFailed
        };

        /**
         * Loads the authored content and, when entering an encounter, opens the first encounter journey in catalog order
         * that a fresh profile can play without prerequisites and that has the property the caller requires.
         */
        internal static IEnumerator Spirit(bool enterEncounter = true, Func<EncounterJourneyAsset, bool> requirement = null)
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            Assert.That(controller, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + Timeout;
            while ((controller.Catalog == null || controller.JourneyCatalog == null) &&
                Time.realtimeSinceStartup < deadline)
            {
                Assert.That(StartupFailures, Has.No.Member(controller.Status.Kind), controller.Status.ToString());
                yield return null;
            }
            Assert.That(controller.Catalog, Is.Not.Null, controller.Status.ToString());
            Assert.That(controller.JourneyCatalog, Is.Not.Null, controller.Status.ToString());
            yield return Routed(controller);
            string savePath = SpiritSavePath();
            Assert.That(controller.InitializeJourney(controller.Catalog, controller.JourneyCatalog, savePath), Is.True,
                controller.Status.ToString());
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.Null);
            if (!enterEncounter) { yield return Routed(controller); yield break; }
            EncounterJourneyAsset journey = null;
            foreach (var entry in controller.JourneyCatalog.Entries)
                if (entry != null && !entry.IsStoryOnly && controller.IsJourneyAvailable(entry)
                    && (requirement == null || requirement(entry)))
                {
                    journey = entry;
                    break;
                }
            Assert.That(journey, Is.Not.Null, requirement == null
                ? "The authored journey catalog has no encounter journey available without prerequisites."
                : "The authored journey catalog has no encounter journey available without prerequisites with the required property.");
            controller.SelectEncounter(journey);
            yield return Routed(controller);
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();
            controller.AcceptEncounter();
            yield return Routed(controller);
            yield return WaitUntil(() => !controller.IsBusy, "Opening encounter presentation did not complete.");
            Assert.That(controller.Snapshot, Is.Not.Null, controller.Status.ToString());
        }

        /**
         * The application's charm presentation: the authored skin's, loaded with its content, or the test skin a test
         * installed.
         */
        internal static ModifierPresentationCatalog Charms
        {
            get
            {
                var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.ModifierPresentation, Is.Not.Null, "The application has not loaded its charm presentation.");
                return controller.ModifierPresentation;
            }
        }

        internal static SpiritApplicationPresentation Router(SpiritEncounterController controller = null)
        {
            if (controller == null) controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            Assert.That(controller, Is.Not.Null);
            var router = controller.GetComponent<SpiritApplicationPresentation>();
            Assert.That(router, Is.Not.Null, "Application requires its authored presentation router.");
            return router;
        }

        internal static SpiritEncounterView EncounterView(SpiritEncounterController controller = null) => Router(controller).EncounterView;
        internal static SpiritFrontendView FrontendView(SpiritEncounterController controller = null) => Router(controller).FrontendView;

        internal static Component PresentationRoot(SpiritEncounterController controller = null)
        {
            var router = Router(controller);
            Assert.That(router.PresentationRoot, Is.Not.Null);
            return router.PresentationRoot.transform;
        }

        internal static RectTransform Portrait(SpiritEncounterController controller = null)
        {
            var router = Router(controller);
            var portrait = router.EncounterView != null ? router.EncounterView.Portrait : router.FrontendView.Portrait;
            Assert.That(portrait, Is.Not.Null);
            return portrait;
        }

        internal static IEnumerator Routed(SpiritEncounterController controller = null)
        {
            var router = Router(controller);
            yield return WaitUntil(() => router.IsReady, "The authored presentation scene did not become ready.");
        }

        /** Loads a fresh application scene and waits for its content, at the investigation board. */
        internal static IEnumerator LoadApplication()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return Spirit(false);
        }

        /** Plays the controller's runs from the fixtures' fixed seed. */
        internal static void UseFixedSeed(SpiritEncounterController controller)
            => controller.ConfigureSeedPolicy(true, Seed);

        /** Waits until the condition holds, failing with the description when it does not within the timeout. */
        internal static IEnumerator WaitUntil(Func<bool> condition, string description, float timeout = Timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, description);
        }

        /** Waits for routing, then for the controller to finish presenting, then one more frame. */
        internal static IEnumerator Idle(SpiritEncounterController controller, float timeout = Timeout)
        {
            yield return Routed(controller);
            yield return WaitUntil(() => !controller.IsBusy, "Presentation did not complete: " + controller.Status, timeout);
            yield return null;
        }

        /**
         * Opens the journey's encounter from the investigation board with the given session seed, completing its
         * prerequisites first and passing any pre-encounter screen, and waits for its opening roll.
         */
        internal static IEnumerator EnterEncounter(SpiritEncounterController controller, EncounterJourneyAsset journey, ulong seed)
        {
            controller.ReturnToInvestigation();
            yield return Routed(controller);
            SeedJourneyPrerequisites(controller, journey);
            controller.SelectEncounter(journey);
            yield return Routed(controller);
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();
            controller.ConfigureSeedPolicy(true, seed);
            controller.AcceptEncounter();
            yield return Routed(controller);
            // Encounters that introduce their additions before play show the pre-encounter screen first.
            var charms = EncounterView(controller).JourneyView.EncounterCharms;
            if (charms.IsOpen) charms.BeginButton.onClick.Invoke();
            yield return Idle(controller);
            Assert.That(controller.Snapshot, Is.Not.Null, controller.Status.ToString());
        }

        /**
         * A copy of the catalog in which the encounter has every stage target at 1 and is otherwise as authored, so each
         * played hand clears its stage.
         */
        internal static MechanicalCatalog ShortStages(MechanicalCatalog authored, EncounterDefinition.Id encounterId, string version)
        {
            var definition = authored.GetEncounter(encounterId);
            var shortEncounter = new EncounterDefinition(definition.Identity,
                definition.Stages.Select(stage => new StageDefinition(1, stage.StartingHands, stage.StartingRerolls, stage.ExitBehavior)),
                definition.StartingUpgrades, definition.StartingModifiers, definition.RewardPolicy, definition.Rules,
                definition.UnlockMilestones, definition.EncounterModifierIds,
                definition.UnlockRewardModifierIds, definition.Pieces, definition.EncounterModifiersFirstClearedStageNumber);
            return new MechanicalCatalog(version, authored.Hands,
                authored.Encounters.Select(value => value.Identity == definition.Identity ? shortEncounter : value),
                authored.Modifiers);
        }

        /** The one encounter journey of the catalog whose encounter has a rule of the given kind. */
        internal static EncounterJourneyAsset JourneyWithRule<TRule>(SpiritEncounterController controller) where TRule : EncounterRuleDefinition
        {
            var journeys = controller.JourneyCatalog.Entries.Where(journey => journey != null && !journey.IsStoryOnly &&
                controller.Catalog.GetEncounter(journey.Encounter.Identity).Rules.OfType<TRule>().Any()).ToArray();
            Assert.That(journeys, Has.Length.EqualTo(1), "One authored encounter journey has the rule " + typeof(TRule).Name + ".");
            return journeys[0];
        }

        /** The named component under the presentation root, preferring an active one where several screens author the same name. */
        internal static T Find<T>(SpiritEncounterController controller, string name) where T : Component
        {
            var value = PresentationRoot(controller).GetComponentsInChildren<T>(true).Where(item => item.name == name)
                .OrderByDescending(item => item.gameObject.activeInHierarchy).FirstOrDefault();
            Assert.That(value, Is.Not.Null, name);
            return value;
        }

        /** Clicks the button through the event system as a pointer at its centre does; fails if it is hidden, disabled or covered. */
        internal static void Click(Button button)
        {
            Assert.That(button.gameObject.activeInHierarchy, Is.True, button.name);
            Assert.That(button.IsInteractable(), Is.True, button.name);
            var handler = TopClickHandler(button, out var pointer, out var hit);
            Assert.That(hit.gameObject, Is.Not.Null, button.name);
            Assert.That(handler, Is.SameAs(button.gameObject), "A graphic blocks " + button.name);
            pointer.pointerCurrentRaycast = hit;
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        /** Clicks a button that may just have been shown, once the canvas has given its graphics their render depth. */
        internal static IEnumerator ClickWhenShown(Button button)
        {
            // Newly shown graphics are skipped by GraphicRaycaster until the canvas assigns their depth when rendering.
            yield return WaitUntil(() => TopClickHandler(button, out _, out _) == button.gameObject, "A graphic blocks " + button.name,
                RaycastDepthTimeout);
            Click(button);
        }

        /** The click handler a pointer at the button's centre reaches, with that pointer and its topmost hit. */
        internal static GameObject TopClickHandler(Button button, out PointerEventData pointer, out RaycastResult hit)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(EventSystem.current, Is.Not.Null);
            var rect = (RectTransform)button.transform;
            pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left,
                pointerId = -1
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            hit = hits.Count > 0 ? hits[0] : default;
            return hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
        }

        /**
         * Sets an authored field of a fabricated copy of content, such as a journey or its beats. Play Mode tests build
         * into the player assembly, which has no serialized-object editing, so the field is set directly.
         */
        internal static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
            field.SetValue(target, value);
        }

        /**
         * The table a run without a journey opens on, with the rules its cards present as the encounter it presents
         * defines them. Such a run opens on the application's default encounter scene, the scene of the configured direct
         * encounter, so an encounter fabricated for it carries these rules for the table's cards to present.
         */
        internal static EncounterTablePresentationAsset DirectTable(DiceApplicationConfig configuration, MechanicalCatalog catalog,
            out EncounterRuleDefinition[] rules)
        {
            Assert.That(configuration.JourneyCatalog, Is.Not.Null, "The application configuration has no journey catalog.");
            Assert.That(configuration.Encounter, Is.Not.Null, "The application configuration has no direct encounter.");
            var table = configuration.JourneyCatalog.Find(configuration.Encounter.Identity)?.TablePresentation;
            Assert.That(table, Is.Not.Null, "The configured direct encounter has no journey with an authored table.");
            var encounter = catalog.GetEncounter(configuration.Encounter.Identity);
            var presented = new List<EncounterRuleDefinition>();
            foreach (var card in table.RulePresentations.Where(card => card != null))
            {
                var rule = encounter.Rules.FirstOrDefault(value => value.Id == card.RuleId);
                Assert.That(rule, Is.Not.Null, "The direct encounter lacks the rule its table card " + card.name + " presents.");
                if (!presented.Contains(rule)) presented.Add(rule);
            }
            rules = presented.ToArray();
            return table;
        }

        internal static string SpiritSavePath() => Path.GetFullPath(Path.Combine(Application.dataPath,
            "..", "..", "Docs", "work", "journey-file-tests", Guid.NewGuid().ToString("N"), "encounter.json"));

        internal static void SeedJourneyPrerequisites(SpiritEncounterController controller, EncounterJourneyAsset journey)
        {
            string workRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work")) +
                Path.DirectorySeparatorChar;
            Assert.That(Path.GetFullPath(controller.ProfilePath).StartsWith(workRoot, StringComparison.OrdinalIgnoreCase),
                Is.True, "Journey prerequisites require an isolated test profile.");
            Assert.That(controller.JourneyCatalog.Entries, Does.Contain(journey));
            var profile = PlayerProfileFileStore.LoadOrCreate(controller.ProfilePath);
            SeedPrerequisites(profile, journey, new HashSet<EncounterJourneyAsset>());
            PlayerProfileFileStore.Save(controller.ProfilePath, profile);
            Assert.That(controller.InitializeJourney(controller.Catalog, controller.JourneyCatalog, controller.SavePath),
                Is.True, controller.JourneyStatus.ToString());
            Assert.That(controller.IsJourneyAvailable(journey), Is.True, journey.name);
        }

        private static void SeedPrerequisites(PlayerProfile profile, EncounterJourneyAsset journey,
            HashSet<EncounterJourneyAsset> visited)
        {
            if (!visited.Add(journey)) return;
            foreach (var required in journey.RequiredCompletedJourneys)
            {
                SeedPrerequisites(profile, required, visited);
                profile.CompleteJourney(required.Identity);
            }
        }
    }

    [SetUpFixture]
    public sealed class SpiritTestProfileIsolation
    {
        [OneTimeSetUp]
        public void IsolateSceneProfiles() => SceneManager.sceneLoaded += IsolateProfile;

        [OneTimeTearDown]
        public void StopIsolatingSceneProfiles() => SceneManager.sceneLoaded -= IsolateProfile;

        private static void IsolateProfile(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Application") return;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            if (controller == null) return;
            // sceneLoaded runs after Awake and before Start loads the persistent profile.
            controller.SavePath = ContentReady.SpiritSavePath();
        }
    }
}
