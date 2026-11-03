using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dice.App;
using UnityEditor;
using UnityEngine;

namespace Dice.Editor
{
    /**
     * Shows and edits the save games in Application.persistentDataPath through SaveGameStore: the slots, the selected slot's
     * profile, the profile presets and backups. It reads the saves afresh on focus and after every write, and in Play Mode
     * it only shows them.
     */
    public sealed class SaveGamesWindow : EditorWindow
    {
        private const string UnlistedGroup = "Not in the content";
        [SerializeField] private int selectedSlot = 1;
        [SerializeField] private int presetJourney = -1;
        [SerializeField] private bool showJourneys = true;
        [SerializeField] private bool showCompletedEncounters;
        [SerializeField] private bool showVisitedEncounters;
        [SerializeField] private bool showFamilies;
        [SerializeField] private bool showCharms;
        private SaveGameContent content;
        private SaveGameStore store;
        private string contentError;
        private SaveGameRoot root;
        private SaveGameProfileFile profileFile;
        private SaveGameProfileEdit edit;
        private string message;
        private MessageType messageType;
        private Vector2 scroll;

        private static string BackupRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work", "save-backups"));

        private static bool ReadOnly => EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Dice/Save Games")]
        public static void Open() => GetWindow<SaveGamesWindow>("Save Games").Show();

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            LoadContent();
        }

        private void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

        private void OnFocus() => Reload(true);

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            Reload(true);
            Repaint();
        }

        private void LoadContent()
        {
            try
            {
                content = SaveGameContent.Load();
                store = new SaveGameStore(Application.persistentDataPath, BackupRoot, content.Catalog);
                contentError = null;
            }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                content = null;
                store = null;
                contentError = "The game content does not load, so the saves cannot be read: " + exception.Message;
            }
            Reload(true);
        }

        /**
         * Reads the save root and the selected slot's profile from disk. Unsaved profile edits are kept only while the
         * profile file is the one they were made on.
         */
        private void Reload(bool keepEdits)
        {
            if (store == null) return;
            try
            {
                root = store.Read();
                var slot = root.Slots.FirstOrDefault(candidate => candidate.Slot == selectedSlot);
                if (slot?.CampaignId == null)
                {
                    DiscardEdits(keepEdits);
                    profileFile = null;
                    edit = null;
                    return;
                }
                var read = store.ReadProfile(selectedSlot);
                if (keepEdits && edit != null && profileFile != null && read.Path == profileFile.Path &&
                    SameContents(read.Contents, profileFile.Contents)) return;
                DiscardEdits(keepEdits);
                profileFile = read;
                // A campaign without a profile file is edited as a new player's profile; Save writes the file.
                edit = read.Profile != null ? new SaveGameProfileEdit(read.Profile) :
                    read.Contents == null ? new SaveGameProfileEdit(new PlayerProfile()) : null;
            }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                SetMessage("Reading the saves failed: " + exception.Message, MessageType.Error);
            }
        }

        private void DiscardEdits(bool keepEdits)
        {
            if (keepEdits && edit != null && edit.IsChanged)
                SetMessage("The profile changed on disk; the unsaved edits were discarded.", MessageType.Warning);
        }

        private static bool SameContents(byte[] left, byte[] right)
            => left == null ? right == null : right != null && left.SequenceEqual(right);

        private void SetMessage(string text, MessageType type)
        {
            message = text;
            messageType = type;
        }

        /**
         * Runs a write from a button, reports what it returns, then reads everything back from disk and ends the GUI pass,
         * whose layout no longer matches the state.
         */
        private void Write(Func<string> action)
        {
            try { SetMessage(action(), MessageType.Info); }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                SetMessage(exception.Message, MessageType.Error);
            }
            Reload(false);
            GUIUtility.ExitGUI();
        }

        private void OnGUI()
        {
            if (store == null)
            {
                EditorGUILayout.HelpBox(contentError ?? "The game content is not loaded.", MessageType.Error);
                if (GUILayout.Button("Reload content")) LoadContent();
                return;
            }
            DrawToolbar();
            if (ReadOnly) EditorGUILayout.HelpBox("Play Mode: the saves are shown read-only.", MessageType.Info);
            if (message != null) EditorGUILayout.HelpBox(message, messageType);
            if (root == null) return;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSlots();
            DrawRecovery();
            DrawProfile();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.SelectableLabel("Save root: " + store.Root, EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh"))
                {
                    message = null;
                    LoadContent();
                    GUIUtility.ExitGUI();
                }
                int entries = root?.SaveEntries.Count ?? 0;
                using (new EditorGUI.DisabledScope(entries == 0))
                    if (GUILayout.Button("Back up saves") && EditorUtility.DisplayDialog("Back up saves",
                            "Copy every save file and campaign folder in the save root (" + entries + " entries) to a new folder under " +
                            store.BackupRoot + "? Nothing in the save root changes.", "Back up", "Cancel"))
                        Write(() => "Backed up the saves to " + store.BackUp());
                using (new EditorGUI.DisabledScope(entries == 0 || ReadOnly))
                    if (GUILayout.Button("Clear saves") && EditorUtility.DisplayDialog("Clear saves",
                            "Back up the saves to a new folder under " + store.BackupRoot + ", then move every save file and campaign " +
                            "folder (" + entries + " entries) out of the save root into that backup's " + SaveGameStore.MovedFolderName +
                            " folder? The game then starts with no saves.", "Clear", "Cancel"))
                        Write(() => "Cleared the saves; the backup and the moved saves are in " + store.Clear());
            }
        }

        private void DrawSlots()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Slots", EditorStyles.boldLabel);
            if (root.LoadError != null)
            {
                EditorGUILayout.HelpBox("The game cannot load the save slots: " + root.LoadError, MessageType.Error);
                return;
            }
            if (root.RecoveredIndexBackup != null)
                EditorGUILayout.HelpBox("The slot index was corrupt and the game rebuilt it; the corrupt index is kept at " +
                    root.RecoveredIndexBackup, MessageType.Warning);
            if (!root.HasCampaigns)
                EditorGUILayout.LabelField("No slot index or campaign: the game starts with three empty slots.");
            for (int slot = 1; slot <= CampaignSaveSlots.SlotCount; ++slot)
                DrawSlot(root.Slots.FirstOrDefault(candidate => candidate.Slot == slot), slot);
        }

        private void DrawSlot(SaveGameSlot state, int slot)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool selected = GUILayout.Toggle(selectedSlot == slot, "Slot " + slot + (state?.IsActive == true ? " (active)" : ""),
                        EditorStyles.radioButton, GUILayout.Width(110));
                    if (selected && selectedSlot != slot) SelectSlot(slot);
                    EditorGUILayout.SelectableLabel(state?.CampaignId == null ? "Empty" : "Campaign " + state.CampaignId.Value.ToString("N"),
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
                if (state?.CampaignId != null)
                {
                    EditorGUILayout.LabelField("Profile", DescribeProfile(state));
                    EditorGUILayout.LabelField("Run in progress", !state.HasRun ? "No" : state.RunError == null ? "Yes" : "Yes, unreadable: " + state.RunError);
                }
                using (new EditorGUI.DisabledScope(ReadOnly))
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(state == null || state.IsActive))
                        if (GUILayout.Button("Set active"))
                            Write(() =>
                            {
                                store.SelectSlot(slot);
                                return "Slot " + slot + " is active.";
                            });
                    if (GUILayout.Button("New game") && ConfirmNewGame(state, slot))
                        Write(() =>
                        {
                            string failure = store.StartNewGame(slot);
                            if (failure != null)
                                throw new IOException("Started a new game in slot " + slot + ", but the replaced campaign was not removed: " + failure);
                            return "Started a new game in slot " + slot + ".";
                        });
                    using (new EditorGUI.DisabledScope(state == null || !state.HasRun))
                        if (GUILayout.Button("Delete run") && EditorUtility.DisplayDialog("Delete run",
                                "Delete the run in progress of slot " + slot + " (" + state.RunPath + ")? The profile stays.", "Delete", "Cancel"))
                            Write(() =>
                            {
                                store.DeleteRun(slot);
                                return "Deleted the run in progress of slot " + slot + ".";
                            });
                }
            }
        }

        private static string DescribeProfile(SaveGameSlot state)
        {
            if (!state.HasProfile) return "No profile file";
            if (state.ProfileError != null) return "Unreadable: " + state.ProfileError;
            return state.Profile.CompletedJourneyIds.Count + " completed journeys, " + state.Profile.CompletedEncounterIds.Count +
                " completed encounters, " + state.Profile.UnlockedModifierIds.Count + " unlocked charms";
        }

        private static bool ConfirmNewGame(SaveGameSlot state, int slot)
            => state?.CampaignId == null || EditorUtility.DisplayDialog("New game",
                "Start a new game in slot " + slot + "? As the game's New Game does, this replaces the slot's campaign " +
                state.CampaignId.Value.ToString("N") + " and removes it with its profile and run.", "Replace", "Cancel");

        private void SelectSlot(int slot)
        {
            if (edit != null && edit.IsChanged && !EditorUtility.DisplayDialog("Save Games",
                    "Discard the unsaved edits to the profile of slot " + selectedSlot + "?", "Discard", "Cancel")) return;
            selectedSlot = slot;
            message = null;
            Reload(false);
            GUIUtility.ExitGUI();
        }

        private void DrawRecovery()
        {
            if (root.RecoveryCampaigns.Count == 0) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Campaigns available for recovery (Load Game > Recover saves)", EditorStyles.boldLabel);
            foreach (var campaign in root.RecoveryCampaigns)
                EditorGUILayout.SelectableLabel(campaign.CampaignId.ToString("N") + ", last written " +
                    campaign.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") +
                    (campaign.SuggestedSlot == 0 ? "" : ", from slot " + campaign.SuggestedSlot), GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        private void DrawProfile()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Profile of slot " + selectedSlot, EditorStyles.boldLabel);
            if (profileFile == null)
            {
                EditorGUILayout.LabelField("The slot holds no campaign.");
                return;
            }
            EditorGUILayout.SelectableLabel(profileFile.Path, EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (profileFile.Error != null)
                EditorGUILayout.HelpBox("The game cannot load this profile: " + profileFile.Error, MessageType.Error);
            else if (profileFile.Contents == null)
                EditorGUILayout.HelpBox("The campaign has no profile file; Save writes one.", MessageType.Info);
            using (new EditorGUI.DisabledScope(ReadOnly))
            {
                DrawPresets();
                if (edit == null) return;
                using (new EditorGUILayout.HorizontalScope())
                using (new EditorGUI.DisabledScope(!edit.IsChanged))
                {
                    if (GUILayout.Button("Save"))
                        Write(() =>
                        {
                            store.SaveProfile(profileFile, edit.Build());
                            return "Saved the profile of slot " + selectedSlot + ".";
                        });
                    if (GUILayout.Button("Revert"))
                    {
                        message = null;
                        Reload(false);
                        GUIUtility.ExitGUI();
                    }
                }
                DrawSelection("Completed journeys", ref showJourneys, content.JourneyEntries, edit.CompletedJourneys, content.JourneyName);
                DrawSelection("Completed encounters", ref showCompletedEncounters, content.EncounterEntries, edit.CompletedEncounters,
                    content.EncounterName);
                DrawSelection("Visited encounters", ref showVisitedEncounters, content.EncounterEntries, edit.VisitedEncounters,
                    content.EncounterName);
                var implicitFamilies = edit.ImplicitDiscoveries(content.Catalog);
                DrawSelection("Discovered families", ref showFamilies, content.FamilyEntries, edit.DiscoveredFamilies, content.FamilyName,
                    id => implicitFamilies.TryGetValue(id, out var discovery) ? Describe(discovery) : null);
                DrawSelection("Unlocked charms", ref showCharms, content.CharmEntries, edit.UnlockedCharms, content.CharmName);
            }
        }

        private static string Describe(SaveGameImplicitDiscovery discovery)
            => discovery == SaveGameImplicitDiscovery.Starter ? "starter family" : "family of an unlocked charm";

        private void DrawPresets()
        {
            var journeys = content.Journeys.Entries;
            if (presetJourney < 0 || presetJourney >= journeys.Count) presetJourney = journeys.Count - 1;
            using (new EditorGUILayout.HorizontalScope())
            {
                string[] labels = content.JourneyEntries.Select(entry => entry.Group + " / " + entry.Name).ToArray();
                presetJourney = EditorGUILayout.Popup(presetJourney, labels);
                var chosen = journeys[presetJourney];
                if (GUILayout.Button("Everything before journey...") && ConfirmReplace("Replace the profile of slot " + selectedSlot +
                        " with a player who completed every journey before " + chosen.LocationTitle + " in chapter order, with every reward " +
                        "and unlock? " + chosen.LocationTitle + " stays available and not started."))
                    Write(() =>
                    {
                        store.WriteEverythingBefore(selectedSlot, content.Journeys, chosen);
                        return "Wrote the profile of a player who completed every journey before " + chosen.LocationTitle + ".";
                    });
                if (GUILayout.Button("Fresh", GUILayout.Width(60)) &&
                    ConfirmReplace("Replace the profile of slot " + selectedSlot + " with a new player's profile?"))
                    Write(() =>
                    {
                        store.WriteFresh(selectedSlot);
                        return "Wrote a new player's profile into slot " + selectedSlot + ".";
                    });
            }
        }

        /** Confirms replacing the selected slot's profile, naming the run in progress that stays and edits that are lost. */
        private bool ConfirmReplace(string question)
        {
            var slot = root.Slots.FirstOrDefault(candidate => candidate.Slot == selectedSlot);
            string run = slot != null && slot.HasRun ? " The slot's run in progress stays." : "";
            string edits = edit != null && edit.IsChanged ? " The unsaved edits are lost." : "";
            return EditorUtility.DisplayDialog("Save Games", question + run + edits, "Replace", "Cancel");
        }

        /**
         * Lists what the content offers for one profile list, then every stored id the content does not list, as checkboxes.
         * An entry the game treats as present without a stored id (implicitNote) shows as checked and not editable.
         */
        private static void DrawSelection<TId>(string title, ref bool expanded, IReadOnlyList<SaveGameContent.Entry<TId>> entries,
            SaveGameIdSelection<TId> selection, Func<TId, string> name, Func<TId, string> implicitNote = null)
            where TId : struct, IEquatable<TId>
        {
            expanded = EditorGUILayout.Foldout(expanded, title + " (" + selection.Ids.Count + " stored)", true);
            if (!expanded) return;
            var listed = new HashSet<TId>(entries.Select(entry => entry.Id));
            var unlisted = selection.Ids.Where(id => !listed.Contains(id))
                .Select(id => new SaveGameContent.Entry<TId>(id, UnlistedGroup, name(id)));
            using (new EditorGUI.IndentLevelScope())
            {
                string group = null;
                foreach (var entry in entries.Concat(unlisted))
                {
                    if (entry.Group != group)
                    {
                        group = entry.Group;
                        if (!string.IsNullOrEmpty(group)) EditorGUILayout.LabelField(group, EditorStyles.miniBoldLabel);
                    }
                    bool stored = selection.Contains(entry.Id);
                    string note = implicitNote?.Invoke(entry.Id);
                    if (note != null && !stored)
                    {
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.ToggleLeft(new GUIContent(entry.Name + " (" + note + ")", entry.Id.ToString()), true);
                        continue;
                    }
                    string label = note == null ? entry.Name : entry.Name + " (stored; also " + note + ")";
                    bool next = EditorGUILayout.ToggleLeft(new GUIContent(label, entry.Id.ToString()), stored);
                    if (next != stored) selection.Set(entry.Id, next);
                }
            }
        }
    }
}
