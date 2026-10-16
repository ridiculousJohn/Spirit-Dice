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
        private MechanicalCatalog catalog;
        private EncounterJourneyCatalogAsset journeys;
        private RectTransform content;
        private ScrollRect scroll;
        private Text summary;
        private Text filterLabel;
        private Text detailTitle;
        private Text detailEffect;
        private Text detailSource;

        public RectTransform Root { get; private set; }
        public bool IsVisible => Root != null && Root.gameObject.activeSelf;
        public string SelectedModifierId { get; private set; }
        public IReadOnlyList<SpiritModifierFamilyRow> Rows => rows;
        public SpiritCollectionFilter Filter { get; private set; }

        public void Build(RectTransform parent, Action close)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (close == null) throw new ArgumentNullException(nameof(close));
            if (Root != null) throw new InvalidOperationException("The collection has already been built.");
            var theme = SpiritUi.GetTheme(parent);
            Root = SpiritUi.Panel("CollectionOverlay", parent, Vector2.zero, Vector2.one,
                new Color(theme.panelColor.r, theme.panelColor.g, theme.panelColor.b, 1)).rectTransform;
            SpiritUi.Panel("CollectionFrame", Root, Vector2.zero, Vector2.one,
                theme.panelColor, 0, true).raycastTarget = false;
            SpiritUi.Heading("CollectionTitle", Root, "COLLECTION", 29,
                new Vector2(.03f, .915f), new Vector2(.73f, .99f), TextAnchor.MiddleLeft, theme.amber);
            var closeImage = SpiritUi.Panel("CloseCollectionButton", Root,
                new Vector2(.75f, .915f), new Vector2(.98f, .99f), theme.panelColor, 5);
            var closeBorder = closeImage.gameObject.AddComponent<Outline>();
            closeBorder.effectColor = new Color(theme.amber.r, theme.amber.g, theme.amber.b, .6f);
            closeBorder.effectDistance = new Vector2(1, -1);
            var closeButton = closeImage.gameObject.AddComponent<SpiritUiButtonVisual>();
            closeButton.targetGraphic = closeImage;
            closeButton.onClick.AddListener(() => close());
            SpiritUi.Heading("CloseCollectionButtonLabel", closeImage.transform, "Close", 20,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, theme.cream);
            closeButton.CaptureContent();
            summary = SpiritUi.Label("CollectionSummary", Root, "", 17,
                new Vector2(.03f, .855f), new Vector2(.97f, .915f), TextAnchor.MiddleLeft, theme.muted);
            var filterImage = SpiritUi.Panel("CollectionFilterButton", Root,
                new Vector2(.03f, .795f), new Vector2(.40f, .85f), theme.panelColor, 5);
            var filterBorder = filterImage.gameObject.AddComponent<Outline>();
            filterBorder.effectColor = closeBorder.effectColor;
            filterBorder.effectDistance = closeBorder.effectDistance;
            var filterButton = filterImage.gameObject.AddComponent<SpiritUiButtonVisual>();
            filterButton.targetGraphic = filterImage;
            filterButton.onClick.AddListener(() => SetFilter((SpiritCollectionFilter)(((int)Filter + 1) % 3)));
            filterLabel = SpiritUi.Heading("CollectionFilterButtonLabel", filterImage.transform, "Owned", 20,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, theme.cream);
            filterButton.CaptureContent();
            scroll = SpiritUi.Scroll("CollectionFamilies", Root, new Vector2(.015f, .275f), new Vector2(.985f, .79f), out content);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var detail = SpiritUi.Panel("CollectionDetail", Root, new Vector2(.03f, .025f), new Vector2(.97f, .27f),
                theme.panelColor, 0, true);
            detailTitle = SpiritUi.Heading("CollectionDetailTitle", detail.transform, "", 22,
                new Vector2(.02f, .76f), new Vector2(.98f, .99f), TextAnchor.MiddleLeft, theme.amber);
            detailEffect = SpiritUi.Label("CollectionDetailEffect", detail.transform, "", 18,
                new Vector2(.02f, .57f), new Vector2(.98f, .77f));
            SpiritUi.Scroll("CollectionDetailSources", detail.transform,
                new Vector2(.02f, .025f), new Vector2(.98f, .57f), out var sourceContent);
            var sourceLayout = sourceContent.gameObject.AddComponent<VerticalLayoutGroup>();
            sourceLayout.childControlWidth = sourceLayout.childControlHeight = true;
            sourceLayout.childForceExpandWidth = true;
            sourceLayout.childForceExpandHeight = false;
            sourceContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            detailSource = SpiritUi.Label("CollectionDetailSource", sourceContent, "", 17,
                Vector2.zero, Vector2.one, TextAnchor.UpperLeft, theme.muted);
            Root.gameObject.SetActive(false);
        }

        public void Show(MechanicalCatalog mechanicalCatalog, IReadOnlyList<string> profileUnlockedModifierIds,
            EncounterJourneyCatalogAsset journeyCatalog = null, IReadOnlyList<string> profileDiscoveredFamilyIds = null)
        {
            if (mechanicalCatalog == null) throw new ArgumentNullException(nameof(mechanicalCatalog));
            if (Root == null) throw new InvalidOperationException("Build the collection before opening it.");
            catalog = mechanicalCatalog;
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
                SpiritModifierFamilyRow.GetFamilyName(left[0]), SpiritModifierFamilyRow.GetFamilyName(right[0])));
            foreach (var family in families)
                family.Sort((left, right) => VariantOrder(left).CompareTo(VariantOrder(right)));
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
                    rowPool.Add(SpiritModifierFamilyRow.Create("CollectionFamily" + index, content));
                var row = rowPool[index];
                row.gameObject.SetActive(true);
                rows.Add(row);
                if (!discovered)
                {
                    row.RenderUndiscovered(familyId);
                    continue;
                }
                row.Render(family, StatusText, Select,
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
            detailTitle.text = SpiritModifierPresentation.GetName(definition) + " - " + StatusText(definition);
            detailEffect.text = SpiritModifierPresentation.GetEffectText(definition);
            detailEffect.color = SpiritModifierIcons.GetEffectColor(definition);
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

        private static int VariantOrder(ModifierDefinition definition)
        {
            bool baseScore = false;
            bool multiplier = false;
            foreach (var effect in definition.Effects)
            {
                if (effect.Kind == ModifierEffectKind.AddRerolls) return 3;
                if (effect.Kind == ModifierEffectKind.AddBaseScore) baseScore = true;
                if (effect.Kind == ModifierEffectKind.AddMultiplier) multiplier = true;
            }
            return baseScore && multiplier ? 2 : multiplier ? 1 : 0;
        }
    }
}
