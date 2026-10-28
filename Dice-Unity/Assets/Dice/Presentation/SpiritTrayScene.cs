using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dice.Presentation
{
    /** One camera frames both bays through the same table coordinate space. */
    public sealed class SpiritTrayScene : MonoBehaviour
    {
        /** The selection halo's size relative to its die. */
        private const float SelectionHaloScale = 1.13f;
        /** Smallest half depth framed around each dice slot, in die sizes. */
        private const float MinimumSlotHalfDepth = .88f;
        [SerializeField] private SpiritTrayPresentationAsset presentation;
        [SerializeField] private GameObject renderRoot;
        [SerializeField] private Camera renderCamera;
        [SerializeField] private SpiritTrayGeometry geometry;
        private bool initialized;
        private readonly List<Material> materials = new List<Material>();
        private readonly Dictionary<Renderer, Material[]> authoredMaterials = new Dictionary<Renderer, Material[]>();
        private readonly List<Vector3> framingPoints = new List<Vector3>();
        private SceneLightingState sceneLighting;
        private RenderTexture authoredTarget;
        private float authoredAspect;
        private bool authoredStageActive;
        public RenderTexture Texture { get; private set; }
        public Camera RenderCamera => renderCamera;
        public Transform RenderRoot => renderRoot != null ? renderRoot.transform : null;
        public SpiritTrayGeometry Geometry => geometry;
        public SpiritTrayPresentationAsset Presentation => presentation;
        public SpiritDiceTray DiceTray => GetComponentInChildren<SpiritDiceTray>(true);
        public SpiritModifierHistoryRenderer HistoryRenderer => GetComponentInChildren<SpiritModifierHistoryRenderer>(true);

        /** Frame the authored geometry when explicitly requested by editor tooling. */
        public void FrameAuthoredGeometry()
        {
            CacheFramingPoints();
            FrameGeometry();
        }

        public void Configure(SpiritTrayPresentationAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            asset.ValidateScene();
            if (presentation != asset) ReleasePresentation();
            presentation = asset;
        }

        public void Initialize()
        {
            if (initialized) return;
            if (presentation == null || renderRoot == null || RenderCamera == null || Geometry == null)
                throw new InvalidOperationException("The tray scene requires authored presentation, geometry, root, and camera references.");
            presentation.ValidateScene();
            Geometry.Validate();
            authoredTarget = RenderCamera.targetTexture;
            authoredAspect = RenderCamera.aspect;
            authoredStageActive = renderRoot.activeSelf;
            if (authoredTarget != null)
                Texture = new RenderTexture(authoredTarget.descriptor)
                {
                    filterMode = authoredTarget.filterMode,
                    wrapModeU = authoredTarget.wrapModeU,
                    wrapModeV = authoredTarget.wrapModeV,
                    wrapModeW = authoredTarget.wrapModeW,
                    anisoLevel = authoredTarget.anisoLevel
                };
            else
                Texture = new RenderTexture(presentation.TextureSize.x, presentation.TextureSize.y, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 4, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
            Texture.name = "Spirit Shared Tray Texture";
            Texture.width = presentation.TextureSize.x;
            Texture.height = presentation.TextureSize.y;
            Texture.Create();
            RenderCamera.targetTexture = Texture;
            RenderCamera.aspect = (float)Texture.width / Texture.height;
            RebuildGeometry();
            initialized = true;
            renderRoot.SetActive(isActiveAndEnabled);
        }

        /** Resize render state while retaining the authored camera and lighting. */
        public void ApplyPresentationSettings()
        {
            if (Texture == null) return;
            if (Texture.width == presentation.TextureSize.x && Texture.height == presentation.TextureSize.y) return;
            RenderCamera.targetTexture = null;
            Texture.Release();
            Texture.width = presentation.TextureSize.x;
            Texture.height = presentation.TextureSize.y;
            Texture.Create();
            RenderCamera.targetTexture = Texture;
            RenderCamera.aspect = (float)Texture.width / Texture.height;
        }

        /** Bind material instances without replacing the authored geometry. */
        public void RebuildGeometry()
        {
            if (renderRoot == null) return;
            presentation.ValidateScene();
            ReleaseMaterialInstances();
            var copies = new Dictionary<Material, Material>();
            foreach (Renderer renderer in renderRoot.GetComponentsInChildren<Renderer>(true))
            {
                Material[] surfaces = renderer.sharedMaterials;
                authoredMaterials[renderer] = (Material[])surfaces.Clone();
                for (int i = 0; i < surfaces.Length; ++i)
                {
                    Material source = surfaces[i];
                    if (source == null) continue;
                    if (!copies.TryGetValue(source, out Material copy))
                    {
                        copy = new Material(source) { name = source.name + " Instance" };
                        copies.Add(source, copy);
                        materials.Add(copy);
                    }
                    surfaces[i] = copy;
                }
                renderer.sharedMaterials = surfaces;
            }
            CacheFramingPoints();
            SetSceneLighting(sceneLighting);
            if (RenderCamera != null) ApplyPresentationSettings();
        }

        private void CacheFramingPoints()
        {
            framingPoints.Clear();
            foreach (MeshFilter filter in Geometry.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    AddFramingBounds(LocalToRenderRoot(filter.transform), filter.sharedMesh.bounds);
            foreach (SkinnedMeshRenderer renderer in Geometry.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                AddFramingBounds(LocalToRenderRoot(renderer.transform), renderer.localBounds);

            // A rotated die and its selection halo stay inside this radius throughout the fixed roll arc.
            float radius = Mathf.Sqrt(3) * .5f * SelectionHaloScale;
            float scale = Geometry.DiceScale;
            float height = (Mathf.Sqrt(3) * .5f + SpiritRollTiming.MaximumLift + radius) * scale;
            float bottom = (.5f - radius) * scale;
            foreach (Transform slot in Geometry.DiceSlots)
                AddFramingBounds(LocalToRenderRoot(slot), new Bounds(
                    Vector3.up * ((height + bottom) * .5f),
                    new Vector3((radius + SpiritRollTiming.MaximumDrift) * 2 * scale, height - bottom,
                        Mathf.Max(radius, MinimumSlotHalfDepth) * 2 * scale)));
            foreach (Transform label in Geometry.DiceLabelAnchors)
                framingPoints.Add(LocalToRenderRoot(label).MultiplyPoint3x4(Vector3.zero));

            // History is clipped across its authored width; arrival adds lift but cannot expand the bay.
            float tileRadius = Mathf.Sqrt(3) * .5f * Geometry.ModifierDieSize;
            float tileHeight = tileRadius * 2 + SpiritModifierHistoryRenderer.ArrivalLift;
            AddFramingBounds(LocalToRenderRoot(Geometry.ModifierRoot), new Bounds(
                Vector3.up * (tileHeight * .5f), new Vector3(Geometry.ModifierWidth, tileHeight,
                    Mathf.Max(Geometry.ModifierDepth, tileRadius * 2))));
        }

        private Matrix4x4 LocalToRenderRoot(Transform target)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            for (Transform current = target; current != renderRoot.transform; current = current.parent)
                matrix = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * matrix;
            return matrix;
        }

        private void AddFramingBounds(Matrix4x4 matrix, Bounds bounds)
        {
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        framingPoints.Add(matrix.MultiplyPoint3x4(bounds.center +
                            Vector3.Scale(bounds.extents, new Vector3(x, y, z))));
        }

        public void FrameGeometry()
        {
            const float margin = 1.08f;
            Quaternion rotation = RenderCamera.transform.localRotation;
            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;
            foreach (Vector3 point in framingPoints)
            {
                Vector3 view = inverse * point;
                min = Vector3.Min(min, view);
                max = Vector3.Max(max, view);
            }
            Vector3 center = (min + max) * .5f;
            Vector3 extent = (max - min) * .5f;
            float clearance = Mathf.Max(.1f, extent.magnitude * .01f);
            float distance = extent.z + clearance;
            float verticalSlope = Mathf.Tan(RenderCamera.fieldOfView * Mathf.Deg2Rad * .5f) / margin;
            float horizontalSlope = verticalSlope * RenderCamera.aspect;
            RenderCamera.orthographicSize = Mathf.Max(.01f,
                Mathf.Max(extent.y, extent.x / RenderCamera.aspect) * margin);
            if (RenderCamera.orthographic) distance += RenderCamera.orthographicSize;
            else
                foreach (Vector3 point in framingPoints)
                {
                    Vector3 view = inverse * point - center;
                    distance = Mathf.Max(distance, Mathf.Max(Mathf.Abs(view.x) / horizontalSlope - view.z,
                        Mathf.Abs(view.y) / verticalSlope - view.z));
                }

            float cameraZ = center.z - distance;
            if (!RenderCamera.orthographic)
            {
                center.x = CenterProjection(inverse, cameraZ, min.x, max.x, 0);
                center.y = CenterProjection(inverse, cameraZ, min.y, max.y, 1);
            }
            RenderCamera.transform.localPosition = rotation * new Vector3(center.x, center.y, cameraZ);
            float depthPadding = Mathf.Max(.05f, extent.z * .1f);
            RenderCamera.nearClipPlane = Mathf.Max(.01f, min.z - cameraZ - depthPadding);
            RenderCamera.farClipPlane = Mathf.Max(RenderCamera.nearClipPlane + .1f, max.z - cameraZ + depthPadding);
        }

        private float CenterProjection(Quaternion inverse, float cameraZ, float min, float max, int axis)
        {
            // Perspective depths differ, so centering view-space bounds alone leaves a visible offset.
            for (int iteration = 0; iteration < 24; ++iteration)
            {
                float center = (min + max) * .5f;
                float projectedMin = float.MaxValue;
                float projectedMax = float.MinValue;
                foreach (Vector3 point in framingPoints)
                {
                    Vector3 view = inverse * point;
                    float projected = (view[axis] - center) / (view.z - cameraZ);
                    projectedMin = Mathf.Min(projectedMin, projected);
                    projectedMax = Mathf.Max(projectedMax, projected);
                }
                if (projectedMin + projectedMax > 0) min = center;
                else max = center;
            }
            return (min + max) * .5f;
        }

        public void SetSceneLighting(SceneLightingState state)
        {
            sceneLighting = state;
            if (renderRoot == null) return;
            foreach (Material material in materials)
                SceneLightingReceiverMaterial.Apply(material, state, renderRoot.transform);
        }

        public void ReleasePresentation()
        {
            if (Texture != null)
            {
                if (RenderCamera != null)
                {
                    RenderCamera.targetTexture = authoredTarget;
                    RenderCamera.aspect = authoredAspect;
                }
                Texture.Release();
                OwnedObject.Release(Texture);
                if (renderRoot != null) renderRoot.SetActive(authoredStageActive);
            }
            ReleaseMaterialInstances();
            framingPoints.Clear();
            Texture = null;
            authoredTarget = null;
            initialized = false;
        }

        private void ReleaseMaterialInstances()
        {
            foreach (var pair in authoredMaterials)
                if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            authoredMaterials.Clear();
            foreach (Material material in materials) OwnedObject.Release(material);
            materials.Clear();
        }

        // Internal so EditMode tests, where Unity sends no lifecycle messages, drive them directly.
        internal void OnEnable() { if (renderRoot != null) renderRoot.SetActive(true); }
        internal void OnDisable() { if (renderRoot != null) renderRoot.SetActive(false); }
        private void OnDestroy() => ReleasePresentation();
    }
}
