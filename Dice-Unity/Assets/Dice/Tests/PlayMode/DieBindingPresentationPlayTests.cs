using System.Collections;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
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
            yield return ContentReady.LoadApplication();
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = controller.JourneyCatalog.Entries.FirstOrDefault(value => value.Encounters.Any() &&
                controller.Catalog.GetEncounter(value.Encounters.First().Identity).Rules.OfType<DieBindingRuleDefinition>().Any());
            Assert.That(journey, Is.Not.Null, "No authored journey's encounter has a die-binding rule.");
            yield return PresentsTheBindingRule(controller, journey);
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator PresentsTheBindingRule(SpiritEncounterController controller, EncounterJourneyAsset journey)
        {
            string name = journey.name;
            var rules = controller.Catalog.GetEncounter(journey.Encounters.First().Identity).Rules.OfType<DieBindingRuleDefinition>().ToArray();
            yield return ContentReady.EnterEncounter(controller, journey, ContentReady.Seed);
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(journey.Encounters.First().Identity));
            var view = ContentReady.EncounterView(controller);
            var visuals = Visuals(view, rules.Length, name);
            yield return Settle(visuals);
            AssertShows(visuals, controller.Snapshot, name + ": the opening roll");

            // Player rerolls of dice that are neither bound nor marked bind the marked die and grow its roots.
            int reroll = 0;
            while (controller.Snapshot.Binding().BoundMask == 0)
            {
                Assert.That(++reroll, Is.LessThanOrEqualTo(DiceValues.Count), name + ": no die bound after repeated rerolls.");
                Assert.That(controller.Snapshot.RerollsRemaining, Is.GreaterThan(0), name + ": the stage needs rerolls to bind a die.");
                int marked = controller.Snapshot.Binding().MarkedMask;
                int free = DiceValues.AllMask & ~controller.Snapshot.Binding().BoundMask & ~marked;
                controller.ToggleDie(Bay(free));
                controller.Reroll();
                yield return ContentReady.Idle(controller);
                if (marked == 0) continue;
                Assert.That(controller.Snapshot.Binding().BoundMask & marked, Is.EqualTo(marked), name + ": the marked die binds.");
            }
            yield return Settle(visuals);
            AssertShows(visuals, controller.Snapshot, name + ": after the binding reroll");

            int bound = controller.Snapshot.Binding().BoundMask;
            controller.ToggleDie(Bay(bound));
            Assert.That(controller.SelectedMask, Is.Zero, name + ": a bound die cannot be selected.");

            // Loading the saved run from the investigation board shows its marked and bound dice at once.
            Assert.That(controller.TrySave(), Is.True, controller.Status.ToString());
            var saved = controller.Snapshot;
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            yield return null;
            Assert.That(controller.TryLoad(), Is.True, controller.Status.ToString());
            yield return ContentReady.Routed(controller);
            yield return null;
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(saved.Revision), controller.Status.ToString());
            view = ContentReady.EncounterView(controller);
            visuals = Visuals(view, rules.Length, name);
            foreach (var die in visuals)
                foreach (var visual in die)
                    Assert.That(visual.IsAnimating, Is.False, name + ": a loaded run shows its state without animation.");
            AssertShows(visuals, saved, name + ": the loaded run");

            // Scoring the hand withdraws every root.
            controller.PlayHand();
            yield return ContentReady.WaitUntil(() => !RootsShown(visuals), name + ": the roots withdraw after scoring.");
            yield return ContentReady.Idle(controller);
            if (controller.Snapshot.Phase == EncounterPhase.Playing)
            {
                yield return Settle(visuals);
                AssertShows(visuals, controller.Snapshot, name + ": the next hand");
            }
        }

        /**
         * The binding visuals under each of the tray's dice, one per binding rule: the table's binding presenters create
         * them as children of the dice they follow.
         */
        private static DieBindingVisual[][] Visuals(SpiritEncounterView view, int ruleCount, string name)
        {
            var visuals = new DieBindingVisual[DiceValues.Count][];
            for (int bay = 0; bay < DiceValues.Count; ++bay)
            {
                visuals[bay] = view.DiceTray.Dice[bay].GetComponentsInChildren<DieBindingVisual>(true);
                Assert.That(visuals[bay].Length, Is.EqualTo(ruleCount), name + ": each binding rule shows a visual on die " + bay + ".");
            }
            return visuals;
        }

        /** Each die shows a mark where the snapshot marks it and roots where it is bound. */
        private static void AssertShows(DieBindingVisual[][] visuals, EncounterSnapshot snapshot, string message)
        {
            for (int bay = 0; bay < DiceValues.Count; ++bay)
            {
                Assert.That(visuals[bay].Max(visual => visual.Mark),
                    Is.EqualTo((snapshot.Binding().MarkedMask & (1 << bay)) != 0 ? 1f : 0f), message + ": mark " + bay);
                Assert.That(visuals[bay].Max(visual => visual.Growth),
                    Is.EqualTo((snapshot.Binding().BoundMask & (1 << bay)) != 0 ? 1f : 0f), message + ": roots " + bay);
            }
        }

        private static bool RootsShown(DieBindingVisual[][] visuals)
            => visuals.Any(die => die.Any(visual => visual.Growth > 0f || visual.GrowthTarget > 0f));

        /** Waits until every die's visual has finished animating. */
        private static IEnumerator Settle(DieBindingVisual[][] visuals)
        {
            yield return ContentReady.WaitUntil(() => !visuals.Any(die => die.Any(visual => visual.IsAnimating)), "The binding visuals settle.");
        }

        private static int Bay(int mask)
        {
            for (int bay = 0; bay < DiceValues.Count; ++bay)
                if ((mask & (1 << bay)) != 0) return bay;
            throw new System.ArgumentException("No die in the mask.", nameof(mask));
        }
    }
}
