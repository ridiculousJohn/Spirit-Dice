using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Dice.Engine
{
    public enum EncounterPhase { Playing, AwaitingRewards, Completed, ChoosingModifier, ChoosingHandUpgrade, ChoosingUnlock }
    public enum EncounterOutcome { None, Won, Lost, Abandoned }
    public enum CommandRejection
    {
        None, StaleRevision, InvalidAction, EncounterCompleted, InvalidPhase, InvalidDiceSelection, NoRerollsRemaining,
        InvalidOffer, InvalidRewardOption, InsufficientRewardRefreshes, InvalidPiece, PieceUnavailable, InvalidPieceInput
    }
    public enum RollKind { Initial, PlayerReroll }
    public enum ResourceKind { Hands, Rerolls, RewardRefreshes }
    public enum ResourceChangeCause { PlayHand, RerollDice, ModifierEffect, StageClear, RefreshReward }

    /** A player action on an encounter. Each action validates and resolves itself; actions are defined only in the engine. */
    public abstract class EncounterAction
    {
        internal EncounterAction() { }

        /** Why the session cannot take this action now, or None; the session has checked the revision and that it is not completed. */
        internal abstract CommandRejection Validate(EncounterSession session);

        /** Applies this action to the session's private copy of its state. */
        internal abstract void Resolve(EncounterSession session, EncounterState next, List<RuleEvent> events);
    }

    public sealed class RerollDiceAction : EncounterAction
    {
        public int SelectedMask { get; }
        public RerollDiceAction(int selectedMask) => SelectedMask = selectedMask;

        internal override CommandRejection Validate(EncounterSession session)
        {
            EncounterState state = session.State;
            if (state.Phase != EncounterPhase.Playing) return CommandRejection.InvalidPhase;
            if (SelectedMask <= 0 || (SelectedMask & ~DiceValues.AllMask) != 0 ||
                (SelectedMask & EncounterRuleResolver.LockedDice(session.Definition, state)) != 0)
                return CommandRejection.InvalidDiceSelection;
            if (!EncounterRuleResolver.CanAffordReroll(session.Definition, state))
                return CommandRejection.NoRerollsRemaining;
            return CommandRejection.None;
        }

        internal override void Resolve(EncounterSession session, EncounterState next, List<RuleEvent> events)
            => session.Reroll(next, SelectedMask, events);
    }

    public sealed class PlayHandAction : EncounterAction
    {
        public static PlayHandAction Instance { get; } = new PlayHandAction();
        private PlayHandAction() { }

        internal override CommandRejection Validate(EncounterSession session)
            => session.State.Phase == EncounterPhase.Playing ? CommandRejection.None : CommandRejection.InvalidPhase;

        internal override void Resolve(EncounterSession session, EncounterState next, List<RuleEvent> events)
            => session.Play(next, events);
    }

    /** Ends the encounter as abandoned; it is accepted in any phase of an encounter that has not completed. */
    public sealed class AbandonEncounterAction : EncounterAction
    {
        public static AbandonEncounterAction Instance { get; } = new AbandonEncounterAction();
        private AbandonEncounterAction() { }

        internal override CommandRejection Validate(EncounterSession session) => CommandRejection.None;

        internal override void Resolve(EncounterSession session, EncounterState next, List<RuleEvent> events)
            => EncounterSession.Complete(next, EncounterOutcome.Abandoned, events);
    }

    public sealed class EncounterSnapshot
    {
        public string EncounterId { get; }
        public string CatalogVersion { get; }
        public long Revision { get; }
        public EncounterPhase Phase { get; }
        public EncounterOutcome Outcome { get; }
        public int StageIndex { get; }
        public int HandIndex { get; }
        public int StageCount { get; }
        public long StartingThreshold { get; }
        public long RemainingThreshold { get; }
        public int StartingHands { get; }
        public int StartingRerolls { get; }
        public int HandsRemaining { get; }
        public int RerollsRemaining { get; }
        public bool CanReroll { get; }
        public ReadOnlyCollection<EncounterRuleSnapshot> EncounterRules { get; }
        public DiceValues CurrentDice { get; }
        public DiceValues InitialDice { get; }
        public DiceValues? DiceBeforeLastReroll { get; }
        public DiceValues? PreviousPlayedDice { get; }
        public int LatestRerollMask { get; }
        public int LatestChangedMask { get; }
        public int EverRerolledMask { get; }
        /** Dice that cannot be selected for a player reroll. */
        public int LockedMask { get; }
        public ReadOnlyCollection<PieceInteractionSnapshot> PieceInteractions { get; }
        public int RerollsSpentOnHand { get; }
        public int HandsPlayedInStage { get; }
        public int? PreviousStageStartingHands { get; }
        public int? PreviousStageHandsRemaining { get; }
        public long TotalScore { get; }
        public long TotalHandsPlayed { get; }
        public long TotalRerollsSpent { get; }
        public ReadOnlyCollection<int> HandUpgrades { get; }
        public ReadOnlyCollection<ModifierInstanceSnapshot> Modifiers { get; }
        public long ModifierBaseScore { get; }
        public long ModifierMultiplier { get; }
        public ReadOnlyCollection<ModifierContribution> ModifierContributions { get; }
        public RewardOffer PendingReward { get; }
        public int RewardRefreshes { get; }
        /** The reward refresh points one refresh of a charm offer costs; zero when the encounter offers no refreshes. */
        public int RewardRefreshCost { get; }
        /** How many charm offer refreshes the held reward refresh points pay for. */
        public int RewardRefreshesAvailable { get; }
        public ReadOnlyCollection<ModifierFamilyDefinition.Id> ExcludedRewardFamilies { get; }
        public ReadOnlyCollection<HandCategory> RecentHandUpgrades { get; }
        public ReadOnlyCollection<string> UnlockedModifierIds { get; }
        public ReadOnlyCollection<string> NewlyUnlockedModifierIds { get; }
        public ReadOnlyCollection<ModifierFamilyDefinition.Id> DiscoveredFamilyIds { get; }

        internal EncounterSnapshot(EncounterState state, EncounterDefinition definition, string catalogVersion)
        {
            var stage = definition.Stages[state.StageIndex];
            EncounterId = definition.Id;
            CatalogVersion = catalogVersion;
            Revision = state.Revision;
            Phase = state.Phase;
            Outcome = state.Outcome;
            StageIndex = state.StageIndex;
            HandIndex = state.HandIndex;
            StageCount = definition.Stages.Count;
            StartingThreshold = stage.Threshold;
            RemainingThreshold = state.RemainingThreshold;
            StartingHands = stage.StartingHands;
            StartingRerolls = stage.StartingRerolls;
            HandsRemaining = state.HandsRemaining;
            RerollsRemaining = state.RerollsRemaining;
            LockedMask = EncounterRuleResolver.LockedDice(definition, state);
            CanReroll = state.Phase == EncounterPhase.Playing && LockedMask != DiceValues.AllMask &&
                EncounterRuleResolver.CanAffordReroll(definition, state);
            EncounterRules = EncounterRuleResolver.Snapshots(definition, state);
            CurrentDice = state.CurrentDice;
            InitialDice = state.InitialDice;
            DiceBeforeLastReroll = state.DiceBeforeLastReroll;
            PreviousPlayedDice = state.PreviousPlayedDice;
            LatestRerollMask = state.LatestRerollMask;
            LatestChangedMask = state.LatestChangedMask;
            EverRerolledMask = state.EverRerolledMask;
            PieceInteractions = TablePieceStates.Capture(definition, state);
            RerollsSpentOnHand = state.RerollsSpentOnHand;
            HandsPlayedInStage = state.HandsPlayedInStage;
            PreviousStageStartingHands = state.PreviousStageStartingHands;
            PreviousStageHandsRemaining = state.PreviousStageHandsRemaining;
            TotalScore = state.TotalScore;
            TotalHandsPlayed = state.TotalHandsPlayed;
            TotalRerollsSpent = state.TotalRerollsSpent;
            HandUpgrades = Array.AsReadOnly((int[])state.HandUpgrades.Clone());
            Modifiers = ModifierResolver.CaptureInstances(state);
            ModifierBaseScore = state.ModifierBaseScore;
            ModifierMultiplier = state.ModifierMultiplier;
            ModifierContributions = new List<ModifierContribution>(state.ModifierContributions).AsReadOnly();
            PendingReward = state.PendingReward;
            RewardRefreshes = state.RewardRefreshes;
            RewardRefreshCost = definition.RewardPolicy?.RefreshCost ?? 0;
            RewardRefreshesAvailable = RewardRefreshCost > 0 ? state.RewardRefreshes / RewardRefreshCost : 0;
            ExcludedRewardFamilies = new List<ModifierFamilyDefinition.Id>(state.ExcludedRewardFamilies).AsReadOnly();
            RecentHandUpgrades = new List<HandCategory>(state.RecentHandUpgrades).AsReadOnly();
            UnlockedModifierIds = new List<string>(state.UnlockedModifierIds).AsReadOnly();
            NewlyUnlockedModifierIds = new List<string>(state.NewlyUnlockedModifierIds).AsReadOnly();
            DiscoveredFamilyIds = new List<ModifierFamilyDefinition.Id>(state.DiscoveredFamilyIds).AsReadOnly();
        }
    }

    /**
     * Formatting an event description takes from its caller: the engine holds no display names, so charm names and
     * number and identifier formatting come from the presentation that shows the description.
     */
    public interface IDiagnosticFormat
    {
        string Number(long value);
        /** Spaces an engine identifier or enum name into words. */
        string Humanize(string value);
        string ModifierName(string definitionId);
    }

    public abstract class RuleEvent
    {
        public int StageIndex { get; }
        public int HandIndex { get; }
        internal RuleEvent(int stageIndex, int handIndex)
        {
            StageIndex = stageIndex;
            HandIndex = handIndex;
        }

        /** Developer diagnostics for logs and debug views, not player-facing text: the stage and hand, then the event's own detail. */
        public string Describe(IDiagnosticFormat format)
        {
            if (format == null) throw new ArgumentNullException(nameof(format));
            return "Stage " + (StageIndex + 1) + " / hand " + (HandIndex + 1) + " - " + DescribeDetail(format);
        }

        /** What happened, in the event's own words. */
        protected abstract string DescribeDetail(IDiagnosticFormat format);

        /** Whether the event ends the player's current dice selection: the dice it was made on are no longer in play. */
        public virtual bool EndsDiceSelection => false;

        private protected static string DescribeDice(DiceValues dice)
            => dice[0] + "  " + dice[1] + "  " + dice[2] + "  " + dice[3] + "  " + dice[4];

        private protected static string DescribeContribution(ModifierContribution contribution, IDiagnosticFormat format)
            => "+" + format.Number(contribution.BaseScore) + " base, +" + format.Number(contribution.Multiplier) +
               " multiplier, +" + format.Number(contribution.Rerolls) + " rerolls";
    }

    public sealed class ModifierUnlockedEvent : RuleEvent
    {
        public string ModifierId { get; }
        public ModifierAvailabilityDefinition Availability { get; }
        internal ModifierUnlockedEvent(EncounterState state, ModifierDefinition modifier) : base(state.StageIndex, state.HandIndex)
        {
            ModifierId = modifier.Id;
            Availability = modifier.Availability;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => "Unlocked " + format.ModifierName(ModifierId) + " (" + format.Humanize(Availability.Access.ToString()) + ", " +
                format.Humanize(Availability.Scope.ToString()) + (Availability.ScopeId != null ? " " + Availability.ScopeId : "") +
                ", power tier " + Availability.PowerTier + ").";
    }

    public sealed class StageStartedEvent : RuleEvent
    {
        public long Threshold { get; }
        public int Hands { get; }
        public int Rerolls { get; }
        internal StageStartedEvent(int stageIndex, StageDefinition definition) : base(stageIndex, 0)
        {
            Threshold = definition.Threshold;
            Hands = definition.StartingHands;
            Rerolls = definition.StartingRerolls;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => "Started. Target " + format.Number(Threshold) + ", " + Hands + " hands, " + Rerolls + " rerolls.";
    }

    public sealed class HandStartedEvent : RuleEvent
    {
        public int HandsRemaining { get; }
        public int RerollsRemaining { get; }
        internal HandStartedEvent(EncounterState state) : base(state.StageIndex, state.HandIndex)
        {
            HandsRemaining = state.HandsRemaining;
            RerollsRemaining = state.RerollsRemaining;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => "Started. " + HandsRemaining + " hands and " + RerollsRemaining + " rerolls remain.";
    }

    public sealed class DiceRolledEvent : RuleEvent
    {
        public RollKind Kind { get; }
        public DiceValues? Before { get; }
        public DiceValues After { get; }
        public int SelectedMask { get; }
        public int ChangedMask { get; }
        internal DiceRolledEvent(EncounterState state, RollKind kind, DiceValues? before, int selectedMask, int changedMask)
            : base(state.StageIndex, state.HandIndex)
        {
            Kind = kind;
            Before = before;
            After = state.CurrentDice;
            SelectedMask = selectedMask;
            ChangedMask = changedMask;
        }

        public override bool EndsDiceSelection => true;

        protected override string DescribeDetail(IDiagnosticFormat format)
            => format.Humanize(Kind.ToString()) + ": [" + DescribeDice(After) + "]. Rerolled mask " + SelectedMask +
                ", changed mask " + ChangedMask + ".";
    }

    public sealed class EncounterRuleActivatedEvent : RuleEvent
    {
        public string RuleId { get; }
        public long MultiplierBonus { get; }

        internal EncounterRuleActivatedEvent(EncounterState state, string ruleId, long multiplierBonus = 0)
            : base(state.StageIndex, state.HandIndex)
        {
            RuleId = ruleId;
            MultiplierBonus = multiplierBonus;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => RuleId + " activated" + (MultiplierBonus != 0 ? ": +" + format.Number(MultiplierBonus) + " multiplier." : ".");
    }

    public sealed class ResourceChangedEvent : RuleEvent
    {
        public ResourceKind Resource { get; }
        public ResourceChangeCause Cause { get; }
        public int Before { get; }
        public int After { get; }
        /** The charm activation whose effect granted the change, or null for a change no charm granted. */
        public ModifierActivatedEvent GrantedBy { get; }
        internal ResourceChangedEvent(EncounterState state, ResourceKind resource, ResourceChangeCause cause, int before, int after,
            ModifierActivatedEvent grantedBy = null)
            : base(state.StageIndex, state.HandIndex)
        {
            Resource = resource;
            Cause = cause;
            Before = before;
            After = after;
            GrantedBy = grantedBy;
            grantedBy?.AddGrant(this);
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => format.Humanize(Resource.ToString()) + " " + Before + " -> " + After + " (" + format.Humanize(Cause.ToString()) + ").";
    }

    public sealed class HandScoredEvent : RuleEvent
    {
        public DiceValues Dice { get; }
        public ScoreBreakdown Score { get; }
        /** The PlayHand charm activations the score includes, in activation order. */
        public ReadOnlyCollection<ModifierActivatedEvent> Activations { get; }
        /** The encounter rule activations whose multiplier bonuses the score includes, in activation order. */
        public ReadOnlyCollection<EncounterRuleActivatedEvent> RuleBonuses { get; }
        internal HandScoredEvent(EncounterState state, ScoreBreakdown score, IEnumerable<ModifierActivatedEvent> activations,
            IEnumerable<EncounterRuleActivatedEvent> ruleBonuses) : base(state.StageIndex, state.HandIndex)
        {
            Dice = state.CurrentDice;
            Score = score;
            Activations = new List<ModifierActivatedEvent>(activations).AsReadOnly();
            RuleBonuses = new List<EncounterRuleActivatedEvent>(ruleBonuses).AsReadOnly();
        }

        public override bool EndsDiceSelection => true;

        /** The played dice, the full score breakdown, then one line per modifier contribution. */
        protected override string DescribeDetail(IDiagnosticFormat format)
        {
            var text = new StringBuilder("Played [").Append(DescribeDice(Dice)).Append("]\n")
                .Append(format.Humanize(Score.Category.ToString())).Append(" (upgrade level ").Append(Score.UpgradeCount).Append(")\n")
                .Append("Base: dice ").Append(format.Number(Score.DiceSum)).Append(" + category ").Append(format.Number(Score.CategoryBaseScore))
                .Append(" + upgrades ").Append(format.Number(Score.UpgradeBaseScore)).Append(" + modifiers ")
                .Append(format.Number(Score.ModifierBaseScore)).Append(" = ").Append(format.Number(Score.BaseScore))
                .Append("\nMultiplier: category ").Append(format.Number(Score.CategoryMultiplier)).Append(" + upgrades ")
                .Append(format.Number(Score.UpgradeMultiplier)).Append(" + modifiers ").Append(format.Number(Score.ModifierMultiplier))
                .Append(" = ").Append(format.Number(Score.Multiplier)).Append("\nFinal score: ").Append(format.Number(Score.BaseScore))
                .Append(" x ").Append(format.Number(Score.Multiplier)).Append(" = ").Append(format.Number(Score.HandScore));
            foreach (ModifierContribution contribution in Score.ModifierContributions)
                text.Append("\n  ").Append(format.ModifierName(contribution.DefinitionId)).Append(" #")
                    .Append(contribution.InstanceId).Append(": ").Append(DescribeContribution(contribution, format));
            return text.ToString();
        }
    }

    public sealed class ThresholdChangedEvent : RuleEvent
    {
        public long Before { get; }
        public long After { get; }
        public long AppliedScore => Before - After;
        public long HandScore { get; }
        public long ExcessScore => HandScore - AppliedScore;
        internal ThresholdChangedEvent(EncounterState state, long before, long handScore) : base(state.StageIndex, state.HandIndex)
        {
            Before = before;
            After = state.RemainingThreshold;
            HandScore = handScore;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => "Target remaining " + format.Number(Before) + " -> " + format.Number(After) + "; applied " +
                format.Number(AppliedScore) + ", excess " + format.Number(ExcessScore) + ".";
    }

    public sealed class StageClearedEvent : RuleEvent
    {
        public int HandsRemaining { get; }
        public int RerollsRemaining { get; }
        internal StageClearedEvent(EncounterState state) : base(state.StageIndex, state.HandIndex)
        {
            HandsRemaining = state.HandsRemaining;
            RerollsRemaining = state.RerollsRemaining;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => "Cleared. " + HandsRemaining + " hands and " + RerollsRemaining + " rerolls remain.";
    }

    public sealed class RewardsRequiredEvent : RuleEvent
    {
        internal RewardsRequiredEvent(EncounterState state) : base(state.StageIndex, state.HandIndex) { }

        protected override string DescribeDetail(IDiagnosticFormat format) => "Rewards required before the next stage.";
    }

    public sealed class EncounterCompletedEvent : RuleEvent
    {
        public EncounterOutcome Outcome { get; }
        public long TotalScore { get; }
        public long HandsPlayed { get; }
        public long RerollsSpent { get; }
        internal EncounterCompletedEvent(EncounterState state) : base(state.StageIndex, state.HandIndex)
        {
            Outcome = state.Outcome;
            TotalScore = state.TotalScore;
            HandsPlayed = state.TotalHandsPlayed;
            RerollsSpent = state.TotalRerollsSpent;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => format.Humanize(Outcome.ToString()) + ". Total score " + format.Number(TotalScore) + "; " + HandsPlayed +
                " hands played, " + RerollsSpent + " rerolls spent.";
    }

    public sealed class Resolution
    {
        public bool Accepted => Rejection == CommandRejection.None;
        public CommandRejection Rejection { get; }
        public long PreviousRevision { get; }
        public EncounterSnapshot Snapshot { get; }
        public ReadOnlyCollection<RuleEvent> Events { get; }

        internal Resolution(long previousRevision, EncounterSnapshot snapshot, IEnumerable<RuleEvent> events,
            CommandRejection rejection = CommandRejection.None)
        {
            PreviousRevision = previousRevision;
            Snapshot = snapshot;
            Rejection = rejection;
            Events = new List<RuleEvent>(events).AsReadOnly();
        }
    }
}
