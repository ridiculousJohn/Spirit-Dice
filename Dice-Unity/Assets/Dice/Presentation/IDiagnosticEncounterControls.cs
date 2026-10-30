using System;
using System.Collections.Generic;
using Dice.Engine;

namespace Dice.Presentation
{
    public interface IDiagnosticEncounterControls
    {
        EncounterSnapshot Snapshot { get; }
        MechanicalCatalog Catalog { get; }
        /** The presentation of the catalog's charms. */
        ModifierPresentationCatalog ModifierPresentation { get; }
        HandPreview Preview { get; }
        IReadOnlyList<ModifierInspection> ModifierInspections { get; }
        IReadOnlyList<string> EventLog { get; }
        int SelectedMask { get; }
        string StatusMessage { get; }
        string SavePath { get; }
        ulong Seed { get; }
        /** Raised after any of the state or status above changes. */
        event Action Changed;

        void StartNew(string seed);
        void ToggleDie(int index);
        void SelectAllDice();
        void ClearDiceSelection();
        void Reroll();
        void PlayHand();
        void RefreshRewards();
        void ChooseReward(long offerId, long optionId);
        void Save();
        void Load();
        void Abandon();
    }
}
