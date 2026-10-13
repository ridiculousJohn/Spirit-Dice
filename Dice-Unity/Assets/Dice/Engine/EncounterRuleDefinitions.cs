using System;

namespace Dice.Engine
{
    public abstract class EncounterRuleDefinition
    {
        public string Id { get; }

        internal EncounterRuleDefinition(string id)
        {
            DefinitionValidation.RequireId(id, nameof(id));
            Id = id;
        }

        internal abstract bool IsAvailable(EncounterState state);
        internal virtual bool WaivesRerollCost(EncounterState state) => false;
        internal virtual int FreeRerollsUsedOnHand(int rerollsSpentOnHand) => 0;
        internal virtual int MaximumFreeRerollsPerHand => 0;
        internal virtual long ScoredHandMultiplierBonus(DiceValues dice, long totalHandsPlayed) => 0;
        internal virtual int FavoredFace(long totalHandsPlayed) => 0;
    }

    public sealed class FirstRerollFreeRuleDefinition : EncounterRuleDefinition
    {
        public const string StableId = "rule.first_reroll_free";

        public FirstRerollFreeRuleDefinition() : base(StableId) { }

        internal override bool IsAvailable(EncounterState state) => state.RerollsSpentOnHand == 0;
        internal override bool WaivesRerollCost(EncounterState state) => IsAvailable(state);
        internal override int FreeRerollsUsedOnHand(int rerollsSpentOnHand) => rerollsSpentOnHand > 0 ? 1 : 0;
        internal override int MaximumFreeRerollsPerHand => 1;
    }

    public sealed class DistinctFacesMultiplierRuleDefinition : EncounterRuleDefinition
    {
        public const string StableId = "rule.distinct_faces_multiplier";

        public DistinctFacesMultiplierRuleDefinition() : base(StableId) { }

        internal override bool IsAvailable(EncounterState state) => true;

        internal override long ScoredHandMultiplierBonus(DiceValues dice, long totalHandsPlayed)
        {
            int faceMask = 0;
            int distinct = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
            {
                int bit = 1 << dice[i];
                if ((faceMask & bit) == 0) ++distinct;
                faceMask |= bit;
            }
            return distinct;
        }
    }

    public sealed class CyclingFavoredFaceMultiplierRuleDefinition : EncounterRuleDefinition
    {
        public const string StableId = "rule.cycling_favored_face_multiplier";

        public CyclingFavoredFaceMultiplierRuleDefinition() : base(StableId) { }

        internal override bool IsAvailable(EncounterState state) => true;

        /** The persisted encounter-wide hand count keeps the cycle stable through stage changes and resume. */
        internal override int FavoredFace(long totalHandsPlayed) => (int)(totalHandsPlayed % 6) + 1;

        internal override long ScoredHandMultiplierBonus(DiceValues dice, long totalHandsPlayed)
        {
            int favoredFace = FavoredFace(totalHandsPlayed);
            long bonus = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if (dice[i] == favoredFace) bonus += 2;
            return bonus;
        }
    }

    public sealed class EncounterRuleSnapshot : IEquatable<EncounterRuleSnapshot>
    {
        public string Id { get; }
        public bool IsAvailable { get; }
        public int FavoredFace { get; }

        internal EncounterRuleSnapshot(EncounterRuleDefinition definition, EncounterState state)
        {
            Id = definition.Id;
            IsAvailable = state.Phase == EncounterPhase.Playing && definition.IsAvailable(state);
            FavoredFace = definition.FavoredFace(state.TotalHandsPlayed);
        }

        public bool Equals(EncounterRuleSnapshot other)
            => other != null && Id == other.Id && IsAvailable == other.IsAvailable && FavoredFace == other.FavoredFace;

        public override bool Equals(object obj) => obj is EncounterRuleSnapshot other && Equals(other);
        public override int GetHashCode() => unchecked((StringComparer.Ordinal.GetHashCode(Id) * 397 ^ IsAvailable.GetHashCode()) * 397 ^ FavoredFace);
    }

    internal static class EncounterRuleResolver
    {
        internal static EncounterRuleDefinition RerollCostWaiver(EncounterDefinition definition, EncounterState state)
        {
            // A reroll has one cost to waive; the first applicable rule owns the activation event.
            foreach (var rule in definition.Rules)
                if (rule.WaivesRerollCost(state)) return rule;
            return null;
        }

        internal static int FreeRerollsUsedOnHand(EncounterDefinition definition, int rerollsSpentOnHand)
        {
            int used = 0;
            foreach (var rule in definition.Rules) used += rule.FreeRerollsUsedOnHand(rerollsSpentOnHand);
            return used;
        }

        internal static int MaximumFreeRerollsPerHand(EncounterDefinition definition)
        {
            int maximum = 0;
            foreach (var rule in definition.Rules) maximum += rule.MaximumFreeRerollsPerHand;
            return maximum;
        }
    }
}
