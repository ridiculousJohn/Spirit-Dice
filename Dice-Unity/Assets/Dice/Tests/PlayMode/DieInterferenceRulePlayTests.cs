using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Resolution = Dice.Engine.Resolution;

namespace Dice.Tests
{
    /**
     * Playback of the die interference rule kind on the first authored encounter whose catalog definition has a
     * DieInterferenceRuleDefinition acting after a player reroll. The case searches session seeds against the shipped
     * catalog for an opening whose rule draws produce a disturbance, then plays that seed through the real controller,
     * engine and playback.
     */
    public sealed class DieInterferenceRulePlayTests
    {
        private const ulong SeedSearchLimit = 4000;
        // Player rerolls take the two leftmost dice, so a reroll disturbance targets one of the kept three.
        private const int RerollMask = 0b00011;

        /**
         * Checks that a disturbed kept die visibly moves after a player reroll, the host reacts, the die shows the
         * disturbed face once playback settles, and the score panel names the rank of the disturbed hand. Fails if the
         * die does not move, the host does not react, or the tray or panel shows the undisturbed result.
         */
        [UnityTest]
        public IEnumerator RerollDisturbanceMovesTheKeptDieToItsNewFaceAndTheHostReacts()
        {
            yield return LoadApplication();
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = controller.JourneyCatalog.Entries.FirstOrDefault(value => value != null && !value.IsStoryOnly &&
                controller.Catalog.GetEncounter(value.EncounterId).Rules.OfType<DieInterferenceRuleDefinition>()
                    .Any(rule => rule.AfterPlayerReroll));
            Assert.That(journey, Is.Not.Null, "No authored encounter has a DieInterferenceRuleDefinition acting after a reroll.");
            string encounterId = journey.EncounterId;
            DieDisturbedEvent expected = null;
            ulong seed = 0;
            for (ulong candidate = 1; candidate <= SeedSearchLimit && seed == 0; candidate++)
            {
                var session = EncounterSession.CreateEncounter(controller.Catalog, encounterId, candidate);
                var rerolled = session.Execute(new RerollDiceAction(RerollMask), session.Snapshot.Revision);
                var disturbed = rerolled.Events.OfType<DieDisturbedEvent>().FirstOrDefault();
                if (disturbed == null || disturbed.Before == disturbed.After) continue;
                expected = disturbed;
                seed = candidate;
            }
            Assert.That(seed, Is.Not.Zero, encounterId + ": no searched seed disturbs a kept die after a reroll.");
            string context = encounterId + " " + expected.Kind;
            yield return Begin(controller, journey.Id, seed);
            var view = ContentReady.EncounterView(controller);
            for (int die = 0; die < DiceValues.Count; die++)
                if ((RerollMask & (1 << die)) != 0) controller.ToggleDie(die);
            Assert.That(controller.SelectedMask, Is.EqualTo(RerollMask), context);
            var target = ContentReady.Die(view.DiceTray, expected.Bay);
            Quaternion rest = target.localRotation;
            Vector3 restPosition = target.localPosition;
            var resolutions = new List<Resolution>();
            controller.Resolved += resolutions.Add;
            controller.Reroll();
            Assert.That(controller.IsBusy, Is.True, controller.StatusMessage);
            bool moved = false, reacted = false;
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                moved |= Quaternion.Angle(target.localRotation, rest) > 1f || (target.localPosition - restPosition).magnitude > .001f;
                reacted |= view.HostPortrait.IsReacting;
                yield return null;
            }
            controller.Resolved -= resolutions.Add;
            Assert.That(controller.IsBusy, Is.False, context + ": playback completes.");
            Assert.That(resolutions, Has.Count.EqualTo(1), context);
            var disturbedEvent = resolutions[0].Events.OfType<DieDisturbedEvent>().Single();
            Assert.That(disturbedEvent.Bay, Is.EqualTo(expected.Bay), context);
            Assert.That(disturbedEvent.Kind, Is.EqualTo(expected.Kind), context);
            Assert.That(moved, Is.True, context + ": the disturbed kept die must visibly move.");
            Assert.That(reacted, Is.True, context + ": the host must react to the disturbance.");
            Assert.That(controller.Snapshot.CurrentDice[expected.Bay], Is.EqualTo(expected.After), context);
            Assert.That(view.DiceTray.GetVisibleFace(expected.Bay), Is.EqualTo(expected.After),
                context + ": the disturbed die shows its new face.");
            Assert.That(view.GameplayHud.CategoryLabel.text, Is.EqualTo(Rank(controller.Preview.Score.Category)),
                context + ": the panel names the rank of the disturbed hand.");
            LogAssert.NoUnexpectedReceived();
        }

        private static string Rank(HandCategory category) => DiagnosticText.Humanize(category.ToString()).ToUpperInvariant();

        private static IEnumerator LoadApplication()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
        }

        /** Opens the journey's encounter with a fixed session seed and waits for its opening roll. */
        private static IEnumerator Begin(SpiritEncounterController controller, string journeyId, ulong seed)
        {
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            ContentReady.SeedJourneyPrerequisites(controller, journeyId);
            controller.SelectEncounter(journeyId);
            yield return ContentReady.Routed(controller);
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();
            controller.ConfigureSeedPolicy(true, seed.ToString(CultureInfo.InvariantCulture));
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.Snapshot, Is.Not.Null, controller.StatusMessage);
            yield return null;
        }
    }
}
