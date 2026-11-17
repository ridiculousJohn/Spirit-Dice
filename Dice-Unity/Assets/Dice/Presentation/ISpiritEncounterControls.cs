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
        /** What the run plays: the catalog's encounter and the items the player carries; null when no run is installed. */
        EncounterInPlay InPlay { get; }
        /** The presentation of the catalog's charms. */
        ModifierPresentationCatalog ModifierPresentation { get; }
        /** The presentation of the items the catalog's encounters lend. */
        ItemPresentationCatalog ItemPresentation { get; }
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
        /** Applies a typed input to a table piece's interaction, by its place in the piece's list; acceptance arrives through Resolved. */
        void InteractWithPiece(TablePieceDefinition.Id pieceId, int interactionIndex, PieceInteractionInput input);
        void RefreshRewards();
        void ChooseReward(long offerId, long optionId);
        void StartNew();
        void Save();
        void Load();
        void Abandon();
        void CompletePresentation();
    }
}
