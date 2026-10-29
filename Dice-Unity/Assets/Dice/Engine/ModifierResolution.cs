using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public readonly struct ModifierInstanceSnapshot
    {
        public long InstanceId { get; }
        public ModifierDefinition.Id DefinitionId { get; }
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
        public ModifierDefinition.Id DefinitionId { get; }
        public ModifierPhase Phase { get; }
        public long BaseScore { get; }
        public long Multiplier { get; }
        public int Rerolls { get; }

        public ModifierContribution(long instanceId, ModifierDefinition definition, long baseScore, long multiplier, int rerolls)
        {
            InstanceId = instanceId;
            DefinitionId = definition.Identity;
            Phase = definition.Phase;
            BaseScore = baseScore;
            Multiplier = multiplier;
            Rerolls = rerolls;
        }
    }

    public sealed class ModifierActivatedEvent : RuleEvent
    {
        public long InstanceId => Contribution.InstanceId;
        public ModifierDefinition.Id DefinitionId => Contribution.DefinitionId;
        public ModifierPhase Phase => Contribution.Phase;
        public ModifierContribution Contribution { get; }
        /** The resource changes the activation's effects granted, in order. */
        public ReadOnlyCollection<ResourceChangedEvent> Grants { get; }
        private readonly List<ResourceChangedEvent> grants = new List<ResourceChangedEvent>();

        internal ModifierActivatedEvent(EncounterState state, ModifierContribution contribution)
            : base(state.StageIndex, state.HandIndex)
        {
            Contribution = contribution;
            Grants = grants.AsReadOnly();
        }

        /** Records a change granted by this activation; the change's constructor calls it. */
        internal void AddGrant(ResourceChangedEvent grant) => grants.Add(grant);

        protected override string DescribeDetail(IDiagnosticFormat format)
            => format.ModifierName(DefinitionId) + " #" + InstanceId + " activated on " + format.Humanize(Phase.ToString()) + ": " +
                DescribeContribution(Contribution, format) + ".";
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
        internal ModifierDefinition.Id DefinitionId;
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

        /** Activates the phase's eligible charms and returns their activations in order. */
        internal static List<ModifierActivatedEvent> Resolve(EncounterState state, MechanicalCatalog catalog,
            EncounterDefinition encounter, ModifierPhase phase, List<RuleEvent> events)
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

                ModifierEffectTotals totals = ModifierEffectTotals.Of(definition);
                checked
                {
                    ++instance.ActivationsThisHand;
                    ++instance.ActivationsThisStage;
                    ++instance.TotalActivations;
                }
                contributions.Add(new ModifierContribution(instance.InstanceId, definition, totals.BaseScore, totals.Multiplier,
                    totals.Rerolls));
            }

            var activations = new List<ModifierActivatedEvent>();
            foreach (var contribution in contributions)
            {
                state.ModifierBaseScore = checked(state.ModifierBaseScore + contribution.BaseScore);
                state.ModifierMultiplier = checked(state.ModifierMultiplier + contribution.Multiplier);
                state.ModifierContributions.Add(contribution);
                var activated = new ModifierActivatedEvent(state, contribution);
                activations.Add(activated);
                events.Add(activated);
                if (contribution.Rerolls == 0) continue;
                int before = state.RerollsRemaining;
                state.RerollsRemaining = checked(before + contribution.Rerolls);
                events.Add(new ResourceChangedEvent(state, ResourceKind.Rerolls, ResourceChangeCause.ModifierEffect,
                    before, state.RerollsRemaining, activated));
            }
            return activations;
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

        /** One context, with one set of rule snapshots, serves every modifier evaluated in a resolution step. */
        private static ModifierContext CreateContext(EncounterState state, EncounterDefinition encounter, int handsRemaining)
            => new ModifierContext(state.CurrentDice, state.InitialDice, state.DiceBeforeLastReroll,
                state.PreviousPlayedDice, state.LatestRerollMask, state.EverRerolledMask, state.RerollsSpentOnHand,
                handsRemaining, state.RerollsRemaining, state.HandsPlayedInStage, encounter.Stages[state.StageIndex].StartingHands,
                state.PreviousStageStartingHands, state.PreviousStageHandsRemaining, EncounterRuleResolver.Snapshots(encounter, state));

        private static bool HasActivationAllowance(ModifierInstanceState instance, ModifierDefinition definition)
            => (definition.ActivationLimit != ModifierActivationLimit.OncePerHand || instance.ActivationsThisHand == 0) &&
                (definition.ActivationLimit != ModifierActivationLimit.OncePerStage || instance.ActivationsThisStage == 0);
    }
}
