using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public readonly struct ModifierInstanceSnapshot
    {
        public long InstanceId { get; }
        public string DefinitionId { get; }
        public int ActivationsThisHand { get; }
        public int ActivationsThisStage { get; }
        public long TotalActivations { get; }

        internal ModifierInstanceSnapshot(ModifierInstanceState state)
        {
            InstanceId = state.InstanceId;
            DefinitionId = state.DefinitionId;
            ActivationsThisHand = state.ActivationsThisHand;
            ActivationsThisStage = state.ActivationsThisStage;
            TotalActivations = state.TotalActivations;
        }
    }

    public readonly struct ModifierContribution
    {
        public long InstanceId { get; }
        public string DefinitionId { get; }
        public ModifierPhase Phase { get; }
        public long BaseScore { get; }
        public long Multiplier { get; }
        public int Rerolls { get; }

        internal ModifierContribution(long instanceId, ModifierDefinition definition, long baseScore, long multiplier, int rerolls)
        {
            InstanceId = instanceId;
            DefinitionId = definition.Id;
            Phase = definition.Phase;
            BaseScore = baseScore;
            Multiplier = multiplier;
            Rerolls = rerolls;
        }
    }

    public sealed class ModifierActivatedEvent : RuleEvent
    {
        public long InstanceId => Contribution.InstanceId;
        public string DefinitionId => Contribution.DefinitionId;
        public ModifierPhase Phase => Contribution.Phase;
        public ModifierContribution Contribution { get; }

        internal ModifierActivatedEvent(EncounterState state, ModifierContribution contribution)
            : base(state.StageIndex, state.HandIndex) => Contribution = contribution;
    }

    public sealed class HandPreview
    {
        public ScoreBreakdown Score { get; }
        public int RerollsRemaining { get; }
        public ReadOnlyCollection<ModifierInstanceSnapshot> Modifiers { get; }
        public ReadOnlyCollection<RuleEvent> Events { get; }

        internal HandPreview(ScoreBreakdown score, EncounterState state, IEnumerable<RuleEvent> events)
        {
            Score = score;
            RerollsRemaining = state.RerollsRemaining;
            Modifiers = ModifierResolver.CaptureInstances(state);
            Events = new List<RuleEvent>(events).AsReadOnly();
        }
    }

    internal sealed class ModifierInstanceState
    {
        internal long InstanceId;
        internal string DefinitionId;
        internal int ActivationsThisHand;
        internal int ActivationsThisStage;
        internal long TotalActivations;

        internal ModifierInstanceState Copy() => (ModifierInstanceState)MemberwiseClone();
    }

    internal static class ModifierResolver
    {
        internal static ReadOnlyCollection<ModifierInstanceSnapshot> CaptureInstances(EncounterState state)
        {
            var values = new ModifierInstanceSnapshot[state.Modifiers.Length];
            for (int i = 0; i < values.Length; ++i) values[i] = new ModifierInstanceSnapshot(state.Modifiers[i]);
            return Array.AsReadOnly(values);
        }

        internal static void Resolve(EncounterState state, MechanicalCatalog catalog, EncounterDefinition encounter,
            ModifierPhase phase, List<RuleEvent> events)
        {
            var context = CreateContext(state, encounter, state.HandsRemaining);
            var contributions = new List<ModifierContribution>();

            // Predicates observe one phase snapshot; resource grants cannot change later eligibility.
            foreach (var instance in state.Modifiers)
            {
                ModifierDefinition definition = catalog.GetModifier(instance.DefinitionId);
                if (definition.Phase != phase ||
                    !HasActivationAllowance(instance, definition) ||
                    !definition.Condition.Evaluate(in context)) continue;

                long baseScore = 0;
                long multiplier = 0;
                int rerolls = 0;
                checked
                {
                    foreach (var effect in definition.Effects)
                    {
                        switch (effect.Kind)
                        {
                            case ModifierEffectKind.AddBaseScore: baseScore += effect.Amount; break;
                            case ModifierEffectKind.AddMultiplier: multiplier += effect.Amount; break;
                            case ModifierEffectKind.AddRerolls: rerolls += (int)effect.Amount; break;
                            default: throw new InvalidOperationException("Unsupported modifier effect.");
                        }
                    }
                    ++instance.ActivationsThisHand;
                    ++instance.ActivationsThisStage;
                    ++instance.TotalActivations;
                }
                contributions.Add(new ModifierContribution(instance.InstanceId, definition, baseScore, multiplier, rerolls));
            }

            foreach (var contribution in contributions)
            {
                state.ModifierBaseScore = checked(state.ModifierBaseScore + contribution.BaseScore);
                state.ModifierMultiplier = checked(state.ModifierMultiplier + contribution.Multiplier);
                state.ModifierContributions.Add(contribution);
                events.Add(new ModifierActivatedEvent(state, contribution));
                if (contribution.Rerolls == 0) continue;
                int before = state.RerollsRemaining;
                state.RerollsRemaining = checked(before + contribution.Rerolls);
                events.Add(new ResourceChangedEvent(state, ResourceKind.Rerolls, ResourceChangeCause.ModifierEffect,
                    before, state.RerollsRemaining, contribution.InstanceId, contribution.DefinitionId));
            }
        }

        internal static ReadOnlyCollection<ModifierInspection> InspectForPlayHand(EncounterState state,
            MechanicalCatalog catalog, EncounterDefinition encounter)
        {
            bool playing = state.Phase == EncounterPhase.Playing;
            // PlayHand predicates see the hands spent on submission and resources before any phase effects.
            var context = playing ? CreateContext(state, encounter, state.HandsRemaining - 1) : default;
            var values = new ModifierInspection[state.Modifiers.Length];
            for (int i = 0; i < values.Length; ++i)
            {
                ModifierInstanceState instance = state.Modifiers[i];
                ModifierDefinition definition = catalog.GetModifier(instance.DefinitionId);
                bool limitReached = !HasActivationAllowance(instance, definition);
                bool? conditionMet = null;
                ModifierInspectionStatus status;
                if (!playing) status = ModifierInspectionStatus.EncounterNotPlaying;
                else if (definition.Phase != ModifierPhase.PlayHand)
                    status = limitReached ? ModifierInspectionStatus.ActivationLimitReached : ModifierInspectionStatus.DifferentPhase;
                else
                {
                    conditionMet = definition.Condition.Evaluate(in context);
                    status = limitReached ? ModifierInspectionStatus.ActivationLimitReached :
                        conditionMet.Value ? ModifierInspectionStatus.WouldActivate : ModifierInspectionStatus.ConditionNotMet;
                }
                values[i] = new ModifierInspection(instance, definition, status, conditionMet, limitReached);
            }
            return Array.AsReadOnly(values);
        }

        private static ModifierContext CreateContext(EncounterState state, EncounterDefinition encounter, int handsRemaining)
        {
            int? favoredFace = null;
            foreach (var rule in encounter.Rules)
            {
                int face = rule.FavoredFace(state.TotalHandsPlayed);
                if (face != 0) { favoredFace = face; break; }
            }
            return new ModifierContext(state.CurrentDice, state.InitialDice, state.DiceBeforeLastReroll,
                state.PreviousPlayedDice, state.LatestRerollMask, state.EverRerolledMask, state.RerollsSpentOnHand,
                handsRemaining, state.RerollsRemaining, state.HandsPlayedInStage, encounter.Stages[state.StageIndex].StartingHands,
                state.PreviousStageStartingHands, state.PreviousStageHandsRemaining, favoredFace,
                EncounterRuleResolver.BoundDice(encounter, state));
        }

        private static bool HasActivationAllowance(ModifierInstanceState instance, ModifierDefinition definition)
            => (definition.ActivationLimit != ModifierActivationLimit.OncePerHand || instance.ActivationsThisHand == 0) &&
                (definition.ActivationLimit != ModifierActivationLimit.OncePerStage || instance.ActivationsThisStage == 0);
    }
}
