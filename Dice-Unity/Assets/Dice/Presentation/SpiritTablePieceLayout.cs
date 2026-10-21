using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dice.Presentation
{
    /** Pure placement math shared by table piece props and the lights that follow them. */
    public static class SpiritTablePieceLayout
    {
        /** Returns the closest candidate within the radius (the earlier one on a tie), or -1 when the drop is too far from every candidate. */
        public static int FindNearest(Vector2 point, IReadOnlyList<Vector2> candidates, float radius)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (float.IsNaN(radius) || radius < 0f) throw new ArgumentOutOfRangeException(nameof(radius));
            int nearest = -1;
            float best = radius * radius;
            for (int i = 0; i < candidates.Count; ++i)
            {
                float distance = (candidates[i] - point).sqrMagnitude;
                if (distance > best || distance == best && nearest >= 0) continue;
                best = distance;
                nearest = i;
            }
            return nearest;
        }

        /** A point in a frame's local space at a normalized position inside a viewport rect of that frame. */
        public static Vector2 ViewportPoint(Rect frame, Rect viewport, Vector2 placement)
        {
            Vector2 normalized = viewport.min + Vector2.Scale(viewport.size, placement);
            return frame.min + Vector2.Scale(frame.size, normalized);
        }

        /** Maps an image-local point to the texture coordinates the image displays through its uvRect. */
        public static Vector2 ImageToTexture(Vector2 localPoint, Rect rect, Rect uvRect)
        {
            if (Mathf.Abs(rect.width) < .0001f || Mathf.Abs(rect.height) < .0001f) return uvRect.center;
            Vector2 normalized = new Vector2((localPoint.x - rect.xMin) / rect.width, (localPoint.y - rect.yMin) / rect.height);
            return uvRect.min + Vector2.Scale(uvRect.size, normalized);
        }

        /** Zero inside the rect; otherwise the distance to its nearest edge. */
        public static float DistanceOutside(Vector2 localPoint, Rect rect)
        {
            float x = Mathf.Max(Mathf.Max(rect.xMin - localPoint.x, localPoint.x - rect.xMax), 0f);
            float y = Mathf.Max(Mathf.Max(rect.yMin - localPoint.y, localPoint.y - rect.yMax), 0f);
            return Mathf.Sqrt(x * x + y * y);
        }

        /** Split backgrounds show slices of one texture; the image containing (or nearest) the point decides its coordinates. */
        public static Vector2 TextureCoordinate(Vector2 frontLocal, Rect frontRect, Rect frontUv,
            Vector2 backLocal, Rect backRect, Rect backUv)
        {
            // The front image is drawn over the back image, so it wins wherever both contain the point.
            return DistanceOutside(frontLocal, frontRect) <= DistanceOutside(backLocal, backRect)
                ? ImageToTexture(frontLocal, frontRect, frontUv)
                : ImageToTexture(backLocal, backRect, backUv);
        }
    }
}
