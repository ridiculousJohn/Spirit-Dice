using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    /**
     * A kind of reward offer and what varies by it. Each kind is one instance of its class, declared here; shared code
     * asks the kind rather than comparing kinds.
     */
    public abstract class RewardKind
    {
        public static readonly RewardKind Modifier = new ModifierRewardKind();
        public static readonly RewardKind HandUpgrade = new HandUpgradeRewardKind();
        public static readonly RewardKind ModifierUnlock = new ModifierUnlockRewardKind();
        // The kinds a saved id resolves to; each kind declares its own id.
        private static readonly RewardKind[] Kinds = { Modifier, HandUpgrade, ModifierUnlock };

        internal RewardKind() { }

        /** Every reward kind, each once. */
        public static IReadOnlyList<RewardKind> All => Kinds;

        /**
         * The kind's stable identifier: what a run save stores for an offer of this kind and what its wording is keyed
         * by. Changing it changes the save format.
         */
        public abstract string Id { get; }
        /** The phase the run waits in while an offer of this kind is pending. */
        public abstract EncounterPhase WaitingPhase { get; }
        /** Whether the player can spend refreshes to replace a pending offer of this kind. */
        public abstract bool Refreshable { get; }
        /**
         * Whether an offer of this kind is made even when nothing is eligible, to be acknowledged; an offer of another
         * kind with nothing eligible is skipped.
         */
        public abstract bool OfferedWhenEmpty { get; }
        /** Whether offers of this kind can contain the encounter's own charms, so one of them can introduce them. */
        internal abstract bool CanOfferEncounterModifiers { get; }

        /** Names a selected option of this kind for the diagnostic log. */
        internal abstract string DescribeSelection(RewardOption option, IDiagnosticFormat format);

        /**
         * The action answering the pending offer with a choice made on the offer the id names, which the action rejects
         * when it is not the pending one.
         */
        internal abstract EncounterAction CreateAnswer(RewardOffer pending, long offerId, long optionId);

        /** The kind whose id this is; false for an id no kind declares. */
        public static bool TryFind(string id, out RewardKind kind)
        {
            foreach (var candidate in Kinds)
                if (string.Equals(candidate.Id, id, StringComparison.Ordinal))
                {
                    kind = candidate;
                    return true;
                }
            kind = null;
            return false;
        }

        public override string ToString() => Id;
    }

    /** A run charm offered at a stage clear, drawn from the encounter's pools and its own charms. */
    internal sealed class ModifierRewardKind : RewardKind
    {
        public override string Id => "Modifier";
        public override EncounterPhase WaitingPhase => EncounterPhase.ChoosingModifier;
        public override bool Refreshable => true;
        public override bool OfferedWhenEmpty => false;
        internal override bool CanOfferEncounterModifiers => true;

        internal override string DescribeSelection(RewardOption option, IDiagnosticFormat format)
            => format.ModifierName(option.DefinitionId.Value);

        internal override EncounterAction CreateAnswer(RewardOffer pending, long offerId, long optionId)
            => new SelectModifierAction(offerId, optionId);
    }

    /** An upgrade of one hand, named by its category, offered after the charm choice. */
    internal sealed class HandUpgradeRewardKind : RewardKind
    {
        public override string Id => "HandUpgrade";
        public override EncounterPhase WaitingPhase => EncounterPhase.ChoosingHandUpgrade;
        public override bool Refreshable => false;
        public override bool OfferedWhenEmpty => false;
        internal override bool CanOfferEncounterModifiers => false;

        internal override string DescribeSelection(RewardOption option, IDiagnosticFormat format)
            => format.Humanize(option.Category.Value.ToString());

        internal override EncounterAction CreateAnswer(RewardOffer pending, long offerId, long optionId)
            => new SelectHandUpgradeAction(offerId, optionId);
    }

    /**
     * A permanent charm unlock offered at a milestone clear. It is offered even when every unlock is owned, and that
     * empty offer is acknowledged instead of chosen from.
     */
    internal sealed class ModifierUnlockRewardKind : RewardKind
    {
        public override string Id => "ModifierUnlock";
        public override EncounterPhase WaitingPhase => EncounterPhase.ChoosingUnlock;
        public override bool Refreshable => false;
        public override bool OfferedWhenEmpty => true;
        // Encounter-exclusive charms are never unlock rewards.
        internal override bool CanOfferEncounterModifiers => false;

        internal override string DescribeSelection(RewardOption option, IDiagnosticFormat format)
            => format.ModifierName(option.DefinitionId.Value);

        internal override EncounterAction CreateAnswer(RewardOffer pending, long offerId, long optionId)
            => pending.Options.Count == 0 ? (EncounterAction)new AcknowledgeModifierUnlockAction(offerId)
                : new SelectModifierUnlockAction(offerId, optionId);
    }

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
            => session.ValidateRewardSelection(RewardKind.ModifierUnlock, OfferId, OptionId);

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
            var rejection = session.ValidateRewardOffer(RewardKind.ModifierUnlock, OfferId);
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
            var rejection = session.ValidateRewardOffer(RewardKind.Modifier, OfferId);
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
            => session.ValidateRewardSelection(RewardKind.Modifier, OfferId, OptionId);

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
            => session.ValidateRewardSelection(RewardKind.HandUpgrade, OfferId, OptionId);

        internal override void Resolve(EncounterSession session, EncounterState next, List<RuleEvent> events)
            => session.SelectHandUpgrade(next, OptionId, events);
    }

    public readonly struct RewardOption
    {
        public long OptionId { get; }
        /** The offered charm's id; null for a hand upgrade. */
        public ModifierDefinition.Id? DefinitionId { get; }
        /** The hand a hand upgrade upgrades, named by its category alone; null for a charm. */
        public HandCategory? Category { get; }

        internal RewardOption(long optionId, ModifierDefinition.Id? definitionId, HandCategory? category = null)
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
         * Whether this offer's kind can contain the encounter's own charms and it is made on the first stage clear whose
         * offers can contain them; a refresh of that offer is one too.
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
            IntroducesEncounterModifiers = kind.CanOfferEncounterModifiers && encounter.EncounterModifierIds.Count > 0 &&
                targetStageIndex == encounter.EncounterModifiersFirstClearedStageNumber;
        }

        /**
         * The action answering this pending offer with the chosen option, the choice made on the offer the id names; the
         * action rejects an id that is not this offer's.
         */
        public EncounterAction Answer(long offerId, long optionId) => Kind.CreateAnswer(this, offerId, optionId);

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
            => (IsRefresh ? "Refreshed " : "Offered ") + format.Humanize(Offer.Kind.Id) + " choices for stage " +
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

        protected override string DescribeDetail(IDiagnosticFormat format)
            => "Selected " + Kind.DescribeSelection(Option, format) +
                " (offer " + OfferId + ", option " + Option.OptionId + ").";
    }

    public sealed class RewardSkippedEvent : RuleEvent
    {
        public RewardKind Kind { get; }
        internal RewardSkippedEvent(EncounterState state, RewardKind kind)
            : base(state.StageIndex, state.HandIndex) => Kind = kind;

        protected override string DescribeDetail(IDiagnosticFormat format)
            => "Skipped " + format.Humanize(Kind.Id) + "; no eligible options.";
    }

    public sealed class ModifierAcquiredEvent : RuleEvent
    {
        public long InstanceId { get; }
        public ModifierDefinition.Id DefinitionId { get; }
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
        public int Before { get; }
        public int After { get; }
        internal HandUpgradedEvent(EncounterState state, HandCategory category, int before)
            : base(state.StageIndex, state.HandIndex)
        {
            Category = category;
            Before = before;
            After = state.HandUpgrades[(int)category];
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => format.Humanize(Category.ToString()) + " upgraded " + Before + " -> " + After + ".";
    }
}
