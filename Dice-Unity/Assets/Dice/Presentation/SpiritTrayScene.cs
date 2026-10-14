using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dice.Presentation
{
    /** One camera frames both bays through the same table coordinate space. */
    public sealed class SpiritTrayScene : MonoBehaviour
    {
        private static int sceneOrdinal;
        private SpiritTrayPresentationAsset presentation;
        private GameObject renderRoot;
        private Light light;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Vector3> framingPoints = new List<Vector3>();
        private SceneLightingState sceneLighting;
        public RenderTexture Texture { get; private set; }
        public Camera RenderCamera { get; private set; }
        public Transform RenderRoot => renderRoot != null ? renderRoot.transform : null;
        public SpiritTrayGeometry Geometry { get; private set; }
        public SpiritTrayPresentationAsset Presentation => presentation;

        public void Configure(SpiritTrayPresentationAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            asset.ValidateScene();
            if (presentation != asset) ReleasePresentation();
            presentation = asset;
        }

        public void Initialize()
        {
            if (renderRoot != null) return;
            if (presentation == null) throw new InvalidOperationException("Configure the tray scene before initialization.");
            presentation.ValidateScene();
            renderRoot = new GameObject("Spirit Tray Scene Root");
            renderRoot.layer = SpiritDiceTray.RenderLayer;
            renderRoot.transform.position = new Vector3(14000 + sceneOrdinal++ * 100, 0, 0);
            RebuildGeometry();
            Texture = new RenderTexture(presentation.TextureSize.x, presentation.TextureSize.y, 24, RenderTextureFormat.ARGB32)
            {
                name = "Spirit Shared Tray Texture", antiAliasing = 4,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            Texture.Create();
            var cameraObject = new GameObject("Spirit Shared Tray Camera");
            cameraObject.transform.SetParent(renderRoot.transform, false);
            RenderCamera = cameraObject.AddComponent<Camera>();
            RenderCamera.clearFlags = CameraClearFlags.SolidColor;
            RenderCamera.backgroundColor = Color.clear;
            RenderCamera.cullingMask = 1 << SpiritDiceTray.RenderLayer;
            RenderCamera.allowHDR = false;
            RenderCamera.allowMSAA = true;
            RenderCamera.targetTexture = Texture;
            var lightObject = new GameObject("Spirit Shared Tray Key Light");
            lightObject.transform.SetParent(renderRoot.transform, false);
            light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = .8f;
            light.shadowBias = .025f;
            light.shadowNormalBias = .04f;
            light.cullingMask = 1 << SpiritDiceTray.RenderLayer;
            ApplyPresentationSettings();
            SetSceneLighting(sceneLighting);
            renderRoot.SetActive(isActiveAndEnabled);
        }

        /** Refresh projection and lighting while retaining the camera and its shared texture references. */
        public void ApplyPresentationSettings()
        {
            if (renderRoot == null) return;
            presentation.ValidateScene();
            if (Texture.width != presentation.TextureSize.x || Texture.height != presentation.TextureSize.y)
            {
                RenderCamera.targetTexture = null;
                Texture.Release();
                Texture.width = presentation.TextureSize.x;
                Texture.height = presentation.TextureSize.y;
                Texture.Create();
                RenderCamera.targetTexture = Texture;
            }
            RenderCamera.transform.localRotation = Quaternion.Euler(presentation.CameraEulerAngles);
            RenderCamera.orthographic = presentation.Orthographic;
            RenderCamera.fieldOfView = presentation.FieldOfView;
            RenderCamera.aspect = (float)Texture.width / Texture.height;
            FrameGeometry();
            light.transform.localRotation = Quaternion.Euler(presentation.FallbackLightEulerAngles);
            light.color = presentation.FallbackLightColor;
            light.intensity = presentation.FallbackLightIntensity;
        }

        /** Release bay content before replacing geometry; the scene camera and texture remain available. */
        public void RebuildGeometry()
        {
            if (renderRoot == null) return;
            presentation.ValidateScene();
            if (Geometry != null) Release(Geometry.gameObject);
            foreach (Material material in materials) Release(material);
            materials.Clear();
            Geometry = Instantiate(presentation.GeometryPrefab, renderRoot.transform, false);
            foreach (Transform child in Geometry.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = SpiritDiceTray.RenderLayer;
            var copies = new Dictionary<Material, Material>();
            foreach (Renderer renderer in Geometry.GetComponentsInChildren<Renderer>(true))
            {
                Material[] surfaces = renderer.sharedMaterials;
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
            float radius = Mathf.Sqrt(3) * .5f * 1.13f;
            float scale = Geometry.DiceScale;
            float height = (Mathf.Sqrt(3) * .5f + .34f + radius) * scale;
            float bottom = (.5f - radius) * scale;
            foreach (Transform slot in Geometry.DiceSlots)
                AddFramingBounds(LocalToRenderRoot(slot), new Bounds(
                    Vector3.up * ((height + bottom) * .5f),
                    new Vector3((radius + .045f) * 2 * scale, height - bottom,
                        Mathf.Max(radius, .88f) * 2 * scale)));
            foreach (Transform label in Geometry.DiceLabelAnchors)
                framingPoints.Add(LocalToRenderRoot(label).MultiplyPoint3x4(Vector3.zero));

            // History is clipped across its authored width; arrival adds lift but cannot expand the bay.
            float tileRadius = Mathf.Sqrt(3) * .5f * Geometry.ModifierDieSize;
            float tileHeight = tileRadius * 2 + .08f;
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

        private void FrameGeometry()
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
            if (RenderCamera != null) { RenderCamera.enabled = false; RenderCamera.targetTexture = null; }
            if (Texture != null) { Texture.Release(); Release(Texture); }
            if (renderRoot != null) renderRoot.SetActive(false);
            Release(renderRoot);
            foreach (Material material in materials) Release(material);
            materials.Clear();
            framingPoints.Clear();
            renderRoot = null;
            RenderCamera = null;
            Texture = null;
            Geometry = null;
            light = null;
        }

        private void OnEnable() { if (renderRoot != null) renderRoot.SetActive(true); }
        private void OnDisable() { if (renderRoot != null) renderRoot.SetActive(false); }
        private void OnDestroy() => ReleasePresentation();
        private static void Release(UnityEngine.Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }
}
