using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dice.Presentation
{
    [Serializable]
    public sealed class SpiritPortraitShape
    {
        public string Id;
        public Vector2[] Offsets = Array.Empty<Vector2>();
    }

    /** Grid shapes store UV displacements independently of artwork size and host identity. */
    [CreateAssetMenu(menuName = "Dice/Presentation/Spirit Portrait Mesh")]
    public sealed class SpiritPortraitMeshAsset : ScriptableObject
    {
        public int Columns = 1;
        public int Rows = 1;
        public float[] Alpha = Array.Empty<float>();
        public SpiritPortraitShape[] Shapes = Array.Empty<SpiritPortraitShape>();

        public void Validate()
        {
            long count = ((long)Columns + 1) * ((long)Rows + 1);
            SpiritHostPresentationAsset.Require(Columns > 0 && Rows > 0 && count < 65000, "Mesh topology must have positive dimensions and fewer than 65000 vertices.");
            SpiritHostPresentationAsset.Require(Alpha == null || Alpha.Length == 0 || Alpha.Length == count, "Mesh Alpha must match the grid vertex count.");
            if (Alpha != null)
                foreach (float alpha in Alpha)
                    SpiritHostPresentationAsset.Require(SpiritHostPresentationAsset.Finite(alpha) && alpha >= 0f && alpha <= 1f, "Mesh Alpha must be in [0,1].");
            SpiritHostPresentationAsset.Require(Shapes != null, "Mesh Shapes cannot be null.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (SpiritPortraitShape shape in Shapes)
            {
                SpiritHostPresentationAsset.Require(shape != null && !string.IsNullOrWhiteSpace(shape.Id) && ids.Add(shape.Id), "Mesh shape ids must be nonempty and unique.");
                SpiritHostPresentationAsset.Require(shape.Offsets != null && shape.Offsets.Length == count, "Shape Offsets must match the grid vertex count.");
                foreach (Vector2 offset in shape.Offsets)
                    SpiritHostPresentationAsset.Require(SpiritHostPresentationAsset.Finite(offset), "Shape Offsets must be finite.");
            }
        }
    }
}
