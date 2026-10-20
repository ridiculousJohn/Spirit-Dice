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
        [Serializable]
        private sealed class VariantView
        {
            [SerializeField] public Button Button;
            [SerializeField] public Image Category;
            [SerializeField] public Text Effect;
            [SerializeField] public Text State;
            [SerializeField] public Graphic Selection;
            [NonSerialized] public ModifierDefinition Definition;
            [SerializeField] public CanvasGroup Visibility;
        }

        [SerializeField] private VariantView[] variants;
        [SerializeField] private Text familyName;
        [SerializeField] private Text condition;
        [SerializeField] private Image icon;
        [SerializeField] private SpiritModifierSigilGraphic sigil;
        [SerializeField] private GameObject artwork;
        [SerializeField] private GameObject variantRoot;
        private Action<ModifierDefinition> selected;

        public string FamilyId { get; private set; }
        public bool IsUndiscovered { get; private set; }

        public static SpiritModifierFamilyRow Create(SpiritModifierFamilyRow prefab, string name, Transform parent)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            var row = Instantiate(prefab, parent, false);
            row.name = name;
            foreach (var item in row.variants)
                item.Button.onClick.AddListener(() => { if (item.Definition != null) row.selected?.Invoke(item.Definition); });
            return row;
        }

        public void Render(IReadOnlyList<ModifierDefinition> definitions,
            Func<ModifierDefinition, string> stateText = null, Action<ModifierDefinition> select = null,
            Func<ModifierDefinition, bool> available = null)
        {
            if (definitions == null || definitions.Count == 0 || definitions.Count > variants.Length)
                throw new ArgumentException("A family row requires one to four variants.", nameof(definitions));
            var first = definitions[0];
            IsUndiscovered = false;
            artwork.SetActive(true);
            variantRoot.SetActive(true);
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
            for (int index = 0; index < variants.Length; ++index)
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
                item.Visibility.alpha = available == null || available(definition) ? 1 : .35f;
                item.Selection.enabled = false;
            }
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        public void RenderUndiscovered(string familyId)
        {
            FamilyId = familyId;
            IsUndiscovered = true;
            artwork.SetActive(false);
            variantRoot.SetActive(false);
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
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
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

    }
}
