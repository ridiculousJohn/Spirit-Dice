using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    /**
     * A table piece an encounter or an item places and the piece's interactions there, at most one of each kind; another
     * encounter or item can place the same piece with other interactions.
     */
    [Serializable]
    internal sealed class PieceData
    {
        public TablePieceAsset piece;
        [SerializeReference, SelectImplementation]
        public PieceInteractionConfig[] interactions = Array.Empty<PieceInteractionConfig>();

        /** The piece with its interactions; owner names the encounter or item that places it in failures, such as "encounter X". */
        internal TablePieceDefinition Build(string owner)
        {
            if (piece == null) throw new InvalidOperationException("The " + owner + " has a table piece entry without its piece.");
            string where = "The table piece " + piece.name + " of the " + owner;
            if (interactions == null) throw new InvalidOperationException(where + " has no interaction list.");
            var definitions = new PieceInteractionDefinition[interactions.Length];
            for (int i = 0; i < definitions.Length; ++i)
            {
                var interaction = interactions[i] ?? throw new InvalidOperationException(where + " has a missing interaction.");
                // An interaction config has no name of its own, so the piece and its owner name its failures.
                try { definitions[i] = interaction.Build(); }
                catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
                {
                    throw new InvalidOperationException(where + " has an invalid interaction: " + exception.Message, exception);
                }
            }
            try { return new TablePieceDefinition(piece.Identity, definitions); }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(where + " is invalid: " + exception.Message, exception);
            }
        }
    }
}
