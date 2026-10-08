using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Localized mesh motion preserves the portrait's planted contact points. */
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpiritPortraitImage : RawImage
    {
        public enum Region { Body, Head }

        private const int Columns = 24;
        private const int Rows = 18;
        private Region deformationRegion;
        private bool tailOnly;
        private float breathing;
        private float tail;
        private float ears;

        public Region DeformationRegion
        {
            get => deformationRegion;
            set
            {
                if (deformationRegion == value) return;
                deformationRegion = value;
                SetVerticesDirty();
            }
        }

        public bool TailOnly
        {
            get => tailOnly;
            set
            {
                if (tailOnly == value) return;
                tailOnly = value;
                SetVerticesDirty();
            }
        }

        public void SetPose(float breathing, float tail, float ears)
        {
            breathing = Mathf.Clamp(breathing, -1f, 1f);
            tail = Mathf.Clamp(tail, -1f, 1f);
            ears = Mathf.Clamp(ears, -1f, 1f);
            if (this.breathing == breathing && this.tail == tail && this.ears == ears) return;
            this.breathing = breathing;
            this.tail = tail;
            this.ears = ears;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Texture portraitTexture = mainTexture;
            if (portraitTexture == null) return;

            Rect area = GetPixelAdjustedRect();
            Rect textureRect = uvRect;
            Vector2 texelSize = portraitTexture.texelSize;
            float uvScaleX = portraitTexture.width * texelSize.x;
            float uvScaleY = portraitTexture.height * texelSize.y;
            Color32 vertexColor = color;

            for (int row = 0; row <= Rows; ++row)
            {
                float normalizedY = (float)row / Rows;
                float artworkY = textureRect.y + textureRect.height * normalizedY;
                for (int column = 0; column <= Columns; ++column)
                {
                    float normalizedX = (float)column / Columns;
                    float artworkX = textureRect.x + textureRect.width * normalizedX;
                    Vector2 displacement = Deformation(artworkX, artworkY, area.width, area.height);
                    Vector3 position = new Vector3(area.x + area.width * normalizedX + displacement.x,
                        area.y + area.height * normalizedY + displacement.y, 0f);
                    Color32 tintedColor = vertexColor;
                    if (tailOnly)
                        tintedColor.a = (byte)Mathf.RoundToInt(vertexColor.a * TailMask(artworkX, artworkY));
                    vertices.AddVert(position, tintedColor,
                        new Vector4(artworkX * uvScaleX, artworkY * uvScaleY, 0f, 0f));
                }
            }

            for (int row = 0; row < Rows; ++row)
            {
                for (int column = 0; column < Columns; ++column)
                {
                    int lowerLeft = row * (Columns + 1) + column;
                    int upperLeft = lowerLeft + Columns + 1;
                    vertices.AddTriangle(lowerLeft, upperLeft, upperLeft + 1);
                    vertices.AddTriangle(upperLeft + 1, lowerLeft + 1, lowerLeft);
                }
            }
        }

        private Vector2 Deformation(float x, float y, float width, float height)
        {
            if (deformationRegion == Region.Head)
            {
                // Ear weights fade to zero above the eyes, leaving the face and chin untouched.
                float heightWeight = Smooth(0.62f, 0.9f, y);
                float left = Smooth(0.12f, 0.22f, x) * (1f - Smooth(0.42f, 0.5f, x)) * heightWeight;
                float right = Smooth(0.57f, 0.67f, x) * (1f - Smooth(0.88f, 0.96f, x)) * heightWeight;
                return new Vector2((right - left) * ears * 2f * width / 200f,
                    (left + right) * ears * 0.7f * height / 200f);
            }

            // The chest and tail weights vanish before the forearms and tail root.
            float chest = Smooth(0.33f, 0.41f, x) * (1f - Smooth(0.55f, 0.63f, x))
                * Smooth(0.32f, 0.46f, y) * (1f - Smooth(0.65f, 0.74f, y));
            float tailWeight = TailMask(x, y) * Smooth(0.72f, 0.97f, x) * Smooth(0.3f, 0.68f, y);
            return new Vector2((breathing * chest * (x - 0.46f) * 2.5f + tail * tailWeight * 8f) * width / 460f,
                (breathing * chest * 0.9f + tail * tailWeight * 3f) * height / (460f * 2f / 3f));
        }

        private static float TailMask(float x, float y)
        {
            // Vertex alpha isolates the tail while the shader retains the texture's alpha.
            return Smooth(0.72f, 0.82f, x) * Smooth(0.3f, 0.45f, y);
        }

        private static float Smooth(float start, float end, float value)
        {
            float progress = Mathf.Clamp01((value - start) / (end - start));
            return progress * progress * (3f - 2f * progress);
        }
    }
}
