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
        /** The number of positioned table pieces in play: the position interactions the snapshot lists. */
        int PositionedPieceCount { get; }
        /** The index of the selected positioned piece among those in play, from 0; meaningful only while one is in play. */
        int SelectedPieceIndex { get; }
        /** The selected positioned piece's interaction as the snapshot shows it, or null when none is in play. */
        PieceInteractionSnapshot SelectedPiece { get; }
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
        /** Selects the next positioned piece in play, after the last the first. */
        void SelectNextPiece();
        /** Moves the selected positioned piece to the position through the generic piece interaction action. */
        void MovePiece(PiecePosition position);
    }
}
