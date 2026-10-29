using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    /**
     * A table piece: an object any encounter can place on its table. Its identity is its asset GUID, under which the run
     * save keeps the piece's interaction states. Each encounter that places it authors the piece's interactions there.
     */
    [CreateAssetMenu(menuName = "Dice/Table Piece")]
    public sealed class TablePieceAsset : ContentAsset
    {
        public TablePieceDefinition.Id Identity => new TablePieceDefinition.Id(AssetGuid);
    }
}
