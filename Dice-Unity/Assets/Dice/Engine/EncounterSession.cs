using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public sealed class EncounterSession
    {
        public const int RulesVersion = 9;
        internal const ulong RuleRandomSalt = 0x6a09e667f3bcc909UL;
        private const ulong RewardRandomSalt = 0xd1b54a32d192ed03UL;
        private readonly MechanicalCatalog catalog;
        private readonly EncounterDefinition definition;
        private EncounterState state;

        public Resolution InitialResolution { get; }
        /** The committed state and the encounter, as actions validate against them. */
        internal EncounterState State => state;
        internal EncounterDefinition Definition => definition;
        public EncounterSnapshot Snapshot => new EncounterSnapshot(state, definition, catalog.Version);

        private EncounterSession(MechanicalCatalog catalog, EncounterDefinition.Id encounterId, ulong seed, IEnumerable<string> unlockedModifierIds,
            IEnumerable<ModifierFamilyDefinition.Id> discoveredFamilyIds)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            definition = catalog.GetEncounter(encounterId);
            state = new EncounterState
            {
                HandUpgrades = new List<int>(definition.StartingUpgrades).ToArray(),
                DiceRandom = new DeterministicRandom(seed),
                RewardRandom = new DeterministicRandom(seed ^ RewardRandomSalt),
                // Only rules that draw from this stream seed it; other encounters keep it empty.
                RuleRandom = new DeterministicRandom(EncounterRuleResolver.UsesRuleRandom(definition) ? seed ^ RuleRandomSalt : 0),
                RewardRefreshes = definition.RewardPolicy?.StartingRefreshes ?? 0,
                Modifiers = new ModifierInstanceState[definition.StartingModifiers.Count],
                PieceStates = TablePieceStates.Initial(definition),
                RuleStates = EncounterRuleResolver.InitialStates(definition)
            };
            for (int i = 0; i < state.Modifiers.Length; ++i)
                state.Modifiers[i] = new ModifierInstanceState
                {
                    InstanceId = i + 1L,
                    DefinitionId = definition.StartingModifiers[i]
                };
            // A session created without the player's discoveries, as previews and diagnostics are, has discovered every family.
            AddDiscoveredFamilies(discoveredFamilyIds ?? FamilyIds(catalog));
            foreach (var modifier in catalog.Modifiers)
                if (modifier.IsDiscoveredFromStart && !state.DiscoveredFamilyIds.Contains(modifier.Family.Identity))
                    state.DiscoveredFamilyIds.Add(modifier.Family.Identity);
            AddUnlockedModifiers(unlockedModifierIds ?? Array.Empty<string>());
            var events = new List<RuleEvent>();
            StartStage(state, events);
            InitialResolution = new Resolution(-1, Snapshot, events);
        }

        public static EncounterSession CreateEncounter(MechanicalCatalog catalog, EncounterDefinition.Id encounterId, ulong seed,
            IEnumerable<string> unlockedModifierIds = null, IEnumerable<ModifierFamilyDefinition.Id> discoveredFamilyIds = null)
            => new EncounterSession(catalog, encounterId, seed, unlockedModifierIds, discoveredFamilyIds);

        private static List<ModifierFamilyDefinition.Id> FamilyIds(MechanicalCatalog catalog)
        {
            var ids = new List<ModifierFamilyDefinition.Id>();
            foreach (var family in catalog.Families) ids.Add(family.Identity);
            return ids;
        }

        public void AddDiscoveredFamilies(IEnumerable<ModifierFamilyDefinition.Id> discoveredFamilyIds)
        {
            if (discoveredFamilyIds == null) throw new ArgumentNullException(nameof(discoveredFamilyIds));
            var next = state.Copy();
            foreach (var id in discoveredFamilyIds)
            {
                if (!catalog.HasFamily(id))
                    throw new ArgumentException("Discovered families must exist in the catalog.", nameof(discoveredFamilyIds));
                if (!next.DiscoveredFamilyIds.Contains(id)) next.DiscoveredFamilyIds.Add(id);
            }
            state = next;
        }

        public void AddUnlockedModifiers(IEnumerable<string> unlockedModifierIds)
        {
            if (unlockedModifierIds == null) throw new ArgumentNullException(nameof(unlockedModifierIds));
            var next = state.Copy();
            var known = new HashSet<string>(next.UnlockedModifierIds, StringComparer.Ordinal);
            foreach (string id in unlockedModifierIds)
            {
                var modifier = catalog.GetModifier(id);
                if (modifier.Availability.Access != ModifierAccess.PermanentUnlock)
                    throw new ArgumentException("Unlocks must reference permanent-unlock modifiers.", nameof(unlockedModifierIds));
                if (known.Add(id)) next.UnlockedModifierIds.Add(id);
                if (!next.DiscoveredFamilyIds.Contains(modifier.Family.Identity)) next.DiscoveredFamilyIds.Add(modifier.Family.Identity);
            }
            state = next;
        }

        private EncounterSession(MechanicalCatalog catalog, EncounterDefinition definition, EncounterState state)
        {
            this.catalog = catalog;
            this.definition = definition;
            this.state = state;
            InitialResolution = new Resolution(state.Revision, Snapshot, Array.Empty<RuleEvent>());
        }

        public EncounterSaveData CaptureSave() => EncounterPersistence.Capture(catalog, definition, state);

        public static RestoreEncounterResult RestoreEncounter(MechanicalCatalog catalog, EncounterSaveData save)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!EncounterPersistence.TryRestore(catalog, save, out var definition, out var state, out var error, out var detail))
                return new RestoreEncounterResult(null, error, detail);
            return new RestoreEncounterResult(new EncounterSession(catalog, definition, state), EncounterRestoreError.None, null);
        }

        public Resolution Execute(EncounterAction action, long expectedRevision)
        {
            CommandRejection rejection = Validate(action, expectedRevision);
            if (rejection != CommandRejection.None)
                return new Resolution(state.Revision, Snapshot, Array.Empty<RuleEvent>(), rejection);

            // Resolve on a private copy so an arithmetic or rules failure cannot partially commit an action.
            EncounterState next = state.Copy();
            var events = new List<RuleEvent>();
            action.Resolve(this, next, events);
            next.Revision = checked(state.Revision + 1);
            var snapshot = new EncounterSnapshot(next, definition, catalog.Version);
            var result = new Resolution(state.Revision, snapshot, events);
            state = next;
            return result;
        }

        public ScoreBreakdown PreviewPlayHand() => PreviewHandResolution().Score;

        /** Inspect PlayHand eligibility without applying effects, consuming resources, or advancing random streams. */
        public ReadOnlyCollection<ModifierInspection> InspectModifiersForPlayHand()
            => ModifierResolver.InspectForPlayHand(state, catalog, definition);

        public HandPreview PreviewHandResolution()
        {
            if (state.Phase != EncounterPhase.Playing) throw new InvalidOperationException("The encounter is not accepting a hand.");
            EncounterState preview = state.Copy();
            var events = new List<RuleEvent>();
            ScoreBreakdown score = ResolvePlayedHand(preview, events, out _, out _);
            return new HandPreview(score, preview, events);
        }

        /** Scores the played hand; the activations and rule bonuses are those the score includes, in order. */
        private ScoreBreakdown ResolvePlayedHand(EncounterState next, List<RuleEvent> events,
            out List<ModifierActivatedEvent> activations, out List<EncounterRuleActivatedEvent> ruleBonuses)
        {
            int beforeHands = next.HandsRemaining;
            --next.HandsRemaining;
            events.Add(new ResourceChangedEvent(next, ResourceKind.Hands, ResourceChangeCause.PlayHand,
                beforeHands, next.HandsRemaining));
            activations = ModifierResolver.Resolve(next, catalog, definition, ModifierPhase.PlayHand, events);
            ruleBonuses = new List<EncounterRuleActivatedEvent>();
            long ruleMultiplier = 0;
            foreach (var rule in definition.Rules)
            {
                long bonus = rule.ScoredHandMultiplierBonus(next.CurrentDice, next.TotalHandsPlayed);
                if (bonus == 0) continue;
                ruleMultiplier = checked(ruleMultiplier + bonus);
                var activated = new EncounterRuleActivatedEvent(next, rule.Id, bonus);
                ruleBonuses.Add(activated);
                events.Add(activated);
            }
            return HandScorer.Calculate(next.CurrentDice, catalog, next.HandUpgrades, next.ModifierContributions,
                ruleMultiplier);
        }

        private CommandRejection Validate(EncounterAction action, long expectedRevision)
        {
            if (expectedRevision != state.Revision) return CommandRejection.StaleRevision;
            if (action == null) return CommandRejection.InvalidAction;
            if (state.Phase == EncounterPhase.Completed) return CommandRejection.EncounterCompleted;
            return action.Validate(this);
        }

        internal CommandRejection ValidateRewardOffer(EncounterPhase phase, long offerId)
        {
            if (state.Phase != phase) return CommandRejection.InvalidPhase;
            if (state.PendingReward == null || state.PendingReward.OfferId != offerId) return CommandRejection.InvalidOffer;
            return CommandRejection.None;
        }

        internal CommandRejection ValidateRewardSelection(EncounterPhase phase, long offerId, long optionId)
        {
            var rejection = ValidateRewardOffer(phase, offerId);
            if (rejection != CommandRejection.None) return rejection;
            foreach (var option in state.PendingReward.Options)
                if (option.OptionId == optionId) return CommandRejection.None;
            return CommandRejection.InvalidRewardOption;
        }

        private void BeginRewards(EncounterState next, List<RuleEvent> events)
        {
            if (definition.RewardPolicy == null)
            {
                next.Phase = EncounterPhase.AwaitingRewards;
                events.Add(new RewardsRequiredEvent(next));
                return;
            }
            int before = next.RewardRefreshes;
            next.RewardRefreshes = checked(before + definition.RewardPolicy.RefreshesPerClear);
            if (before != next.RewardRefreshes)
                events.Add(new ResourceChangedEvent(next, ResourceKind.RewardRefreshes, ResourceChangeCause.StageClear,
                    before, next.RewardRefreshes));
            OfferModifiers(next, false, events);
        }

        private void OfferModifiers(EncounterState next, bool isRefresh, List<RuleEvent> events)
        {
            var owned = new string[next.Modifiers.Length];
            for (int i = 0; i < owned.Length; ++i) owned[i] = next.Modifiers[i].DefinitionId;
            var generated = RewardGenerator.GenerateModifiers(catalog, definition.RewardPolicy, next.ClearedStageNumber,
                owned, next.ExcludedRewardFamilies, ref next.RewardRandom, definition, next.UnlockedModifierIds);
            next.PendingModifierOfferUnlockedIds = new List<string>(next.UnlockedModifierIds);
            next.ExcludedRewardFamilies = new List<ModifierFamilyDefinition.Id>(generated.ExcludedFamilyIds);
            // Upgrade choices are fixed before any modifier refresh can consume the reward stream.
            if (!isRefresh)
                next.PendingUpgradeCategories = new List<HandCategory>(RewardGenerator.GenerateHandUpgrades(catalog,
                    definition.RewardPolicy, next.IncomingStageNumber, next.HandUpgrades, next.RecentHandUpgrades,
                    ref next.RewardRandom));
            var options = new List<RewardOption>();
            foreach (string id in generated.ModifierIds)
                options.Add(new RewardOption(AllocateOptionId(next), id));
            if (options.Count == 0)
            {
                next.PendingReward = null;
                next.ExcludedRewardFamilies.Clear();
                events.Add(new RewardSkippedEvent(next, RewardKind.Modifier));
                OfferHandUpgrades(next, events);
                return;
            }
            SetOffer(next, RewardKind.Modifier, options, isRefresh, events);
        }

        internal void RefreshModifierOffer(EncounterState next, List<RuleEvent> events)
        {
            int before = next.RewardRefreshes;
            next.RewardRefreshes -= definition.RewardPolicy.RefreshCost;
            events.Add(new ResourceChangedEvent(next, ResourceKind.RewardRefreshes, ResourceChangeCause.RefreshReward,
                before, next.RewardRefreshes));
            OfferModifiers(next, true, events);
        }

        internal void SelectModifier(EncounterState next, long optionId, List<RuleEvent> events)
        {
            RewardOption option = GetSelectedOption(next, optionId);
            events.Add(new RewardSelectedEvent(next, next.PendingReward, option));
            int index = next.Modifiers.Length;
            Array.Resize(ref next.Modifiers, checked(index + 1));
            var modifier = new ModifierInstanceState { InstanceId = index + 1L, DefinitionId = option.DefinitionId };
            next.Modifiers[index] = modifier;
            events.Add(new ModifierAcquiredEvent(next, modifier));
            next.PendingReward = null;
            next.ExcludedRewardFamilies.Clear();
            next.PendingModifierOfferUnlockedIds = null;
            next.PendingUnlockOfferDiscoveredFamilyIds = null;
            OfferHandUpgrades(next, events);
        }

        private void OfferHandUpgrades(EncounterState next, List<RuleEvent> events)
        {
            next.PendingModifierOfferUnlockedIds = null;
            var categories = next.PendingUpgradeCategories
                ?? throw new InvalidOperationException("The hand-upgrade choices have not been generated.");
            var options = new List<RewardOption>();
            foreach (HandCategory category in categories)
                options.Add(new RewardOption(AllocateOptionId(next), null, category));
            next.PendingUpgradeCategories = null;
            if (options.Count == 0)
            {
                next.PendingReward = null;
                events.Add(new RewardSkippedEvent(next, RewardKind.HandUpgrade));
                AdvanceStage(next, events);
                return;
            }
            SetOffer(next, RewardKind.HandUpgrade, options, false, events);
        }

        internal void SelectHandUpgrade(EncounterState next, long optionId, List<RuleEvent> events)
        {
            RewardOption option = GetSelectedOption(next, optionId);
            HandCategory category = option.Category.Value;
            int before = next.HandUpgrades[(int)category];
            if (before >= catalog.GetHand(category).MaximumUpgrades)
                throw new InvalidOperationException("An offered hand cannot exceed its upgrade limit.");
            events.Add(new RewardSelectedEvent(next, next.PendingReward, option));
            next.HandUpgrades[(int)category] = checked(before + 1);
            events.Add(new HandUpgradedEvent(next, category, before));
            next.RecentHandUpgrades.Insert(0, category);
            int historyCount = definition.RewardPolicy.MaximumExcludedRecentChoices;
            if (next.RecentHandUpgrades.Count > historyCount)
                next.RecentHandUpgrades.RemoveRange(historyCount, next.RecentHandUpgrades.Count - historyCount);
            next.PendingReward = null;
            AdvanceStage(next, events);
        }

        private static RewardOption GetSelectedOption(EncounterState next, long optionId)
        {
            foreach (var option in next.PendingReward.Options)
                if (option.OptionId == optionId) return option;
            throw new InvalidOperationException("The validated reward option is missing.");
        }

        private static long AllocateOptionId(EncounterState next)
        {
            long id = next.NextOptionId;
            next.NextOptionId = checked(id + 1);
            return id;
        }

        private void SetOffer(EncounterState next, RewardKind kind, IEnumerable<RewardOption> options,
            bool isRefresh, List<RuleEvent> events)
        {
            long id = next.NextOfferId;
            next.NextOfferId = checked(id + 1);
            next.PendingReward = new RewardOffer(id, kind, next.StageIndex + 1, options, definition);
            next.Phase = kind == RewardKind.Modifier ? EncounterPhase.ChoosingModifier :
                kind == RewardKind.ModifierUnlock ? EncounterPhase.ChoosingUnlock : EncounterPhase.ChoosingHandUpgrade;
            events.Add(new RewardOfferedEvent(next, next.PendingReward, isRefresh));
        }

        private void AdvanceStage(EncounterState next, List<RuleEvent> events)
        {
            next.PreviousStageStartingHands = definition.Stages[next.StageIndex].StartingHands;
            next.PreviousStageHandsRemaining = next.HandsRemaining;
            ++next.StageIndex;
            StartStage(next, events);
        }

        private void StartStage(EncounterState next, List<RuleEvent> events)
        {
            StageDefinition stage = definition.Stages[next.StageIndex];
            next.Phase = EncounterPhase.Playing;
            next.RemainingThreshold = stage.Threshold;
            next.HandsRemaining = stage.StartingHands;
            next.RerollsRemaining = stage.StartingRerolls;
            next.HandsPlayedInStage = 0;
            next.RerollsSpentInStage = 0;
            next.HandIndex = 0;
            foreach (var modifier in next.Modifiers) modifier.ActivationsThisStage = 0;
            EncounterRuleResolver.StageStarted(definition, next);
            events.Add(new StageStartedEvent(next.StageIndex, stage));
            StartHand(next, events);
        }

        private void StartHand(EncounterState next, List<RuleEvent> events)
        {
            next.ModifierBaseScore = 0;
            next.ModifierMultiplier = 0;
            next.ModifierContributions.Clear();
            foreach (var modifier in next.Modifiers) modifier.ActivationsThisHand = 0;
            next.RerollsSpentOnHand = 0;
            next.LatestRerollMask = 0;
            next.LatestChangedMask = 0;
            next.EverRerolledMask = 0;
            next.DiceBeforeLastReroll = null;
            events.Add(new HandStartedEvent(next));
            next.CurrentDice = Roll(next, DiceValues.AllMask);
            next.InitialDice = next.CurrentDice;
            events.Add(new DiceRolledEvent(next, RollKind.Initial, null, DiceValues.AllMask, DiceValues.AllMask));
            EncounterRuleResolver.HandStarted(definition, next, events);
            ModifierResolver.Resolve(next, catalog, definition, ModifierPhase.InitialRoll, events);
        }

        private static DiceValues Roll(EncounterState next, int selectedMask)
        {
            var faces = new int[DiceValues.Count];
            for (int i = 0; i < faces.Length; ++i)
                faces[i] = (selectedMask & (1 << i)) != 0 ? DiceValues.RollFace(ref next.DiceRandom) : next.CurrentDice[i];
            return new DiceValues(faces[0], faces[1], faces[2], faces[3], faces[4]);
        }

        internal void Reroll(EncounterState next, int selectedMask, List<RuleEvent> events)
        {
            int beforeRerolls = next.RerollsRemaining;
            var costWaiver = EncounterRuleResolver.WaiveRerollCost(definition, next);
            if (costWaiver == null) --next.RerollsRemaining;
            else events.Add(new EncounterRuleActivatedEvent(next, costWaiver.Id));
            next.RerollsSpentOnHand = checked(next.RerollsSpentOnHand + 1);
            next.RerollsSpentInStage = checked(next.RerollsSpentInStage + 1);
            next.TotalRerollsSpent = checked(next.TotalRerollsSpent + 1);
            if (beforeRerolls != next.RerollsRemaining)
                events.Add(new ResourceChangedEvent(next, ResourceKind.Rerolls, ResourceChangeCause.RerollDice,
                    beforeRerolls, next.RerollsRemaining));
            DiceValues before = next.CurrentDice;
            next.DiceBeforeLastReroll = before;
            next.CurrentDice = Roll(next, selectedMask);
            next.LatestRerollMask = selectedMask;
            next.EverRerolledMask |= selectedMask;
            next.LatestChangedMask = before.DifferingMask(next.CurrentDice);
            events.Add(new DiceRolledEvent(next, RollKind.PlayerReroll, before, selectedMask, next.LatestChangedMask));
            EncounterRuleResolver.PlayerRerollResolved(definition, next, selectedMask, events);
            // Interference targets kept dice, then reroll charms observe the disturbed faces.
            EncounterRuleResolver.Interfere(definition, next, InterferenceTrigger.PlayerReroll,
                DiceValues.AllMask & ~selectedMask, events);
            ModifierResolver.Resolve(next, catalog, definition, ModifierPhase.PlayerReroll, events);
        }

        internal void InteractWithPiece(EncounterState next, InteractWithPieceAction action, List<RuleEvent> events)
        {
            TablePieceStates.TryFind(definition, action.PieceId, action.InteractionId, out int slot, out var interaction);
            var before = next.PieceStates[slot];
            var after = interaction.Apply(before, action.Input);
            next.PieceStates[slot] = after;
            events.Add(new PieceInteractionChangedEvent(next, action.PieceId, action.InteractionId, before, after));
        }

        internal void Play(EncounterState next, List<RuleEvent> events)
        {
            // Committed hands may be disturbed before scoring; previews never are.
            EncounterRuleResolver.Interfere(definition, next, InterferenceTrigger.PlayHand, DiceValues.AllMask, events);
            ScoreBreakdown score = ResolvePlayedHand(next, events, out var activations, out var ruleBonuses);
            next.TotalScore = checked(next.TotalScore + score.HandScore);
            next.TotalHandsPlayed = checked(next.TotalHandsPlayed + 1);
            next.HandsPlayedInStage = checked(next.HandsPlayedInStage + 1);
            events.Add(new HandScoredEvent(next, score, activations, ruleBonuses));
            long beforeThreshold = next.RemainingThreshold;
            next.RemainingThreshold = score.HandScore >= beforeThreshold ? 0 : beforeThreshold - score.HandScore;
            events.Add(new ThresholdChangedEvent(next, beforeThreshold, score.HandScore));
            next.PreviousPlayedDice = next.CurrentDice;

            // Clearing on the final available hand still wins; exhaustion is checked after clearing.
            if (next.RemainingThreshold == 0)
            {
                events.Add(new StageClearedEvent(next));
                if (!OfferModifierUnlocks(next, events)) ContinueAfterUnlock(next, events);
            }
            else if (next.HandsRemaining == 0)
            {
                Complete(next, EncounterOutcome.Lost, events);
            }
            else
            {
                ++next.HandIndex;
                StartHand(next, events);
            }
        }

        internal static void Complete(EncounterState next, EncounterOutcome outcome, List<RuleEvent> events)
        {
            next.Phase = EncounterPhase.Completed;
            next.Outcome = outcome;
            next.PendingReward = null;
            next.PendingUnlockOfferDiscoveredFamilyIds = null;
            next.PendingUpgradeCategories = null;
            next.PendingModifierOfferUnlockedIds = null;
            events.Add(new EncounterCompletedEvent(next));
        }

        private bool OfferModifierUnlocks(EncounterState next, List<RuleEvent> events)
        {
            if (!definition.HasUnlockMilestone(next.ClearedStageNumber)) return false;
            var candidates = definition.UnlockCandidates(catalog, next.UnlockedModifierIds, next.DiscoveredFamilyIds);
            next.PendingUnlockOfferDiscoveredFamilyIds = new List<ModifierFamilyDefinition.Id>(next.DiscoveredFamilyIds);
            var options = new List<RewardOption>();
            while (options.Count < ModifierUnlockMilestone.OfferSize && candidates.Count > 0)
            {
                int index = next.RewardRandom.NextInt(candidates.Count);
                options.Add(new RewardOption(AllocateOptionId(next), candidates[index]));
                candidates.RemoveAt(index);
            }
            SetOffer(next, RewardKind.ModifierUnlock, options, false, events);
            return true;
        }

        internal void SelectModifierUnlock(EncounterState next, long optionId, List<RuleEvent> events)
        {
            RewardOption option = GetSelectedOption(next, optionId);
            events.Add(new RewardSelectedEvent(next, next.PendingReward, option));
            if (!next.UnlockedModifierIds.Contains(option.DefinitionId))
            {
                next.UnlockedModifierIds.Add(option.DefinitionId);
                var familyId = catalog.GetModifier(option.DefinitionId).Family.Identity;
                if (!next.DiscoveredFamilyIds.Contains(familyId)) next.DiscoveredFamilyIds.Add(familyId);
                next.NewlyUnlockedModifierIds.Add(option.DefinitionId);
                events.Add(new ModifierUnlockedEvent(next, catalog.GetModifier(option.DefinitionId)));
            }
            ContinueAfterUnlock(next, events);
        }

        internal void ContinueAfterUnlock(EncounterState next, List<RuleEvent> events)
        {
            next.PendingReward = null;
            next.PendingUnlockOfferDiscoveredFamilyIds = null;
            if (next.StageIndex == definition.Stages.Count - 1) Complete(next, EncounterOutcome.Won, events);
            else if (definition.Stages[next.StageIndex].ExitBehavior == StageExitBehavior.AwaitRewards) BeginRewards(next, events);
            else AdvanceStage(next, events);
        }
    }

    internal sealed class EncounterState
    {
        internal long Revision;
        internal EncounterPhase Phase;
        internal EncounterOutcome Outcome;
        internal int StageIndex;
        internal int HandIndex;
        /** The number of the stage just cleared, whose rewards are offered. */
        internal int ClearedStageNumber => StageIndex + 1;
        /** The number of the stage the offered rewards lead into. */
        internal int IncomingStageNumber => StageIndex + 2;
        internal long RemainingThreshold;
        internal int HandsRemaining;
        internal int RerollsRemaining;
        internal DiceValues CurrentDice;
        internal DiceValues InitialDice;
        internal DiceValues? DiceBeforeLastReroll;
        internal DiceValues? PreviousPlayedDice;
        internal int LatestRerollMask;
        internal int LatestChangedMask;
        internal int EverRerolledMask;
        internal int RerollsSpentOnHand;
        internal int RerollsSpentInStage;
        internal int HandsPlayedInStage;
        internal int? PreviousStageStartingHands;
        internal int? PreviousStageHandsRemaining;
        internal long TotalScore;
        internal long TotalHandsPlayed;
        internal long TotalRerollsSpent;
        internal int[] HandUpgrades;
        internal DeterministicRandom DiceRandom;
        internal ModifierInstanceState[] Modifiers;
        internal long ModifierBaseScore;
        internal long ModifierMultiplier;
        internal List<ModifierContribution> ModifierContributions = new List<ModifierContribution>();
        internal DeterministicRandom RewardRandom;
        internal DeterministicRandom RuleRandom;
        internal PieceInteractionState[] PieceStates = Array.Empty<PieceInteractionState>();
        /** Parallel to the definition's rules; null for rules without state. */
        internal EncounterRuleState[] RuleStates = Array.Empty<EncounterRuleState>();
        internal int RewardRefreshes;
        internal RewardOffer PendingReward;
        internal long NextOfferId = 1;
        internal long NextOptionId = 1;
        internal List<ModifierFamilyDefinition.Id> ExcludedRewardFamilies = new List<ModifierFamilyDefinition.Id>();
        internal List<HandCategory> RecentHandUpgrades = new List<HandCategory>();
        internal List<HandCategory> PendingUpgradeCategories;
        internal List<string> UnlockedModifierIds = new List<string>();
        internal List<string> NewlyUnlockedModifierIds = new List<string>();
        internal List<ModifierFamilyDefinition.Id> DiscoveredFamilyIds = new List<ModifierFamilyDefinition.Id>();
        internal List<ModifierFamilyDefinition.Id> PendingUnlockOfferDiscoveredFamilyIds;
        internal List<string> PendingModifierOfferUnlockedIds;

        internal EncounterState Copy()
        {
            var copy = (EncounterState)MemberwiseClone();
            copy.HandUpgrades = (int[])HandUpgrades.Clone();
            copy.Modifiers = new ModifierInstanceState[Modifiers.Length];
            for (int i = 0; i < Modifiers.Length; ++i) copy.Modifiers[i] = Modifiers[i].Copy();
            copy.ModifierContributions = new List<ModifierContribution>(ModifierContributions);
            copy.PieceStates = (PieceInteractionState[])PieceStates.Clone();
            copy.RuleStates = new EncounterRuleState[RuleStates.Length];
            for (int i = 0; i < RuleStates.Length; ++i) copy.RuleStates[i] = RuleStates[i]?.Copy();
            copy.ExcludedRewardFamilies = new List<ModifierFamilyDefinition.Id>(ExcludedRewardFamilies);
            copy.RecentHandUpgrades = new List<HandCategory>(RecentHandUpgrades);
            copy.PendingUpgradeCategories = PendingUpgradeCategories == null ? null : new List<HandCategory>(PendingUpgradeCategories);
            copy.UnlockedModifierIds = new List<string>(UnlockedModifierIds);
            copy.NewlyUnlockedModifierIds = new List<string>(NewlyUnlockedModifierIds);
            copy.DiscoveredFamilyIds = new List<ModifierFamilyDefinition.Id>(DiscoveredFamilyIds);
            copy.PendingUnlockOfferDiscoveredFamilyIds = PendingUnlockOfferDiscoveredFamilyIds == null ? null
                : new List<ModifierFamilyDefinition.Id>(PendingUnlockOfferDiscoveredFamilyIds);
            copy.PendingModifierOfferUnlockedIds = PendingModifierOfferUnlockedIds == null ? null : new List<string>(PendingModifierOfferUnlockedIds);
            return copy;
        }
    }
}
