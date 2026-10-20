using System.Collections.Generic;
using UnityEngine;
namespace Dice.Editor
{
    internal static class SpiritEncounterMeshes
    {
        private const float DieBevelRadius = .18f;
        private static readonly Vector3[] FaceNormals = { Vector3.forward, Vector3.up, Vector3.right, Vector3.left, Vector3.down, Vector3.back };
        public static Mesh CreateRoundedCube(bool recessedPips = true, float bevelRadius = DieBevelRadius, int divisions = 64)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> coordinates = new List<Vector2>();
            List<Vector2> faces = new List<Vector2>();
            List<Vector2> grainCoordinates = new List<Vector2>();
            List<int> triangles = new List<int>();
            for (int face = 1; face <= FaceNormals.Length; face++)
            {
                Vector3 faceNormal = FaceNormals[face - 1];
                Vector3 tangent = Mathf.Abs(faceNormal.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 bitangent = Vector3.Cross(faceNormal, tangent);
                List<Vector2> pips = recessedPips ? PipPositions(face) : null;
                int start = vertices.Count;
                for (int y = 0; y <= divisions; y++)
                {
                    for (int x = 0; x <= divisions; x++)
                    {
                        Vector2 coordinate = new Vector2((float)x / divisions - 0.5f, (float)y / divisions - 0.5f);
                        Vector3 point = faceNormal * 0.5f + tangent * coordinate.x + bitangent * coordinate.y;
                        // Shared position controls the wear, so adjacent face patches meet at the same bevel.
                        float radius = recessedPips ? bevelRadius * (1f + 0.065f
                            * Mathf.Sin(point.x * 7f + point.z * 4f) * Mathf.Cos(point.y * 6f - point.z * 3f)) : bevelRadius;
                        float innerHalf = 0.5f - radius;
                        Vector3 inner = new Vector3(Mathf.Clamp(point.x, -innerHalf, innerHalf),
                            Mathf.Clamp(point.y, -innerHalf, innerHalf), Mathf.Clamp(point.z, -innerHalf, innerHalf));
                        Vector3 normal = (point - inner).normalized;
                        Vector3 rounded = inner + normal * radius;
                        if (recessedPips)
                        {
                            Vector2 nearest = Vector2.one;
                            float distance = float.MaxValue;
                            foreach (Vector2 pip in pips)
                            {
                                Vector2 offset = coordinate - pip;
                                if (offset.sqrMagnitude >= distance * distance) continue;
                                nearest = offset;
                                distance = offset.magnitude;
                            }
                            if (distance < 0.096f)
                            {
                                float progress = Mathf.Clamp01((distance - 0.026f) / 0.07f);
                                float depth = 0.052f * (1f - progress * progress * (3f - 2f * progress));
                                rounded -= faceNormal * depth;
                                float slope = -0.052f * 6f * progress * (1f - progress) / 0.07f;
                                Vector2 direction = distance > 0.00001f ? nearest / distance : Vector2.zero;
                                normal = (faceNormal + tangent * (direction.x * slope) + bitangent * (direction.y * slope)).normalized;
                            }
                        }
                        vertices.Add(rounded);
                        normals.Add(normal);
                        coordinates.Add(coordinate);
                        faces.Add(new Vector2(face, 0f));
                        grainCoordinates.Add(new Vector2(Mathf.Abs(faceNormal.x) > 0.5f ? point.z : point.x,
                            Mathf.Abs(faceNormal.y) > 0.5f ? point.z : point.y) + Vector2.one * 0.5f);
                        if (x == divisions || y == divisions) continue;
                        int corner = start + y * (divisions + 1) + x;
                        triangles.Add(corner);
                        triangles.Add(corner + 1);
                        triangles.Add(corner + divisions + 2);
                        triangles.Add(corner);
                        triangles.Add(corner + divisions + 2);
                        triangles.Add(corner + divisions + 1);
                    }
                }
            }
            Mesh mesh = new Mesh { name = recessedPips ? "Spirit Dice Rounded Bone" : "Spirit Tray Beveled Block" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, coordinates);
            mesh.SetUVs(1, faces);
            mesh.SetUVs(2, grainCoordinates);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<Vector2> PipPositions(int face)
        {
            const float offset = 0.215f;
            List<Vector2> positions = new List<Vector2>(6);
            if (face % 2 != 0) positions.Add(Vector2.zero);
            if (face >= 2)
            {
                positions.Add(new Vector2(-offset, offset));
                positions.Add(new Vector2(offset, -offset));
            }
            if (face >= 4)
            {
                positions.Add(new Vector2(offset, offset));
                positions.Add(new Vector2(-offset, -offset));
            }
            if (face == 6)
            {
                positions.Add(new Vector2(-offset, 0f));
                positions.Add(new Vector2(offset, 0f));
            }
            return positions;
        }

        public static Mesh CreateShadowPlane()
        {
            Mesh mesh = new Mesh
            {
                name = "Spirit Dice Contact Plane",
                vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                    new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) },
                normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                uv = new[] { new Vector2(-0.5f, -0.5f), new Vector2(-0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, -0.5f) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh CreateSelectionRing()
        {
            const int segments = 48;
            const float radius = 0.17f;
            const float thickness = 0.028f;
            Vector3[] vertices = new Vector3[segments * 2];
            Vector3[] normals = new Vector3[vertices.Length];
            int[] triangles = new int[segments * 6];
            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / segments;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 corner = new Vector3(Mathf.Sign(direction.x) * (0.605f - radius), 0f,
                    Mathf.Sign(direction.z) * (0.88f - radius));
                vertices[segment * 2] = corner + direction * radius;
                vertices[segment * 2 + 1] = corner + direction * (radius - thickness);
                normals[segment * 2] = normals[segment * 2 + 1] = Vector3.up;
                int next = (segment + 1) % segments * 2;
                int start = segment * 6;
                triangles[start] = segment * 2;
                triangles[start + 1] = segment * 2 + 1;
                triangles[start + 2] = next;
                triangles[start + 3] = segment * 2 + 1;
                triangles[start + 4] = next + 1;
                triangles[start + 5] = next;
            }
            Mesh mesh = new Mesh { name = "Spirit Dice Rounded Selection Bay", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateRivet()
        {
            const int segments = 20;
            Vector3[] vertices = new Vector3[segments * 2 + 1];
            Vector3[] normals = new Vector3[vertices.Length];
            int[] triangles = new int[segments * 9];
            vertices[0] = Vector3.up * 0.006f;
            normals[0] = Vector3.up;
            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / segments;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                int inner = segment * 2 + 1;
                int outer = inner + 1;
                int nextInner = (segment + 1) % segments * 2 + 1;
                int nextOuter = nextInner + 1;
                vertices[inner] = radial * 0.014f + Vector3.up * 0.006f;
                vertices[outer] = radial * 0.021f + Vector3.up * 0.001f;
                normals[inner] = Vector3.up;
                normals[outer] = (Vector3.up + radial * 0.7f).normalized;
                int start = segment * 9;
                triangles[start] = 0;
                triangles[start + 1] = nextInner;
                triangles[start + 2] = inner;
                triangles[start + 3] = inner;
                triangles[start + 4] = nextInner;
                triangles[start + 5] = outer;
                triangles[start + 6] = outer;
                triangles[start + 7] = nextInner;
                triangles[start + 8] = nextOuter;
            }
            Mesh mesh = new Mesh { name = "Spirit Tray Brass Rivet", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

    }
}
