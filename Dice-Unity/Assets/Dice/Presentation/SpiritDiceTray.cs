using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    public sealed class SpiritDiceTray : MonoBehaviour
    {
        public const int RenderLayer = 31;
        private const float SlotSpacing = 1.45f;
        private const float FeltHeight = 0.04f;
        private const float RestHeight = FeltHeight + 0.5f;
        private const float DieBevelRadius = 0.18f;
        private static readonly Vector3[] FaceNormals =
        {
            Vector3.forward, Vector3.up, Vector3.right,
            Vector3.left, Vector3.down, Vector3.back
        };

        [SerializeField] private Shader diceShader;
        private readonly Transform[] dice = new Transform[DiceValues.Count];
        private readonly Material[] diceMaterials = new Material[DiceValues.Count];
        private readonly GameObject[] selectionOutlines = new GameObject[DiceValues.Count];
        private readonly GameObject[] selectionFrames = new GameObject[DiceValues.Count];
        private readonly MeshRenderer[] contactShadows = new MeshRenderer[DiceValues.Count];
        private readonly List<UnityEngine.Object> ownedResources = new List<UnityEngine.Object>();
        private GameObject renderRoot;
        private Camera renderCamera;
        private RenderTexture texture;
        private Vector3[] supportPoints;
        private int selectionMask;
        private int rollingMask;

        public RenderTexture Texture => texture;
        public bool IsRolling { get; private set; }

        public void Initialize()
        {
            if (renderRoot != null) return;

            Shader shader = Resources.Load<Shader>("SpiritDiceSurface")
                ?? (diceShader != null ? diceShader : FindShader());
            if (shader == null) throw new InvalidOperationException("A dice rendering shader is required.");

            renderRoot = new GameObject("Spirit Dice Render Root");
            renderRoot.layer = RenderLayer;
            renderRoot.transform.position = new Vector3(10000f, 0f, 0f);

            texture = new RenderTexture(1024, 320, 24, RenderTextureFormat.ARGB32)
            {
                name = "Spirit Dice Texture",
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();

            GameObject cameraObject = CreateChild("Spirit Dice Camera", renderRoot.transform);
            renderCamera = cameraObject.AddComponent<Camera>();
            renderCamera.transform.localPosition = new Vector3(0f, 6.8f, -4.8f);
            renderCamera.transform.LookAt(renderRoot.transform.position + Vector3.up * 0.27f);
            renderCamera.orthographic = false;
            renderCamera.fieldOfView = 20f;
            renderCamera.aspect = (float)texture.width / texture.height;
            renderCamera.nearClipPlane = 0.1f;
            renderCamera.farClipPlane = 30f;
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = Color.clear;
            renderCamera.cullingMask = 1 << RenderLayer;
            renderCamera.allowHDR = false;
            renderCamera.allowMSAA = true;
            renderCamera.targetTexture = texture;

            GameObject lightObject = CreateChild("Spirit Dice Key Light", renderRoot.transform);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.8f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.8f;
            light.shadowBias = 0.025f;
            light.shadowNormalBias = 0.04f;
            light.cullingMask = 1 << RenderLayer;
            light.transform.localRotation = Quaternion.Euler(65f, -32f, 0f);

            Mesh bodyMesh = CreateRoundedCube();
            supportPoints = CreateSupportPoints(bodyMesh);
            Mesh boxMesh = CreateRoundedCube(false, 0.09f, 8);
            Mesh shadowMesh = CreateShadowPlane();
            Mesh selectionMesh = CreateSelectionRing();
            ownedResources.Add(bodyMesh);
            ownedResources.Add(boxMesh);
            ownedResources.Add(shadowMesh);
            ownedResources.Add(selectionMesh);
            BuildTray(shader, boxMesh);

            Material shadowMaterial = CreateSurfaceMaterial(shader, new Color(0.025f, 0.018f, 0.011f, 0.66f),
                "Spirit Dice Contact Shadow", 5, true);
            Material outlineMaterial = CreateSurfaceMaterial(shader, new Color(1f, 0.82f, 0.34f, 1f),
                "Spirit Dice Amber Edge", 6, true);
            Material haloMaterial = CreateSurfaceMaterial(shader, new Color(1f, 0.49f, 0.08f, 0.32f),
                "Spirit Dice Amber Halo", 6, true);
            Material outerHaloMaterial = CreateSurfaceMaterial(shader, new Color(1f, 0.35f, 0.035f, 0.13f),
                "Spirit Dice Amber Outer Halo", 6, true);
            Material frameMaterial = CreateSurfaceMaterial(shader, new Color(1f, 0.72f, 0.2f, 0.9f),
                "Spirit Dice Selected Bay", 6, true);
            if (outlineMaterial.HasProperty("_Cull")) outlineMaterial.SetFloat("_Cull", 1f);
            if (haloMaterial.HasProperty("_Cull")) haloMaterial.SetFloat("_Cull", 1f);
            if (outerHaloMaterial.HasProperty("_Cull")) outerHaloMaterial.SetFloat("_Cull", 1f);
            outlineMaterial.renderQueue = 3030;
            haloMaterial.renderQueue = 3020;
            outerHaloMaterial.renderQueue = 3010;

            Texture2D boneTexture = Resources.Load<Texture2D>("SpiritBoneSurface");

            Mesh pipMesh = null;
            Material pipMaterial = null;
            if (!shadowMaterial.HasProperty("_SurfaceKind"))
            {
                pipMesh = CreatePip();
                ownedResources.Add(pipMesh);
                pipMaterial = CreateMaterial(shader, new Color(0.04f, 0.03f, 0.02f), "Spirit Dice Pips");
            }

            for (int index = 0; index < DiceValues.Count; index++)
            {
                GameObject die = CreateChild("Spirit Die " + index, renderRoot.transform);
                dice[index] = die.transform;
                diceMaterials[index] = CreateSurfaceMaterial(shader, new Color(1f, 0.97f, 0.9f),
                    "Spirit Dice Ivory " + index, 0);
                if (boneTexture != null && diceMaterials[index].HasProperty("_SurfaceTexture"))
                    diceMaterials[index].SetTexture("_SurfaceTexture", boneTexture);
                if (diceMaterials[index].HasProperty("_Seed")) diceMaterials[index].SetFloat("_Seed", index * 17.31f);
                AddMesh(die, bodyMesh, diceMaterials[index]);
                if (pipMesh != null)
                    for (int face = 1; face <= 6; face++) AddPips(die.transform, face, pipMesh, pipMaterial);

                GameObject shadow = CreateChild("Die " + index + " Contact Shadow", renderRoot.transform);
                shadow.transform.localPosition = new Vector3((index - 2) * SlotSpacing, FeltHeight + 0.003f, 0f);
                contactShadows[index] = AddMesh(shadow, shadowMesh, shadowMaterial);
                SetShadowHeight(index, 0f);

                selectionOutlines[index] = CreateChild("Die " + index + " Selection Outline", die.transform);
                GameObject outerHalo = CreateChild("Amber Outer Halo", selectionOutlines[index].transform);
                outerHalo.transform.localScale = Vector3.one * 1.13f;
                AddMesh(outerHalo, bodyMesh, outerHaloMaterial);
                GameObject halo = CreateChild("Amber Halo", selectionOutlines[index].transform);
                halo.transform.localScale = Vector3.one * 1.07f;
                AddMesh(halo, bodyMesh, haloMaterial);
                GameObject outline = CreateChild("Amber Edge", selectionOutlines[index].transform);
                outline.transform.localScale = Vector3.one * 1.022f;
                AddMesh(outline, bodyMesh, outlineMaterial);

                selectionFrames[index] = CreateChild("Bay " + index + " Selection Frame", renderRoot.transform);
                selectionFrames[index].transform.localPosition = new Vector3((index - 2) * SlotSpacing, FeltHeight + 0.009f, 0f);
                AddMesh(selectionFrames[index], selectionMesh, frameMaterial);
            }

            SetValues(new DiceValues(1, 2, 3, 4, 5));
            SetSelection(0);
        }

        public void SetValues(DiceValues values)
        {
            if (!values.IsValid) throw new ArgumentException("Every die must have a face from 1 to 6.", nameof(values));
            Initialize();
            if (IsRolling) throw new InvalidOperationException("Cannot replace dice values during a roll.");
            for (int index = 0; index < DiceValues.Count; index++)
            {
                dice[index].localRotation = RotationForFace(values[index], index);
                dice[index].localPosition = SlotPosition(index);
                SetShadowHeight(index, 0f);
            }
        }

        public void SetSelection(int mask)
        {
            Initialize();
            selectionMask = mask & DiceValues.AllMask;
            for (int index = 0; index < DiceValues.Count; index++)
            {
                bool selected = (selectionMask & (1 << index)) != 0;
                selectionOutlines[index].SetActive(selected);
                selectionFrames[index].SetActive(selected);
                if ((rollingMask & (1 << index)) == 0) dice[index].localPosition = SlotPosition(index);
            }
        }

        public IEnumerator Roll(DiceValues after, int mask, float duration = 0.55f)
            => Roll(after, mask, SpiritRollTiming.ForDuration(duration));

        public IEnumerator Roll(DiceValues after, int mask, SpiritRollTiming timing)
        {
            if (!after.IsValid) throw new ArgumentException("Every die must have a face from 1 to 6.", nameof(after));
            if (timing == null) throw new ArgumentNullException(nameof(timing));
            Initialize();
            if (IsRolling) throw new InvalidOperationException("A dice roll is already running.");
            rollingMask = mask & DiceValues.AllMask;
            if (rollingMask == 0) yield break;

            Quaternion[] startRotations = new Quaternion[DiceValues.Count];
            Quaternion[] endRotations = new Quaternion[DiceValues.Count];
            for (int index = 0; index < DiceValues.Count; index++)
            {
                startRotations[index] = dice[index].localRotation;
                endRotations[index] = RotationForFace(after[index], index);
            }

            IsRolling = true;
            try
            {
                int diceCount = 0;
                for (int index = 0; index < DiceValues.Count; index++)
                    if ((rollingMask & (1 << index)) != 0) diceCount++;
                while (true)
                {
                    float elapsed = timing.Elapsed;
                    int ordinal = 0;
                    for (int index = 0; index < DiceValues.Count; index++)
                    {
                        if ((rollingMask & (1 << index)) == 0) continue;
                        timing.SamplePose(ordinal++, diceCount, elapsed, startRotations[index], endRotations[index],
                            out Quaternion rotation, out float lift, out float drift);
                        dice[index].localRotation = rotation;
                        Vector3 position = SlotPosition(index);
                        position.y = FeltHeight + SupportHeight(rotation) + lift;
                        position.x += drift;
                        dice[index].localPosition = position;
                        SetShadowHeight(index, lift / .34f);
                    }
                    if (elapsed >= timing.Duration) break;
                    yield return null;
                }
            }
            finally
            {
                for (int index = 0; index < DiceValues.Count; index++)
                {
                    if ((rollingMask & (1 << index)) == 0) continue;
                    if (dice[index] == null) continue;
                    dice[index].localRotation = endRotations[index];
                    dice[index].localPosition = SlotPosition(index);
                    SetShadowHeight(index, 0f);
                }
                rollingMask = 0;
                IsRolling = false;
            }
        }

        public int GetVisibleFace(int index)
        {
            if (index < 0 || index >= DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(index));
            Initialize();
            Vector3 towardCamera = (renderCamera.transform.position - dice[index].position).normalized;
            float bestAlignment = float.NegativeInfinity;
            int visibleFace = 1;
            for (int face = 1; face <= 6; face++)
            {
                float alignment = Vector3.Dot(dice[index].TransformDirection(FaceNormals[face - 1]), towardCamera);
                if (alignment <= bestAlignment) continue;
                bestAlignment = alignment;
                visibleFace = face;
            }
            return visibleFace;
        }

        public Vector2 GetSlotViewportPosition(int index)
        {
            if (index < 0 || index >= DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(index));
            Initialize();
            Vector3 point = renderRoot.transform.TransformPoint(SlotPosition(index) + Vector3.up * 0.18f);
            return renderCamera.WorldToViewportPoint(point);
        }

        public Vector2 GetSlotLabelViewportPosition(int index)
        {
            if (index < 0 || index >= DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(index));
            Initialize();
            Vector3 point = renderRoot.transform.TransformPoint(new Vector3((index - 2) * SlotSpacing, 0.35f, -1.13f));
            return renderCamera.WorldToViewportPoint(point);
        }

        private void SetShadowHeight(int index, float lift)
        {
            if (contactShadows[index] == null) return;
            contactShadows[index].transform.localScale = new Vector3(1.24f + lift * 0.24f, 1f, 1.16f + lift * 0.18f);
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetFloat("_ShadowLift", lift);
            contactShadows[index].SetPropertyBlock(properties);
        }

        private Vector3 SlotPosition(int index)
        {
            return new Vector3((index - 2) * SlotSpacing, RestHeight, 0f);
        }

        private float SupportHeight(Quaternion rotation)
        {
            Vector3 downward = Quaternion.Inverse(rotation) * Vector3.down;
            float height = 0f;
            foreach (Vector3 point in supportPoints) height = Mathf.Max(height, Vector3.Dot(point, downward));
            return height;
        }

        private static Vector3[] CreateSupportPoints(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            List<Vector3> points = new List<Vector3>();
            // Support lies on the bevel or flat-face perimeter, never inside a pip depression.
            foreach (Vector3 point in vertices)
            {
                int outerAxes = (Mathf.Abs(point.x) >= 0.3f ? 1 : 0)
                    + (Mathf.Abs(point.y) >= 0.3f ? 1 : 0) + (Mathf.Abs(point.z) >= 0.3f ? 1 : 0);
                if (outerAxes >= 2) points.Add(point);
            }
            return points.ToArray();
        }

        private static Quaternion RotationForFace(int face, int index)
        {
            // Results face upward, so a resting die always has a flat base on the felt.
            Quaternion result = Quaternion.FromToRotation(FaceNormals[face - 1], Vector3.up);
            return Quaternion.AngleAxis(index % 4 * 90f + (index - 2) * 3f, Vector3.up) * result;
        }

        private static GameObject CreateChild(string objectName, Transform parent)
        {
            GameObject child = new GameObject(objectName);
            child.layer = RenderLayer;
            child.transform.SetParent(parent, false);
            return child;
        }

        private static MeshRenderer AddMesh(GameObject target, Mesh mesh, Material material)
        {
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            bool transparent = material.renderQueue >= 3000;
            renderer.shadowCastingMode = transparent ? UnityEngine.Rendering.ShadowCastingMode.Off
                : UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = !transparent;
            return renderer;
        }

        private static Shader FindShader()
        {
            return Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");
        }

        private Material CreateMaterial(Shader shader, Color color, string materialName)
        {
            Material material = new Material(shader) { name = materialName };
            SetMaterialColor(material, color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.23f);
            if (material.HasProperty("_SpecColor")) material.SetColor("_SpecColor", new Color(0.2f, 0.2f, 0.2f));
            ownedResources.Add(material);
            return material;
        }

        private Material CreateSurfaceMaterial(Shader shader, Color color, string materialName, int kind,
            bool transparent = false)
        {
            Material material = CreateMaterial(shader, color, materialName);
            if (material.HasProperty("_SurfaceKind")) material.SetFloat("_SurfaceKind", kind);
            if (transparent)
            {
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3000;
            }
            return material;
        }

        private void BuildTray(Shader shader, Mesh boxMesh)
        {
            Texture2D woodTexture = Resources.Load<Texture2D>("SpiritWoodSurface");
            Material wood = CreateSurfaceMaterial(shader, woodTexture != null ? Color.white : new Color(0.33f, 0.145f, 0.052f),
                "Spirit Tray Worn Walnut", 1);
            if (woodTexture != null && wood.HasProperty("_SurfaceTexture")) wood.SetTexture("_SurfaceTexture", woodTexture);
            Material cavity = CreateSurfaceMaterial(shader, new Color(0.026f, 0.039f, 0.023f), "Spirit Tray Bay Recess", 2);
            Material felt = CreateSurfaceMaterial(shader, new Color(0.085f, 0.12f, 0.067f), "Spirit Tray Deep Green Felt", 2);
            Material brass = CreateSurfaceMaterial(shader, new Color(0.66f, 0.43f, 0.16f), "Spirit Tray Aged Brass", 3);
            AddBox("Tray Foundation", boxMesh, wood, new Vector3(0f, -0.095f, 0f), new Vector3(7.94f, 0.18f, 2.48f));
            AddBox("Tray Front Rail", boxMesh, wood, new Vector3(0f, 0.245f, -1.115f), new Vector3(7.86f, 0.5f, 0.26f));
            AddBox("Tray Back Rail", boxMesh, wood, new Vector3(0f, 0.255f, 1.115f), new Vector3(7.86f, 0.52f, 0.26f));
            for (int side = -1; side <= 1; side += 2)
            {
                AddBox("Tray Side Rail " + side, boxMesh, wood, new Vector3(side * 3.8f, 0.25f, 0f),
                    new Vector3(0.26f, 0.51f, 2.24f));
                AddBox("Tray Side Brass " + side, boxMesh, brass, new Vector3(side * 3.8f, 0.511f, 0f),
                    new Vector3(0.02f, 0.01f, 2.15f));
            }
            AddBox("Front Brass Inlay", boxMesh, brass, new Vector3(0f, 0.501f, -1.115f), new Vector3(7.55f, 0.01f, 0.022f));
            AddBox("Back Brass Inlay", boxMesh, brass, new Vector3(0f, 0.521f, 1.115f), new Vector3(7.55f, 0.01f, 0.022f));

            for (int index = 0; index < DiceValues.Count; index++)
            {
                float x = (index - 2) * SlotSpacing;
                AddBox("Bay " + index + " Recess", boxMesh, cavity, new Vector3(x, 0.003f, 0f), new Vector3(1.36f, 0.042f, 1.98f));
                AddBox("Bay " + index + " Felt", boxMesh, felt, new Vector3(x, 0.027f, 0f), new Vector3(1.31f, 0.026f, 1.91f));
                if (index == DiceValues.Count - 1) continue;
                float dividerX = x + SlotSpacing * 0.5f;
                AddBox("Bay Divider " + index, boxMesh, wood, new Vector3(dividerX, 0.18f, 0f), new Vector3(0.11f, 0.37f, 1.99f));
                AddBox("Divider Brass " + index, boxMesh, brass, new Vector3(dividerX, 0.371f, 0f), new Vector3(0.018f, 0.01f, 1.94f));
            }

            Mesh rivetMesh = CreateRivet();
            ownedResources.Add(rivetMesh);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int end = -1; end <= 1; end += 2)
                {
                    AddBox("Brass Corner " + side + " " + end, boxMesh, brass,
                        new Vector3(side * 3.73f, end > 0 ? 0.526f : 0.506f, end * 1.115f), new Vector3(0.29f, 0.014f, 0.25f));
                    for (int rivet = 0; rivet < 2; rivet++)
                    {
                        GameObject head = CreateChild("Corner Rivet", renderRoot.transform);
                        head.transform.localPosition = new Vector3(side * (3.66f + rivet * 0.08f), end > 0 ? 0.538f : 0.518f, end * 1.115f);
                        AddMesh(head, rivetMesh, brass);
                    }
                }
            }
        }

        private void AddBox(string objectName, Mesh mesh, Material material, Vector3 position, Vector3 size)
        {
            GameObject box = CreateChild(objectName, renderRoot.transform);
            box.transform.localPosition = position;
            bool wood = material.HasProperty("_SurfaceKind") && material.GetFloat("_SurfaceKind") == 1f;
            if (wood && size.z > size.x)
            {
                box.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                size = new Vector3(size.z, size.y, size.x);
            }
            box.transform.localScale = size;
            MeshRenderer renderer = AddMesh(box, mesh, material);
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetVector("_GrainScale", new Vector4(size.x * 2f, size.y * 20f, size.z * 16f, 0f));
            properties.SetFloat("_Seed", position.x * 11.17f + position.z * 31.73f);
            properties.SetVector("_SurfaceTexture_ST", new Vector4(size.x * 0.2f,
                Mathf.Max(size.y, size.z) * 2f, position.x * 0.19f + position.z * 0.27f, 0f));
            renderer.SetPropertyBlock(properties);
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static void AddPips(Transform parent, int face, Mesh mesh, Material material)
        {
            Quaternion orientation = Quaternion.FromToRotation(Vector3.forward, FaceNormals[face - 1]);
            List<Vector2> positions = new List<Vector2>(6);
            const float offset = 0.215f;
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
            foreach (Vector2 position in positions)
            {
                GameObject pip = CreateChild("Face " + face + " Pip", parent);
                pip.transform.localRotation = orientation;
                pip.transform.localPosition = orientation * new Vector3(position.x, position.y, 0.501f);
                AddMesh(pip, mesh, material);
            }
        }

        private static Mesh CreatePip()
        {
            const int segments = 24;
            Vector3[] vertices = new Vector3[segments + 1];
            Vector3[] normals = new Vector3[vertices.Length];
            int[] triangles = new int[segments * 3];
            normals[0] = Vector3.forward;
            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / segments;
                vertices[segment + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.075f;
                normals[segment + 1] = Vector3.forward;
                triangles[segment * 3] = 0;
                triangles[segment * 3 + 1] = segment + 1;
                triangles[segment * 3 + 2] = (segment + 1) % segments + 1;
            }
            Mesh mesh = new Mesh { name = "Spirit Dice Pip", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        internal static Mesh CreateRoundedCube(bool recessedPips = true, float bevelRadius = DieBevelRadius, int divisions = 64)
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

        internal static Mesh CreateShadowPlane()
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

        private static Mesh CreateSelectionRing()
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

        private void OnDestroy()
        {
            if (renderCamera != null)
            {
                renderCamera.enabled = false;
                renderCamera.targetTexture = null;
            }
            if (texture != null)
            {
                texture.Release();
                DestroyOwned(texture);
            }
            DestroyOwned(renderRoot);
            foreach (UnityEngine.Object resource in ownedResources) DestroyOwned(resource);
            ownedResources.Clear();
            texture = null;
            renderRoot = null;
        }

        private static void DestroyOwned(UnityEngine.Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }
}
