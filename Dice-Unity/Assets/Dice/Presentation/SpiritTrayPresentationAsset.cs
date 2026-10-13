using System;
using UnityEngine;

namespace Dice.Presentation
{
    /** Shared surface references keep encounter dice and modifier history trays visually consistent. */
    [CreateAssetMenu(menuName = "Dice/Presentation/Spirit Tray")]
    public sealed class SpiritTrayPresentationAsset : ScriptableObject
    {
        public Shader DiceShader;
        public Texture2D BoneTexture;
        public Texture2D WoodTexture;

        public void Validate()
        {
            if (DiceShader == null) throw new InvalidOperationException("Spirit trays require a dice surface shader.");
            if (BoneTexture == null) throw new InvalidOperationException("Spirit trays require a bone surface texture.");
            if (WoodTexture == null) throw new InvalidOperationException("Spirit trays require a wood surface texture.");
        }
    }
}
