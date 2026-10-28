using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Dice.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dice.App
{
    /** A validated campaign whose files are not currently assigned to a save slot. */
    public sealed class CampaignRecoveryInfo
    {
        public string CampaignId { get; }
        public string ProfilePath { get; }
        public string SavePath { get; }
        public DateTime LastWriteTimeUtc { get; }
        public int SuggestedSlot { get; }

        internal CampaignRecoveryInfo(string campaignId, string profilePath, string savePath, DateTime lastWriteTimeUtc, int suggestedSlot)
        {
            CampaignId = campaignId;
            ProfilePath = profilePath;
            SavePath = savePath;
            LastWriteTimeUtc = lastWriteTimeUtc;
            SuggestedSlot = suggestedSlot;
        }
    }

    /** Keeps recovery discovery and destructive filesystem operations separate from slot selection. */
    internal sealed class CampaignRecoveryStore
    {
        private const string MetadataFileName = "spirit-campaign.json";
        /** The file name prefix of a corrupt slot index's backup copy; a campaign identifier and .json follow it. */
        internal const string BackupPrefix = "spirit-slots.corrupt-";
        private const int MetadataFormatVersion = 1;
        private const int MaximumMetadataBytes = 256;
        private const int MetadataMaximumDepth = 3;
        private const string FormatVersionField = "formatVersion";
        private const string SlotField = "slot";
        // To change the metadata format, raise MetadataFormatVersion and append a step that upgrades the previous format.
        private static readonly SaveFormatUpgrades MetadataUpgrades = new SaveFormatUpgrades(FormatVersionField, MetadataFormatVersion);
        private readonly string root;
        private readonly MechanicalCatalog catalog;

        internal CampaignRecoveryStore(string root, MechanicalCatalog catalog)
        {
            this.root = root;
            this.catalog = catalog;
        }

        internal bool IsSafeCampaignDirectory(string id) => TryAttributes(CampaignDirectory(id), out var attributes) &&
            (attributes & FileAttributes.Directory) != 0 && (attributes & FileAttributes.ReparsePoint) == 0;

        internal List<CampaignRecoveryInfo> Scan(string[] assigned)
        {
            var result = new List<CampaignRecoveryInfo>();
            if (!TryAttributes(root, out _)) return result;
            var mapped = new HashSet<string>(assigned, StringComparer.OrdinalIgnoreCase);
            foreach (string directory in Directory.GetDirectories(root))
            {
                string id = Path.GetFileName(directory);
                if (!IsCampaignId(id) || mapped.Contains(id)) continue;
                var info = ReadCampaign(id);
                if (info != null) result.Add(info);
            }
            result.Sort((left, right) => string.CompareOrdinal(left.CampaignId, right.CampaignId));
            return result;
        }

        internal CampaignRecoveryInfo ReadCampaign(string id)
        {
            string directory = CampaignDirectory(id);
            if (!IsSafeCampaignDirectory(id)) return null;
            FileAttributes attributes;
            string profile = Path.Combine(directory, PlayerProfileFileStore.FileName);
            string save = Path.Combine(directory, EncounterSaveFileStore.FileName);
            bool hasProfile = TryAttributes(profile, out attributes);
            if (hasProfile && (attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) return null;
            bool hasRun = TryAttributes(save, out attributes);
            if (hasRun && (attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) return null;
            if (!hasProfile && !hasRun) return null;
            try
            {
                if (hasProfile) PlayerProfileFileStore.Load(profile);
                if (hasRun) EncounterSaveFileStore.Load(save, catalog);
            }
            catch (FormatException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            DateTime written = hasProfile ? File.GetLastWriteTimeUtc(profile) : DateTime.MinValue;
            if (hasRun && File.GetLastWriteTimeUtc(save) > written) written = File.GetLastWriteTimeUtc(save);
            return new CampaignRecoveryInfo(id, profile, save, written, ReadSlot(directory));
        }

        internal void WriteSlot(string id, int slot)
        {
            string directory = CampaignDirectory(id);
            if (!TryAttributes(directory, out var attributes) || (attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("The campaign directory is unavailable: " + directory);
            string path = Path.Combine(directory, MetadataFileName);
            RequireRegularFile(path);
            RequireRegularFile(path + ".bak");
            var document = new JObject { [FormatVersionField] = MetadataFormatVersion, [SlotField] = slot };
            SaveFileCommit.Write(path, SaveFileText.Encoding.GetBytes(document.ToString(Formatting.Indented)));
        }

        internal void BackfillSlot(string id, int slot)
        {
            // The committed index remains usable even when auxiliary metadata cannot be repaired.
            try
            {
                string directory = CampaignDirectory(id);
                if (ReadSlot(directory) != slot) WriteSlot(id, slot);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        internal void DeleteCampaign(string id)
        {
            string directory = CampaignDirectory(id);
            if (!TryAttributes(directory, out _)) return;
            RequireRegularTree(directory);
            Directory.Delete(directory, true);
        }

        internal void PruneCorruptBackups(string keep)
        {
            string[] backups;
            try { backups = Directory.GetFiles(root, BackupPrefix + "*.json"); }
            catch (IOException) { return; }
            catch (UnauthorizedAccessException) { return; }
            foreach (string path in backups)
            {
                string name = Path.GetFileName(path);
                string id = name.Substring(BackupPrefix.Length, name.Length - BackupPrefix.Length - ".json".Length);
                if (string.Equals(path, keep, StringComparison.OrdinalIgnoreCase) || !IsCampaignId(id)) continue;
                try
                {
                    if (IsRegularFile(path)) File.Delete(path);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private int ReadSlot(string directory)
        {
            string path = Path.Combine(directory, MetadataFileName);
            if (!IsRegularFile(path)) return 0;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length > MaximumMetadataBytes) return 0;
                    using (var text = new StreamReader(stream, SaveFileText.Encoding, true))
                    using (var reader = new JsonTextReader(text) { MaxDepth = MetadataMaximumDepth, DateParseHandling = DateParseHandling.None })
                    {
                        var document = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                        if (reader.Read() || MetadataUpgrades.Upgrade(document) != SaveFormatCheck.Upgraded) return 0;
                        if (document.Count != 2 || document[SlotField]?.Type != JTokenType.Integer) return 0;
                        long slot = document[SlotField].Value<long>();
                        return slot >= 1 && slot <= CampaignSaveSlots.SlotCount ? (int)slot : 0;
                    }
                }
            }
            catch (JsonException) { return 0; }
            catch (DecoderFallbackException) { return 0; }
            catch (OverflowException) { return 0; }
            catch (InvalidCastException) { return 0; }
            catch (FileNotFoundException) { return 0; }
        }

        private string CampaignDirectory(string id)
        {
            if (!IsCampaignId(id)) throw new ArgumentException("A generated campaign identifier is required.", nameof(id));
            string path = Path.GetFullPath(Path.Combine(root, id));
            if (!string.Equals(Path.GetDirectoryName(path), root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new IOException("The campaign directory is outside the save root.");
            return path;
        }

        internal static bool IsCampaignId(string id) => id != null && id.Length == 32 && Guid.TryParseExact(id, "N", out Guid value) && value != Guid.Empty;

        private static bool TryAttributes(string path, out FileAttributes attributes)
        {
            try { attributes = File.GetAttributes(path); return true; }
            catch (FileNotFoundException) { attributes = 0; return false; }
            catch (DirectoryNotFoundException) { attributes = 0; return false; }
        }

        private static bool IsRegularFile(string path) => TryAttributes(path, out var attributes) &&
            (attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) == 0;

        private static void RequireRegularFile(string path)
        {
            if (TryAttributes(path, out var attributes) && (attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Campaign storage cannot traverse a reparse point: " + path);
        }

        private static void RequireRegularTree(string directory)
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("A campaign containing reparse points cannot be removed: " + directory);
            foreach (string entry in Directory.GetFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("A campaign containing reparse points cannot be removed: " + entry);
                if ((attributes & FileAttributes.Directory) != 0) RequireRegularTree(entry);
            }
        }
    }
}
