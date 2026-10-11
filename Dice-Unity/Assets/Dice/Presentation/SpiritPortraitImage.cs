using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Authored grid shapes preserve contact points while deforming independent portrait layers. */
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpiritPortraitImage : RawImage
    {
        private SpiritPortraitMeshAsset mesh;
        private float[] weights = Array.Empty<float>();
        private IReadOnlyList<float> readOnlyWeights = Array.Empty<float>();
        public IReadOnlyList<float> Weights => readOnlyWeights;

        public void Configure(SpiritPortraitMeshAsset asset)
        {
            if (asset != null) asset.Validate();
            mesh = asset;
            weights = new float[mesh != null ? mesh.Shapes.Length : 0];
            readOnlyWeights = Array.AsReadOnly(weights);
            SetVerticesDirty();
        }

        public void SetWeights(float[] values)
        {
            if (values == null || values.Length != weights.Length) throw new ArgumentException("Weights must match the configured mesh shapes.", nameof(values));
            bool changed = false;
            for (int i = 0; i < values.Length; ++i)
            {
                if (!SpiritHostPresentationAsset.Finite(values[i])) throw new ArgumentException("Weights must be finite.", nameof(values));
                changed |= weights[i] != values[i];
            }
            if (!changed) return;
            Array.Copy(values, weights, weights.Length);
            SetVerticesDirty();
        }

        public Vector2 EvaluateDisplacement(Vector2 uv, Vector2 size)
        {
            if (mesh == null) return Vector2.zero;
            float x = Mathf.Clamp01(uv.x) * mesh.Columns;
            float y = Mathf.Clamp01(uv.y) * mesh.Rows;
            int column = Mathf.Min((int)x, mesh.Columns - 1);
            int row = Mathf.Min((int)y, mesh.Rows - 1);
            int first = row * (mesh.Columns + 1) + column;
            float along = x - column;
            float up = y - row;
            Vector2 result = Vector2.zero;
            for (int i = 0; i < weights.Length; ++i)
            {
                if (weights[i] == 0f) continue;
                Vector2[] shape = mesh.Shapes[i].Offsets;
                result += Vector2.LerpUnclamped(Vector2.LerpUnclamped(shape[first], shape[first + 1], along),
                    Vector2.LerpUnclamped(shape[first + mesh.Columns + 1], shape[first + mesh.Columns + 2], along), up) * weights[i];
            }
            return Vector2.Scale(result, size);
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (texture == null) return;
            Rect area = GetPixelAdjustedRect();
            int columns = mesh != null ? mesh.Columns : 1;
            int rows = mesh != null ? mesh.Rows : 1;
            for (int row = 0; row <= rows; ++row)
            {
                for (int column = 0; column <= columns; ++column)
                {
                    int index = row * (columns + 1) + column;
                    Vector2 uv = new Vector2((float)column / columns, (float)row / rows);
                    Vector2 displacement = Vector2.zero;
                    if (mesh != null)
                        for (int i = 0; i < weights.Length; ++i) displacement += mesh.Shapes[i].Offsets[index] * weights[i];
                    displacement = Vector2.Scale(displacement, area.size);
                    Color tint = color;
                    if (mesh != null && mesh.Alpha != null && mesh.Alpha.Length > 0) tint.a *= mesh.Alpha[index];
                    vertices.AddVert(new Vector3(area.x + uv.x * area.width + displacement.x,
                        area.y + uv.y * area.height + displacement.y, 0f), tint,
                        new Vector2(uvRect.x + uv.x * uvRect.width, uvRect.y + uv.y * uvRect.height));
                }
            }
            for (int row = 0; row < rows; ++row)
            {
                for (int column = 0; column < columns; ++column)
                {
                    int lowerLeft = row * (columns + 1) + column;
                    int upperLeft = lowerLeft + columns + 1;
                    vertices.AddTriangle(lowerLeft, upperLeft, upperLeft + 1);
                    vertices.AddTriangle(upperLeft + 1, lowerLeft + 1, lowerLeft);
                }
            }
        }
    }
}
