using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Charm icon state and text shared by the gameplay view, its panels, and score playback. */
    internal static class SpiritCharmVisuals
    {
        internal static void SetIcon(Image icon, SpiritModifierCategoryBadge category, ModifierPresentationCatalog charms,
            ModifierDefinition definition)
        {
            icon.sprite = definition == null ? null : charms.GetIcon(definition);
            icon.color = definition == null ? Color.white : charms.GetEffectColor(definition);
            icon.gameObject.SetActive(icon.sprite != null);
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

        internal static string AvailabilityText(ModifierDefinition definition, EncounterSnapshot snapshot,
            SpiritUiWording.CharmAvailability wording)
        {
            var availability = definition.Availability;
            string access = availability.Access == ModifierAccess.PermanentUnlock ?
                (snapshot.UnlockedModifierIds.Contains(definition.Identity) ? wording.permanentlyUnlocked : wording.requiresUnlock) :
                availability.Access == ModifierAccess.EncounterExclusive ? wording.encounterExclusive : wording.availableFromStart;
            return wording.Availability(access, AvailabilityScopeText(definition, wording));
        }

        internal static string AvailabilityScopeText(ModifierDefinition definition, SpiritUiWording.CharmAvailability wording)
        {
            var availability = definition.Availability;
            string scope = availability.Access == ModifierAccess.EncounterExclusive ? wording.thisEncounter : wording.allEncounters;
            return wording.ScopeTier(scope, availability.PowerTier);
        }

        internal static bool HasPendingProgress(ISpiritEncounterControls controls) =>
            controls is ISpiritProgressionControls progression && progression.ProfileStatus.IsPending;

        internal static string UnlockSummary(ISpiritEncounterControls controls, IReadOnlyList<ModifierDefinition.Id> ids, SpiritUiWording wording)
        {
            if (ids.Count == 0) return wording.ending.noUnlocks;
            var names = new List<string>();
            foreach (var id in ids)
            {
                var definition = controls.Catalog.GetModifier(id);
                names.Add(wording.ending.UnlockedCharm(controls.ModifierPresentation.GetName(definition),
                    AvailabilityScopeText(definition, wording.charmAvailability)));
            }
            return wording.ending.Unlocks(names, HasPendingProgress(controls));
        }
    }
}
