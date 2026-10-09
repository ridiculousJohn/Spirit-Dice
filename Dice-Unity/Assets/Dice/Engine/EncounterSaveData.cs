using System;

namespace Dice.Engine
{
    [Serializable]
    public sealed class EncounterSaveData
    {
        public const int CurrentFormatVersion = 3;
        public int SaveFormatVersion;
        public int RulesVersion;
        public int RandomAlgorithmVersion;
        public string CatalogVersion;
        public string EncounterId;
        public long Revision;
        public EncounterPhase Phase;
        public EncounterOutcome Outcome;
        public int StageIndex;
        public int HandIndex;
        public long RemainingThreshold;
        public int HandsRemaining;
        public int RerollsRemaining;
        public int[] CurrentDice;
        public int[] InitialDice;
        public int[] DiceBeforeLastReroll;
        public int[] PreviousPlayedDice;
        public int LatestRerollMask;
        public int LatestChangedMask;
        public int EverRerolledMask;
        public int RerollsSpentOnHand;
        public int RerollsSpentInStage;
        public int FreeRerollsUsedInStage;
        public int HandsPlayedInStage;
        public bool HasPreviousStage;
        public int PreviousStageStartingHands;
        public int PreviousStageHandsRemaining;
        public long TotalScore;
        public long TotalHandsPlayed;
        public long TotalRerollsSpent;
        public long TotalFreeRerollsUsed;
        public int[] HandUpgrades;
        public ulong DiceRandomState;
        public EncounterSaveModifierData[] Modifiers;
        public long ModifierBaseScore;
        public long ModifierMultiplier;
        public EncounterSaveContributionData[] ModifierContributions;
        public ulong RewardRandomState;
        public int RewardRefreshes;
        public EncounterSaveRewardData PendingReward;
        public long NextOfferId;
        public long NextOptionId;
        public string[] ExcludedRewardFamilies;
        public HandCategory[] RecentHandUpgrades;
        public HandCategory[] PendingUpgradeCategories;
        public string[] UnlockedModifierIds;
        public string[] NewlyUnlockedModifierIds;
        public bool PendingModifierOfferUsesLegacyEligibility;
        public string[] PendingModifierOfferUnlockedIds;
    }

    [Serializable]
    public sealed class EncounterSaveModifierData
    {
        public long InstanceId;
        public string DefinitionId;
        public int ActivationsThisHand;
        public int ActivationsThisStage;
        public long TotalActivations;
    }

    [Serializable]
    public sealed class EncounterSaveContributionData
    {
        public long InstanceId;
        public string DefinitionId;
        public ModifierPhase Phase;
        public long BaseScore;
        public long Multiplier;
        public int Rerolls;
    }

    [Serializable]
    public sealed class EncounterSaveRewardData
    {
        public long OfferId;
        public RewardKind Kind;
        public int TargetStageIndex;
        public EncounterSaveRewardOptionData[] Options;
    }

    [Serializable]
    public sealed class EncounterSaveRewardOptionData
    {
        public long OptionId;
        public string DefinitionId;
        public bool HasCategory;
        public HandCategory Category;
    }

    public enum EncounterRestoreError
    {
        None, NullSave, SaveFormatVersionMismatch, RulesVersionMismatch, RandomAlgorithmVersionMismatch,
        CatalogVersionMismatch, EncounterNotFound, InvalidDefinition, InvalidState
    }

    public sealed class RestoreEncounterResult
    {
        public bool Success => Error == EncounterRestoreError.None;
        public EncounterSession Session { get; }
        public EncounterRestoreError Error { get; }
        public string Detail { get; }

        internal RestoreEncounterResult(EncounterSession session, EncounterRestoreError error, string detail)
        {
            Session = session;
            Error = error;
            Detail = detail;
        }
    }
}
