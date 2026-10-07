using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public enum EncounterPhase { Playing, AwaitingRewards, Completed, ChoosingModifier, ChoosingHandUpgrade }
    public enum EncounterOutcome { None, Won, Lost, Abandoned }
    public enum CommandRejection
    {
        None, StaleRevision, InvalidAction, EncounterCompleted, InvalidPhase, InvalidDiceSelection, NoRerollsRemaining,
        InvalidOffer, InvalidRewardOption, InsufficientRewardRefreshes
    }
    public enum RollKind { Initial, PlayerReroll }
    public enum ResourceKind { Hands, Rerolls, RewardRefreshes }
    public enum ResourceChangeCause { PlayHand, RerollDice, ModifierEffect, StageClear, RefreshReward }

    public abstract class EncounterAction
    {
        internal EncounterAction() { }
    }

    public sealed class RerollDiceAction : EncounterAction
    {
        public int SelectedMask { get; }
        public RerollDiceAction(int selectedMask) => SelectedMask = selectedMask;
    }

    public sealed class PlayHandAction : EncounterAction
    {
        public static PlayHandAction Instance { get; } = new PlayHandAction();
        private PlayHandAction() { }
    }

    public sealed class AbandonEncounterAction : EncounterAction
    {
        public static AbandonEncounterAction Instance { get; } = new AbandonEncounterAction();
        private AbandonEncounterAction() { }
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
        public DiceValues CurrentDice { get; }
        public DiceValues InitialDice { get; }
        public DiceValues? DiceBeforeLastReroll { get; }
        public DiceValues? PreviousPlayedDice { get; }
        public int LatestRerollMask { get; }
        public int LatestChangedMask { get; }
        public int EverRerolledMask { get; }
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
        public ReadOnlyCollection<string> ExcludedRewardFamilies { get; }
        public ReadOnlyCollection<HandCategory> RecentHandUpgrades { get; }

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
            CurrentDice = state.CurrentDice;
            InitialDice = state.InitialDice;
            DiceBeforeLastReroll = state.DiceBeforeLastReroll;
            PreviousPlayedDice = state.PreviousPlayedDice;
            LatestRerollMask = state.LatestRerollMask;
            LatestChangedMask = state.LatestChangedMask;
            EverRerolledMask = state.EverRerolledMask;
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
            ExcludedRewardFamilies = new List<string>(state.ExcludedRewardFamilies).AsReadOnly();
            RecentHandUpgrades = new List<HandCategory>(state.RecentHandUpgrades).AsReadOnly();
        }
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
    }

    public sealed class ResourceChangedEvent : RuleEvent
    {
        public ResourceKind Resource { get; }
        public ResourceChangeCause Cause { get; }
        public int Before { get; }
        public int After { get; }
        public long? SourceInstanceId { get; }
        public string SourceDefinitionId { get; }
        internal ResourceChangedEvent(EncounterState state, ResourceKind resource, ResourceChangeCause cause, int before, int after,
            long? sourceInstanceId = null, string sourceDefinitionId = null)
            : base(state.StageIndex, state.HandIndex)
        {
            Resource = resource;
            Cause = cause;
            Before = before;
            After = after;
            SourceInstanceId = sourceInstanceId;
            SourceDefinitionId = sourceDefinitionId;
        }
    }

    public sealed class HandScoredEvent : RuleEvent
    {
        public DiceValues Dice { get; }
        public ScoreBreakdown Score { get; }
        internal HandScoredEvent(EncounterState state, ScoreBreakdown score) : base(state.StageIndex, state.HandIndex)
        {
            Dice = state.CurrentDice;
            Score = score;
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
    }

    public sealed class RewardsRequiredEvent : RuleEvent
    {
        internal RewardsRequiredEvent(EncounterState state) : base(state.StageIndex, state.HandIndex) { }
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
