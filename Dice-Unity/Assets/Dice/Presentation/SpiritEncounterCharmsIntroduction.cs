using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * Introduces an encounter's own charm families inside the encounter, on the encounter screen's instance of the
     * encounter-charms screen. Encounters that first offer their additions after a later stage clear show it while the
     * first charm offer that can contain them is pending; additions offered from the first clear are introduced after
     * Accept instead (EncounterJourneyView). Continue dismisses it for that offer's target stage, including refreshed
     * offers, until the run is shown again without being presented: a loaded or resumed run, a rebound or reloaded
     * presentation, or a return to the table from the journey. Presentation only; nothing is saved.
     */
    [DisallowMultipleComponent]
    public sealed class SpiritEncounterCharmsIntroduction : MonoBehaviour
    {
        [SerializeField] private SpiritEncounterCharmsScreen screen;
        // Target stage of the offer whose introduction was dismissed; -1 when none was.
        private int dismissedTargetStageIndex = -1;
        private int shownTargetStageIndex = -1;

        public SpiritEncounterCharmsScreen Screen => screen;
        public bool IsOpen => screen != null && screen.IsOpen;

        public void Build()
        {
            if (screen == null) throw new InvalidOperationException("The charms introduction requires its authored screen.");
            screen.Build(Dismiss);
            screen.Hide();
        }

        /**
         * True while the snapshot's pending charm offer is the first that can contain the encounter's own charms and the
         * encounter first offers them after a stage clear later than the first.
         */
        public static bool IntroducesOffer(EncounterDefinition encounter, EncounterSnapshot snapshot)
        {
            if (encounter == null || snapshot == null) return false;
            return !encounter.EncounterModifiersAvailableFromStart && snapshot.Phase == EncounterPhase.ChoosingModifier &&
                snapshot.PendingReward != null && snapshot.PendingReward.IntroducesEncounterModifiers;
        }

        /** Shows the introduction over the pending offer when it introduces it and was not dismissed; hides it otherwise. */
        public void Refresh(MechanicalCatalog catalog, ModifierPresentationCatalog charms, EncounterSnapshot snapshot,
            EncounterJourneyAsset journey)
        {
            var encounter = catalog != null && snapshot != null ? catalog.GetEncounter(snapshot.EncounterId) : null;
            if (!IntroducesOffer(encounter, snapshot) || snapshot.PendingReward.TargetStageIndex == dismissedTargetStageIndex)
            {
                screen.Hide();
                return;
            }
            if (screen.IsOpen) return;
            shownTargetStageIndex = snapshot.PendingReward.TargetStageIndex;
            string narration = journey != null && !journey.IsStoryOnly &&
                string.Equals(journey.EncounterId, encounter.Id, StringComparison.Ordinal)
                ? journey.EncounterCharmsIntroductionText : null;
            screen.Show(catalog, charms, encounter.EncounterModifierIds, narration);
        }

        /** Hides the introduction and forgets its dismissal, so the next refresh shows it again while its offer is pending. */
        public void ResetDismissal()
        {
            dismissedTargetStageIndex = shownTargetStageIndex = -1;
            if (screen != null) screen.Hide();
        }

        public void Hide()
        {
            if (screen != null) screen.Hide();
        }

        private void Dismiss()
        {
            dismissedTargetStageIndex = shownTargetStageIndex;
            screen.Hide();
        }
    }
}
