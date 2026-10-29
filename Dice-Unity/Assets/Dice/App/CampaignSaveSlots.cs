using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Dice.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dice.App
{
    /** Owns campaign directory selection; run and profile contents remain with their existing stores. */
    public sealed class CampaignSaveSlots
    {
        public const int SlotCount = 3;
        private const int MaximumIndexBytes = 4096;
        private const int IndexFormatVersion = 1;
        private const int IndexMaximumDepth = 4;
        private const string FormatVersionField = "formatVersion";
        private const string ActiveSlotField = "activeSlot";
        private const string CampaignsField = "campaigns";
        // To change the index format, raise IndexFormatVersion and append a step that upgrades the previous format.
        private static readonly SaveFormatUpgrades IndexUpgrades = new SaveFormatUpgrades(FormatVersionField, IndexFormatVersion);
        private readonly string root;
        private readonly string indexPath;
        private readonly CampaignRecoveryStore recovery;
        private IReadOnlyList<CampaignRecoveryInfo> recoveryCampaigns = Array.Empty<CampaignRecoveryInfo>();
        private Guid?[] campaigns;
        private int activeSlot;

        public int ActiveSlot { get { RequireLoaded(); return activeSlot; } }
        public IReadOnlyList<CampaignRecoveryInfo> RecoveryCampaigns { get { RequireLoaded(); return recoveryCampaigns; } }

        /** The catalog reads run saves when scanning campaigns for recovery. */
        public CampaignSaveSlots(string root, MechanicalCatalog catalog)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("A save directory is required.", nameof(root));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.root = Path.GetFullPath(root);
            indexPath = Path.Combine(this.root, "spirit-slots.json");
            recovery = new CampaignRecoveryStore(this.root, catalog);
        }

        public string Load()
        {
            string backupPath = null;
            int selected = 1;
            Guid?[] loaded = null;
            // A missing index is rebuilt from the campaigns' slot claims, like a corrupt one but without a backup.
            try { ReadIndex(out selected, out loaded); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch (FormatException)
            {
                backupPath = Path.Combine(root, CampaignRecoveryStore.BackupPrefix + Guid.NewGuid().ToString("N") + ".json");
                File.Copy(indexPath, backupPath);
            }
            if (loaded != null && backupPath == null)
            {
                for (int index = 0; index < loaded.Length; ++index)
                    if (loaded[index] != null) recovery.BackfillSlot(loaded[index].Value, index + 1);
                var unresolved = recovery.Scan(loaded);
                campaigns = loaded;
                activeSlot = selected;
                recoveryCampaigns = unresolved.AsReadOnly();
                return null;
            }

            var initial = new Guid?[SlotCount];
            var candidates = recovery.Scan(initial);
            foreach (var candidate in candidates)
            {
                if (candidate.SuggestedSlot == 0) continue;
                int count = 0;
                foreach (var other in candidates)
                    if (other.SuggestedSlot == candidate.SuggestedSlot) ++count;
                if (count == 1) initial[candidate.SuggestedSlot - 1] = candidate.CampaignId;
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

        public string GetSavePath(int slot) => GetPath(slot, EncounterSaveFileStore.FileName);
        public string GetProfilePath(int slot) => GetPath(slot, PlayerProfileFileStore.FileName);

        public void SelectSlot(int slot)
        {
            ValidateSlot(slot);
            RequireLoaded();
            if (slot != activeSlot) Commit(slot, campaigns);
        }

        /** Returns the developer detail of a failed removal of the replaced campaign, or null. */
        public string StartNewGame(int slot)
        {
            ValidateSlot(slot);
            RequireLoaded();
            Guid campaign = CreateCampaignDirectory();
            string path = Path.Combine(root, CampaignRecoveryStore.FormatCampaignId(campaign), PlayerProfileFileStore.FileName);
            SaveFileCommit.Write(path, PlayerProfileCodec.Serialize(new PlayerProfile()));
            recovery.WriteSlot(campaign, slot);
            Guid? previous = campaigns[slot - 1];
            var next = (Guid?[])campaigns.Clone();
            next[slot - 1] = campaign;
            // Publish the prepared campaign before removing the explicitly replaced campaign.
            Commit(slot, next);
            if (previous == null) return null;
            try { recovery.DeleteCampaign(previous.Value); }
            catch (IOException exception) { return CleanupFailure(previous.Value, exception); }
            catch (UnauthorizedAccessException exception) { return CleanupFailure(previous.Value, exception); }
            return null;
        }

        private string CleanupFailure(Guid previous, Exception exception)
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
            return exception.Message;
        }

        public void RecoverCampaign(Guid campaignId, int slot)
        {
            ValidateSlot(slot);
            RequireLoaded();
            CampaignRecoveryInfo candidate = null;
            foreach (var available in recoveryCampaigns)
                if (available.CampaignId == campaignId) candidate = available;
            if (candidate == null) throw new ArgumentException("The campaign is not available for recovery.", nameof(campaignId));
            if (recovery.ReadCampaign(candidate.CampaignId) == null)
                throw new FormatException("The recovery campaign no longer contains readable saves.");
            Guid? previous = campaigns[slot - 1];
            var displaced = previous == null ? null : recovery.ReadCampaign(previous.Value);
            var next = (Guid?[])campaigns.Clone();
            next[slot - 1] = candidate.CampaignId;
            recovery.WriteSlot(candidate.CampaignId, slot);
            Commit(slot, next);
            var unresolved = new List<CampaignRecoveryInfo>(recoveryCampaigns);
            unresolved.RemoveAll(value => value.CampaignId == candidate.CampaignId);
            if (displaced != null) unresolved.Add(displaced);
            recoveryCampaigns = unresolved.AsReadOnly();
        }

        private string GetPath(int slot, string fileName)
        {
            ValidateSlot(slot);
            RequireLoaded();
            Guid? campaign = campaigns[slot - 1];
            return campaign == null ? null : Path.Combine(root, CampaignRecoveryStore.FormatCampaignId(campaign.Value), fileName);
        }

        private Guid CreateCampaignDirectory()
        {
            Guid campaign;
            string directory;
            do
            {
                campaign = Guid.NewGuid();
                directory = Path.Combine(root, CampaignRecoveryStore.FormatCampaignId(campaign));
            }
            while (Directory.Exists(directory) || File.Exists(directory));
            Directory.CreateDirectory(directory);
            return campaign;
        }

        private void Commit(int selected, Guid?[] next)
        {
            var document = new JObject
            {
                [FormatVersionField] = IndexFormatVersion,
                [ActiveSlotField] = selected,
                [CampaignsField] = new JArray(Array.ConvertAll(next, campaign => campaign == null ? null : CampaignRecoveryStore.FormatCampaignId(campaign.Value)))
            };
            SaveFileCommit.Write(indexPath, SaveFileText.Encoding.GetBytes(document.ToString(Formatting.Indented)));
            campaigns = next;
            activeSlot = selected;
        }

        private void ReadIndex(out int selected, out Guid?[] loaded)
        {
            try
            {
                byte[] bytes = SaveFileText.ReadBounded(indexPath, MaximumIndexBytes, "The save slot index");
                using (var input = new StringReader(SaveFileText.Decode(bytes)))
                using (var reader = new JsonTextReader(input) { MaxDepth = IndexMaximumDepth, DateParseHandling = DateParseHandling.None })
                {
                    var document = JObject.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                    });
                    if (reader.Read()) throw new FormatException("The save slot index contains trailing content.");
                    if (IndexUpgrades.Upgrade(document) != SaveFormatCheck.Upgraded)
                        throw new FormatException("The save slot index format version is unsupported.");
                    foreach (var property in document.Properties())
                        if (property.Name != FormatVersionField && property.Name != ActiveSlotField && property.Name != CampaignsField)
                            throw new FormatException("The save slot index contains an unsupported field: " + property.Name);
                    if (document[ActiveSlotField]?.Type != JTokenType.Integer)
                        throw new FormatException("The active save slot must be an integer.");
                    long selectedValue = document[ActiveSlotField].Value<long>();
                    if (selectedValue < 1 || selectedValue > SlotCount)
                        throw new FormatException("The active save slot is outside its supported range.");
                    selected = (int)selectedValue;
                    if (!(document[CampaignsField] is JArray array) || array.Count != SlotCount)
                        throw new FormatException("The save slot index must contain three campaigns.");
                    loaded = new Guid?[SlotCount];
                    var unique = new HashSet<Guid>();
                    for (int index = 0; index < SlotCount; ++index)
                    {
                        JToken value = array[index];
                        if (value.Type == JTokenType.Null) continue;
                        if (value.Type != JTokenType.String)
                            throw new FormatException("Campaign directories must be identifiers or null.");
                        if (!CampaignRecoveryStore.TryParseCampaignId(value.Value<string>(), out Guid campaign) || !unique.Add(campaign))
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
