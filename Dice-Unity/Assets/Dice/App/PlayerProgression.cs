using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Cryptography;
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
        public IReadOnlyList<string> CompletedEncounterIds => profile?.CompletedEncounterIds ?? (IReadOnlyList<string>)Array.Empty<string>();
        public IReadOnlyList<string> CompletedStoryIds => profile?.CompletedStoryIds ?? (IReadOnlyList<string>)Array.Empty<string>();
        public IReadOnlyList<string> VisitedEncounterIds => profile?.VisitedEncounterIds ?? (IReadOnlyList<string>)Array.Empty<string>();

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
                string path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(savePath)), "spirit-profile.json");
                var loaded = PlayerProfileFileStore.LoadOrCreate(path);
                if (profile != null && path == ProfilePath) profile.Merge(loaded);
                else profile = loaded;
                ProfilePath = path;
                Status = PlayerProfileStatus.None;
                return true;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
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

        public IReadOnlyList<string> KnownDiscoveredFamilies(MechanicalCatalog catalog)
        {
            if (profile == null || catalog == null) return Array.Empty<string>();
            var discovered = new HashSet<string>(profile.GetDiscoveredFamilyIds(catalog), StringComparer.Ordinal);
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var modifier in catalog.Modifiers)
                if (discovered.Contains(modifier.FamilyId)) known.Add(modifier.FamilyId);
            var result = new List<string>(known);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        public bool RecordProgression(EncounterSnapshot snapshot, MechanicalCatalog catalog,
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
                if (completionReward != null) profileDirty |= profile.ApplyReward(completionReward, catalog);
            }
            return PersistProfile();
        }

        public bool ApplyProgressionReward(ProgressionRewardDefinition reward, MechanicalCatalog catalog, out bool changed)
        {
            changed = false;
            if (catalog == null || profile == null) return false;
            if (!PersistProfile()) return false;
            try
            {
                var nextProfile = new PlayerProfile();
                nextProfile.Merge(profile);
                if (!nextProfile.ApplyReward(reward, catalog)) return true;
                PlayerProfileFileStore.Save(ProfilePath, nextProfile);
                profile = nextProfile;
                Status = PlayerProfileStatus.None;
                changed = true;
                return true;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                Status = new PlayerProfileStatus(PlayerProfileStatusKind.RewardFailed, exception.Message);
                return false;
            }
        }

        public bool CompleteStoryJourney(string storyId, IEnumerable<ProgressionRewardDefinition> rewards,
            MechanicalCatalog catalog)
        {
            if (profile == null) return false;
            if (!PersistProfile()) return false;
            try
            {
                var nextProfile = new PlayerProfile();
                nextProfile.Merge(profile);
                foreach (var reward in rewards)
                    if (reward != null) nextProfile.ApplyReward(reward, catalog);
                nextProfile.CompleteStory(storyId);
                PlayerProfileFileStore.Save(ProfilePath, nextProfile);
                profile = nextProfile;
                Status = PlayerProfileStatus.None;
                return true;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                Status = new PlayerProfileStatus(PlayerProfileStatusKind.StoryFailed, exception.Message);
                return false;
            }
        }

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
                catch (Exception exception) when (IsExpectedFailure(exception))
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
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                Status = new PlayerProfileStatus(PlayerProfileStatusKind.ProfileSavePending, exception.Message);
                return false;
            }
        }

        public bool PersistVisit(string id)
        {
            if (profile == null) return false;
            if (!PersistProfile()) return false;
            var nextProfile = new PlayerProfile();
            nextProfile.Merge(profile);
            nextProfile.VisitEncounter(id);
            try
            {
                PlayerProfileFileStore.Save(ProfilePath, nextProfile);
                profile = nextProfile;
                Status = PlayerProfileStatus.None;
                return true;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                Status = new PlayerProfileStatus(PlayerProfileStatusKind.VisitFailed, exception.Message);
                return false;
            }
        }

        private static bool IsExpectedFailure(Exception exception)
            => exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException
                || exception is CryptographicException
                || exception is FormatException || exception is NotSupportedException || exception is ArgumentException
                || exception is InvalidOperationException || exception is OverflowException || exception is KeyNotFoundException;
    }
}
