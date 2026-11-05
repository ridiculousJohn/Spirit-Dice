using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dice.Presentation
{
    /** One camera frames both bays through the same table coordinate space. */
    public sealed class SpiritTrayScene : MonoBehaviour
    {
        [SerializeField] private SpiritTrayPresentationAsset presentation;
        [SerializeField] private GameObject renderRoot;
        [SerializeField] private Camera renderCamera;
        [SerializeField] private SpiritTrayGeometry geometry;
        private bool initialized;
        private readonly List<Material> materials = new List<Material>();
        private readonly Dictionary<Renderer, Material[]> authoredMaterials = new Dictionary<Renderer, Material[]>();
        private SceneLightingState sceneLighting;
        private RenderTexture authoredTarget;
        private float authoredAspect;
        private bool authoredStageActive;
        public RenderTexture Texture { get; private set; }
        /**
         * The part of the texture the tray's visible content covers, as a viewport rectangle of the camera: the projected
         * bounds of the geometry's shown renderers apart from the surface anchors' contents (the dice, their shadows and
         * highlights, and the charm row's dice), so the rails, bays and charm row; the texture outside it is empty margin.
         * Measured when the scene is initialized and when its texture changes shape.
         */
        public Rect ContentViewport { get; private set; }
        public Camera RenderCamera => renderCamera;
        public Transform RenderRoot => renderRoot != null ? renderRoot.transform : null;
        public SpiritTrayGeometry Geometry => geometry;
        public SpiritTrayPresentationAsset Presentation => presentation;
        public SpiritDiceTray DiceTray => GetComponentInChildren<SpiritDiceTray>(true);
        public SpiritModifierHistoryRenderer HistoryRenderer => GetComponentInChildren<SpiritModifierHistoryRenderer>(true);

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
            MeasureContent();
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
            MeasureContent();
        }

        /** Projects the bounds of the tray's shown static renderers through the camera; see ContentViewport. */
        private void MeasureContent()
        {
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            foreach (Renderer renderer in Geometry.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !ShownWithGeometry(renderer.transform) || OnSurfaceAnchor(renderer.transform)) continue;
                Bounds bounds = renderer.localBounds;
                Matrix4x4 toWorld = renderer.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; ++corner)
                {
                    var direction = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                    Vector3 point = RenderCamera.WorldToViewportPoint(
                        toWorld.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, direction)));
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            }
            if (min.x > max.x)
                throw new InvalidOperationException("The tray geometry " + Geometry.name + " shows no renderer to frame.");
            ContentViewport = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /** Whether the object and its parents up to the geometry are active, as authored, whatever the stage's own state. */
        private bool ShownWithGeometry(Transform target)
        {
            for (Transform current = target; current != Geometry.transform; current = current.parent)
                if (!current.gameObject.activeSelf) return false;
            return Geometry.gameObject.activeSelf;
        }

        /** Whether the object is content placed on a dice slot or the charm row's surface rather than the tray itself. */
        private bool OnSurfaceAnchor(Transform target)
        {
            if (target.IsChildOf(Geometry.ModifierRoot)) return true;
            foreach (Transform slot in Geometry.DiceSlots)
                if (target.IsChildOf(slot)) return true;
            return false;
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
            SetSceneLighting(sceneLighting);
            if (RenderCamera != null) ApplyPresentationSettings();
        }

        public void SetSceneLighting(SceneLightingState state)
        {
            sceneLighting = state;
            foreach (Material material in materials)
                SceneLightingReceiverMaterial.Apply(material, state, renderRoot.transform);
        }

        /**
         * Unbinds the scene so it can be configured again: the camera, stage and renderers get their authored target,
         * aspect, active state and materials back, and the texture and material instances the scene created are destroyed.
         * A scene never initialized has a texture of none and changed nothing.
         */
        public void ReleasePresentation()
        {
            if (Texture != null)
            {
                RenderCamera.targetTexture = authoredTarget;
                RenderCamera.aspect = authoredAspect;
                renderRoot.SetActive(authoredStageActive);
            }
            ReleaseMaterialInstances();
            ReleaseOwnedResources();
        }

        private void ReleaseMaterialInstances()
        {
            foreach (var pair in authoredMaterials) pair.Key.sharedMaterials = pair.Value;
            DestroyMaterialInstances();
        }

        private void DestroyMaterialInstances()
        {
            authoredMaterials.Clear();
            foreach (Material material in materials) OwnedObject.Release(material);
            materials.Clear();
        }

        /** Destroys the texture and material instances the scene created, which are assets the scene does not unload. */
        private void ReleaseOwnedResources()
        {
            if (Texture != null)
            {
                Texture.Release();
                OwnedObject.Release(Texture);
            }
            DestroyMaterialInstances();
            Texture = null;
            authoredTarget = null;
            initialized = false;
        }

        // Internal so EditMode tests, where Unity sends no lifecycle messages, drive them directly.
        internal void OnEnable() => renderRoot.SetActive(true);
        internal void OnDisable() => renderRoot.SetActive(false);
        // Teardown destroys the camera, stage and renderers in no fixed order, so it leaves their authored state alone.
        private void OnDestroy() => ReleaseOwnedResources();
    }
}
