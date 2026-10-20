using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Lists the charms kept for this run, with details for the selected charm. */
    public sealed class SpiritOwnedCharmsPanel : MonoBehaviour
    {
        [SerializeField] private SpiritOwnedCharmCard cardPrefab;
        private readonly List<SpiritOwnedCharmCard> cards = new List<SpiritOwnedCharmCard>();
        private ISpiritEncounterControls controls;
        private Func<bool> isBusy;
        [SerializeField] private SpiritCharmGridLayout grid;
        [SerializeField] private SpiritCharmDetailsLayout detailsLayout;
        private long renderedRevision = -1;

        public RectTransform Root => (RectTransform)transform;
        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public ScrollRect Scroll { get; private set; }
        [field: SerializeField] public RectTransform Content { get; private set; }
        [field: SerializeField] public Text DetailTitle { get; private set; }
        [field: SerializeField] public Text DetailBody { get; private set; }
        [field: SerializeField] public Image DetailIcon { get; private set; }
        [field: SerializeField] public SpiritModifierSigilGraphic DetailSigil { get; private set; }
        [field: SerializeField] public SpiritModifierCategoryBadge DetailCategory { get; private set; }
        [field: SerializeField] public RectTransform DetailStatusRow { get; private set; }
        [field: SerializeField] public Text DetailStatus { get; private set; }
        [field: SerializeField] public SpiritCharmStatusBadge DetailBadge { get; private set; }
        public long SelectedInstanceId { get; private set; } = -1;

        public void Build(RectTransform parent, Action close, Func<bool> busy)
        {
            isBusy = busy ?? throw new ArgumentNullException(nameof(busy));
            CloseButton.onClick.RemoveAllListeners();
            CloseButton.onClick.AddListener(() => close());
        }

        public void Bind(ISpiritEncounterControls encounterControls)
        {
            controls = encounterControls ?? throw new ArgumentNullException(nameof(encounterControls));
        }

        /** Forces the next render to refresh every card, as when a different snapshot is presented. */
        public void Invalidate() => renderedRevision = -1;

        public void Show()
        {
            Render();
            Root.gameObject.SetActive(true);
            ScrollToSelected();
        }

        public void Render()
        {
            var snapshot = controls.Snapshot;
            if (snapshot == null) return;
            if (renderedRevision != snapshot.Revision)
            {
                renderedRevision = snapshot.Revision;
                while (cards.Count < snapshot.Modifiers.Count)
                    cards.Add(MakeCard("OwnedCharm" + (cards.Count + 1)));
                for (int i = 0; i < cards.Count; ++i)
                {
                    var card = cards[i];
                    card.Button.gameObject.SetActive(i < snapshot.Modifiers.Count);
                    if (i >= snapshot.Modifiers.Count) continue;
                    var instance = snapshot.Modifiers[i];
                    var definition = controls.Catalog.GetModifier(instance.DefinitionId);
                    card.InstanceId = instance.InstanceId;
                    SpiritCharmVisuals.SetIcon(card.Icon, card.Sigil, card.Category, definition);
                    card.Title.text = SpiritModifierPresentation.GetName(definition);
                    card.Body.text = SpiritModifierPresentation.GetConditionText(definition);
                    card.Effect.text = SpiritModifierPresentation.GetEffectText(definition);
                    card.Effect.color = SpiritModifierIcons.GetEffectColor(definition);
                    card.Badge.Render(SpiritCharmVisuals.Status(controls, instance, definition));
                }
                grid.RefreshLayout();
            }
            if (snapshot.Modifiers.Count > 0 && !ContainsCharm(snapshot, SelectedInstanceId))
                SelectedInstanceId = snapshot.Modifiers[0].InstanceId;
            RenderDetail();
        }

        private void ScrollToSelected()
        {
            Canvas.ForceUpdateCanvases();
            grid.RefreshLayout();
            LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
            Canvas.ForceUpdateCanvases();
            Scroll.StopMovement();
            foreach (var card in cards)
            {
                if (card.InstanceId != SelectedInstanceId || !card.Button.gameObject.activeSelf) continue;
                var viewport = Scroll.viewport;
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, card.Button.transform);
                float overflow = Content.rect.height - viewport.rect.height;
                if (overflow <= 0) return;
                float shift = 0;
                if (bounds.max.y > viewport.rect.yMax || bounds.size.y > viewport.rect.height)
                    shift = viewport.rect.yMax - bounds.max.y;
                else if (bounds.min.y < viewport.rect.yMin)
                    shift = viewport.rect.yMin - bounds.min.y;
                Scroll.verticalNormalizedPosition = Mathf.Clamp01(Scroll.verticalNormalizedPosition - shift / overflow);
                return;
            }
        }

        private SpiritOwnedCharmCard MakeCard(string name)
        {
            var card = Instantiate(cardPrefab, Content, false);
            card.name = name;
            card.Button.onClick.AddListener(() =>
            {
                if (isBusy()) return;
                SelectedInstanceId = card.InstanceId;
                RenderDetail();
            });
            return card;
        }

        private static bool ContainsCharm(EncounterSnapshot snapshot, long instanceId)
        {
            foreach (var modifier in snapshot.Modifiers) if (modifier.InstanceId == instanceId) return true;
            return false;
        }

        private void RenderDetail()
        {
            foreach (var instance in controls.Snapshot.Modifiers)
            {
                if (instance.InstanceId != SelectedInstanceId) continue;
                var definition = controls.Catalog.GetModifier(instance.DefinitionId);
                DetailIcon.sprite = SpiritModifierIcons.GetSprite(definition);
                Color color = SpiritModifierIcons.GetEffectColor(definition);
                DetailIcon.color = DetailSigil.color = color;
                DetailIcon.gameObject.SetActive(DetailIcon.sprite != null);
                DetailSigil.Seed = SpiritModifierIcons.GetSigilSeed(definition);
                DetailSigil.gameObject.SetActive(DetailIcon.sprite == null);
                DetailCategory.Render(definition);
                DetailTitle.text = SpiritModifierPresentation.GetName(definition);
                DetailBody.text = SpiritModifierPresentation.GetConditionText(definition) + "\n" +
                    "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" +
                    SpiritModifierPresentation.GetEffectText(definition) + "</color>\n" +
                    SpiritModifierPresentation.GetTimingText(definition) + "\n" +
                    SpiritCharmVisuals.AvailabilityText(definition, controls.Snapshot);
                var status = SpiritCharmVisuals.Status(controls, instance, definition);
                DetailBadge.Render(status);
                DetailStatus.text = status.GetDetailText(definition.Phase, controls.Snapshot.Phase);
                DetailStatus.color = DetailBadge.Icon.color;
                DetailStatusRow.gameObject.SetActive(true);
                detailsLayout.RefreshLayout();
                foreach (var card in cards)
                {
                    bool selected = card.InstanceId == SelectedInstanceId;
                    card.Button.targetGraphic.color = selected ? card.SelectedColor : card.NormalColor;
                    card.SelectionOutline.gameObject.SetActive(selected);
                }
                return;
            }
            DetailTitle.text = "No charms yet";
            DetailIcon.sprite = null;
            DetailIcon.gameObject.SetActive(false);
            DetailSigil.gameObject.SetActive(false);
            DetailCategory.Render(null);
            DetailBody.text = "Clear a stage to choose a charm.";
            DetailStatusRow.gameObject.SetActive(false);
            detailsLayout.RefreshLayout();
        }
    }
}
