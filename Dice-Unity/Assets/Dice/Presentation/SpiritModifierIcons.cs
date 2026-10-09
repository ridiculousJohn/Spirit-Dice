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

        public static void RegisterIcon(string definitionOrFamilyId, Sprite sprite)
        {
            if (string.IsNullOrEmpty(definitionOrFamilyId)) throw new ArgumentException("An icon identity is required.", nameof(definitionOrFamilyId));
            if (sprite == null) icons.Remove(definitionOrFamilyId);
            else icons[definitionOrFamilyId] = sprite;
        }

        public static Sprite GetSprite(ModifierDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return FindSprite(definition.Id) ?? FindSprite(definition.FamilyId) ?? SpiritModifierArtwork.GetSprite(definition.Id);
        }

        public static uint GetSigilSeed(ModifierDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            uint value = 2166136261;
            // A fixed hash keeps family artwork stable across runtime sessions and platform hash seeds.
            foreach (char character in definition.FamilyId) value = unchecked((value ^ character) * 16777619);
            return value;
        }

        public static Color GetEffectColor(ModifierContribution contribution)
        {
            int kinds = (contribution.BaseScore != 0 ? 1 : 0) + (contribution.Multiplier != 0 ? 1 : 0) +
                (contribution.Rerolls != 0 ? 1 : 0);
            if (kinds > 1) return new Color32(174, 116, 31, 255);
            if (contribution.Multiplier != 0) return new Color32(106, 64, 143, 255);
            if (contribution.Rerolls != 0) return new Color32(38, 111, 137, 255);
            return new Color32(57, 108, 66, 255);
        }

        private static Sprite FindSprite(string id)
        {
            if (icons.TryGetValue(id, out Sprite sprite) && sprite != null) return sprite;
            sprite = Resources.Load<Sprite>("SpiritModifierIcons/" + id);
            if (sprite != null) icons[id] = sprite;
            return sprite;
        }
    }
}
