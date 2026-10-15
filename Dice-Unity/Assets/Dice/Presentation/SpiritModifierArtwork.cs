using System.Collections.Generic;
using UnityEngine;

namespace Dice.Presentation
{
    /** Authored line drawings rasterized as white ink for UI tint and dice-surface alpha. */
    internal static class SpiritModifierArtwork
    {
        private static readonly Dictionary<ModifierArtworkKind, Sprite> sprites = new Dictionary<ModifierArtworkKind, Sprite>();
        private static readonly Dictionary<ModifierCategorySymbol, Sprite> categorySprites = new Dictionary<ModifierCategorySymbol, Sprite>();

        static SpiritModifierArtwork()
        {
            Application.quitting += Release;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
            UnityEditor.EditorApplication.quitting += Release;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            Release();
            Application.quitting -= Release;
            Application.quitting += Release;
        }

        internal static Sprite GetSprite(string id)
        {
            if (!SpiritModifierPresentation.TryGetArtwork(id, out ModifierArtworkKind kind)) return null;
            if (sprites.TryGetValue(kind, out Sprite sprite) && sprite != null) return sprite;
            var drawing = new Drawing();
            switch (kind)
            {
                case ModifierArtworkKind.WelcomingPair: WelcomingPair(drawing); break;
                case ModifierArtworkKind.SecondChance: SecondChance(drawing); break;
                case ModifierArtworkKind.LastLantern: LastLantern(drawing); break;
                case ModifierArtworkKind.QuietGrove: QuietGrove(drawing); break;
                case ModifierArtworkKind.OneSpark: OneSpark(drawing); break;
                case ModifierArtworkKind.LoneWatcher: LoneWatcher(drawing); break;
                case ModifierArtworkKind.TwinKnots: TwinKnots(drawing); break;
            }
            Texture2D texture = drawing.CreateTexture(kind.ToString());
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 128,
                0, SpriteMeshType.FullRect);
            sprite.name = "Spirit Charm " + kind;
            sprite.hideFlags = HideFlags.DontSave;
            sprites[kind] = sprite;
            return sprite;
        }

        internal static Sprite GetCategorySprite(ModifierCategorySymbol symbol)
        {
            if (symbol == ModifierCategorySymbol.None) return null;
            if (categorySprites.TryGetValue(symbol, out Sprite sprite) && sprite != null) return sprite;
            var drawing = new Drawing();
            switch (symbol)
            {
                case ModifierCategorySymbol.Base:
                    drawing.Line(14, 32, 50, 32);
                    drawing.Line(32, 14, 32, 50);
                    break;
                case ModifierCategorySymbol.Multiplier:
                    drawing.Line(18, 18, 46, 46);
                    drawing.Line(18, 46, 46, 18);
                    break;
                case ModifierCategorySymbol.Combined:
                    drawing.Line(7, 32, 27, 32);
                    drawing.Line(17, 22, 17, 42);
                    drawing.Line(36, 22, 56, 42);
                    drawing.Line(36, 42, 56, 22);
                    break;
                case ModifierCategorySymbol.Rerolls:
                    drawing.Arc(32, 32, 21, -135, 155);
                    drawing.Line(12.97f, 40.87f, 13, 52);
                    drawing.Line(12.97f, 40.87f, 24, 45);
                    break;
                default: return null;
            }
            Texture2D texture = drawing.CreateTexture("Category " + symbol);
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 128,
                0, SpriteMeshType.FullRect);
            sprite.name = "Spirit Charm Category " + symbol;
            sprite.hideFlags = HideFlags.DontSave;
            categorySprites[symbol] = sprite;
            return sprite;
        }

        private static void WelcomingPair(Drawing drawing)
        {
            drawing.Box(8, 8, 21, 21);
            drawing.Box(35, 8, 21, 21);
            drawing.Dot(14, 14, 2.4f);
            drawing.Dot(23, 23, 2.4f);
            drawing.Dot(41, 14, 2.4f);
            drawing.Dot(50, 23, 2.4f);
            drawing.Arc(32, 35, 10, 0, 180);
            drawing.Line(18, 35, 46, 35);
            drawing.Line(32, 51, 32, 57);
            drawing.Line(20, 46, 16, 51);
            drawing.Line(44, 46, 48, 51);
        }

        private static void SecondChance(Drawing drawing)
        {
            drawing.Arc(32, 32, 24, -135, 155);
            drawing.Line(10.25f, 42.14f, 11, 54);
            drawing.Line(10.25f, 42.14f, 22, 46);
            drawing.Box(21, 21, 22, 22);
            drawing.Dot(32, 32, 3);
        }

        private static void LastLantern(Drawing drawing)
        {
            drawing.Arc(32, 48, 8, 0, 180);
            drawing.Path(19, 41, 24, 48, 40, 48, 45, 41, 19, 41);
            drawing.Path(21, 39, 24, 14, 40, 14, 43, 39);
            drawing.Line(19, 39, 45, 39);
            drawing.Path(20, 14, 17, 9, 47, 9, 44, 14);
            drawing.Curve(32, 19, 21, 22, 31, 28, 32, 32);
            drawing.Curve(32, 32, 39, 25, 39, 21, 32, 19);
            drawing.Line(32, 14, 32, 18);
        }

        private static void QuietGrove(Drawing drawing)
        {
            drawing.Path(15, 14, 10, 14, 10, 50, 15, 50);
            drawing.Path(49, 14, 54, 14, 54, 50, 49, 50);
            drawing.Curve(22, 17, 16, 33, 24, 47, 43, 49);
            drawing.Curve(43, 49, 49, 32, 39, 17, 22, 17);
            drawing.Curve(20, 12, 25, 22, 30, 33, 39, 43);
            drawing.Line(29, 29, 39, 31);
        }

        private static void OneSpark(Drawing drawing)
        {
            drawing.Path(32, 58, 39, 41, 58, 32, 41, 25, 32, 6, 25, 23, 6, 32, 23, 39, 32, 58);
            drawing.Dot(32, 32, 4.5f);
        }

        private static void LoneWatcher(Drawing drawing)
        {
            drawing.Curve(6, 32, 19, 50, 45, 50, 58, 32);
            drawing.Curve(58, 32, 45, 14, 19, 14, 6, 32);
            drawing.Box(23, 23, 18, 18);
            drawing.Dot(32, 32, 2.8f);
            drawing.Path(13, 32, 17, 36, 21, 32, 17, 28, 13, 32);
            drawing.Path(43, 32, 47, 36, 51, 32, 47, 28, 43, 32);
        }

        private static void TwinKnots(Drawing drawing)
        {
            // Gaps at the crossings separate the over and under strands of both loops.
            drawing.Curve(28, 41, 14, 59, 2, 38, 16, 25);
            drawing.Curve(16, 25, 29, 10, 45, 28, 32, 39);
            drawing.Curve(36, 23, 50, 5, 62, 26, 48, 39);
            drawing.Curve(48, 39, 35, 54, 19, 36, 32, 25);
            drawing.Line(23, 47, 39, 19);
            drawing.Line(25, 45, 29, 38);
            drawing.Line(35, 27, 41, 17);
            drawing.Path(16, 25, 10, 14, 16, 16);
            drawing.Path(48, 39, 54, 50, 48, 48);
        }

        private static void Release()
        {
            foreach (Sprite sprite in sprites.Values)
            {
                if (sprite == null) continue;
                Texture2D texture = sprite.texture;
                DestroyResource(sprite);
                DestroyResource(texture);
            }
            sprites.Clear();
            foreach (Sprite sprite in categorySprites.Values)
            {
                if (sprite == null) continue;
                Texture2D texture = sprite.texture;
                DestroyResource(sprite);
                DestroyResource(texture);
            }
            categorySprites.Clear();
        }

        private static void DestroyResource(Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Object.Destroy(resource);
            else Object.DestroyImmediate(resource);
        }

        private sealed class Drawing
        {
            private readonly List<Stroke> strokes = new List<Stroke>();

            private readonly struct Stroke
            {
                internal readonly Vector2 Start;
                internal readonly Vector2 End;
                internal readonly float Radius;

                internal Stroke(Vector2 start, Vector2 end, float radius)
                {
                    Start = start;
                    End = end;
                    Radius = radius;
                }
            }

            internal void Line(float x1, float y1, float x2, float y2)
                => strokes.Add(new Stroke(new Vector2(x1, y1), new Vector2(x2, y2), 1.6f));

            internal void Dot(float x, float y, float radius)
                => strokes.Add(new Stroke(new Vector2(x, y), new Vector2(x, y), radius));

            internal void Path(params float[] points)
            {
                for (int i = 2; i < points.Length; i += 2)
                    Line(points[i - 2], points[i - 1], points[i], points[i + 1]);
            }

            internal void Box(float x, float y, float width, float height)
                => Path(x, y, x + width, y, x + width, y + height, x, y + height, x, y);

            internal void Arc(float x, float y, float radius, float startDegrees, float endDegrees)
            {
                int segments = Mathf.CeilToInt(Mathf.Abs(endDegrees - startDegrees) / 10);
                Vector2 previous = Vector2.zero;
                for (int i = 0; i <= segments; ++i)
                {
                    float angle = Mathf.Lerp(startDegrees, endDegrees, (float)i / segments) * Mathf.Deg2Rad;
                    var point = new Vector2(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
                    if (i > 0) Line(previous.x, previous.y, point.x, point.y);
                    previous = point;
                }
            }

            internal void Curve(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
            {
                Vector2 previous = new Vector2(x1, y1);
                for (int i = 1; i <= 18; ++i)
                {
                    float t = i / 18f;
                    float u = 1 - t;
                    Vector2 point = u * u * u * new Vector2(x1, y1) + 3 * u * u * t * new Vector2(x2, y2) +
                        3 * u * t * t * new Vector2(x3, y3) + t * t * t * new Vector2(x4, y4);
                    Line(previous.x, previous.y, point.x, point.y);
                    previous = point;
                }
            }

            internal Texture2D CreateTexture(string name)
            {
                const int size = 128;
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; ++y)
                    for (int x = 0; x < size; ++x)
                    {
                        Vector2 point = new Vector2((x + .5f) * 64 / size, (y + .5f) * 64 / size);
                        float distance = float.MaxValue;
                        foreach (Stroke stroke in strokes)
                        {
                            Vector2 offset = stroke.End - stroke.Start;
                            float progress = offset.sqrMagnitude > 0
                                ? Mathf.Clamp01(Vector2.Dot(point - stroke.Start, offset) / offset.sqrMagnitude) : 0;
                            distance = Mathf.Min(distance, Vector2.Distance(point, stroke.Start + offset * progress) - stroke.Radius);
                        }
                        byte alpha = (byte)Mathf.RoundToInt(255 * (1 - Mathf.SmoothStep(0, 1,
                            Mathf.InverseLerp(-.35f, .35f, distance))));
                        pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                    }
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
                {
                    name = "Spirit Charm Ink " + name,
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };
                texture.SetPixels32(pixels);
                texture.Apply(true, true);
                return texture;
            }
        }
    }
}
