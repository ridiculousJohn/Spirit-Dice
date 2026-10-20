using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dice.App
{
    /** Owns campaign directory selection; run and profile contents remain with their existing stores. */
    public sealed class CampaignSaveSlots
    {
        public const int SlotCount = 3;
        private const int MaximumIndexBytes = 4096;
        private static readonly Encoding TextEncoding = new UTF8Encoding(false, true);
        private readonly string root;
        private readonly string indexPath;
        private readonly CampaignRecoveryStore recovery;
        private IReadOnlyList<CampaignRecoveryInfo> recoveryCampaigns = Array.Empty<CampaignRecoveryInfo>();
        private string[] campaigns;
        private int activeSlot;

        public int ActiveSlot { get { RequireLoaded(); return activeSlot; } }
        public IReadOnlyList<CampaignRecoveryInfo> RecoveryCampaigns { get { RequireLoaded(); return recoveryCampaigns; } }

        public CampaignSaveSlots(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("A save directory is required.", nameof(root));
            this.root = Path.GetFullPath(root);
            indexPath = Path.Combine(this.root, "spirit-slots.json");
            recovery = new CampaignRecoveryStore(this.root);
        }

        public string Load()
        {
            bool missing = false;
            string backupPath = null;
            int selected = 1;
            string[] loaded = null;
            try { ReadIndex(out selected, out loaded); }
            catch (FileNotFoundException) { missing = true; }
            catch (DirectoryNotFoundException) { missing = true; }
            catch (FormatException)
            {
                backupPath = Path.Combine(root, "spirit-slots.corrupt-" + Guid.NewGuid().ToString("N") + ".json");
                File.Copy(indexPath, backupPath);
            }
            if (loaded != null && backupPath == null)
            {
                for (int index = 0; index < loaded.Length; ++index)
                    if (loaded[index] != null) recovery.BackfillSlot(loaded[index], index + 1);
                var unresolved = recovery.Scan(loaded, out _);
                campaigns = loaded;
                activeSlot = selected;
                recoveryCampaigns = unresolved.AsReadOnly();
                return null;
            }

            var initial = new string[SlotCount];
            var candidates = recovery.Scan(initial, out bool foundCampaigns);
            foreach (var candidate in candidates)
            {
                if (candidate.SuggestedSlot == 0) continue;
                int count = 0;
                foreach (var other in candidates)
                    if (other.SuggestedSlot == candidate.SuggestedSlot) ++count;
                if (count == 1) initial[candidate.SuggestedSlot - 1] = candidate.CampaignId;
            }
            string legacyRun = Path.Combine(root, "spirit-encounter.json");
            string legacyProfile = Path.Combine(root, "spirit-profile.json");
            if (missing && !foundCampaigns && (File.Exists(legacyRun) || File.Exists(legacyProfile)))
            {
                initial[0] = recovery.ImportLegacy();
                recovery.WriteSlot(initial[0], 1);
            }
            int recoveredSelection = Array.FindIndex(initial, campaign => campaign != null) + 1;
            Commit(recoveredSelection == 0 ? 1 : recoveredSelection, initial);
            candidates.RemoveAll(candidate => Array.IndexOf(initial, candidate.CampaignId) >= 0);
            recoveryCampaigns = candidates.AsReadOnly();
            if (backupPath != null) recovery.PruneCorruptBackups(backupPath);
            return backupPath;
        }

        public bool HasGame(int slot)
        {
            ValidateSlot(slot);
            RequireLoaded();
            return campaigns[slot - 1] != null;
        }

        public string GetSavePath(int slot) => GetPath(slot, "spirit-encounter.json");
        public string GetProfilePath(int slot) => GetPath(slot, "spirit-profile.json");

        public void SelectSlot(int slot)
        {
            ValidateSlot(slot);
            RequireLoaded();
            if (slot != activeSlot) Commit(slot, campaigns);
        }

        public string StartNewGame(int slot)
        {
            ValidateSlot(slot);
            RequireLoaded();
            string campaign = CreateCampaignDirectory();
            string path = Path.Combine(root, campaign, "spirit-profile.json");
            SaveFileCommit.Write(path, PlayerProfileCodec.Serialize(new PlayerProfile()));
            recovery.WriteSlot(campaign, slot);
            string previous = campaigns[slot - 1];
            var next = (string[])campaigns.Clone();
            next[slot - 1] = campaign;
            // Publish the prepared campaign before removing the explicitly replaced campaign.
            Commit(slot, next);
            if (previous == null) return null;
            try { recovery.DeleteCampaign(previous); }
            catch (IOException exception) { return CleanupWarning(previous, exception); }
            catch (UnauthorizedAccessException exception) { return CleanupWarning(previous, exception); }
            return null;
        }

        private string CleanupWarning(string previous, Exception exception)
        {
            try
            {
                var retained = recovery.ReadCampaign(previous);
                if (retained != null)
                {
                    var unresolved = new List<CampaignRecoveryInfo>(recoveryCampaigns) { retained };
                    recoveryCampaigns = unresolved.AsReadOnly();
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return "The new campaign was saved, but the previous campaign could not be removed: " + exception.Message;
        }

        public void RecoverCampaign(string campaignId, int slot)
        {
            ValidateSlot(slot);
            RequireLoaded();
            CampaignRecoveryInfo candidate = null;
            foreach (var available in recoveryCampaigns)
                if (string.Equals(available.CampaignId, campaignId, StringComparison.OrdinalIgnoreCase)) candidate = available;
            if (candidate == null) throw new ArgumentException("The campaign is not available for recovery.", nameof(campaignId));
            if (recovery.ReadCampaign(candidate.CampaignId) == null)
                throw new FormatException("The recovery campaign no longer contains readable saves.");
            string previous = campaigns[slot - 1];
            var displaced = previous == null ? null : recovery.ReadCampaign(previous);
            var next = (string[])campaigns.Clone();
            next[slot - 1] = candidate.CampaignId;
            recovery.WriteSlot(candidate.CampaignId, slot);
            Commit(slot, next);
            var unresolved = new List<CampaignRecoveryInfo>(recoveryCampaigns);
            unresolved.RemoveAll(value => string.Equals(value.CampaignId, candidate.CampaignId, StringComparison.OrdinalIgnoreCase));
            if (displaced != null) unresolved.Add(displaced);
            recoveryCampaigns = unresolved.AsReadOnly();
        }

        private string GetPath(int slot, string fileName)
        {
            ValidateSlot(slot);
            RequireLoaded();
            string campaign = campaigns[slot - 1];
            return campaign == null ? null : Path.Combine(root, campaign, fileName);
        }

        private string CreateCampaignDirectory()
        {
            string campaign;
            do { campaign = Guid.NewGuid().ToString("N"); }
            while (Directory.Exists(Path.Combine(root, campaign)) || File.Exists(Path.Combine(root, campaign)));
            Directory.CreateDirectory(Path.Combine(root, campaign));
            return campaign;
        }

        private void Commit(int selected, string[] next)
        {
            var document = new JObject
            {
                ["formatVersion"] = 1,
                ["activeSlot"] = selected,
                ["campaigns"] = new JArray(next)
            };
            SaveFileCommit.Write(indexPath, TextEncoding.GetBytes(document.ToString(Formatting.Indented)));
            campaigns = next;
            activeSlot = selected;
        }

        private void ReadIndex(out int selected, out string[] loaded)
        {
            try
            {
                byte[] bytes;
                using (var stream = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length > MaximumIndexBytes) throw new FormatException("The save slot index exceeds the size limit.");
                    bytes = new byte[(int)stream.Length];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int count = stream.Read(bytes, offset, bytes.Length - offset);
                        if (count == 0) throw new FormatException("The save slot index is truncated.");
                        offset += count;
                    }
                }
                int start = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
                using (var input = new StringReader(TextEncoding.GetString(bytes, start, bytes.Length - start)))
                using (var reader = new JsonTextReader(input) { MaxDepth = 4, DateParseHandling = DateParseHandling.None })
                {
                    var document = JObject.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                    });
                    if (reader.Read()) throw new FormatException("The save slot index contains trailing content.");
                    foreach (var property in document.Properties())
                        if (property.Name != "formatVersion" && property.Name != "activeSlot" && property.Name != "campaigns")
                            throw new FormatException("The save slot index contains an unsupported field: " + property.Name);
                    if (document["formatVersion"]?.Type != JTokenType.Integer || document["formatVersion"].Value<long>() != 1)
                        throw new FormatException("The save slot index format version is unsupported.");
                    if (document["activeSlot"]?.Type != JTokenType.Integer)
                        throw new FormatException("The active save slot must be an integer.");
                    long selectedValue = document["activeSlot"].Value<long>();
                    if (selectedValue < 1 || selectedValue > SlotCount)
                        throw new FormatException("The active save slot is outside its supported range.");
                    selected = (int)selectedValue;
                    if (!(document["campaigns"] is JArray array) || array.Count != SlotCount)
                        throw new FormatException("The save slot index must contain three campaigns.");
                    loaded = new string[SlotCount];
                    var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int index = 0; index < SlotCount; ++index)
                    {
                        JToken value = array[index];
                        if (value.Type == JTokenType.Null) continue;
                        if (value.Type != JTokenType.String)
                            throw new FormatException("Campaign directories must be identifiers or null.");
                        string campaign = value.Value<string>();
                        if (campaign.Length != 32 || !Guid.TryParseExact(campaign, "N", out Guid id) || id == Guid.Empty ||
                            !unique.Add(campaign))
                            throw new FormatException("Campaign directories must be unique generated identifiers.");
                        if (!recovery.IsSafeCampaignDirectory(campaign))
                            throw new FormatException("The indexed campaign directory is unavailable.");
                        loaded[index] = campaign;
                    }
                }
            }
            catch (JsonException exception) { throw new FormatException("The save slot index JSON is invalid.", exception); }
            catch (DecoderFallbackException exception) { throw new FormatException("The save slot index contains invalid UTF-8.", exception); }
            catch (OverflowException exception) { throw new FormatException("A save slot index integer is outside its supported range.", exception); }
            catch (InvalidCastException exception) { throw new FormatException("A save slot index integer is outside its supported range.", exception); }
        }

        private static void ValidateSlot(int slot)
        {
            if (slot < 1 || slot > SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
        }

        private void RequireLoaded()
        {
            if (campaigns == null) throw new InvalidOperationException("Load the save slot index before using it.");
        }
    }
}
