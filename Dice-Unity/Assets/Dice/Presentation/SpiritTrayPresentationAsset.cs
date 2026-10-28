using System;
using UnityEngine;

namespace Dice.Presentation
{
    /** The encounter's authored tray rig and the size of the texture it renders into. */
    [CreateAssetMenu(menuName = "Dice/Presentation/Spirit Tray")]
    public sealed class SpiritTrayPresentationAsset : ScriptableObject
    {
        public SpiritTrayScene RigPrefab;
        public Vector2Int TextureSize = new Vector2Int(1024, 512);

        public void ValidateScene()
        {
            if (TextureSize.x <= 0 || TextureSize.y <= 0)
                throw new InvalidOperationException("Spirit tray render textures require a positive size.");
        }
    }
}
