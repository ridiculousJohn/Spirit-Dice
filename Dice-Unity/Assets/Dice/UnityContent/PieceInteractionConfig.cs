using System;
using Dice.Engine;

namespace Dice.UnityContent
{
    /**
     * A table piece's authored interaction in the encounter that places it. Each interaction kind is its own config class
     * with its own settings, and builds the engine interaction it describes.
     */
    [Serializable]
    public abstract class PieceInteractionConfig
    {
        /** The engine interaction with this config's settings; settings outside their range fail the engine's validation. */
        public abstract PieceInteractionDefinition Build();
    }
}
