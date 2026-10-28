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
        /** The presentation of the catalog's charms. */
        ModifierPresentationCatalog ModifierPresentation { get; }
        HandPreview Preview { get; }
        IReadOnlyList<ModifierInspection> ModifierInspections { get; }
        int SelectedMask { get; }
        bool IsBusy { get; }
        SpiritStatus Status { get; }

        event Action StateChanged;
        event Action<Resolution> Resolved;

        void ToggleDie(int index);
        void Reroll();
        void PlayHand();
        /** Applies a typed input to a named table piece interaction; acceptance arrives through Resolved. */
        void InteractWithPiece(string pieceId, string interactionId, PieceInteractionInput input);
        void RefreshRewards();
        void ChooseReward(long offerId, long optionId);
        void StartNew();
        void Save();
        void Load();
        void Abandon();
        void CompletePresentation();
    }
}
