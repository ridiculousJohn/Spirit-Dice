using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

        /**
         * The charm's description in the wording's description template: its phase's lead in the trigger highlight, its
         * condition with numbers and the wording's highlighted terms in the highlight, and its activation limit's note.
         */
        internal static string GetModifierDescription(ModifierPresentationCatalog charms, ModifierDefinition definition,
            SpiritUiWording.CharmText wording, Color highlight, Color? triggerHighlight = null)
        {
            if (charms == null) throw new ArgumentNullException(nameof(charms));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (wording == null) throw new ArgumentNullException(nameof(wording));
            string color = ColorUtility.ToHtmlStringRGB(highlight);
            var terms = new List<string> { @"\d+" };
            foreach (string term in wording.highlightedTerms) terms.Add(Regex.Escape(term));
            string condition = Regex.Replace(charms.GetConditionText(definition), @"\b(?:" + string.Join("|", terms) + @")\b",
                match => "<b><color=#" + color + ">" + match.Value + "</color></b>", RegexOptions.IgnoreCase);
            string triggerColor = ColorUtility.ToHtmlStringRGB(triggerHighlight ?? highlight);
            string lead = "<b><color=#" + triggerColor + ">" + charms.GetDescriptionLead(definition) + "</color></b>";
            string note = charms.GetDescriptionNote(definition);
            return wording.Description(lead, condition, note.Length > 0 ? "<b>" + note + "</b>" : "");
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
