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
     * Playback of the bay ward rule kind on the first authored encounter whose catalog definition has a
     * BayWardRuleDefinition and a die interference rule it can repel. The case searches session seeds against the
     * shipped catalog for an attempt on a bay, moves the ward's piece onto that bay through the controller, and plays
     * the committed action through the real engine and playback.
     */
    public sealed class BayWardRulePlayTests
    {
        /**
         * Checks, on the first encounter with a ward, that an interference attempt on the bay occupied by the ward's
         * piece is reported as blocked by that ward, that the piece flares and the host reacts during playback, and that
         * a kept die attacked after a reroll keeps its face on the tray. Fails if the attempt changes the die, names
         * another ward or bay, or the piece does not flare.
         */
        [UnityTest]
        public IEnumerator WardedBayRepelsTheInterferenceAndItsPieceFlares()
        {
            yield return ContentReady.LoadApplication();
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            (EncounterJourneyAsset Journey, BayWardRuleDefinition Ward)? found = null;
            foreach (var journey in controller.JourneyCatalog.Entries.Where(value => value != null && !value.IsStoryOnly))
            {
                var definition = controller.Catalog.GetEncounter(journey.Encounter.Identity);
                var ward = definition.Rules.OfType<BayWardRuleDefinition>().FirstOrDefault(value =>
                    definition.Rules.OfType<DieInterferenceRuleDefinition>().Any(rule => Repels(value, rule)));
                if (ward == null) continue;
                found = (journey, ward);
                break;
            }
            Assert.That(found, Is.Not.Null,
                "No authored encounter has a BayWardRuleDefinition together with a die interference rule it can repel.");
            var encounter = controller.Catalog.GetEncounter(found.Value.Journey.Encounter.Identity);
            yield return RepelOnWardedBay(controller, found.Value.Journey, encounter, found.Value.Ward);
        }

        private static IEnumerator RepelOnWardedBay(SpiritEncounterController controller, EncounterJourneyAsset journey,
            EncounterDefinition encounter, BayWardRuleDefinition ward)
        {
            // An attempt after a player reroll leaves the attacked die on the board; otherwise the ward meets Play Hand.
            bool afterReroll = encounter.Rules.OfType<DieInterferenceRuleDefinition>()
                .Any(rule => rule.AfterPlayerReroll && Repels(ward, rule));
            EncounterAction Action() => afterReroll ? (EncounterAction)new RerollDiceAction(ContentReady.RerollMask) : PlayHandAction.Instance;
            string context = journey.name + " " + ward.Id;
            ulong seed = 0;
            int bay = -1;
            for (ulong candidate = 1; candidate <= ContentReady.RuleSeedSearchLimit && seed == 0; candidate++)
            {
                var unwarded = EncounterSession.CreateEncounter(controller.Catalog, encounter.Identity, candidate);
                var attempt = unwarded.Execute(Action(), unwarded.Snapshot.Revision).Events.OfType<DieDisturbedEvent>()
                    .FirstOrDefault(value => ward.Blocks(value.Kind));
                if (attempt == null) continue;
                // Moving the piece draws nothing, so the same rule draws target the bay it now wards.
                var warded = EncounterSession.CreateEncounter(controller.Catalog, encounter.Identity, candidate);
                var moved = warded.Execute(new InteractWithPieceAction(ward.PieceId, PositionIndex(encounter, ward),
                    new MovePieceInput(PiecePosition.Bay(attempt.Bay))), warded.Snapshot.Revision);
                if (!moved.Accepted) continue;
                var blocked = warded.Execute(Action(), warded.Snapshot.Revision).Events.OfType<DisturbanceBlockedEvent>()
                    .FirstOrDefault(value => value.Bay == attempt.Bay && value.ProtectingRuleId == ward.Id);
                if (blocked == null) continue;
                seed = candidate;
                bay = attempt.Bay;
            }
            Assert.That(seed, Is.Not.Zero, context + ": no searched seed attempts interference the ward can repel.");
            yield return ContentReady.EnterEncounter(controller, journey, seed);
            var view = ContentReady.EncounterView(controller);
            var piece = SpiritTablePieceView.Find(view.TablePieces, ward.PieceId);
            Assert.That(piece, Is.Not.Null, context + ": the table presents no prop for the ward's piece.");
            yield return Move(controller, piece, PiecePosition.Bay(bay));
            if (afterReroll)
                for (int die = 0; die < DiceValues.Count; die++)
                    if ((ContentReady.RerollMask & (1 << die)) != 0) controller.ToggleDie(die);
            int keptFace = controller.Snapshot.CurrentDice[bay];
            var resolutions = new List<Resolution>();
            controller.Resolved += resolutions.Add;
            if (afterReroll) controller.Reroll();
            else controller.PlayHand();
            Assert.That(controller.IsBusy, Is.True, controller.Status.ToString());
            bool flared = false, reacted = false;
            float deadline = Time.realtimeSinceStartup + ContentReady.Timeout;
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

        /** The place of the ward's piece's position interaction in the piece's interaction list. */
        private static int PositionIndex(EncounterDefinition encounter, BayWardRuleDefinition ward)
            => encounter.Pieces.Single(value => value.Identity == ward.PieceId).IndexOf<PositionInteractionDefinition>();

        /** Moves the piece through the controller and waits for its prop to settle at the new position. */
        private static IEnumerator Move(SpiritEncounterController controller, SpiritTablePieceView piece, PiecePosition position)
        {
            var placement = piece.GetComponent<SpiritTablePiecePosition>();
            if (placement.Position != position)
            {
                controller.InteractWithPiece(piece.Piece.Identity, controller.Snapshot.PieceInteractions
                    .Single(value => value.PieceId == piece.Piece.Identity && value.State is PiecePositionState).InteractionIndex,
                    new MovePieceInput(position));
                yield return ContentReady.WaitUntil(() => !controller.IsBusy && placement.Position == position, piece.name + " must move.");
            }
            Assert.That(placement.Position, Is.EqualTo(position), piece.name);
            // The prop has settled once it holds still for a few frames.
            const int SettledFrames = 3;
            Vector3 previous = piece.transform.position;
            int stable = 0;
            float settle = Time.realtimeSinceStartup + ContentReady.Timeout;
            while (stable < SettledFrames && Time.realtimeSinceStartup < settle)
            {
                yield return null;
                stable = (piece.transform.position - previous).sqrMagnitude < 1e-6f ? stable + 1 : 0;
                previous = piece.transform.position;
            }
            Assert.That(stable, Is.EqualTo(SettledFrames), piece.name + " must settle.");
        }
    }
}
