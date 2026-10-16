using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Shares family identity and trigger wording while keeping effect variants individually selectable. */
    public sealed class SpiritModifierFamilyRow : MonoBehaviour
    {
        private sealed class VariantView
        {
            public Button Button;
            public Image Category;
            public Text Effect;
            public Text State;
            public Graphic Selection;
            public ModifierDefinition Definition;
            public CanvasGroup Visibility;
        }

        private readonly List<VariantView> variants = new List<VariantView>();
        private Text familyName;
        private Text condition;
        private Image icon;
        private SpiritModifierSigilGraphic sigil;
        private Action<ModifierDefinition> selected;

        public string FamilyId { get; private set; }
        public bool IsUndiscovered { get; private set; }

        public static SpiritModifierFamilyRow Create(string name, Transform parent)
        {
            var theme = SpiritUi.GetTheme(parent);
            var panel = SpiritUi.Panel(name, parent, Vector2.zero, Vector2.one, theme.panelColor, 0, true);
            panel.gameObject.AddComponent<LayoutElement>().preferredHeight = 210;
            var row = panel.gameObject.AddComponent<SpiritModifierFamilyRow>();
            var iconRoot = SpiritUi.Rect("FamilyArtwork", panel.transform, new Vector2(0, 1), new Vector2(0, 1));
            iconRoot.pivot = new Vector2(0, 1);
            iconRoot.anchoredPosition = new Vector2(16, -12);
            iconRoot.sizeDelta = new Vector2(40, 40);
            row.icon = SpiritUi.Full("FamilyIcon", iconRoot).gameObject.AddComponent<Image>();
            row.icon.preserveAspect = true;
            row.icon.raycastTarget = false;
            row.sigil = SpiritUi.Full("FamilySigil", iconRoot).gameObject.AddComponent<SpiritModifierSigilGraphic>();
            row.sigil.raycastTarget = false;
            row.sigil.color = theme.cream;
            row.familyName = SpiritUi.Heading("FamilyName", panel.transform, "", 23,
                Vector2.zero, Vector2.one, TextAnchor.MiddleLeft, theme.amber);
            row.condition = SpiritUi.Label("FamilyCondition", panel.transform, "", 18,
                Vector2.zero, Vector2.one, TextAnchor.UpperLeft);
            row.condition.supportRichText = true;
            for (int index = 0; index < 4; ++index)
            {
                var item = new VariantView();
                float left = .02f + index * .24f;
                var cell = SpiritUi.Panel("FamilyVariant" + index, panel.transform,
                    new Vector2(left, 0), new Vector2(left + .24f, 0), new Color32(15, 25, 20, 255));
                item.Button = cell.gameObject.AddComponent<SpiritUiButtonVisual>();
                item.Button.targetGraphic = cell;
                var colors = item.Button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = colors.selectedColor = theme.highlightedTint;
                colors.pressedColor = theme.pressedTint;
                colors.fadeDuration = .12f;
                item.Button.colors = colors;
                item.Button.onClick.AddListener(() =>
                {
                    if (item.Definition != null) row.selected?.Invoke(item.Definition);
                });
                var border = cell.gameObject.AddComponent<Outline>();
                border.effectColor = new Color(theme.muted.r, theme.muted.g, theme.muted.b, .3f);
                border.effectDistance = new Vector2(1, -1);
                var buttonRect = (RectTransform)item.Button.transform;
                buttonRect.offsetMin = new Vector2(3, 12);
                buttonRect.offsetMax = new Vector2(-3, 78);
                item.Visibility = item.Button.gameObject.AddComponent<CanvasGroup>();
                item.Category = SpiritUi.Rect("VariantCategory", item.Button.transform,
                    new Vector2(.08f, .40f), new Vector2(.32f, .88f)).gameObject.AddComponent<Image>();
                item.Category.preserveAspect = true;
                item.Category.raycastTarget = false;
                item.Effect = SpiritUi.Label("VariantEffect", item.Button.transform, "", 16,
                    new Vector2(.32f, .30f), new Vector2(.92f, .96f), TextAnchor.MiddleCenter);
                item.Effect.rectTransform.offsetMin = new Vector2(0, 1);
                item.Effect.rectTransform.offsetMax = new Vector2(-3, -1);
                item.State = SpiritUi.Label("VariantState", item.Button.transform, "", 12,
                    new Vector2(.04f, .05f), new Vector2(.96f, .30f), TextAnchor.MiddleCenter, theme.muted);
                item.State.rectTransform.offsetMin = new Vector2(2, 0);
                item.State.rectTransform.offsetMax = new Vector2(-2, 0);
                item.Selection = SpiritUi.Full("VariantSelection", item.Button.transform)
                    .gameObject.AddComponent<SpiritCharmSelectionGraphic>();
                item.Selection.color = theme.amber;
                item.Selection.raycastTarget = false;
                item.Selection.enabled = false;
                ((SpiritUiButtonVisual)item.Button).CaptureContent();
                row.variants.Add(item);
            }
            return row;
        }

        public void Render(IReadOnlyList<ModifierDefinition> definitions,
            Func<ModifierDefinition, string> stateText = null, Action<ModifierDefinition> select = null,
            Func<ModifierDefinition, bool> available = null)
        {
            if (definitions == null || definitions.Count == 0 || definitions.Count > variants.Count)
                throw new ArgumentException("A family row requires one to four variants.", nameof(definitions));
            var first = definitions[0];
            IsUndiscovered = false;
            FamilyId = first.FamilyId;
            familyName.text = GetFamilyName(first);
            var theme = SpiritUi.GetTheme(transform);
            condition.text = SpiritRewardCard.GetModifierDescription(first, theme.amber, theme.GetModifierTriggerColor(first.Phase));
            icon.sprite = SpiritModifierIcons.GetSprite(first);
            icon.color = SpiritUi.GetTheme(transform).cream;
            icon.gameObject.SetActive(icon.sprite != null);
            sigil.Seed = SpiritModifierIcons.GetSigilSeed(first);
            sigil.gameObject.SetActive(icon.sprite == null);
            selected = select;
            for (int index = 0; index < variants.Count; ++index)
            {
                var item = variants[index];
                item.Button.gameObject.SetActive(index < definitions.Count);
                if (index >= definitions.Count) continue;
                var definition = definitions[index];
                if (definition.FamilyId != FamilyId)
                    throw new ArgumentException("A family row cannot mix families.", nameof(definitions));
                item.Definition = definition;
                item.Category.sprite = SpiritModifierIcons.GetCategorySprite(definition);
                item.Category.color = Color.white;
                item.Category.gameObject.SetActive(item.Category.sprite != null);
                item.Effect.text = GetCompactEffect(definition);
                item.Effect.color = SpiritModifierIcons.GetEffectColor(definition);
                item.State.text = stateText?.Invoke(definition) ?? "";
                item.State.gameObject.SetActive(stateText != null);
                item.Category.rectTransform.anchorMin = new Vector2(.08f, stateText != null ? .40f : .20f);
                item.Category.rectTransform.anchorMax = new Vector2(.32f, stateText != null ? .88f : .80f);
                item.Effect.rectTransform.anchorMin = new Vector2(.32f, stateText != null ? .30f : 0);
                item.Effect.rectTransform.anchorMax = new Vector2(.96f, stateText != null ? .96f : 1);
                item.Effect.rectTransform.offsetMin = new Vector2(0, stateText != null ? 1 : 6);
                item.Effect.rectTransform.offsetMax = new Vector2(-6, stateText != null ? -1 : -6);
                item.Visibility.alpha = available == null || available(definition) ? 1 : .35f;
                item.Selection.enabled = false;
            }
            LayoutRow();
        }

        public void RenderUndiscovered(string familyId)
        {
            FamilyId = familyId;
            IsUndiscovered = true;
            selected = null;
            familyName.text = "Undiscovered family";
            condition.text = "Locked";
            icon.gameObject.SetActive(false);
            sigil.gameObject.SetActive(false);
            foreach (var item in variants)
            {
                item.Definition = null;
                item.Effect.text = item.State.text = "";
                item.Selection.enabled = false;
                item.Button.gameObject.SetActive(false);
            }
            LayoutRow();
        }

        public void SetSelected(string modifierId)
        {
            foreach (var item in variants)
                item.Selection.enabled = item.Definition != null && item.Definition.Id == modifierId;
        }

        public static string GetFamilyName(ModifierDefinition definition)
        {
            string name = SpiritModifierPresentation.GetName(definition);
            int separator = name.LastIndexOf(" / ", StringComparison.Ordinal);
            return separator < 0 ? name : name.Substring(0, separator);
        }

        private static string GetCompactEffect(ModifierDefinition definition)
        {
            var lines = new List<string>();
            foreach (var effect in definition.Effects)
                lines.Add("+" + DiagnosticText.Number(effect.Amount) + (definition.Effects.Count > 1 ?
                    effect.Kind == ModifierEffectKind.AddBaseScore ? " B" : " M" : ""));
            return string.Join("\n", lines);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (condition != null) LayoutRow();
        }

        private void LayoutRow()
        {
            if (IsUndiscovered)
            {
                Place(familyName, 16, 16, 8, 34);
                Place(condition, 16, 16, 42, 26);
                GetComponent<LayoutElement>().preferredHeight = 78;
                return;
            }
            float width = ((RectTransform)transform).rect.width;
            if (width <= 0) width = 400;
            var settings = condition.GetGenerationSettings(new Vector2(Mathf.Max(1, width - 32), 0));
            float descriptionHeight = Mathf.Max(40, Mathf.Ceil(condition.cachedTextGeneratorForLayout
                .GetPreferredHeight(condition.text, settings) / condition.pixelsPerUnit) + 2);
            Place(familyName, 64, 12, 12, 36);
            Place(condition, 16, 16, 58, descriptionHeight);
            var layout = GetComponent<LayoutElement>();
            float height = Mathf.Max(190, 58 + descriptionHeight + 90);
            if (Mathf.Abs(layout.preferredHeight - height) > .5f) layout.preferredHeight = height;
        }

        private static void Place(Text text, float left, float right, float top, float height)
        {
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0, 1);
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(left, -top - height);
            text.rectTransform.offsetMax = new Vector2(-right, -top);
        }
    }
}
