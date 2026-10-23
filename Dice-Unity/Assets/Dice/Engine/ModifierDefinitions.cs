using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public enum ModifierAvailabilityScope { Global, Spirit, Encounter }
    public enum ModifierAccess { Starter, PermanentUnlock, EncounterExclusive }

    public sealed class ModifierAvailabilityDefinition
    {
        public int PowerTier { get; }
        public ModifierAvailabilityScope Scope { get; }
        public string ScopeId { get; }
        public ModifierAccess Access { get; }

        public ModifierAvailabilityDefinition(int powerTier = 1, ModifierAvailabilityScope scope = ModifierAvailabilityScope.Global,
            string scopeId = null, ModifierAccess access = ModifierAccess.Starter)
        {
            if (powerTier < 1) throw new ArgumentOutOfRangeException(nameof(powerTier));
            if (!Enum.IsDefined(typeof(ModifierAvailabilityScope), scope)) throw new ArgumentOutOfRangeException(nameof(scope));
            if (!Enum.IsDefined(typeof(ModifierAccess), access)) throw new ArgumentOutOfRangeException(nameof(access));
            if (scope == ModifierAvailabilityScope.Global)
            {
                if (scopeId != null || access == ModifierAccess.EncounterExclusive)
                    throw new ArgumentException("Global modifiers cannot have a scope ID or exclusive access.");
            }
            else DefinitionValidation.RequireId(scopeId, nameof(scopeId));
            PowerTier = powerTier;
            Scope = scope;
            ScopeId = scopeId;
            Access = access;
        }

        public bool MatchesEncounter(EncounterDefinition encounter)
            => Scope == ModifierAvailabilityScope.Global || (encounter != null &&
                (Scope == ModifierAvailabilityScope.Spirit ? ScopeId == encounter.SpiritId : ScopeId == encounter.Id));

        internal bool IsAvailable(EncounterDefinition encounter, ISet<string> unlockedIds, string modifierId)
            => MatchesEncounter(encounter) && (Access != ModifierAccess.PermanentUnlock || unlockedIds.Contains(modifierId));
    }

    public enum ModifierPhase { InitialRoll, PlayerReroll, PlayHand }
    public enum ModifierEffectKind { AddBaseScore, AddMultiplier, AddRerolls }
    public enum ModifierActivationLimit { Unlimited, OncePerHand, OncePerStage }
    public enum ModifierConditionKind
    {
        NoDiceRerolled = 0, ContainsFace = 1, ExactlyOneSelectedFace = 2, OutOfRerolls = 3,
        RangeLessThan = 4, SumLessThanInitial = 5, LastHand = 6, SelectedFaceUnchanged = 7,
        SelectedMatchingGroupAtLeast = 8, ContainsUpperFace = 9, RankBelowPrevious = 10,
        RankAbovePrevious = 11, RankEqualsPrevious = 12, RankEqualsBeforeReroll = 13,
        SumDivisibleBy = 14, EdgePairSumsEqual = 15, Ordered = 16, HighDie = 17,
        AdjacentDifferent = 18, AdjacentMatchingTriple = 19, ExactlyOneFace = 20,
        ContainsRerollsRemaining = 21, SelectedMatchesHandsRemaining = 22, AdjacentDifferenceAtLeast = 23,
        PositiveRerollsDivisibleBy = 24, MiddleUnique = 25, HighDieOrStraight = 26,
        AlternatingParity = 27, InteriorEqualsNeighborSum = 28, SelectedInteriorEqualsNeighborSum = 29,
        EveryDieRerolled = 30, ExactlyOneSelectedMatchesSelectedCount = 31,
        MinimumEqualsRerollsSpent = 32, RerollsExceedHandsRemaining = 33,
        MaximumAtMostRerollsRemaining = 34, LargestGroupEqualsHandsOrRerolls = 35,
        EdgeSumEqualsEither = 36, FaceEqualsPosition = 37, FirstHand = 38,
        ExtremeEqualsHandsRemaining = 39, SumLessThan = 40, TwoPair = 41,
        SelectedSumEqualsEither = 42, RerollsSpentAtLeast = 43, AdjacentDifferenceAtMost = 44,
        EdgeSumGreaterThanInterior = 45, InitialPair = 46, InitialRankAbovePrevious = 47,
        RangeGreaterThan = 48, PreviousStageExhaustedHands = 49, SameParity = 50,
        InteriorSumDivisibleBy = 51, EdgeSumEqualsHandsOrRerolls = 52,
        PreviousStageAtMostHalfHandsPlayed = 53, LastRerollOnLastHand = 54,
        RankBelowBeforeReroll = 55, Never = 56, ThreeSmallestSumAtMostMaximum = 57,
        SecondFourthProductAtLeastSum = 58, InteriorEqualsNeighborDifference = 59,
        MiddleEqualsOddCount = 60, SumGreaterThan = 61, HandsRemainingEven = 62,
        EdgePairDifferencesEqual = 63, InteriorContainsPair = 64,
        OddSumGreaterThanEvenSum = 65, SumPrime = 66, EdgesMatchInterior = 67,
        UniqueCountLessThanRerollsSpent = 68, NoExtremeFaces = 69, MiddleFacesCountAtLeast = 70,
        MiddleHand = 71, InteriorPairMatchesEdge = 72, Always = 73, FavoredFaceCountAtLeast = 74,
        BoundFaceCountAtLeast = 75
    }

    public readonly struct ModifierEffect
    {
        public ModifierEffectKind Kind { get; }
        public long Amount { get; }
        public ModifierEffect(ModifierEffectKind kind, long amount)
        {
            if (!Enum.IsDefined(typeof(ModifierEffectKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (amount <= 0 || (kind == ModifierEffectKind.AddRerolls && amount > int.MaxValue))
                throw new ArgumentOutOfRangeException(nameof(amount));
            Kind = kind;
            Amount = amount;
        }
    }

    // Built-in condition configuration used when constructing runtime condition objects.
    public readonly struct ModifierCondition
    {
        public ModifierConditionKind Kind { get; }
        public int Parameter { get; }
        public int SecondaryParameter { get; }
        public ModifierCondition(ModifierConditionKind kind, int parameter = 0, int secondaryParameter = 0)
        {
            if (!Enum.IsDefined(typeof(ModifierConditionKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            switch (kind)
            {
                case ModifierConditionKind.ContainsFace:
                case ModifierConditionKind.ExactlyOneSelectedFace: parameter = parameter == 0 ? 1 : parameter; break;
                case ModifierConditionKind.ContainsUpperFace: parameter = parameter == 0 ? 6 : parameter; break;
                case ModifierConditionKind.ExactlyOneFace: parameter = parameter == 0 ? 2 : parameter; break;
                case ModifierConditionKind.FavoredFaceCountAtLeast: parameter = parameter == 0 ? 2 : parameter; break;
                case ModifierConditionKind.BoundFaceCountAtLeast: parameter = parameter == 0 ? 3 : parameter; break;
                case ModifierConditionKind.RangeLessThan:
                case ModifierConditionKind.RangeGreaterThan:
                case ModifierConditionKind.SelectedMatchingGroupAtLeast:
                case ModifierConditionKind.PositiveRerollsDivisibleBy:
                case ModifierConditionKind.RerollsSpentAtLeast:
                case ModifierConditionKind.InteriorSumDivisibleBy: parameter = parameter == 0 ? 3 : parameter; break;
                case ModifierConditionKind.SumDivisibleBy: parameter = parameter == 0 ? 5 : parameter; break;
                case ModifierConditionKind.AdjacentDifferenceAtLeast: parameter = parameter == 0 ? 2 : parameter; break;
                case ModifierConditionKind.AdjacentDifferenceAtMost: parameter = parameter == 0 ? 1 : parameter; break;
                case ModifierConditionKind.SumLessThan: parameter = parameter == 0 ? 15 : parameter; break;
                case ModifierConditionKind.SumGreaterThan: parameter = parameter == 0 ? 22 : parameter; break;
                case ModifierConditionKind.EdgeSumEqualsEither:
                    parameter = parameter == 0 ? 6 : parameter;
                    secondaryParameter = secondaryParameter == 0 ? 8 : secondaryParameter; break;
                case ModifierConditionKind.SelectedSumEqualsEither:
                    parameter = parameter == 0 ? 8 : parameter;
                    secondaryParameter = secondaryParameter == 0 ? 10 : secondaryParameter; break;
                case ModifierConditionKind.NoExtremeFaces:
                    parameter = parameter == 0 ? 1 : parameter;
                    secondaryParameter = secondaryParameter == 0 ? 6 : secondaryParameter; break;
                case ModifierConditionKind.MiddleFacesCountAtLeast: parameter = parameter == 0 ? 4 : parameter; break;
            }
            if (parameter < 0 || secondaryParameter < 0) throw new ArgumentOutOfRangeException(nameof(parameter));
            switch (kind)
            {
                case ModifierConditionKind.ContainsFace:
                case ModifierConditionKind.ContainsUpperFace:
                case ModifierConditionKind.ExactlyOneSelectedFace:
                case ModifierConditionKind.ExactlyOneFace:
                    if (parameter < 1 || parameter > 6) throw new ArgumentOutOfRangeException(nameof(parameter)); break;
                case ModifierConditionKind.NoExtremeFaces:
                    if (parameter < 1 || parameter > 6 || secondaryParameter < 1 || secondaryParameter > 6)
                        throw new ArgumentOutOfRangeException(nameof(parameter)); break;
                case ModifierConditionKind.SelectedMatchingGroupAtLeast:
                case ModifierConditionKind.MiddleFacesCountAtLeast:
                case ModifierConditionKind.FavoredFaceCountAtLeast:
                case ModifierConditionKind.BoundFaceCountAtLeast:
                    if (parameter < 1 || parameter > DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(parameter)); break;
                case ModifierConditionKind.SumDivisibleBy:
                case ModifierConditionKind.PositiveRerollsDivisibleBy:
                case ModifierConditionKind.InteriorSumDivisibleBy:
                    if (parameter < 1) throw new ArgumentOutOfRangeException(nameof(parameter)); break;
            }
            Kind = kind;
            Parameter = parameter;
            SecondaryParameter = secondaryParameter;
        }
    }

    public sealed class ModifierDefinition
    {
        public string Id { get; }
        public string FamilyId { get; }
        public ModifierPhase Phase { get; }
        public IModifierCondition Condition { get; }
        public ReadOnlyCollection<ModifierEffect> Effects { get; }
        public ModifierActivationLimit ActivationLimit { get; }
        public bool RewardEligible { get; }
        public ModifierAvailabilityDefinition Availability { get; }
        public ModifierDefinition(string id, string familyId, ModifierPhase phase, ModifierCondition condition,
            IEnumerable<ModifierEffect> effects, ModifierActivationLimit activationLimit = ModifierActivationLimit.OncePerHand,
            bool rewardEligible = true, ModifierAvailabilityDefinition availability = null)
            : this(id, familyId, phase, ModifierConditions.Create(condition), effects, activationLimit, rewardEligible, availability)
        {
        }

        public ModifierDefinition(string id, string familyId, ModifierPhase phase, IModifierCondition condition,
            IEnumerable<ModifierEffect> effects, ModifierActivationLimit activationLimit = ModifierActivationLimit.OncePerHand,
            bool rewardEligible = true, ModifierAvailabilityDefinition availability = null)
        {
            DefinitionValidation.RequireId(id, nameof(id));
            DefinitionValidation.RequireId(familyId, nameof(familyId));
            if (!Enum.IsDefined(typeof(ModifierPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            if (!Enum.IsDefined(typeof(ModifierActivationLimit), activationLimit)) throw new ArgumentOutOfRangeException(nameof(activationLimit));
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            if (effects == null) throw new ArgumentNullException(nameof(effects));
            var entries = new List<ModifierEffect>(effects);
            foreach (var effect in entries)
                if (effect.Amount <= 0) throw new ArgumentException("Effects require positive amounts.", nameof(effects));
            Id = id;
            FamilyId = familyId;
            Phase = phase;
            Condition = condition;
            Effects = entries.AsReadOnly();
            ActivationLimit = activationLimit;
            RewardEligible = rewardEligible;
            Availability = availability ?? new ModifierAvailabilityDefinition();
        }
    }

    public readonly struct ModifierContext
    {
        public DiceValues CurrentDice { get; }
        public DiceValues InitialDice { get; }
        public DiceValues? BeforeRerollDice { get; }
        public DiceValues? PreviousPlayedDice { get; }
        public int SelectedMask { get; }
        public int EverRerolledMask { get; }
        public int RerollsSpentOnHand { get; }
        public int HandsRemaining { get; }
        public int RerollsRemaining { get; }
        public int HandsPlayedInStage { get; }
        public int StartingHands { get; }
        public int? PreviousStageStartingHands { get; }
        public int? PreviousStageHandsRemaining { get; }
        public int? FavoredFace { get; }
        /** Dice bound by encounter rules; they score normally but cannot be rerolled. */
        public int BoundMask { get; }
        public ModifierContext(DiceValues currentDice, DiceValues initialDice, DiceValues? beforeRerollDice,
            DiceValues? previousPlayedDice, int selectedMask, int everRerolledMask, int rerollsSpentOnHand,
            int handsRemaining, int rerollsRemaining, int handsPlayedInStage, int startingHands,
            int? previousStageStartingHands, int? previousStageHandsRemaining, int? favoredFace = null, int boundMask = 0)
        {
            if (!currentDice.IsValid || !initialDice.IsValid ||
                (beforeRerollDice.HasValue && !beforeRerollDice.Value.IsValid) ||
                (previousPlayedDice.HasValue && !previousPlayedDice.Value.IsValid))
                throw new ArgumentException("Context dice must contain five valid faces.");
            if ((selectedMask & ~DiceValues.AllMask) != 0 || (everRerolledMask & ~DiceValues.AllMask) != 0)
                throw new ArgumentOutOfRangeException(nameof(selectedMask));
            if ((boundMask & ~DiceValues.AllMask) != 0) throw new ArgumentOutOfRangeException(nameof(boundMask));
            if (rerollsSpentOnHand < 0 || handsRemaining < 0 || rerollsRemaining < 0 ||
                handsPlayedInStage < 0 || startingHands < 1 || previousStageStartingHands < 1 ||
                previousStageHandsRemaining < 0)
                throw new ArgumentOutOfRangeException(nameof(handsRemaining));
            CurrentDice = currentDice;
            InitialDice = initialDice;
            BeforeRerollDice = beforeRerollDice;
            PreviousPlayedDice = previousPlayedDice;
            SelectedMask = selectedMask;
            EverRerolledMask = everRerolledMask;
            RerollsSpentOnHand = rerollsSpentOnHand;
            HandsRemaining = handsRemaining;
            RerollsRemaining = rerollsRemaining;
            HandsPlayedInStage = handsPlayedInStage;
            StartingHands = startingHands;
            PreviousStageStartingHands = previousStageStartingHands;
            PreviousStageHandsRemaining = previousStageHandsRemaining;
            if (favoredFace < 1 || favoredFace > 6) throw new ArgumentOutOfRangeException(nameof(favoredFace));
            FavoredFace = favoredFace;
            BoundMask = boundMask;
        }
    }
}
