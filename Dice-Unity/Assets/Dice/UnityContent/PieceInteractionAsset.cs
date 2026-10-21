using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    public abstract class PieceInteractionAsset : ScriptableObject
    {
        public abstract PieceInteractionDefinition Build();
    }
}
