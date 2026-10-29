using System;
using Dice.Engine;
using Dice.UnityContent;
using UnityEngine;

namespace Dice.Presentation
{
    /** Narration, optionally presenting a charm that its text names with {charm}. */
    [Serializable]
    public sealed class DialogueBeat : EncounterJourneyBeat
    {
        [Tooltip("The charm shown with the narration; presentation only, it grants nothing.")]
        [SerializeField] private ModifierAsset charm;

        public ModifierAsset Charm => charm;

        internal override void Show(IJourneyStage stage, EncounterJourneyAsset journey, int index)
        {
            if (charm != null) stage.ShowCharm(charm);
        }

        internal override void Validate(EncounterJourneyAsset journey, int index, MechanicalCatalog catalog)
        {
            base.Validate(journey, index, catalog);
            if (charm != null) catalog.GetModifier(charm.Identity);
        }
    }
}
