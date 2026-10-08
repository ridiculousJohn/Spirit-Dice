using System.Collections.Generic;
using UnityEngine;

namespace Dice.Presentation
{
    /** Transparent ink textures let provisional family sigils share the dice surface shading. */
    internal static class SpiritModifierSigilTexture
    {
        internal static Texture2D Create(uint seed)
        {
            const int size = 128;
            var lines = new List<Vector4>();
            int kind = (int)(seed % 12);
            const float radius = .30f;
            if (kind < 4)
            {
                int sides = kind + 3;
                for (int i = 0; i < sides; ++i) Line(lines, Point(i, sides) * radius, Point(i + 1, sides) * radius);
                if ((seed & 16) != 0) Diamond(lines, Vector2.zero, radius * .22f);
                else Line(lines, Vector2.down * radius * .42f, Vector2.up * radius * .42f);
            }
            else if (kind < 8)
            {
                int points = kind + 1;
                for (int i = 0; i < points; ++i)
                {
                    Vector2 tip = Point(i, points) * radius;
                    Line(lines, Vector2.zero, tip);
                    Diamond(lines, tip, radius * .13f);
                }
                Diamond(lines, Vector2.zero, radius * .18f);
            }
            else
            {
                Line(lines, Vector2.down * radius, Vector2.up * radius);
                for (int i = 0; i < kind - 6; ++i)
                {
                    float height = Mathf.Lerp(-.65f, .65f, (float)i / (kind - 7));
                    Vector2 start = Vector2.up * height * radius;
                    Line(lines, start, start + new Vector2((i & 1) == 0 ? -.65f : .65f, .32f) * radius);
                }
            }
            for (int i = 0; i < 4; ++i)
                if ((seed & (1u << (i + 7))) != 0) Diamond(lines, Point(i, 4) * radius * 1.3f, radius * .10f);

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; ++y)
                for (int x = 0; x < size; ++x)
                {
                    Vector2 sample = new Vector2((x + .5f) / size - .5f, (y + .5f) / size - .5f);
                    float distance = 1;
                    foreach (Vector4 line in lines)
                    {
                        Vector2 start = new Vector2(line.x, line.y);
                        Vector2 offset = new Vector2(line.z, line.w) - start;
                        float progress = Mathf.Clamp01(Vector2.Dot(sample - start, offset) / offset.sqrMagnitude);
                        distance = Mathf.Min(distance, Vector2.Distance(sample, start + offset * progress));
                    }
                    byte alpha = (byte)Mathf.RoundToInt(255 * (1 - Mathf.SmoothStep(0, 1,
                        Mathf.InverseLerp(.009f, .017f, distance))));
                    pixels[y * size + x] = new Color32(0, 0, 0, alpha);
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Spirit Modifier Family Sigil " + seed,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Vector2 Point(int index, int count)
        {
            float angle = Mathf.PI * .5f + index * Mathf.PI * 2 / count;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static void Diamond(List<Vector4> lines, Vector2 center, float size)
        {
            Line(lines, center + Vector2.left * size, center + Vector2.up * size);
            Line(lines, center + Vector2.up * size, center + Vector2.right * size);
            Line(lines, center + Vector2.right * size, center + Vector2.down * size);
            Line(lines, center + Vector2.down * size, center + Vector2.left * size);
        }

        private static void Line(List<Vector4> lines, Vector2 start, Vector2 end)
            => lines.Add(new Vector4(start.x, start.y, end.x, end.y));
    }
}
