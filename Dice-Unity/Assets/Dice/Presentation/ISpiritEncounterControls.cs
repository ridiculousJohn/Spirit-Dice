using System;
using System.Collections.Generic;
using Dice.Engine;
using Resolution = Dice.Engine.Resolution;

namespace Dice.Presentation
{
    public interface ISpiritEncounterControls
    {
        EncounterSnapshot Snapshot { get; }
        MechanicalCatalog Catalog { get; }
        HandPreview Preview { get; }
        IReadOnlyList<ModifierInspection> ModifierInspections { get; }
        int SelectedMask { get; }
        bool IsBusy { get; }
        string StatusMessage { get; }

        event Action StateChanged;
        event Action<Resolution> Resolved;

        void ToggleDie(int index);
        void Reroll();
        void PlayHand();
        void RefreshRewards();
        void ChooseReward(long offerId, long optionId);
        void StartNew();
        void Save();
        void Load();
        void Abandon();
        void CompletePresentation();
    }
}
