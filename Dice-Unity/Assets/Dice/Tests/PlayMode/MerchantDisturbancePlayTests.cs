using System.Collections;
using System.Collections.Generic;
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
     * Playback of the poltergeist in the merchant encounter: when it changes a die on Play Hand, the score panel names
     * the rank that will be scored once the disturbed die lands. The case searches session seeds against the shipped
     * catalog for an opening whose rule draws produce it.
     */
    public sealed class MerchantDisturbancePlayTests
    {
        private const string EncounterId = "encounter.merchant.unquiet_cart";
        private const ulong SeedSearchLimit = 4000;

        [UnityTest]
        public IEnumerator PlayHandDisturbanceNamesTheScoredRankWhenTheDieLands()
        {
            ScoreBreakdown scored = null;
            SpiritEncounterController controller = null;
            yield return Begin(catalog =>
            {
                for (ulong candidate = 1; candidate <= SeedSearchLimit; candidate++)
                {
                    var session = EncounterSession.CreateEncounter(catalog, EncounterId, candidate);
                    var opened = session.Snapshot.CurrentDice;
                    var played = session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
                    var disturbed = played.Events.OfType<DieDisturbedEvent>().SingleOrDefault();
                    if (disturbed == null || disturbed.Before == disturbed.After) continue;
                    var score = played.Events.OfType<HandScoredEvent>().Single().Score;
                    if (score.Category == HandEvaluator.Evaluate(opened)) continue;
                    scored = score;
                    return candidate;
                }
                Assert.Fail("No searched seed disturbs a played hand into another rank.");
                return 0;
            }, value => controller = value);
            var view = ContentReady.EncounterView(controller);
            HandCategory opening = HandEvaluator.Evaluate(controller.Snapshot.CurrentDice);
            Assert.That(view.GameplayHud.CategoryLabel.text, Is.EqualTo(Rank(opening)), "The panel previews the undisturbed rank.");
            var resolutions = new List<Resolution>();
            controller.Resolved += resolutions.Add;
            controller.PlayHand();

            float deadline = Time.realtimeSinceStartup + 10;
            while (!view.DiceTray.IsRolling && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(view.DiceTray.IsRolling, Is.True, "The poltergeist rolls the disturbed die.");
            while (view.DiceTray.IsRolling && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(view.DiceTray.IsRolling, Is.False, "The disturbed die lands.");
            yield return null;
            Assert.That(view.GameplayHud.CategoryLabel.text, Is.EqualTo(Rank(scored.Category)),
                "The panel names the scored rank once the disturbed die lands.");

            deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            controller.Resolved -= resolutions.Add;
            Assert.That(controller.IsBusy, Is.False, "Playback completes.");
            Assert.That(resolutions, Has.Count.EqualTo(1));
            Assert.That(resolutions[0].Events.OfType<HandScoredEvent>().Single().Score.Category, Is.EqualTo(scored.Category),
                "The rank shown on landing is the rank that was scored.");
            LogAssert.NoUnexpectedReceived();
        }

        private static string Rank(HandCategory category) => DiagnosticText.Humanize(category.ToString()).ToUpperInvariant();

        /** Opens the merchant encounter with a fixed session seed and waits for its opening roll. */
        private static IEnumerator Begin(System.Func<MechanicalCatalog, ulong> chooseSeed, System.Action<SpiritEncounterController> ready)
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = controller.JourneyCatalog.Entries.Single(value => !value.IsStoryOnly && value.EncounterId == EncounterId);
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            ContentReady.SeedJourneyPrerequisites(controller, journey.Id);
            controller.SelectEncounter(journey.EncounterId);
            yield return ContentReady.Routed(controller);
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();
            ulong seed = chooseSeed(controller.Catalog);
            controller.ConfigureSeedPolicy(true, seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            yield return null;
            ready(controller);
        }
    }
}
