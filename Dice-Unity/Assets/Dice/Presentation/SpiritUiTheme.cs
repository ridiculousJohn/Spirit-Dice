using System;
using UnityEngine;

namespace Dice.Presentation
{
    /** Replaceable artwork and typography for the encounter's live controls. */
    [CreateAssetMenu(menuName = "Dice/Spirit UI Theme", fileName = "SpiritHouseTheme")]
    public sealed class SpiritUiTheme : ScriptableObject
    {
        [Serializable]
        public sealed class Frame
        {
            public Texture2D texture;
            public Sprite sprite;
            public Material surfaceMaterial;
            public Vector2 referenceTextureSize;
            public Rect spriteRect;
            public Vector4 border = new Vector4(120, 60, 120, 60);
            public float pixelsPerUnitMultiplier = 3;
            [NonSerialized] private Sprite runtimeSprite;

            internal Sprite GetSprite()
            {
                if (sprite != null) return sprite;
                if (runtimeSprite != null) return runtimeSprite;
                var source = texture;
                if (source == null) return null;
                float scaleX = referenceTextureSize.x > 0 ? source.width / referenceTextureSize.x : 1;
                float scaleY = referenceTextureSize.y > 0 ? source.height / referenceTextureSize.y : 1;
                var area = spriteRect.width > 0 && spriteRect.height > 0 ? spriteRect :
                    new Rect(0, 0, source.width, source.height);
                if (spriteRect.width > 0 && spriteRect.height > 0)
                    area = new Rect(area.x * scaleX, area.y * scaleY, area.width * scaleX, area.height * scaleY);
                var edges = new Vector4(Mathf.Min(border.x * scaleX, area.width * .5f),
                    Mathf.Min(border.y * scaleY, area.height * .5f), Mathf.Min(border.z * scaleX, area.width * .5f),
                    Mathf.Min(border.w * scaleY, area.height * .5f));
                runtimeSprite = Sprite.Create(source, area, new Vector2(.5f, .5f), 100 * scaleY, 0,
                    SpriteMeshType.FullRect, edges);
                runtimeSprite.name = source.name + " Frame";
                runtimeSprite.hideFlags = HideFlags.DontSave;
                return runtimeSprite;
            }

            internal void Release()
            {
                if (runtimeSprite != null)
                {
                    if (Application.isPlaying) Destroy(runtimeSprite);
                    else DestroyImmediate(runtimeSprite);
                }
                runtimeSprite = null;
            }
        }

        public Font headingFont;
        public Font bodyFont;
        public Frame greenFrame = new Frame();
        public Frame amberFrame = new Frame();
        public Frame darkFrame = new Frame();
        public Frame woodFrame = new Frame();
        public Color cream = new Color32(246, 231, 190, 255);
        public Color muted = new Color32(191, 187, 162, 255);
        public Color amber = new Color32(210, 166, 88, 255);
        public Color panelColor = new Color32(25, 36, 29, 255);
        public Color highlightedTint = new Color(1.10f, 1.08f, 1.01f, 1);
        public Color pressedTint = new Color(.76f, .71f, .58f, 1);
        public Color disabledTint = new Color(.46f, .47f, .43f, .88f);

        private void OnDisable()
        {
            greenFrame?.Release();
            amberFrame?.Release();
            darkFrame?.Release();
            woodFrame?.Release();
        }
    }
}
