using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Lights one UI image per pixel from the state that samples its authored lighting, without changing its serialized material. */
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Dice/Presentation/Scene Lit Image")]
    public sealed class SceneLitImage : MonoBehaviour, IMaterialModifier
    {
        private static readonly List<SceneLitImage> active = new List<SceneLitImage>();
        [Tooltip("Any view sampling this lighting asset lights the image; without a sample it shows the asset's authored levels.")]
        [SerializeField] private SceneLightingAsset lighting;
        [Tooltip("Lit-layer shader used when the image's material does not already use one.")]
        [SerializeField] private Shader shader;
        [Tooltip("Receiver rig position added to the layer's frame mapping.")]
        [SerializeField] private Vector3 position;
        [SerializeField] private SceneLitLayer layer = new SceneLitLayer();
        private SceneLitLayerMaterial lit;
        private SceneLightingState sampled;
        private bool explicitBinding;

        public SceneLightingAsset Lighting => lighting;
        public SceneLitLayer Layer => layer;
        public Vector3 Position => position;
        public SceneLightingState BoundState => sampled;
        public Material LitMaterial => lit?.Material;

        public void Configure(SceneLightingAsset asset, SceneLitLayer description, Shader litShader = null, Vector3 rigPosition = default)
        {
            description?.Validate();
            lighting = asset;
            layer = description ?? new SceneLitLayer();
            shader = litShader;
            position = rigPosition;
            Rebuild();
        }

        /** An explicit state takes precedence over other views sampling the same asset until it is unbound. */
        public void Bind(SceneLightingState state)
        {
            explicitBinding = state != null;
            sampled = state;
            Refresh();
        }

        public void Unbind()
        {
            explicitBinding = false;
            sampled = null;
            Refresh();
        }

        /** Images following a released state return to authored levels. */
        public static void Release(SceneLightingState state)
        {
            if (state == null) return;
            for (int i = active.Count - 1; i >= 0; --i)
                if (active[i].sampled == state) active[i].Unbind();
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!isActiveAndEnabled || layer == null || !layer.Configured) return baseMaterial;
            if (lit == null || lit.Source != baseMaterial)
            {
                lit?.Dispose();
                lit = null;
                // An image without a lit shader keeps its own material instead of breaking canvas rebuilds.
                if (!SceneLitLayerMaterial.UsesLitShader(baseMaterial) && shader == null) return baseMaterial;
                lit = new SceneLitLayerMaterial(layer, shader, baseMaterial);
                Refresh();
            }
            return lit.Material;
        }

        private static void Sampled(SceneLightingState state)
        {
            for (int i = active.Count - 1; i >= 0; --i)
            {
                SceneLitImage image = active[i];
                if (image.sampled == state)
                {
                    if (!image.explicitBinding && (state.Asset == null || state.Asset != image.lighting)) image.sampled = null;
                    image.Refresh();
                }
                else if (!image.explicitBinding && image.lighting != null && state.Asset == image.lighting)
                {
                    image.sampled = state;
                    image.Refresh();
                }
            }
        }

        private void Refresh()
        {
            if (lit == null || lit.Material == null) return;
            Rect uvRect = TryGetComponent(out RawImage raw) ? raw.uvRect : new Rect(0f, 0f, 1f, 1f);
            if (sampled != null && sampled.Asset != null) lit.Apply(sampled, position, uvRect);
            else lit.ApplyAuthored(lighting, position, uvRect);
        }

        private void Rebuild()
        {
            lit?.Dispose();
            lit = null;
            if (TryGetComponent(out Graphic graphic)) graphic.SetMaterialDirty();
        }

        private void OnEnable()
        {
            if (active.Count == 0) SceneLightingState.Sampled += Sampled;
            active.Add(this);
            Rebuild();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= RebuildValidated;
#endif
            active.Remove(this);
            if (active.Count == 0) SceneLightingState.Sampled -= Sampled;
            sampled = null;
            explicitBinding = false;
            Rebuild();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= RebuildValidated;
            UnityEditor.EditorApplication.delayCall += RebuildValidated;
#endif
        }

#if UNITY_EDITOR
        private void RebuildValidated()
        {
            if (this != null && isActiveAndEnabled) Rebuild();
        }
#endif
    }
}
