using System.Collections;
using System.Linq;
using System.Reflection;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dice.Tests
{
    /**
     * The die-binding rule's presentation on the encounter screen, for the first authored journey whose encounter has a
     * die-binding rule: the dice show the run's marks and bindings, the roots grow when a player reroll binds the marked
     * die, a bound die cannot be selected, a loaded run shows its marked and bound dice at once, and the roots withdraw
     * after a played hand's scoring.
     */
    public sealed class DieBindingPresentationPlayTests
    {
        [UnityTest]
        public IEnumerator BindingRulesShowMarksGrowRootsLockBoundDiceAndReleaseAfterScoring()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = controller.JourneyCatalog.Entries.FirstOrDefault(value => !value.IsStoryOnly &&
                controller.Catalog.GetEncounter(value.EncounterId).Rules.OfType<DieBindingRuleDefinition>().Any());
            Assert.That(journey, Is.Not.Null, "No authored journey's encounter has a die-binding rule.");
            yield return PresentsTheBindingRule(controller, journey);
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator PresentsTheBindingRule(SpiritEncounterController controller, EncounterJourneyAsset journey)
        {
            string name = journey.Id;
            var rules = controller.Catalog.GetEncounter(journey.EncounterId).Rules.OfType<DieBindingRuleDefinition>().ToArray();
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            ContentReady.SeedJourneyPrerequisites(controller, journey.Id);
            controller.SelectEncounter(journey.EncounterId);
            yield return ContentReady.Routed(controller);
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            var view = ContentReady.EncounterView(controller);
            // Encounters that introduce their additions before play show the pre-encounter screen first.
            if (view.JourneyView.EncounterCharms.IsOpen)
            {
                view.JourneyView.EncounterCharms.BeginButton.onClick.Invoke();
                yield return ContentReady.Routed(controller);
            }
            yield return WaitIdle(controller);
            Assert.That(controller.Snapshot, Is.Not.Null, controller.StatusMessage);
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(journey.EncounterId));
            view = ContentReady.EncounterView(controller);
            var presenters = Presenters(view, rules, name);
            yield return Settle(presenters);
            AssertShows(presenters, controller.Snapshot, name + ": the opening roll");

            // Player rerolls of dice that are neither bound nor marked bind the marked die and grow its roots.
            int reroll = 0;
            while (controller.Snapshot.BoundMask == 0)
            {
                Assert.That(++reroll, Is.LessThanOrEqualTo(DiceValues.Count), name + ": no die bound after repeated rerolls.");
                Assert.That(controller.Snapshot.RerollsRemaining, Is.GreaterThan(0), name + ": the stage needs rerolls to bind a die.");
                int marked = controller.Snapshot.MarkedMask;
                int free = DiceValues.AllMask & ~controller.Snapshot.BoundMask & ~marked;
                controller.ToggleDie(Bay(free));
                controller.Reroll();
                yield return WaitIdle(controller);
                if (marked == 0) continue;
                Assert.That(controller.Snapshot.BoundMask & marked, Is.EqualTo(marked), name + ": the marked die binds.");
            }
            yield return Settle(presenters);
            AssertShows(presenters, controller.Snapshot, name + ": after the binding reroll");

            int bound = controller.Snapshot.BoundMask;
            controller.ToggleDie(Bay(bound));
            Assert.That(controller.SelectedMask, Is.Zero, name + ": a bound die cannot be selected.");

            // Loading the saved run from the investigation board shows its marked and bound dice at once.
            Assert.That(controller.TrySave(), Is.True, controller.StatusMessage);
            var saved = controller.Snapshot;
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            yield return null;
            Assert.That(controller.TryLoad(), Is.True, controller.StatusMessage);
            yield return ContentReady.Routed(controller);
            yield return null;
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(saved.Revision), controller.StatusMessage);
            view = ContentReady.EncounterView(controller);
            presenters = Presenters(view, rules, name);
            for (int bay = 0; bay < DiceValues.Count; ++bay)
                foreach (var presenter in presenters)
                    Assert.That(presenter[bay].IsAnimating, Is.False, name + ": a loaded run shows its state without animation.");
            AssertShows(presenters, saved, name + ": the loaded run");

            // Scoring the hand withdraws every root.
            controller.PlayHand();
            float releaseDeadline = Time.realtimeSinceStartup + 30;
            while (RootsShown(presenters) && Time.realtimeSinceStartup < releaseDeadline) yield return null;
            Assert.That(RootsShown(presenters), Is.False, name + ": the roots withdraw after scoring.");
            yield return WaitIdle(controller);
            if (controller.Snapshot.Phase == EncounterPhase.Playing)
            {
                yield return Settle(presenters);
                AssertShows(presenters, controller.Snapshot, name + ": the next hand");
            }
        }

        /** The presenters of the encounter screen's dice visuals for the encounter's binding rules. */
        private static DieBindingVisualPresenter[] Presenters(SpiritEncounterView view, DieBindingRuleDefinition[] rules, string name)
        {
            var visuals = (EncounterRuleDiceVisuals)typeof(SpiritEncounterView)
                .GetField("ruleDiceVisuals", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            Assert.That(visuals, Is.Not.Null, name + ": the table presents rule dice visuals.");
            var presenters = rules.Select(rule => visuals.Presenters.OfType<DieBindingVisualPresenter>()
                .SingleOrDefault(presenter => presenter.RuleId == rule.Id)).ToArray();
            for (int i = 0; i < rules.Length; ++i)
                Assert.That(presenters[i], Is.Not.Null, name + ": the table presents binding rule " + rules[i].Id + ".");
            return presenters;
        }

        /** Each die shows a mark where the snapshot marks it and roots where it is bound. */
        private static void AssertShows(DieBindingVisualPresenter[] presenters, EncounterSnapshot snapshot, string message)
        {
            for (int bay = 0; bay < DiceValues.Count; ++bay)
            {
                Assert.That(presenters.Max(presenter => presenter[bay].Mark),
                    Is.EqualTo((snapshot.MarkedMask & (1 << bay)) != 0 ? 1f : 0f), message + ": mark " + bay);
                Assert.That(Growth(presenters, bay),
                    Is.EqualTo((snapshot.BoundMask & (1 << bay)) != 0 ? 1f : 0f), message + ": roots " + bay);
            }
        }

        private static float Growth(DieBindingVisualPresenter[] presenters, int bay) => presenters.Max(presenter => presenter[bay].Growth);

        private static bool RootsShown(DieBindingVisualPresenter[] presenters)
            => presenters.Any(presenter => Enumerable.Range(0, DiceValues.Count)
                .Any(bay => presenter[bay].Growth > 0f || presenter[bay].GrowthTarget > 0f));

        /** Waits until every die's visual has finished animating. */
        private static IEnumerator Settle(DieBindingVisualPresenter[] presenters)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (presenters.Any(presenter => Enumerable.Range(0, DiceValues.Count).Any(bay => presenter[bay].IsAnimating)) &&
                Time.realtimeSinceStartup < deadline) yield return null;
        }

        private static int Bay(int mask)
        {
            for (int bay = 0; bay < DiceValues.Count; ++bay)
                if ((mask & (1 << bay)) != 0) return bay;
            throw new System.ArgumentException("No die in the mask.", nameof(mask));
        }

        private static IEnumerator WaitIdle(SpiritEncounterController controller)
        {
            yield return null;
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False, "Presentation did not complete.");
            yield return null;
        }
    }
}
