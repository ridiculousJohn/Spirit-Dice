using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
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
        /**
         * Checks that a disturbed kept die visibly moves after a player reroll, the host reacts, the die shows the
         * disturbed face once playback settles, and the score panel shows no hand rank after the reroll. Fails if the
         * die does not move, the host does not react, the tray shows the undisturbed face or the panel shows a rank.
         */
        [UnityTest]
        public IEnumerator RerollDisturbanceMovesTheKeptDieToItsNewFaceAndTheHostReacts()
        {
            yield return ContentReady.LoadApplication();
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = controller.JourneyCatalog.Entries.FirstOrDefault(value => value != null && !value.IsStoryOnly &&
                controller.Catalog.GetEncounter(value.Encounter.Identity).Rules.OfType<DieInterferenceRuleDefinition>()
                    .Any(rule => rule.AfterPlayerReroll));
            Assert.That(journey, Is.Not.Null, "No authored encounter has a DieInterferenceRuleDefinition acting after a reroll.");
            var encounterId = journey.Encounter.Identity;
            DieDisturbedEvent expected = null;
            ulong seed = 0;
            for (ulong candidate = 1; candidate <= ContentReady.RuleSeedSearchLimit && seed == 0; candidate++)
            {
                var session = EncounterSession.CreateEncounter(controller.Catalog, encounterId, candidate);
                var rerolled = session.Execute(new RerollDiceAction(ContentReady.RerollMask), session.Snapshot.Revision);
                var disturbed = rerolled.Events.OfType<DieDisturbedEvent>().FirstOrDefault();
                if (disturbed == null || disturbed.Before == disturbed.After) continue;
                expected = disturbed;
                seed = candidate;
            }
            Assert.That(seed, Is.Not.Zero, journey.name + ": no searched seed disturbs a kept die after a reroll.");
            string context = journey.name + " " + expected.Kind;
            yield return ContentReady.EnterEncounter(controller, journey, seed);
            var view = ContentReady.EncounterView(controller);
            for (int die = 0; die < DiceValues.Count; die++)
                if ((ContentReady.RerollMask & (1 << die)) != 0) controller.ToggleDie(die);
            Assert.That(controller.SelectedMask, Is.EqualTo(ContentReady.RerollMask), context);
            var target = view.DiceTray.Dice[expected.Bay];
            Quaternion rest = target.localRotation;
            Vector3 restPosition = target.localPosition;
            var resolutions = new List<Resolution>();
            controller.Resolved += resolutions.Add;
            controller.Reroll();
            Assert.That(controller.IsBusy, Is.True, controller.Status.ToString());
            bool moved = false, reacted = false;
            float deadline = Time.realtimeSinceStartup + ContentReady.Timeout;
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
            Assert.That(view.GameplayHud.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(label => label.name == "HandCategory").text,
                Is.Empty,
                context + ": the hand rank shows only while a played hand is scored.");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
