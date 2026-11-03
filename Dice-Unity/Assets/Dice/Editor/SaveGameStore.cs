using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;

namespace Dice.Editor
{
    /** One save slot as the game reads it. */
    internal sealed class SaveGameSlot
    {
        public int Slot { get; }
        public bool IsActive { get; }
        /** The slot's campaign, or null for an empty slot. */
        public Guid? CampaignId { get; }
        public string ProfilePath { get; }
        public string RunPath { get; }
        public bool HasProfile { get; }
        /** A run in progress: the campaign has a run save, which Continue resumes. */
        public bool HasRun { get; }
        /** The profile as the game loads it; null for an empty slot or a missing profile, or when loading it failed. */
        public PlayerProfile Profile { get; }
        public string ProfileError { get; }
        public string RunError { get; }

        internal SaveGameSlot(int slot, bool isActive, Guid? campaignId = null, string profilePath = null, string runPath = null,
            bool hasProfile = false, bool hasRun = false, PlayerProfile profile = null, string profileError = null,
            string runError = null)
        {
            Slot = slot;
            IsActive = isActive;
            CampaignId = campaignId;
            ProfilePath = profilePath;
            RunPath = runPath;
            HasProfile = hasProfile;
            HasRun = hasRun;
            Profile = profile;
            ProfileError = profileError;
            RunError = runError;
        }
    }

    /** The save root as the game reads it: its slots, the campaigns available for recovery and every save it holds. */
    internal sealed class SaveGameRoot
    {
        /** Whether the root holds a slot index or a campaign; without either the game has no campaign to read. */
        public bool HasCampaigns { get; }
        /** The failure CampaignSaveSlots.Load reported, or null. */
        public string LoadError { get; }
        /** The copy CampaignSaveSlots.Load kept of a corrupt slot index it rebuilt, or null. */
        public string RecoveredIndexBackup { get; }
        public IReadOnlyList<SaveGameSlot> Slots { get; }
        public IReadOnlyList<CampaignRecoveryInfo> RecoveryCampaigns { get; }
        /** The save files and campaign folders in the root, by full path. */
        public IReadOnlyList<string> SaveEntries { get; }

        internal SaveGameRoot(bool hasCampaigns, string loadError, string recoveredIndexBackup, IReadOnlyList<SaveGameSlot> slots,
            IReadOnlyList<CampaignRecoveryInfo> recoveryCampaigns, IReadOnlyList<string> saveEntries)
        {
            HasCampaigns = hasCampaigns;
            LoadError = loadError;
            RecoveredIndexBackup = recoveredIndexBackup;
            Slots = slots;
            RecoveryCampaigns = recoveryCampaigns;
            SaveEntries = saveEntries;
        }
    }

    /** A slot's profile file as it was read: its contents, which a later save compares, and the profile the game loads from it. */
    internal sealed class SaveGameProfileFile
    {
        public int Slot { get; }
        public string Path { get; }
        /** The file's bytes when read, or null when there was no file. */
        public byte[] Contents { get; }
        /** The loaded profile; null when there was no file or loading failed. */
        public PlayerProfile Profile { get; }
        public string Error { get; }

        internal SaveGameProfileFile(int slot, string path, byte[] contents, PlayerProfile profile, string error)
        {
            Slot = slot;
            Path = path;
            Contents = contents;
            Profile = profile;
            Error = error;
        }
    }

    /**
     * The save games of one save root, read and written only through the game's save code: slots through
     * CampaignSaveSlots, profiles through the profile store and codec and SaveFileCommit, run saves through their store.
     * Backups are new timestamped folders under the backup root. Every operation reads the root afresh, so nothing acts
     * on state read earlier.
     */
    internal sealed class SaveGameStore
    {
        /** The folder of a Clear backup that receives the save files and campaign folders moved out of the save root. */
        internal const string MovedFolderName = "moved";
        private const string BackupFolderFormat = "yyyy-MM-dd_HH-mm-ss";
        // The files the save code writes in the save root itself: the slot index, and the run save and profile of a run
        // played without a campaign (direct play and the diagnostic scene).
        private static readonly string[] RootSaveFileNames =
        {
            CampaignSaveSlots.IndexFileName, EncounterSaveFileStore.FileName, PlayerProfileFileStore.FileName,
            DiagnosticEncounterController.SaveFileName
        };
        private readonly MechanicalCatalog catalog;

        public string Root { get; }
        public string BackupRoot { get; }

        /** The catalog resolves the ids of run saves, as the game's loading does. */
        public SaveGameStore(string root, string backupRoot, MechanicalCatalog catalog)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("A save root is required.", nameof(root));
            if (string.IsNullOrWhiteSpace(backupRoot)) throw new ArgumentException("A backup root is required.", nameof(backupRoot));
            Root = Path.GetFullPath(root);
            BackupRoot = Path.GetFullPath(backupRoot);
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /**
         * Reads the slots through CampaignSaveSlots.Load, which rebuilds a missing or corrupt slot index from the campaigns
         * as the game's startup does. A root without a slot index or a campaign is not loaded, since loading it would only
         * create an empty index.
         */
        public SaveGameRoot Read()
        {
            var entries = SaveEntries();
            if (!File.Exists(Path.Combine(Root, CampaignSaveSlots.IndexFileName)) && !entries.Any(Directory.Exists))
                return new SaveGameRoot(false, null, null, Array.Empty<SaveGameSlot>(), Array.Empty<CampaignRecoveryInfo>(), entries);
            var slots = new CampaignSaveSlots(Root, catalog);
            string recovered;
            try { recovered = slots.Load(); }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                return new SaveGameRoot(true, exception.Message, null, Array.Empty<SaveGameSlot>(), Array.Empty<CampaignRecoveryInfo>(),
                    SaveEntries());
            }
            var slotStates = Enumerable.Range(1, CampaignSaveSlots.SlotCount).Select(slot => ReadSlot(slots, slot)).ToList();
            return new SaveGameRoot(true, null, recovered, slotStates, slots.RecoveryCampaigns, SaveEntries());
        }

        private SaveGameSlot ReadSlot(CampaignSaveSlots slots, int slot)
        {
            bool active = slots.ActiveSlot == slot;
            if (!slots.HasGame(slot)) return new SaveGameSlot(slot, active);
            string profilePath = slots.GetProfilePath(slot);
            string runPath = slots.GetSavePath(slot);
            // The campaign's folder is named by its id.
            Guid? campaign = CampaignRecoveryStore.TryParseCampaignId(Path.GetFileName(Path.GetDirectoryName(profilePath)), out Guid id)
                ? id : (Guid?)null;
            bool hasProfile = File.Exists(profilePath);
            PlayerProfile profile = null;
            string profileError = null;
            if (hasProfile)
            {
                try { profile = PlayerProfileFileStore.Load(profilePath); }
                catch (Exception exception) when (ExpectedFailures.Includes(exception)) { profileError = exception.Message; }
            }
            bool hasRun = File.Exists(runPath);
            string runError = null;
            if (hasRun)
            {
                try { EncounterSaveFileStore.Load(runPath, catalog); }
                catch (Exception exception) when (ExpectedFailures.Includes(exception)) { runError = exception.Message; }
            }
            return new SaveGameSlot(slot, active, campaign, profilePath, runPath, hasProfile, hasRun, profile, profileError, runError);
        }

        public void SelectSlot(int slot) => LoadSlots().SelectSlot(slot);

        /**
         * Starts a new game in the slot as the game's New Game does, replacing and removing the campaign it holds; returns
         * the developer detail of a failed removal of that campaign, or null.
         */
        public string StartNewGame(int slot) => LoadSlots().StartNewGame(slot);

        /** Deletes the slot's run save; the campaign's profile and the previous run save the save code keeps beside it (.bak) stay. */
        public void DeleteRun(int slot)
        {
            string path = LoadSlots().GetSavePath(slot) ?? throw new InvalidOperationException("Slot " + slot + " holds no campaign.");
            if (!File.Exists(path)) throw new InvalidOperationException("Slot " + slot + " has no run in progress.");
            File.Delete(path);
        }

        public SaveGameProfileFile ReadProfile(int slot)
        {
            string path = ProfilePath(LoadSlots(), slot);
            byte[] contents = File.Exists(path) ? File.ReadAllBytes(path) : null;
            if (contents == null) return new SaveGameProfileFile(slot, path, null, null, null);
            try { return new SaveGameProfileFile(slot, path, contents, PlayerProfileFileStore.Load(path), null); }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                return new SaveGameProfileFile(slot, path, contents, null, exception.Message);
            }
        }

        /**
         * Writes the profile in place of the profile file that was read. It refuses when the slot's profile file is no
         * longer the one read, so an edit never replaces newer progress.
         */
        public void SaveProfile(SaveGameProfileFile read, PlayerProfile profile)
        {
            if (read == null) throw new ArgumentNullException(nameof(read));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            string path = ProfilePath(LoadSlots(), read.Slot);
            if (path != read.Path)
                throw new InvalidOperationException("Slot " + read.Slot + " holds another campaign than the profile that was read; nothing was written.");
            byte[] current = File.Exists(path) ? File.ReadAllBytes(path) : null;
            bool unchanged = current == null ? read.Contents == null : read.Contents != null && current.SequenceEqual(read.Contents);
            if (!unchanged)
                throw new InvalidOperationException("The profile changed on disk after it was read; nothing was written: " + path);
            Replace(path, profile);
        }

        /** Writes a new player's profile into the slot's campaign. */
        public void WriteFresh(int slot) => Replace(ProfilePath(LoadSlots(), slot), new PlayerProfile());

        /** Writes into the slot's campaign the profile of a player who completed every journey before the chosen one (EverythingBefore). */
        public void WriteEverythingBefore(int slot, EncounterJourneyCatalogAsset journeys, EncounterJourneyAsset chosen)
        {
            var profile = EverythingBefore(journeys, chosen);
            Replace(ProfilePath(LoadSlots(), slot), profile);
        }

        /**
         * The profile of a new player who played every journey before the chosen one, in chapter order, through the game's
         * journey navigation: each invitation accepted, each encounter visited and won with every milestone unlock chosen,
         * and every beat and completion reward applied where the game applies it. The chosen journey stays available and
         * not started.
         */
        public PlayerProfile EverythingBefore(EncounterJourneyCatalogAsset journeys, EncounterJourneyAsset chosen)
        {
            if (journeys == null) throw new ArgumentNullException(nameof(journeys));
            var entries = journeys.Entries.ToList();
            int index = chosen == null ? -1 : entries.IndexOf(chosen);
            if (index < 0) throw new ArgumentException("The chosen journey is not in the journey catalog.", nameof(chosen));
            var profile = new PlayerProfile();
            var navigation = new EncounterJourneyNavigation();
            navigation.InitializeJourney(journeys, ProgressOf(profile));
            foreach (var journey in entries.Take(index)) Play(navigation, journey, profile);
            if (!navigation.IsJourneyAvailable(chosen, ProgressOf(profile)))
                throw new InvalidOperationException("The journey " + chosen.name +
                    " is not available after every journey before it: it requires a journey that follows it in chapter order.");
            return profile;
        }

        private static EncounterJourneyNavigation.Progress ProgressOf(PlayerProfile profile) =>
            new EncounterJourneyNavigation.Progress(true, profile.VisitedEncounterIds, profile.CompletedJourneyIds,
                profile.CompletedEncounterIds);

        /** Plays the journey through the game's navigation, applying each profile change where the game applies it. */
        private void Play(EncounterJourneyNavigation navigation, EncounterJourneyAsset journey, PlayerProfile profile)
        {
            if (!navigation.SelectEncounter(journey, ProgressOf(profile), out SpiritStatusKind error))
                throw new InvalidOperationException("The journey " + journey.name + " is not available after the journeys before it (" +
                    error + ").");
            while (navigation.JourneyPhase != EncounterJourneyPhase.Investigation)
            {
                EncounterJourneyNavigation.Transition transition;
                if (journey.EncounterBeatAt(navigation.BeatIndex + 1) != null)
                {
                    // Accept: the rewards of the beats through the invitation apply, then the encounter is visited and won.
                    if (!navigation.AcceptEncounter(out var accept))
                        throw new InvalidOperationException("Accept was refused in the journey " + journey.name + ".");
                    ApplyRewards(accept.Rewards, profile);
                    var encounter = catalog.GetEncounter(journey.EncounterBeatAt(accept.BeatIndex).Encounter.Identity);
                    profile.VisitEncounter(encounter.Identity);
                    navigation.Commit(accept);
                    Win(journey, accept.BeatIndex, encounter, profile);
                    if (!navigation.AdvanceJourney(EncounterOutcome.Won, out transition))
                        throw new InvalidOperationException("Continuing after the win was refused in the journey " + journey.name + ".");
                }
                else if (!navigation.AdvanceJourney(EncounterOutcome.None, out transition))
                    throw new InvalidOperationException("Continue was refused at beat " + navigation.BeatIndex + " of the journey " +
                        journey.name + ".");
                // The last beat's Continue completes the journey, with every beat reward and the completion reward.
                ApplyRewards(transition.Rewards, profile);
                if (transition.CompletesJourney) profile.CompleteJourney(journey.Identity);
                navigation.Commit(transition, ProgressOf(profile));
            }
            if (!profile.CompletedJourneyIds.Contains(journey.Identity))
                throw new InvalidOperationException("The journey " + journey.name + " ended without completing.");
        }

        /** A win with every milestone unlock chosen over its plays, recorded as PlayerProgression.RecordProgression records a win. */
        private void Win(EncounterJourneyAsset journey, int beatIndex, EncounterDefinition encounter, PlayerProfile profile)
        {
            if (encounter.UnlockRewardModifierIds.Count > 0 && encounter.UnlockMilestones.Count == 0)
                throw new InvalidOperationException("The encounter " + encounter.Identity + " has unlock rewards but no milestone.");
            var discovered = new HashSet<ModifierFamilyDefinition.Id>(profile.GetDiscoveredFamilyIds(catalog));
            foreach (var id in encounter.UnlockRewardModifierIds)
            {
                // A milestone offers only charms whose family is discovered.
                if (!discovered.Contains(catalog.GetModifier(id).Family.Identity))
                    throw new InvalidOperationException("The unlock " + id + " of the encounter " + encounter.Identity +
                        " is never offered: its family is undiscovered when the encounter is played.");
                profile.UnlockModifier(id);
            }
            profile.CompleteEncounter(encounter.Identity);
            // A win completes the journey when its Encounter beat is the last beat, with the completion reward.
            if (beatIndex != journey.Beats.Count - 1) return;
            profile.CompleteJourney(journey.Identity);
            if (journey.CompletionReward != null) profile.ApplyReward(journey.CompletionReward.Build(catalog), catalog);
        }

        private void ApplyRewards(IEnumerable<ProgressionRewardAsset> rewards, PlayerProfile profile)
        {
            foreach (var reward in rewards) profile.ApplyReward(reward.Build(catalog), catalog);
        }

        /** The save files and campaign folders in the save root, by full path: what Back up copies and Clear moves. */
        public IReadOnlyList<string> SaveEntries()
        {
            if (!Directory.Exists(Root)) return Array.Empty<string>();
            return Directory.GetDirectories(Root)
                .Where(path => CampaignRecoveryStore.TryParseCampaignId(Path.GetFileName(path), out _))
                .Concat(Directory.GetFiles(Root).Where(path => IsRootSaveFile(Path.GetFileName(path))))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        // SaveFileCommit keeps beside a save file its previous version (.bak) and, during a write, a temporary file
        // (.tmp-<id>); a rebuilt corrupt slot index is kept as a copy named with the recovery store's backup prefix.
        private static bool IsRootSaveFile(string name)
            => name.StartsWith(CampaignRecoveryStore.BackupPrefix, StringComparison.Ordinal) ||
                RootSaveFileNames.Any(save => name == save || name.StartsWith(save + ".", StringComparison.Ordinal));

        /** Copies every save file and campaign folder into a new timestamped folder under the backup root; returns that folder. */
        public string BackUp()
        {
            var entries = SaveEntries();
            if (entries.Count == 0) throw new InvalidOperationException("The save root holds no saves: " + Root);
            string folder = CreateBackupFolder();
            foreach (string entry in entries) Copy(entry, Path.Combine(folder, Path.GetFileName(entry)));
            return folder;
        }

        /**
         * Backs up the saves as BackUp does, then moves every save file and campaign folder out of the save root into the
         * backup's moved folder, so the root holds no saves and nothing is deleted. Returns the backup folder.
         */
        public string Clear()
        {
            string folder = BackUp();
            string moved = Path.Combine(folder, MovedFolderName);
            Directory.CreateDirectory(moved);
            foreach (string entry in SaveEntries())
            {
                string destination = Path.Combine(moved, Path.GetFileName(entry));
                if (Directory.Exists(entry)) Directory.Move(entry, destination);
                else File.Move(entry, destination);
            }
            return folder;
        }

        private string CreateBackupFolder()
        {
            string name = DateTime.Now.ToString(BackupFolderFormat, CultureInfo.InvariantCulture);
            string folder = Path.Combine(BackupRoot, name);
            for (int suffix = 2; Directory.Exists(folder) || File.Exists(folder); ++suffix)
                folder = Path.Combine(BackupRoot, name + "-" + suffix);
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static void Copy(string source, string destination)
        {
            if (File.Exists(source))
            {
                File.Copy(source, destination);
                return;
            }
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            foreach (string directory in Directory.GetDirectories(source)) Copy(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }

        private CampaignSaveSlots LoadSlots()
        {
            var slots = new CampaignSaveSlots(Root, catalog);
            slots.Load();
            return slots;
        }

        private static string ProfilePath(CampaignSaveSlots slots, int slot)
            => slots.GetProfilePath(slot) ?? throw new InvalidOperationException("Slot " + slot + " holds no campaign.");

        /**
         * Writes the profile as it is, sealed, keeping the replaced file as its previous version (.bak), as StartNewGame
         * writes a new player's profile. The profile store's Save merges the file's progress into what it writes, so it
         * cannot remove an entry.
         */
        private static void Replace(string path, PlayerProfile profile) => SaveFileCommit.Write(path, PlayerProfileCodec.Serialize(profile));
    }
}
