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
    }

    public sealed class AcknowledgeModifierUnlockAction : EncounterAction
    {
        public long OfferId { get; }
        public AcknowledgeModifierUnlockAction(long offerId) => OfferId = offerId;
    }

    public sealed class RefreshModifierOfferAction : EncounterAction
    {
        public long OfferId { get; }
        public RefreshModifierOfferAction(long offerId) => OfferId = offerId;
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

        internal RewardOffer(long offerId, RewardKind kind, int targetStageIndex, IEnumerable<RewardOption> options)
        {
            OfferId = offerId;
            Kind = kind;
            TargetStageIndex = targetStageIndex;
            Options = new List<RewardOption>(options).AsReadOnly();
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
    }

    public sealed class RewardSkippedEvent : RuleEvent
    {
        public RewardKind Kind { get; }
        internal RewardSkippedEvent(EncounterState state, RewardKind kind)
            : base(state.StageIndex, state.HandIndex) => Kind = kind;
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
    }
}
