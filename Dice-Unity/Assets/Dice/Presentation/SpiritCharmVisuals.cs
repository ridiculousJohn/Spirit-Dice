using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Charm icon construction and text shared by the gameplay view, owned charms panel, and charm popup. */
    internal static class SpiritCharmVisuals
    {
        internal static Image MakeIcon(string name, Transform parent, Vector2 anchor, float size)
        {
            var rect = SpiritUi.Rect(name, parent, anchor, anchor);
            rect.sizeDelta = new Vector2(size, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.gameObject.SetActive(false);
            return image;
        }

        internal static SpiritModifierSigilGraphic MakeSigil(string name, Image icon)
        {
            var rect = SpiritUi.Rect(name, icon.transform.parent, icon.rectTransform.anchorMin, icon.rectTransform.anchorMax);
            rect.pivot = icon.rectTransform.pivot;
            rect.anchoredPosition = icon.rectTransform.anchoredPosition;
            rect.sizeDelta = icon.rectTransform.sizeDelta;
            var sigil = rect.gameObject.AddComponent<SpiritModifierSigilGraphic>();
            sigil.color = SpiritUi.GetTheme(icon.transform).amber;
            sigil.raycastTarget = false;
            sigil.gameObject.SetActive(false);
            return sigil;
        }

        internal static void SetIcon(Image icon, SpiritModifierSigilGraphic sigil, SpiritModifierCategoryBadge category,
            ModifierDefinition definition)
        {
            icon.sprite = definition == null ? null : SpiritModifierIcons.GetSprite(definition);
            icon.color = sigil.color = definition == null ? Color.white : SpiritModifierIcons.GetEffectColor(definition);
            bool visible = icon.sprite != null;
            icon.gameObject.SetActive(visible);
            if (definition != null) sigil.Seed = SpiritModifierIcons.GetSigilSeed(definition);
            sigil.gameObject.SetActive(definition != null && !visible);
            category.Render(definition);
        }

        internal static SpiritCharmStatusPresentation Status(ISpiritEncounterControls controls,
            ModifierInstanceSnapshot instance, ModifierDefinition definition)
        {
            ModifierInspection inspection = null;
            foreach (var candidate in controls.ModifierInspections)
                if (candidate.InstanceId == instance.InstanceId) { inspection = candidate; break; }
            return SpiritCharmStatusPresentation.Create(instance, definition, controls.Snapshot.Phase, inspection);
        }

        internal static string AvailabilityText(ModifierDefinition definition, EncounterSnapshot snapshot)
        {
            var availability = definition.Availability;
            var unlocked = new HashSet<string>(snapshot.UnlockedModifierIds, StringComparer.Ordinal);
            string access = availability.Access == ModifierAccess.PermanentUnlock ?
                (unlocked.Contains(definition.Id) ? "Permanently unlocked" : "Requires permanent unlock") :
                availability.Access == ModifierAccess.EncounterExclusive ? "Encounter exclusive" : "Available from the start";
            return access + " / " + AvailabilityScopeText(definition);
        }

        internal static string AvailabilityScopeText(ModifierDefinition definition)
        {
            var availability = definition.Availability;
            string scope = availability.Scope == ModifierAvailabilityScope.Global ? "All encounters" :
                availability.Scope == ModifierAvailabilityScope.Spirit ? "This spirit's encounters" : "This encounter";
            return scope + " / Tier " + availability.PowerTier;
        }
    }
}
