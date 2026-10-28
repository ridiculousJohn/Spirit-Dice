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
     * Playback of the bay ward rule kind on the first authored encounter whose catalog definition has a
     * BayWardRuleDefinition and a die interference rule it can repel. The case searches session seeds against the
     * shipped catalog for an attempt on a bay, moves the ward's piece onto that bay through the controller, and plays
     * the committed action through the real engine and playback.
     */
    public sealed class BayWardRulePlayTests
    {
        private const ulong SeedSearchLimit = 4000;
        // Player rerolls take the two leftmost dice, so a reroll attempt targets one of the kept three.
        private const int RerollMask = 0b00011;

        /**
         * Checks, on the first encounter with a ward, that an interference attempt on the bay occupied by the ward's
         * piece is reported as blocked by that ward, that the piece flares and the host reacts during playback, and that
         * a kept die attacked after a reroll keeps its face on the tray. Fails if the attempt changes the die, names
         * another ward or bay, or the piece does not flare.
         */
        [UnityTest]
        public IEnumerator WardedBayRepelsTheInterferenceAndItsPieceFlares()
        {
            yield return LoadApplication();
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            (string Journey, BayWardRuleDefinition Ward)? found = null;
            foreach (var journey in controller.JourneyCatalog.Entries.Where(value => value != null && !value.IsStoryOnly))
            {
                var definition = controller.Catalog.GetEncounter(journey.EncounterId);
                var ward = definition.Rules.OfType<BayWardRuleDefinition>().FirstOrDefault(value =>
                    definition.Rules.OfType<DieInterferenceRuleDefinition>().Any(rule => Repels(value, rule)));
                if (ward == null) continue;
                found = (journey.Id, ward);
                break;
            }
            Assert.That(found, Is.Not.Null,
                "No authored encounter has a BayWardRuleDefinition together with a die interference rule it can repel.");
            var encounter = controller.Catalog.GetEncounter(controller.JourneyCatalog.Find(found.Value.Journey).EncounterId);
            yield return RepelOnWardedBay(controller, found.Value.Journey, encounter, found.Value.Ward);
        }

        private static IEnumerator RepelOnWardedBay(SpiritEncounterController controller, string journeyId,
            EncounterDefinition encounter, BayWardRuleDefinition ward)
        {
            // An attempt after a player reroll leaves the attacked die on the board; otherwise the ward meets Play Hand.
            bool afterReroll = encounter.Rules.OfType<DieInterferenceRuleDefinition>()
                .Any(rule => rule.AfterPlayerReroll && Repels(ward, rule));
            EncounterAction Action() => afterReroll ? (EncounterAction)new RerollDiceAction(RerollMask) : PlayHandAction.Instance;
            string context = encounter.Id + " " + ward.Id;
            ulong seed = 0;
            int bay = -1;
            for (ulong candidate = 1; candidate <= SeedSearchLimit && seed == 0; candidate++)
            {
                var unwarded = EncounterSession.CreateEncounter(controller.Catalog, encounter.Id, candidate);
                var attempt = unwarded.Execute(Action(), unwarded.Snapshot.Revision).Events.OfType<DieDisturbedEvent>()
                    .FirstOrDefault(value => ward.Blocks(value.Kind));
                if (attempt == null) continue;
                // Moving the piece draws nothing, so the same rule draws target the bay it now wards.
                var warded = EncounterSession.CreateEncounter(controller.Catalog, encounter.Id, candidate);
                var moved = warded.Execute(new InteractWithPieceAction(ward.PieceId, ward.InteractionId,
                    new MovePieceInput(PiecePosition.Bay(attempt.Bay))), warded.Snapshot.Revision);
                if (!moved.Accepted) continue;
                var blocked = warded.Execute(Action(), warded.Snapshot.Revision).Events.OfType<DisturbanceBlockedEvent>()
                    .FirstOrDefault(value => value.Bay == attempt.Bay && value.ProtectingRuleId == ward.Id);
                if (blocked == null) continue;
                seed = candidate;
                bay = attempt.Bay;
            }
            Assert.That(seed, Is.Not.Zero, context + ": no searched seed attempts interference the ward can repel.");
            yield return Begin(controller, journeyId, seed);
            var view = ContentReady.EncounterView(controller);
            var piece = view.TablePieces.SingleOrDefault(value => value.PieceId == ward.PieceId && value.InteractionId == ward.InteractionId);
            Assert.That(piece, Is.Not.Null, context + ": the table presents no prop for the ward's piece.");
            yield return Move(controller, piece, PiecePosition.Bay(bay));
            if (afterReroll)
                for (int die = 0; die < DiceValues.Count; die++)
                    if ((RerollMask & (1 << die)) != 0) controller.ToggleDie(die);
            int keptFace = controller.Snapshot.CurrentDice[bay];
            var resolutions = new List<Resolution>();
            controller.Resolved += resolutions.Add;
            if (afterReroll) controller.Reroll();
            else controller.PlayHand();
            Assert.That(controller.IsBusy, Is.True, controller.Status.ToString());
            bool flared = false, reacted = false;
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                flared |= piece.IsFlaring;
                reacted |= view.HostPortrait.IsReacting;
                yield return null;
            }
            controller.Resolved -= resolutions.Add;
            Assert.That(controller.IsBusy, Is.False, context + ": playback completes.");
            Assert.That(resolutions, Has.Count.EqualTo(1), context);
            var events = resolutions[0].Events;
            var repelled = events.OfType<DisturbanceBlockedEvent>().Single();
            Assert.That(repelled.Bay, Is.EqualTo(bay), context);
            Assert.That(repelled.ProtectingRuleId, Is.EqualTo(ward.Id), context);
            Assert.That(events.OfType<DieDisturbedEvent>().Where(value => value.Bay == bay), Is.Empty, context);
            Assert.That(flared, Is.True, context + ": the ward's piece must flare when it repels the attempt.");
            Assert.That(reacted, Is.True, context + ": the host must react to the repelled attempt.");
            if (afterReroll)
            {
                Assert.That(controller.Snapshot.CurrentDice[bay], Is.EqualTo(keptFace), context);
                Assert.That(view.DiceTray.GetVisibleFace(bay), Is.EqualTo(keptFace), context + ": the warded die keeps its face.");
            }
            LogAssert.NoUnexpectedReceived();
        }

        /** A ward can repel an interference rule that acts at some point with an effect the ward blocks. */
        private static bool Repels(BayWardRuleDefinition ward, DieInterferenceRuleDefinition rule)
            => rule.ChancePercent > 0 && (rule.AfterPlayerReroll || rule.AfterPlayHand) &&
                (rule.ForcedRerollWeight > 0 && ward.Blocks(DieInterferenceKind.ForcedReroll) ||
                    rule.FlipWeight > 0 && ward.Blocks(DieInterferenceKind.Flip));

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
            Assert.That(controller.Snapshot, Is.Not.Null, controller.Status.ToString());
            yield return null;
        }

        /** Moves the piece through the controller and waits for its prop to settle at the new position. */
        private static IEnumerator Move(SpiritEncounterController controller, SpiritTablePieceView piece, PiecePosition position)
        {
            var placement = piece.GetComponent<SpiritTablePiecePosition>();
            if (placement.Position != position)
            {
                controller.InteractWithPiece(piece.PieceId, piece.InteractionId, new MovePieceInput(position));
                float deadline = Time.realtimeSinceStartup + 10;
                while ((controller.IsBusy || placement.Position != position) && Time.realtimeSinceStartup < deadline) yield return null;
            }
            Assert.That(placement.Position, Is.EqualTo(position), piece.PieceId);
            Vector3 previous = piece.transform.position;
            int stable = 0;
            float settle = Time.realtimeSinceStartup + 5;
            while (stable < 3 && Time.realtimeSinceStartup < settle)
            {
                yield return null;
                stable = (piece.transform.position - previous).sqrMagnitude < 1e-6f ? stable + 1 : 0;
                previous = piece.transform.position;
            }
            Assert.That(stable, Is.EqualTo(3), piece.PieceId + " must settle.");
        }
    }
}
