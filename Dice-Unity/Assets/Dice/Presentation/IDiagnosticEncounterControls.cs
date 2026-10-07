using System.Collections.Generic;
using Dice.Engine;

namespace Dice.Presentation
{
    public interface IDiagnosticEncounterControls
    {
        EncounterSnapshot Snapshot { get; }
        MechanicalCatalog Catalog { get; }
        HandPreview Preview { get; }
        IReadOnlyList<ModifierInspection> ModifierInspections { get; }
        IReadOnlyList<string> EventLog { get; }
        int SelectedMask { get; }
        string StatusMessage { get; }
        string SavePath { get; }
        string SeedText { get; }

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
