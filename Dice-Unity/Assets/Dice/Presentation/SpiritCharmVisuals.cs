using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Charm icon state and text shared by the gameplay view, its panels, and score playback. */
    internal static class SpiritCharmVisuals
    {
        internal static void SetIcon(Image icon, SpiritModifierSigilGraphic sigil, SpiritModifierCategoryBadge category,
            ModifierPresentationCatalog charms, ModifierDefinition definition)
        {
            icon.sprite = definition == null ? null : charms.GetIcon(definition);
            icon.color = sigil.color = definition == null ? Color.white : charms.GetEffectColor(definition);
            bool visible = icon.sprite != null;
            icon.gameObject.SetActive(visible);
            if (definition != null) sigil.Seed = charms.GetSigilSeed(definition);
            sigil.gameObject.SetActive(definition != null && !visible);
            category.Render(charms, definition);
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

        internal static bool HasPendingProgress(ISpiritEncounterControls controls) =>
            controls is ISpiritProgressionControls progression &&
            progression.ProfileStatusMessage.StartsWith("Progress is pending:", StringComparison.Ordinal);

        internal static string UnlockSummary(ISpiritEncounterControls controls, IReadOnlyList<string> ids)
        {
            if (ids.Count == 0) return "No new charms unlocked this run.";
            var names = new List<string>();
            foreach (string id in ids)
            {
                var definition = controls.Catalog.GetModifier(id);
                names.Add(controls.ModifierPresentation.GetName(definition) + "\n" + AvailabilityScopeText(definition));
            }
            return (HasPendingProgress(controls) ? "UNLOCKED THIS RUN - SAVE PENDING\n" : "PERMANENTLY UNLOCKED THIS RUN\n") +
                "Available in future reward pools.\n\n" + string.Join("\n\n", names);
        }
    }
}
