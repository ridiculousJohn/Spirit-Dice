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
