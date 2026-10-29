using System;
using System.Collections.Generic;
using System.IO;
using Dice.Engine;
using Dice.Presentation;

namespace Dice.App
{
    /** Owns profile progress and its writes; unlock consumption must reach the run save before the profile. */
    public sealed class PlayerProgression
    {
        private PlayerProfile profile;
        private bool profileDirty;
        private EncounterSaveData pendingUnlockSave;
        private string pendingUnlockSavePath;

        public bool HasProfile => profile != null;
        public string ProfilePath { get; private set; }
        public PlayerProfileStatus Status { get; private set; } = PlayerProfileStatus.None;
        public IReadOnlyList<string> ProfileUnlockedModifierIds => profile?.UnlockedModifierIds ?? (IReadOnlyList<string>)Array.Empty<string>();
        public IReadOnlyList<EncounterDefinition.Id> CompletedEncounterIds =>
            profile?.CompletedEncounterIds ?? (IReadOnlyList<EncounterDefinition.Id>)Array.Empty<EncounterDefinition.Id>();
        public IReadOnlyList<EncounterJourneyAsset.Id> CompletedJourneyIds =>
            profile?.CompletedJourneyIds ?? (IReadOnlyList<EncounterJourneyAsset.Id>)Array.Empty<EncounterJourneyAsset.Id>();
        public IReadOnlyList<EncounterDefinition.Id> VisitedEncounterIds =>
            profile?.VisitedEncounterIds ?? (IReadOnlyList<EncounterDefinition.Id>)Array.Empty<EncounterDefinition.Id>();

        /** Releases profile memory after the controller has safely stopped using the current campaign. */
        public void Clear()
        {
            profile = null;
            profileDirty = false;
            pendingUnlockSave = null;
            pendingUnlockSavePath = null;
            ProfilePath = null;
            Status = PlayerProfileStatus.None;
        }

        public bool PrepareProfile(string savePath)
        {
            if (!PersistProfile()) return false;
            try
            {
                string path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(savePath)), PlayerProfileFileStore.FileName);
                var loaded = PlayerProfileFileStore.LoadOrCreate(path);
                if (profile != null && path == ProfilePath) profile.Merge(loaded);
                else profile = loaded;
                ProfilePath = path;
                Status = PlayerProfileStatus.None;
                return true;
            }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                Status = new PlayerProfileStatus(PlayerProfileStatusKind.LoadFailed, exception.Message);
                return false;
            }
        }

        public IEnumerable<string> KnownUnlockedModifiers(MechanicalCatalog catalog)
        {
            if (profile == null || catalog == null) yield break;
            var unlocked = new HashSet<string>(ProfileUnlockedModifierIds, StringComparer.Ordinal);
            foreach (var modifier in catalog.Modifiers)
                if (modifier.Availability.Access == ModifierAccess.PermanentUnlock && unlocked.Contains(modifier.Id))
                    yield return modifier.Id;
        }

        /** The profile's discovered families that the catalog has, in the catalog's family order. */
        public IReadOnlyList<ModifierFamilyDefinition.Id> KnownDiscoveredFamilies(MechanicalCatalog catalog)
        {
            if (profile == null || catalog == null) return Array.Empty<ModifierFamilyDefinition.Id>();
            var discovered = new HashSet<ModifierFamilyDefinition.Id>(profile.GetDiscoveredFamilyIds(catalog));
            var result = new List<ModifierFamilyDefinition.Id>();
            foreach (var family in catalog.Families)
                if (discovered.Contains(family.Identity)) result.Add(family.Identity);
            return result;
        }

        /**
         * Records the snapshot's unlocks and, when it is won, its encounter and the journey the win completes (none in direct
         * play or when later beats follow) as completed, with the journey's completion reward.
         */
        public bool RecordProgression(EncounterSnapshot snapshot, MechanicalCatalog catalog, EncounterJourneyAsset.Id? journeyId,
            ProgressionRewardDefinition completionReward, EncounterSaveData unlockSave = null, string unlockSavePath = null)
        {
            if (profile == null) return false;
            if (unlockSave != null)
            {
                // Callers must successfully flush pending progress before advancing or replacing the live run.
                // Otherwise retrying this captured save could overwrite newer run state.
                pendingUnlockSave = unlockSave;
                pendingUnlockSavePath = unlockSavePath;
            }
            foreach (string id in snapshot.UnlockedModifierIds) profileDirty |= profile.UnlockModifier(id);
            if (snapshot.Outcome == EncounterOutcome.Won)
            {
                profileDirty |= profile.CompleteEncounter(snapshot.EncounterId);
                if (journeyId.HasValue) profileDirty |= profile.CompleteJourney(journeyId.Value);
                if (completionReward != null) profileDirty |= profile.ApplyReward(completionReward, catalog);
            }
            return PersistProfile();
        }

        public bool ApplyProgressionReward(ProgressionRewardDefinition reward, MechanicalCatalog catalog, out bool changed)
        {
            changed = false;
            if (catalog == null) return false;
            return CommitProfileChange(next => next.ApplyReward(reward, catalog), PlayerProfileStatusKind.RewardFailed, out changed);
        }

        /** Completes the journey on its last beat's Continue, with the rewards it grants, in one profile write. */
        public bool CompleteJourney(EncounterJourneyAsset.Id journeyId, IEnumerable<ProgressionRewardDefinition> rewards,
            MechanicalCatalog catalog)
            => CommitProfileChange(next =>
            {
                foreach (var reward in rewards)
                    if (reward != null) next.ApplyReward(reward, catalog);
                next.CompleteJourney(journeyId);
                return true;
            }, PlayerProfileStatusKind.StoryFailed, out _);

        public bool PersistProfile()
        {
            if (pendingUnlockSave != null)
            {
                // Commit the consumed offer before profile progress so a retry cannot grant two choices.
                try
                {
                    EncounterSaveFileStore.Save(pendingUnlockSavePath, pendingUnlockSave);
                    pendingUnlockSave = null;
                    pendingUnlockSavePath = null;
                    Status = PlayerProfileStatus.None;
                }
                catch (Exception exception) when (ExpectedFailures.Includes(exception))
                {
                    Status = new PlayerProfileStatus(PlayerProfileStatusKind.EncounterSavePending, exception.Message);
                    return false;
                }
            }
            if (!profileDirty) return true;
            try
            {
                PlayerProfileFileStore.Save(ProfilePath, profile);
                profileDirty = false;
                Status = PlayerProfileStatus.None;
                return true;
            }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                Status = new PlayerProfileStatus(PlayerProfileStatusKind.ProfileSavePending, exception.Message);
                return false;
            }
        }

        public bool PersistVisit(EncounterDefinition.Id id)
        {
            if (profile == null || !PersistProfile()) return false;
            return CommitProfileChange(next =>
            {
                next.VisitEncounter(id);
                return true;
            }, PlayerProfileStatusKind.VisitFailed, out _);
        }

        /**
         * Applies a change to a copy of the profile after pending writes are flushed, then saves the copy and adopts it.
         * A change that reports nothing changed skips the save; a failed write keeps the current profile and reports the failure.
         */
        private bool CommitProfileChange(Func<PlayerProfile, bool> change, PlayerProfileStatusKind failure, out bool changed)
        {
            changed = false;
            if (profile == null) return false;
            if (!PersistProfile()) return false;
            try
            {
                var nextProfile = new PlayerProfile();
                nextProfile.Merge(profile);
                if (!change(nextProfile)) return true;
                PlayerProfileFileStore.Save(ProfilePath, nextProfile);
                profile = nextProfile;
                Status = PlayerProfileStatus.None;
                changed = true;
                return true;
            }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                Status = new PlayerProfileStatus(failure, exception.Message);
                return false;
            }
        }
    }
}
