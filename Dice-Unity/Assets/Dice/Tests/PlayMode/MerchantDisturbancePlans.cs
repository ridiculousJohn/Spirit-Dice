using System.Collections;
using System.Globalization;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    /**
     * The merchant encounter's disturbances for captures that show them: searches session seeds against a catalog for an
     * opening whose rule draws produce a disturbance case, so a capture can play that seed through the real controller,
     * engine and playback, and moves the candle to a position as the player's drag does. Player rerolls take the dice of
     * ContentReady.RerollMask, so an attempt after a reroll targets one of the kept dice.
     */
    internal static class MerchantDisturbancePlans
    {
        internal enum Case { FlipAfterReroll, ForcedRerollOnPlayHand, BlockedByCandle }

        /** A searched seed, the opening it rolls, and the bay its first interference attempt targets. */
        internal sealed class Plan
        {
            public ulong Seed;
            public DiceValues Opening;
            public int Bay;
            public bool Preferred;
            public string Summary;
        }

        /** The first seed whose interference produces the case, preferring one whose result changes the hand rank. */
        internal static Plan FindPlan(MechanicalCatalog catalog, EncounterDefinition.Id encounterId, Case kind, BayWardRuleDefinition ward)
        {
            Plan fallback = null;
            for (ulong seed = 1; seed <= ContentReady.RuleSeedSearchLimit; seed++)
            {
                var plan = Trial(catalog, encounterId, kind, ward, seed);
                if (plan == null) continue;
                if (plan.Preferred) return plan;
                if (fallback == null) fallback = plan;
            }
            Assert.That(fallback, Is.Not.Null, "No searched seed produces " + kind);
            return fallback;
        }

        private static Plan Trial(MechanicalCatalog catalog, EncounterDefinition.Id encounterId, Case kind, BayWardRuleDefinition ward, ulong seed)
        {
            var session = EncounterSession.CreateEncounter(catalog, encounterId, seed);
            var opening = session.Snapshot.CurrentDice;
            var plan = new Plan { Seed = seed, Opening = opening };
            if (kind == Case.ForcedRerollOnPlayHand)
            {
                var played = session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
                var disturbed = played.Events.OfType<DieDisturbedEvent>().SingleOrDefault();
                if (disturbed == null || disturbed.Kind != DieInterferenceKind.ForcedReroll || disturbed.After == disturbed.Before)
                    return null;
                var scored = played.Events.OfType<HandScoredEvent>().Single().Dice;
                plan.Bay = disturbed.Bay;
                plan.Preferred = HandEvaluator.Evaluate(HandDice.Full(opening)) != HandEvaluator.Evaluate(HandDice.Full(scored));
                plan.Summary = "seed " + seed + ", opening " + Faces(opening) + ", Play Hand; bay " + (plan.Bay + 1) +
                    " forced reroll " + disturbed.Before + " -> " + disturbed.After + ", scored " + Faces(scored) + " as " +
                    HandEvaluator.Evaluate(HandDice.Full(scored)) + " (undisturbed " + HandEvaluator.Evaluate(HandDice.Full(opening)) + ")";
                return plan;
            }

            var rerolled = session.Execute(new RerollDiceAction(ContentReady.RerollMask), session.Snapshot.Revision);
            var rolled = rerolled.Events.OfType<DiceRolledEvent>().Single();
            var disturbance = rerolled.Events.OfType<DieDisturbedEvent>().SingleOrDefault();
            if (disturbance == null) return null;
            plan.Bay = disturbance.Bay;
            if (kind == Case.FlipAfterReroll)
            {
                if (disturbance.Kind != DieInterferenceKind.Flip) return null;
                var flipped = With(rolled.After, disturbance.Bay, disturbance.After);
                plan.Preferred = HandEvaluator.Evaluate(HandDice.Full(rolled.After)) != HandEvaluator.Evaluate(HandDice.Full(flipped));
                plan.Summary = "seed " + seed + ", opening " + Faces(opening) + ", reroll dice 1-2 -> " + Faces(rolled.After) +
                    "; bay " + (plan.Bay + 1) + " flipped " + disturbance.Before + " -> " + disturbance.After + ", hand " +
                    Faces(flipped) + " as " + HandEvaluator.Evaluate(HandDice.Full(flipped)) + " (undisturbed " + HandEvaluator.Evaluate(HandDice.Full(rolled.After)) + ")";
                return plan;
            }

            // Moving the candle draws nothing, so the same rule draws target the bay it now wards.
            var warded = EncounterSession.CreateEncounter(catalog, encounterId, seed);
            var moved = warded.Execute(new InteractWithPieceAction(ward.PieceId, catalog.GetEncounter(encounterId).Pieces
                .Single(value => value.Identity == ward.PieceId).IndexOf<PositionInteractionDefinition>(),
                new MovePieceInput(PiecePosition.Bay(plan.Bay))), warded.Snapshot.Revision);
            if (!moved.Accepted) return null;
            var attempted = warded.Execute(new RerollDiceAction(ContentReady.RerollMask), warded.Snapshot.Revision);
            var blocked = attempted.Events.OfType<DisturbanceBlockedEvent>().SingleOrDefault();
            if (blocked == null || blocked.Bay != plan.Bay) return null;
            plan.Preferred = true;
            plan.Summary = "seed " + seed + ", opening " + Faces(opening) + ", candle on bay " + (plan.Bay + 1) +
                ", reroll dice 1-2 -> " + Faces(attempted.Events.OfType<DiceRolledEvent>().Single().After) + "; " + blocked.Kind +
                " on bay " + (plan.Bay + 1) + " blocked by " + blocked.ProtectingRuleId;
            return plan;
        }

        private static DiceValues With(DiceValues dice, int bay, int face)
        {
            var faces = new int[DiceValues.Count];
            for (int die = 0; die < faces.Length; die++) faces[die] = die == bay ? face : dice[die];
            return new DiceValues(faces[0], faces[1], faces[2], faces[3], faces[4]);
        }

        internal static string Faces(DiceValues dice)
            => string.Join(" ", Enumerable.Range(0, DiceValues.Count).Select(die => dice[die].ToString(CultureInfo.InvariantCulture)));

        /** Moves the piece to the position through the controller, as a drag released over it does, and waits for it to settle there. */
        internal static IEnumerator Move(SpiritEncounterController controller, SpiritTablePieceView piece, PiecePosition position)
        {
            var placement = piece.GetComponent<SpiritTablePiecePosition>();
            if (placement.Position != position)
            {
                controller.InteractWithPiece(piece.Piece.Identity, controller.Snapshot.PieceInteractions
                    .Single(value => value.PieceId == piece.Piece.Identity && value.State is PiecePositionState).InteractionIndex,
                    new MovePieceInput(position));
                float deadline = Time.realtimeSinceStartup + 10;
                while ((controller.IsBusy || placement.Position != position) && Time.realtimeSinceStartup < deadline) yield return null;
            }
            Assert.That(placement.Position, Is.EqualTo(position), piece.name);
            Vector3 previous = piece.transform.position;
            int stable = 0;
            float settle = Time.realtimeSinceStartup + 5;
            while (stable < 3 && Time.realtimeSinceStartup < settle)
            {
                yield return null;
                stable = (piece.transform.position - previous).sqrMagnitude < 1e-6f ? stable + 1 : 0;
                previous = piece.transform.position;
            }
            Assert.That(stable, Is.EqualTo(3), piece.name + " must settle.");
        }
    }
}
