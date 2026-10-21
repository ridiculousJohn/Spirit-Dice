using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Piece Interactions/Position")]
    public sealed class PositionInteractionAsset : PieceInteractionAsset
    {
        [SerializeField] private string interactionId = "position";
        [Tooltip("Starting dice bay index, or -1 for the neutral position.")]
        [SerializeField, Range(-1, DiceValues.Count - 1)] private int startingBay = -1;
        [SerializeField] private PieceMovementTiming timing = PieceMovementTiming.DuringHand;
        [SerializeField] private bool allowsNeutral = true;
        [SerializeField] private bool[] allowedBays = { true, true, true, true, true };

        public override PieceInteractionDefinition Build()
        {
            if (allowedBays == null || allowedBays.Length != DiceValues.Count)
                throw new InvalidOperationException("Position interactions require one entry per dice bay.");
            int mask = 0;
            for (int i = 0; i < allowedBays.Length; ++i)
                if (allowedBays[i]) mask |= 1 << i;
            var start = startingBay < 0 ? PiecePosition.Neutral : PiecePosition.Bay(startingBay);
            return new PositionInteractionDefinition(interactionId, start, timing, allowsNeutral, mask);
        }
    }
}
