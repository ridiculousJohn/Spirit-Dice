using UnityEngine;

namespace Dice.Presentation
{
    /**
     * Authored position behaviour of a table piece's prop, owned by the position interaction's presenter: the prop rests at
     * the engine's position, on its neutral spot or above a dice bay at its authored bay placement, and a drag released near
     * an allowed position proposes a move there (SpiritPositionDrag). The engine owns the position; this component only
     * proposes moves.
     */
    [RequireComponent(typeof(SpiritTablePieceView))]
    public sealed class SpiritTablePiecePosition : SpiritPositionDrag
    {
        [Tooltip("Pivot rest point on a bay, normalized within that die's tray slot rect (0,0 is bottom-left); values outside 0-1 rest beside the die.")]
        [SerializeField] private Vector2 bayPlacement = new Vector2(.5f, 0f);

        public Vector2 BayPlacement => bayPlacement;
    }
}
