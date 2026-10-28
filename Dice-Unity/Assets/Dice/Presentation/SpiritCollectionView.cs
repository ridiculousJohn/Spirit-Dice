using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    public enum SpiritCollectionFilter
    {
        Owned,
        Discovered,
        All
    }

    /** Browses authored charm families using permanent profile progress without mutating it. */
    public sealed class SpiritCollectionView : MonoBehaviour
    {
        private readonly List<SpiritModifierFamilyRow> rows = new List<SpiritModifierFamilyRow>();
        private readonly List<SpiritModifierFamilyRow> rowPool = new List<SpiritModifierFamilyRow>();
        private readonly List<List<ModifierDefinition>> families = new List<List<ModifierDefinition>>();
        private readonly HashSet<string> unlockedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> ownedFamilyIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> discoveredFamilyIds = new HashSet<string>(StringComparer.Ordinal);
        [SerializeField] private Button closeButton;
        [SerializeField] private Button filterButton;
        [SerializeField] private SpiritModifierFamilyRow rowPrefab;
        private MechanicalCatalog catalog;
        private ModifierPresentationCatalog charms;
        private EncounterJourneyCatalogAsset journeys;
        [SerializeField] private RectTransform content;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private Text summary;
        [SerializeField] private Text filterLabel;
        [SerializeField] private Text detailTitle;
        [SerializeField] private Text detailEffect;
        [SerializeField] private Text detailSource;

        public RectTransform Root => (RectTransform)transform;
        public bool IsVisible => Root != null && Root.gameObject.activeSelf;
        public string SelectedModifierId { get; private set; }
        public IReadOnlyList<SpiritModifierFamilyRow> Rows => rows;
        [field: SerializeField] public SpiritCollectionFilter Filter { get; private set; }

        public void Build(RectTransform parent, Action close)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => close());
            filterButton.onClick.RemoveAllListeners();
            filterButton.onClick.AddListener(() => SetFilter((SpiritCollectionFilter)(((int)Filter + 1) % 3)));
        }

        public void Show(MechanicalCatalog mechanicalCatalog, ModifierPresentationCatalog charmPresentation,
            IReadOnlyList<string> profileUnlockedModifierIds, EncounterJourneyCatalogAsset journeyCatalog = null,
            IReadOnlyList<string> profileDiscoveredFamilyIds = null)
        {
            if (mechanicalCatalog == null) throw new ArgumentNullException(nameof(mechanicalCatalog));
            if (charmPresentation == null) throw new ArgumentNullException(nameof(charmPresentation));
            if (Root == null) throw new InvalidOperationException("Build the collection before opening it.");
            catalog = mechanicalCatalog;
            charms = charmPresentation;
            journeys = journeyCatalog;
            unlockedIds.Clear();
            ownedFamilyIds.Clear();
            discoveredFamilyIds.Clear();
            if (profileUnlockedModifierIds != null)
                foreach (string id in profileUnlockedModifierIds) unlockedIds.Add(id);
            if (profileDiscoveredFamilyIds != null)
                foreach (string id in profileDiscoveredFamilyIds) discoveredFamilyIds.Add(id);
            var grouped = new Dictionary<string, List<ModifierDefinition>>(StringComparer.Ordinal);
            int permanentCount = 0;
            int unlockedCount = 0;
            int starterCount = 0;
            foreach (var definition in catalog.Modifiers)
            {
                if (!definition.RewardEligible || definition.Availability.Access == ModifierAccess.EncounterExclusive) continue;
                if (!grouped.TryGetValue(definition.FamilyId, out var family))
                {
                    family = new List<ModifierDefinition>();
                    grouped.Add(definition.FamilyId, family);
                }
                family.Add(definition);
                if (definition.Availability.Access == ModifierAccess.Starter || unlockedIds.Contains(definition.Id))
                {
                    ownedFamilyIds.Add(definition.FamilyId);
                    discoveredFamilyIds.Add(definition.FamilyId);
                }
                if (definition.Availability.Access == ModifierAccess.PermanentUnlock)
                {
                    ++permanentCount;
                    if (unlockedIds.Contains(definition.Id)) ++unlockedCount;
                }
                else if (definition.Availability.Access == ModifierAccess.Starter) ++starterCount;
            }
            families.Clear();
            families.AddRange(grouped.Values);
            families.Sort((left, right) => StringComparer.Ordinal.Compare(
                SpiritModifierFamilyRow.GetFamilyName(charms, left[0]), SpiritModifierFamilyRow.GetFamilyName(charms, right[0])));
            foreach (var family in families)
                family.Sort((left, right) => charms.GetOrder(left).CompareTo(charms.GetOrder(right)));
            summary.text = "Collected " + unlockedCount + " / " + permanentCount + "  |  Available at start " + starterCount;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            SetFilter(SpiritCollectionFilter.Owned);
        }

        public void SetFilter(SpiritCollectionFilter filter)
        {
            if (!Enum.IsDefined(typeof(SpiritCollectionFilter), filter)) throw new ArgumentOutOfRangeException(nameof(filter));
            if (catalog == null) throw new InvalidOperationException("Open the collection before selecting a filter.");
            Filter = filter;
            filterLabel.text = filter.ToString();
            rows.Clear();
            bool selectionExists = false;
            string firstSelectableId = null;
            foreach (var family in families)
            {
                string familyId = family[0].FamilyId;
                bool discovered = discoveredFamilyIds.Contains(familyId);
                if (filter == SpiritCollectionFilter.Owned && !ownedFamilyIds.Contains(familyId) ||
                    filter == SpiritCollectionFilter.Discovered && !discovered) continue;
                int index = rows.Count;
                if (index == rowPool.Count)
                    rowPool.Add(SpiritModifierFamilyRow.Create(rowPrefab, "CollectionFamily" + index, content));
                var row = rowPool[index];
                row.gameObject.SetActive(true);
                rows.Add(row);
                if (!discovered)
                {
                    row.RenderUndiscovered(familyId);
                    continue;
                }
                row.Render(charms, family, StatusText, Select,
                    definition => definition.Availability.Access == ModifierAccess.Starter || unlockedIds.Contains(definition.Id));
                if (firstSelectableId == null) firstSelectableId = family[0].Id;
                foreach (var definition in family)
                    if (definition.Id == SelectedModifierId) selectionExists = true;
            }
            for (int index = rows.Count; index < rowPool.Count; ++index) rowPool[index].gameObject.SetActive(false);
            if (!selectionExists) SelectedModifierId = firstSelectableId;
            if (SelectedModifierId != null) Select(catalog.GetModifier(SelectedModifierId));
            else
            {
                detailTitle.text = filter == SpiritCollectionFilter.All && rows.Count > 0 ? "Undiscovered families" : "No " + filter.ToString().ToLowerInvariant() + " families";
                detailEffect.text = "";
                detailSource.text = "Discover charm families during play to reveal their effects and sources.";
            }
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
        }

        public void Hide()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }

        private void Select(ModifierDefinition definition)
        {
            bool visible = false;
            foreach (var row in rows)
                if (!row.IsUndiscovered && row.FamilyId == definition.FamilyId) visible = true;
            if (!visible) return;
            SelectedModifierId = definition.Id;
            detailTitle.text = charms.GetName(definition) + " - " + StatusText(definition);
            detailEffect.text = charms.GetEffectText(definition);
            detailEffect.color = charms.GetEffectColor(definition);
            detailSource.text = GetSourceText(definition, catalog, journeys);
            foreach (var row in rows) row.SetSelected(definition.Id);
        }

        private string StatusText(ModifierDefinition definition) => GetStatusText(definition, unlockedIds.Contains(definition.Id));

        public static string GetStatusText(ModifierDefinition definition, bool unlocked)
        {
            switch (definition.Availability.Access)
            {
                case ModifierAccess.Starter: return "Available";
                case ModifierAccess.EncounterExclusive: return "Encounter only";
                default: return unlocked ? "Unlocked" : "Locked";
            }
        }

        public static string GetSourceText(ModifierDefinition definition, MechanicalCatalog mechanicalCatalog,
            EncounterJourneyCatalogAsset journeyCatalog = null)
        {
            var unlockSources = new List<string>();
            var giftSources = new List<string>();
            var rewardSources = new List<string>();
            foreach (var encounter in mechanicalCatalog.Encounters)
            {
                var journey = journeyCatalog?.Find(encounter.Id);
                if (journeyCatalog != null && journey == null) continue;
                string name = journey?.LocationTitle ?? DiagnosticText.Humanize(encounter.Id);
                var stages = new List<string>();
                bool selectionSource = encounter.UnlockRewardModifierIds.Contains(definition.Id);
                foreach (var milestone in encounter.UnlockMilestones)
                    if (selectionSource)
                        stages.Add(milestone.StageNumber.ToString());
                if (stages.Count > 0)
                    unlockSources.Add(name + " - after stage" + (stages.Count > 1 ? "s " : " ") + string.Join(", ", stages));
                if (journey != null)
                {
                    foreach (var beat in journey.Beats)
                        if (beat.ProgressionReward != null && beat.ProgressionReward.Build(mechanicalCatalog).GrantedModifierIds.Contains(definition.Id))
                        {
                            giftSources.Add(name + " - story");
                            break;
                        }
                    if (journey.CompletionReward != null && journey.CompletionReward.Build(mechanicalCatalog).GrantedModifierIds.Contains(definition.Id))
                        giftSources.Add(name + " - completion");
                }
                if (definition.RewardEligible && definition.Availability.MatchesEncounter(encounter) &&
                    IsRewardSource(definition.Id, encounter, mechanicalCatalog))
                    rewardSources.Add(name);
            }
            if (journeyCatalog != null)
                foreach (var journey in journeyCatalog.Entries)
                {
                    if (journey == null || !journey.IsStoryOnly) continue;
                    foreach (var beat in journey.Beats)
                        if (beat.ProgressionReward != null && beat.ProgressionReward.Build(mechanicalCatalog).GrantedModifierIds.Contains(definition.Id))
                        {
                            giftSources.Add(journey.LocationTitle + " - story");
                            break;
                        }
                    if (journey.CompletionReward != null && journey.CompletionReward.Build(mechanicalCatalog).GrantedModifierIds.Contains(definition.Id))
                        giftSources.Add(journey.LocationTitle + " - completion");
                }
            string rewards = rewardSources.Count > 0 ? string.Join(", ", rewardSources) : "Not offered in current encounters";
            if (definition.Availability.Access == ModifierAccess.EncounterExclusive)
                return "Encounter only; never permanently collected.\nRun charm pool: " + rewards + ".";
            if (definition.Availability.Access == ModifierAccess.Starter)
                return "Available from the start.\nRun charm pool: " + rewards + ".";
            string source = unlockSources.Count > 0 ? "Unlock choice: " + string.Join("; ", unlockSources) : "";
            if (giftSources.Count > 0) source += (source.Length > 0 ? ".\n" : "") + "Gift: " + string.Join("; ", giftSources);
            return (source.Length > 0 ? source : "Not obtainable yet") + ".\nOnce unlocked, run charm pool: " + rewards + ".";
        }

        private static bool IsRewardSource(string modifierId, EncounterDefinition encounter, MechanicalCatalog mechanicalCatalog)
        {
            if (encounter.EncounterModifierIds.Contains(modifierId)) return true;
            if (encounter.RewardPolicy == null) return false;
            foreach (var layout in encounter.RewardPolicy.ModifierLayouts)
                foreach (string poolId in layout.PoolIds)
                {
                    var pool = mechanicalCatalog.GetRewardPool(poolId);
                    if (pool.ModifierIds.Contains(modifierId) || pool.FallbackModifierId == modifierId) return true;
                }
            return false;
        }
    }
}
