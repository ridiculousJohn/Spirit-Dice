using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /** Presentation artwork can replace a definition or family's provisional sigil. */
    public static class SpiritModifierIcons
    {
        private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime() { icons.Clear(); }

        public static void RegisterIcon(string definitionOrFamilyId, Sprite sprite)
        {
            if (string.IsNullOrEmpty(definitionOrFamilyId)) throw new ArgumentException("An icon identity is required.", nameof(definitionOrFamilyId));
            if (sprite == null) icons.Remove(definitionOrFamilyId);
            else icons[definitionOrFamilyId] = sprite;
        }

        public static Sprite GetSprite(ModifierDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            Sprite sprite = FindSprite(definition.Id);
            if (sprite != null) return sprite;
            sprite = FindSprite(definition.FamilyId);
            if (sprite != null) return sprite;
            sprite = SpiritModifierPresentation.GetIcon(definition.Id);
            if (sprite != null) return sprite;
            return SpiritModifierArtwork.GetSprite(definition.Id);
        }

        public static uint GetSigilSeed(ModifierDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            uint value = 2166136261;
            // A fixed hash keeps family artwork stable across runtime sessions and platform hash seeds.
            foreach (char character in definition.FamilyId) value = unchecked((value ^ character) * 16777619);
            return value;
        }

        public static Color GetEffectColor(ModifierDefinition definition)
        {
            var category = SpiritModifierPresentation.GetCategory(definition);
            return category != null ? category.AccentColor : new Color32(239, 224, 190, 255);
        }

        public static Color GetInkColor(ModifierDefinition definition)
        {
            var category = SpiritModifierPresentation.GetCategory(definition);
            return category != null ? category.InkColor : new Color32(70, 52, 35, 255);
        }

        public static ModifierCategorySymbol GetCategorySymbol(ModifierDefinition definition)
        {
            var category = SpiritModifierPresentation.GetCategory(definition);
            return category != null ? category.Symbol : ModifierCategorySymbol.None;
        }

        public static Sprite GetCategorySprite(ModifierDefinition definition)
            => SpiritModifierArtwork.GetCategorySprite(GetCategorySymbol(definition));

        public static Color GetEffectColor(ModifierContribution contribution)
        {
            var category = SpiritModifierPresentation.GetCategory(contribution.DefinitionId);
            return category != null ? category.AccentColor : new Color32(239, 224, 190, 255);
        }

        private static Sprite FindSprite(string id)
        {
            if (icons.TryGetValue(id, out Sprite sprite) && sprite != null) return sprite;
            return null;
        }
    }
}
