using System;
using System.Linq;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    /** Lets the player move the piece between the neutral position and the dice bays it allows. */
    [Serializable]
    public sealed class PositionInteractionConfig : PieceInteractionConfig
    {
        [Tooltip("Starting dice bay index, or -1 for the neutral position.")]
        [SerializeField, Range(-1, DiceValues.Count - 1)] private int startingBay = -1;
        [SerializeField] private PieceMovementTiming timing = PieceMovementTiming.DuringHand;
        [SerializeField] private bool allowsNeutral = true;
        [SerializeField] private bool[] allowedBays = Enumerable.Repeat(true, DiceValues.Count).ToArray();

        public override PieceInteractionDefinition Build()
        {
            if (allowedBays == null || allowedBays.Length != DiceValues.Count)
                throw new InvalidOperationException("Position interactions require one entry per dice bay.");
            int mask = 0;
            for (int i = 0; i < allowedBays.Length; ++i)
                if (allowedBays[i]) mask |= 1 << i;
            var start = startingBay < 0 ? PiecePosition.Neutral : PiecePosition.Bay(startingBay);
            return new PositionInteractionDefinition(start, timing, allowsNeutral, mask);
        }
    }
}
