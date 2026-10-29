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
        private static readonly int FilterCount = Enum.GetValues(typeof(SpiritCollectionFilter)).Length;
        private readonly List<SpiritModifierFamilyRow> rows = new List<SpiritModifierFamilyRow>();
        private readonly List<SpiritModifierFamilyRow> rowPool = new List<SpiritModifierFamilyRow>();
        private readonly List<List<ModifierDefinition>> families = new List<List<ModifierDefinition>>();
        private readonly HashSet<string> unlockedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<ModifierFamilyDefinition.Id> ownedFamilyIds = new HashSet<ModifierFamilyDefinition.Id>();
        private readonly HashSet<ModifierFamilyDefinition.Id> discoveredFamilyIds = new HashSet<ModifierFamilyDefinition.Id>();
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

        public void Build(Action close)
        {
            SpiritUi.RequireWording(transform);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => close());
            filterButton.onClick.RemoveAllListeners();
            filterButton.onClick.AddListener(() => SetFilter((SpiritCollectionFilter)(((int)Filter + 1) % FilterCount)));
        }

        public void Show(MechanicalCatalog mechanicalCatalog, ModifierPresentationCatalog charmPresentation,
            IReadOnlyList<string> profileUnlockedModifierIds, EncounterJourneyCatalogAsset journeyCatalog,
            IReadOnlyList<ModifierFamilyDefinition.Id> profileDiscoveredFamilyIds = null)
        {
            if (mechanicalCatalog == null) throw new ArgumentNullException(nameof(mechanicalCatalog));
            if (charmPresentation == null) throw new ArgumentNullException(nameof(charmPresentation));
            if (journeyCatalog == null) throw new ArgumentNullException(nameof(journeyCatalog));
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
                foreach (var id in profileDiscoveredFamilyIds) discoveredFamilyIds.Add(id);
            var grouped = new Dictionary<ModifierFamilyDefinition, List<ModifierDefinition>>();
            int permanentCount = 0;
            int unlockedCount = 0;
            int starterCount = 0;
            foreach (var definition in catalog.Modifiers)
            {
                if (!definition.RewardEligible || definition.Availability.Access == ModifierAccess.EncounterExclusive) continue;
                if (!grouped.TryGetValue(definition.Family, out var family))
                {
                    family = new List<ModifierDefinition>();
                    grouped.Add(definition.Family, family);
                }
                family.Add(definition);
                if (definition.Availability.Access == ModifierAccess.Starter || unlockedIds.Contains(definition.Id))
                {
                    ownedFamilyIds.Add(definition.Family.Identity);
                    discoveredFamilyIds.Add(definition.Family.Identity);
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
                charms.GetFamilyName(left[0]), charms.GetFamilyName(right[0])));
            foreach (var family in families)
                family.Sort((left, right) => charms.GetOrder(left).CompareTo(charms.GetOrder(right)));
            summary.text = Wording.Summary(unlockedCount, permanentCount, starterCount);
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            SetFilter(SpiritCollectionFilter.Owned);
        }

        public void SetFilter(SpiritCollectionFilter filter)
        {
            if (!Enum.IsDefined(typeof(SpiritCollectionFilter), filter)) throw new ArgumentOutOfRangeException(nameof(filter));
            if (catalog == null) throw new InvalidOperationException("Open the collection before selecting a filter.");
            Filter = filter;
            var wording = Wording;
            filterLabel.text = wording.FilterName(filter);
            rows.Clear();
            bool selectionExists = false;
            string firstSelectableId = null;
            foreach (var family in families)
            {
                var familyId = family[0].Family.Identity;
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
                    row.RenderUndiscovered(family[0].Family);
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
                detailTitle.text = filter == SpiritCollectionFilter.All && rows.Count > 0 ? wording.undiscoveredFamilies : wording.NoFamilies(filter);
                detailEffect.text = "";
                detailSource.text = wording.undiscoveredHint;
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
                if (!row.IsUndiscovered && row.Family == definition.Family) visible = true;
            if (!visible) return;
            SelectedModifierId = definition.Id;
            var wording = Wording;
            detailTitle.text = wording.DetailTitle(charms.GetName(definition), StatusText(definition));
            detailEffect.text = charms.GetEffectText(definition);
            detailEffect.color = charms.GetEffectColor(definition);
            detailSource.text = GetSourceText(definition, catalog, journeys, wording);
            foreach (var row in rows) row.SetSelected(definition.Id);
        }

        private SpiritUiWording.Collection Wording => SpiritUi.GetWording(transform).collection;

        private string StatusText(ModifierDefinition definition) =>
            GetStatusText(definition, unlockedIds.Contains(definition.Id), Wording);

        public static string GetStatusText(ModifierDefinition definition, bool unlocked, SpiritUiWording.Collection wording)
        {
            switch (definition.Availability.Access)
            {
                case ModifierAccess.Starter: return wording.available;
                case ModifierAccess.EncounterExclusive: return wording.encounterOnly;
                default: return unlocked ? wording.unlocked : wording.locked;
            }
        }

        /**
         * Where a charm can be obtained: its unlock and run reward sources as the mechanical catalog states them, and its
         * story and completion gifts from the journeys' progression rewards. Encounters are named by their journeys'
         * locations; encounters without a journey are not listed.
         */
        public static string GetSourceText(ModifierDefinition definition, MechanicalCatalog mechanicalCatalog,
            EncounterJourneyCatalogAsset journeyCatalog, SpiritUiWording.Collection wording)
        {
            if (journeyCatalog == null) throw new ArgumentNullException(nameof(journeyCatalog));
            if (wording == null) throw new ArgumentNullException(nameof(wording));
            ModifierSources sources = mechanicalCatalog.GetModifierSources(definition.Id);
            var unlockSources = new List<string>();
            foreach (var unlock in sources.Unlocks)
            {
                if (!TryGetEncounterName(unlock.Encounter, journeyCatalog, out string name)) continue;
                unlockSources.Add(wording.UnlockSource(name, unlock.StageNumbers));
            }
            var rewardSources = new List<string>();
            foreach (var encounter in sources.RunRewards)
                if (TryGetEncounterName(encounter, journeyCatalog, out string name)) rewardSources.Add(name);
            var giftSources = new List<string>();
            // Encounter journeys are listed in the catalog's encounter order, then story journeys in journey order.
            foreach (var encounter in mechanicalCatalog.Encounters)
            {
                var journey = journeyCatalog.Find(encounter.Identity);
                if (journey != null) AddGiftSources(journey, definition.Id, giftSources, wording);
            }
            foreach (var journey in journeyCatalog.Entries)
                if (journey != null && journey.IsStoryOnly) AddGiftSources(journey, definition.Id, giftSources, wording);
            string rewards = wording.Rewards(rewardSources);
            if (definition.Availability.Access == ModifierAccess.EncounterExclusive) return wording.EncounterOnlySource(rewards);
            if (definition.Availability.Access == ModifierAccess.Starter) return wording.StarterSource(rewards);
            var sourceLines = new List<string>();
            if (unlockSources.Count > 0) sourceLines.Add(wording.UnlockChoices(unlockSources));
            if (giftSources.Count > 0) sourceLines.Add(wording.Gifts(giftSources));
            string source = sourceLines.Count > 0 ? string.Join(wording.sourceLineSeparator, sourceLines) : wording.notObtainable;
            return wording.UnlockableSource(source, rewards);
        }

        /** The encounter's name, its journey's location, or false when the journey catalog has no journey for it. */
        private static bool TryGetEncounterName(EncounterDefinition encounter, EncounterJourneyCatalogAsset journeyCatalog,
            out string name)
        {
            var journey = journeyCatalog.Find(encounter.Identity);
            name = journey != null ? LocationTitle(journey) : null;
            return journey != null;
        }

        private static string LocationTitle(EncounterJourneyAsset journey)
            => !string.IsNullOrWhiteSpace(journey.LocationTitle) ? journey.LocationTitle
                : throw new InvalidOperationException("Journey " + journey.name + " has no location title.");

        private static void AddGiftSources(EncounterJourneyAsset journey, string modifierId, List<string> giftSources,
            SpiritUiWording.Collection wording)
        {
            foreach (var beat in journey.Beats)
                if (beat.ProgressionReward != null && beat.ProgressionReward.GrantsModifier(modifierId))
                {
                    giftSources.Add(wording.StoryGift(LocationTitle(journey)));
                    break;
                }
            if (journey.CompletionReward != null && journey.CompletionReward.GrantsModifier(modifierId))
                giftSources.Add(wording.CompletionGift(LocationTitle(journey)));
        }
    }
}
