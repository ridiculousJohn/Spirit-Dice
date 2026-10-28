using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public enum RewardKind { Modifier, HandUpgrade, ModifierUnlock }

    public sealed class SelectModifierUnlockAction : EncounterAction
    {
        public long OfferId { get; }
        public long OptionId { get; }
        public SelectModifierUnlockAction(long offerId, long optionId)
        {
            OfferId = offerId;
            OptionId = optionId;
        }

        internal override CommandRejection Validate(EncounterSession session)
            => session.ValidateRewardSelection(EncounterPhase.ChoosingUnlock, OfferId, OptionId);

        internal override void Resolve(EncounterSession session, EncounterState next, List<RuleEvent> events)
            => session.SelectModifierUnlock(next, OptionId, events);
    }

    public sealed class AcknowledgeModifierUnlockAction : EncounterAction
    {
        public long OfferId { get; }
        public AcknowledgeModifierUnlockAction(long offerId) => OfferId = offerId;

        /** Accepted only for an unlock offer without options; an offer with options is answered by selecting one. */
        internal override CommandRejection Validate(EncounterSession session)
        {
            var rejection = session.ValidateRewardOffer(EncounterPhase.ChoosingUnlock, OfferId);
            if (rejection != CommandRejection.None) return rejection;
            return session.State.PendingReward.Options.Count == 0 ? CommandRejection.None : CommandRejection.InvalidRewardOption;
        }

        internal override void Resolve(EncounterSession session, EncounterState next, List<RuleEvent> events)
            => session.ContinueAfterUnlock(next, events);
    }

    public sealed class RefreshModifierOfferAction : EncounterAction
    {
        public long OfferId { get; }
        public RefreshModifierOfferAction(long offerId) => OfferId = offerId;

        internal override CommandRejection Validate(EncounterSession session)
        {
            var rejection = session.ValidateRewardOffer(EncounterPhase.ChoosingModifier, OfferId);
            if (rejection != CommandRejection.None) return rejection;
            return session.State.RewardRefreshes < session.Definition.RewardPolicy.RefreshCost
                ? CommandRejection.InsufficientRewardRefreshes : CommandRejection.None;
        }

        internal override void Resolve(EncounterSession session, EncounterState next, List<RuleEvent> events)
            => session.RefreshModifierOffer(next, events);
    }

    public sealed class SelectModifierAction : EncounterAction
    {
        public long OfferId { get; }
        public long OptionId { get; }
        public SelectModifierAction(long offerId, long optionId)
        {
            OfferId = offerId;
            OptionId = optionId;
        }

        internal override CommandRejection Validate(EncounterSession session)
            => session.ValidateRewardSelection(EncounterPhase.ChoosingModifier, OfferId, OptionId);

        internal override void Resolve(EncounterSession session, EncounterState next, List<RuleEvent> events)
            => session.SelectModifier(next, OptionId, events);
    }

    public sealed class SelectHandUpgradeAction : EncounterAction
    {
        public long OfferId { get; }
        public long OptionId { get; }
        public SelectHandUpgradeAction(long offerId, long optionId)
        {
            OfferId = offerId;
            OptionId = optionId;
        }

        internal override CommandRejection Validate(EncounterSession session)
            => session.ValidateRewardSelection(EncounterPhase.ChoosingHandUpgrade, OfferId, OptionId);

        internal override void Resolve(EncounterSession session, EncounterState next, List<RuleEvent> events)
            => session.SelectHandUpgrade(next, OptionId, events);
    }

    public readonly struct RewardOption
    {
        public long OptionId { get; }
        public string DefinitionId { get; }
        public HandCategory? Category { get; }

        internal RewardOption(long optionId, string definitionId, HandCategory? category = null)
        {
            OptionId = optionId;
            DefinitionId = definitionId;
            Category = category;
        }
    }

    public sealed class RewardOffer : IEquatable<RewardOffer>
    {
        public long OfferId { get; }
        public RewardKind Kind { get; }
        public int TargetStageIndex { get; }
        public ReadOnlyCollection<RewardOption> Options { get; }
        /**
         * Whether this is a charm offer made on the first stage clear whose offers can contain the encounter's own charms;
         * a refresh of that offer is one too.
         */
        public bool IntroducesEncounterModifiers { get; }

        internal RewardOffer(long offerId, RewardKind kind, int targetStageIndex, IEnumerable<RewardOption> options,
            EncounterDefinition encounter)
        {
            OfferId = offerId;
            Kind = kind;
            TargetStageIndex = targetStageIndex;
            Options = new List<RewardOption>(options).AsReadOnly();
            // An offer's target stage index is the number of the stage whose clear produced it.
            IntroducesEncounterModifiers = kind == RewardKind.Modifier && encounter.EncounterModifierIds.Count > 0 &&
                targetStageIndex == encounter.EncounterModifiersFirstClearedStageNumber;
        }

        public bool Equals(RewardOffer other)
        {
            if (other == null || OfferId != other.OfferId || Kind != other.Kind ||
                TargetStageIndex != other.TargetStageIndex || Options.Count != other.Options.Count) return false;
            for (int i = 0; i < Options.Count; ++i)
                if (!Options[i].Equals(other.Options[i])) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is RewardOffer other && Equals(other);
        public override int GetHashCode() => OfferId.GetHashCode();
    }

    public sealed class RewardOfferedEvent : RuleEvent
    {
        public RewardOffer Offer { get; }
        public bool IsRefresh { get; }
        internal RewardOfferedEvent(EncounterState state, RewardOffer offer, bool isRefresh)
            : base(state.StageIndex, state.HandIndex)
        {
            Offer = offer;
            IsRefresh = isRefresh;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => (IsRefresh ? "Refreshed " : "Offered ") + format.Humanize(Offer.Kind.ToString()) + " choices for stage " +
                (Offer.TargetStageIndex + 1) + " (offer " + Offer.OfferId + ").";
    }

    public sealed class RewardSelectedEvent : RuleEvent
    {
        public long OfferId { get; }
        public RewardKind Kind { get; }
        public RewardOption Option { get; }
        internal RewardSelectedEvent(EncounterState state, RewardOffer offer, RewardOption option)
            : base(state.StageIndex, state.HandIndex)
        {
            OfferId = offer.OfferId;
            Kind = offer.Kind;
            Option = option;
        }

        /** Names a selected charm by its presentation and a hand upgrade by its engine id. */
        protected override string DescribeDetail(IDiagnosticFormat format)
            => "Selected " + (Kind == RewardKind.HandUpgrade ? format.Humanize(Option.DefinitionId) : format.ModifierName(Option.DefinitionId)) +
                " (offer " + OfferId + ", option " + Option.OptionId + ").";
    }

    public sealed class RewardSkippedEvent : RuleEvent
    {
        public RewardKind Kind { get; }
        internal RewardSkippedEvent(EncounterState state, RewardKind kind)
            : base(state.StageIndex, state.HandIndex) => Kind = kind;

        protected override string DescribeDetail(IDiagnosticFormat format)
            => "Skipped " + format.Humanize(Kind.ToString()) + "; no eligible options.";
    }

    public sealed class ModifierAcquiredEvent : RuleEvent
    {
        public long InstanceId { get; }
        public string DefinitionId { get; }
        internal ModifierAcquiredEvent(EncounterState state, ModifierInstanceState modifier)
            : base(state.StageIndex, state.HandIndex)
        {
            InstanceId = modifier.InstanceId;
            DefinitionId = modifier.DefinitionId;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => "Acquired " + format.ModifierName(DefinitionId) + " #" + InstanceId + ".";
    }

    public sealed class HandUpgradedEvent : RuleEvent
    {
        public HandCategory Category { get; }
        public string DefinitionId { get; }
        public int Before { get; }
        public int After { get; }
        internal HandUpgradedEvent(EncounterState state, HandCategory category, string definitionId, int before)
            : base(state.StageIndex, state.HandIndex)
        {
            Category = category;
            DefinitionId = definitionId;
            Before = before;
            After = state.HandUpgrades[(int)category];
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => format.Humanize(Category.ToString()) + " upgraded " + Before + " -> " + After + ".";
    }
}
