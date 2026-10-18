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
        private sealed class Card
        {
            internal Button Button;
            internal Text Title;
            internal Text Body;
            internal Text Effect;
            internal Image Icon;
            internal SpiritModifierSigilGraphic Sigil;
            internal SpiritModifierCategoryBadge Category;
            internal SpiritCharmCardLayout Layout;
            internal SpiritCharmStatusBadge Badge;
            internal SpiritCharmSelectionGraphic SelectionOutline;
            internal long InstanceId;
        }

        private readonly List<Card> cards = new List<Card>();
        private ISpiritEncounterControls controls;
        private Func<bool> isBusy;
        private SpiritCharmGridLayout grid;
        private SpiritCharmDetailsLayout detailsLayout;
        private long renderedRevision = -1;

        public RectTransform Root { get; private set; }
        public Button CloseButton { get; private set; }
        public ScrollRect Scroll { get; private set; }
        public RectTransform Content { get; private set; }
        public Text DetailTitle { get; private set; }
        public Text DetailBody { get; private set; }
        public Image DetailIcon { get; private set; }
        public SpiritModifierSigilGraphic DetailSigil { get; private set; }
        public SpiritModifierCategoryBadge DetailCategory { get; private set; }
        public RectTransform DetailStatusRow { get; private set; }
        public Text DetailStatus { get; private set; }
        public SpiritCharmStatusBadge DetailBadge { get; private set; }
        public long SelectedInstanceId { get; private set; } = -1;

        public void Build(RectTransform parent, Action close, Func<bool> busy)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (close == null) throw new ArgumentNullException(nameof(close));
            if (busy == null) throw new ArgumentNullException(nameof(busy));
            if (Root != null) throw new InvalidOperationException("The owned charms panel has already been built.");

            isBusy = busy;
            Root = SpiritUi.Panel("CharmsOverlay", parent, Vector2.zero, Vector2.one,
                new Color32(20, 32, 27, 255), 0, true).rectTransform;
            SpiritUi.Heading("CharmsTitle", Root, "YOUR CHARMS", 29, new Vector2(.03f, .9f), new Vector2(.73f, .98f));
            CloseButton = SpiritUi.Button("CloseCharmsButton", Root, "Close", new Vector2(.75f, .9f), new Vector2(.98f, .98f),
                () => close());
            SpiritUi.Label("CharmsSubtitle", Root, "Kept for this run. Tap a charm to inspect it.", 18,
                new Vector2(.03f, .84f), new Vector2(.97f, .9f), TextAnchor.MiddleLeft, SpiritUi.GetTheme(transform).muted);
            Scroll = SpiritUi.Scroll("OwnedCharmScroll", Root, new Vector2(0, .34f), new Vector2(1, .84f), out var content);
            Content = content;
            var layout = Content.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(235, 156);
            layout.spacing = new Vector2(12, 12);
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            Content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            grid = Content.gameObject.AddComponent<SpiritCharmGridLayout>();
            var details = SpiritUi.Panel("CharmDetails", Root, new Vector2(.02f, .04f), new Vector2(.98f, .33f),
                SpiritUi.GetTheme(transform).panelColor, 6, true);
            DetailTitle = SpiritUi.Heading("CharmDetailTitle", details.transform, "No charms yet", 23, new Vector2(0, .72f), Vector2.one);
            DetailIcon = SpiritCharmVisuals.MakeIcon("CharmDetailIcon", details.transform, new Vector2(0, .86f), 44);
            DetailIcon.rectTransform.anchoredPosition = new Vector2(42, 0);
            DetailSigil = SpiritCharmVisuals.MakeSigil("CharmDetailSigil", DetailIcon);
            DetailCategory = SpiritModifierCategoryBadge.Create("CharmDetailCategory", DetailIcon);
            DetailBody = SpiritUi.Label("CharmDetailBody", details.transform, "Clear a stage to choose a charm.", 17,
                Vector2.zero, new Vector2(1, .72f));
            foreach (var text in new[] { DetailTitle, DetailBody })
            {
                text.rectTransform.offsetMin = new Vector2(24, 5);
                text.rectTransform.offsetMax = new Vector2(-24, -5);
            }
            DetailStatusRow = SpiritUi.Rect("CharmDetailStatusRow", details.transform, Vector2.zero, Vector2.one);
            DetailStatus = SpiritUi.Label("CharmDetailStatus", DetailStatusRow, "", 17,
                Vector2.zero, Vector2.one, TextAnchor.MiddleLeft);
            DetailStatus.resizeTextForBestFit = false;
            DetailBadge = SpiritCharmStatusBadge.Create(DetailStatusRow);
            ((RectTransform)DetailBadge.transform).anchoredPosition = Vector2.zero;
            DetailStatusRow.gameObject.SetActive(false);
            detailsLayout = details.gameObject.AddComponent<SpiritCharmDetailsLayout>();
            detailsLayout.Initialize(DetailTitle, DetailBody, DetailIcon, (RectTransform)Scroll.transform,
                DetailStatusRow, DetailStatus, DetailBadge, DetailSigil, DetailCategory);
            Root.gameObject.SetActive(false);
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

        private Card MakeCard(string name)
        {
            var image = SpiritUi.Panel(name, Content, Vector2.zero, Vector2.one, SpiritUi.GetTheme(transform).panelColor, 0, true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var card = new Card { Button = button };
            card.Title = SpiritUi.Heading("Title", image.transform, "", 17,
                new Vector2(0, .70f), new Vector2(1, .96f), TextAnchor.MiddleLeft, SpiritUi.GetTheme(transform).cream);
            card.Icon = SpiritCharmVisuals.MakeIcon("CharmIcon", image.transform, new Vector2(0, .83f), 36);
            card.Icon.rectTransform.anchoredPosition = new Vector2(42, 0);
            card.Sigil = SpiritCharmVisuals.MakeSigil("CharmSigil", card.Icon);
            card.Category = SpiritModifierCategoryBadge.Create("CharmCategory", card.Icon);
            card.Body = SpiritUi.Label("Condition", image.transform, "", 17,
                new Vector2(0, .40f), new Vector2(1, .70f));
            card.Effect = SpiritUi.Label("Effects", image.transform, "", 17, new Vector2(0, .20f), new Vector2(1, .4f),
                TextAnchor.MiddleLeft, SpiritUi.GetTheme(transform).amber);
            foreach (var text in new[] { card.Title, card.Body, card.Effect })
            {
                text.rectTransform.offsetMin = new Vector2(24, 5);
                text.rectTransform.offsetMax = new Vector2(-24, -5);
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 14;
                text.resizeTextMaxSize = text.fontSize;
            }
            card.Body.fontSize = 15;
            card.Effect.fontSize = 16;
            card.Layout = image.gameObject.AddComponent<SpiritCharmCardLayout>();
            card.Layout.Initialize(card.Title, card.Body, card.Effect, card.Icon, card.Sigil, card.Category);
            card.Badge = SpiritCharmStatusBadge.Create(image.transform);
            card.SelectionOutline = SpiritUi.Full("SelectionOutline", image.transform).gameObject.AddComponent<SpiritCharmSelectionGraphic>();
            card.SelectionOutline.color = new Color32(255, 215, 119, 255);
            card.SelectionOutline.raycastTarget = false;
            card.SelectionOutline.gameObject.SetActive(false);
            button.onClick.AddListener(() =>
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
                DetailBody.supportRichText = true;
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
                    card.Button.GetComponent<Image>().color = card.InstanceId == SelectedInstanceId ? new Color32(255, 232, 179, 255) : Color.white;
                    card.SelectionOutline.gameObject.SetActive(card.InstanceId == SelectedInstanceId);
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
