using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    [Serializable]
    public sealed class SceneLightingSourceBinding
    {
        [Min(0)] public int LightIndex;
        public RectTransform Source;
        [Tooltip("Source pivot in normalized bottom-left background coordinates at the authored light position.")]
        public Vector2 ReferencePosition;
        public bool Enabled = true;
        [Tooltip("Receiver-space displacement produced by one horizontal normalized-image unit.")]
        public Vector3 ReceiverOffsetX;
        [Tooltip("Receiver-space displacement produced by one vertical normalized-image unit.")]
        public Vector3 ReceiverOffsetY;
        public RawImage[] EmissiveImages = Array.Empty<RawImage>();
    }

    [Serializable]
    public sealed class SceneLightingImageReceiver
    {
        public RawImage Image;
        public Vector3 Position;
        public Vector3 Normal = Vector3.back;
        public RectTransform Source;
        [Tooltip("Source pivot in normalized bottom-left background coordinates at the authored receiver position.")]
        public Vector2 ReferencePosition;
        public Vector3 ReceiverOffsetX;
        public Vector3 ReceiverOffsetY;
    }

    [Serializable]
    public sealed class SceneLightingHostReceiver
    {
        public SpiritHostPortrait Portrait;
        public SpiritHostPresentationAsset Presentation;
        public Vector3 Position;
    }

    /** Binds movable image sources and optional flat receivers to one sampled lighting instance. */
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(10000)]
    [AddComponentMenu("Dice/Presentation/Scene Lighting Presentation")]
    public sealed class SceneLightingPresentation : MonoBehaviour
    {
        [SerializeField] private SceneLightingAsset lightingAsset;
        [SerializeField] private RawImage background;
        [Tooltip("Independent multipliers for authored light intensities; omitted entries default to one.")]
        [SerializeField] private float[] intensityMultipliers = Array.Empty<float>();
        [SerializeField] private SceneLightingSourceBinding[] bindings = Array.Empty<SceneLightingSourceBinding>();
        [SerializeField] private SceneLightingImageReceiver[] receivers = Array.Empty<SceneLightingImageReceiver>();
        [SerializeField] private SceneLightingHostReceiver[] hosts = Array.Empty<SceneLightingHostReceiver>();
        [Tooltip("Images lit per pixel by this sample, taking precedence over other views sampling the same asset.")]
        [SerializeField] private SceneLitImage[] litImages = Array.Empty<SceneLitImage>();
        private readonly SceneLightingState state = new SceneLightingState();
        private readonly Dictionary<RawImage, Color> originalColors = new Dictionary<RawImage, Color>();
        private SceneLightingMaterial compositor;
        private RawImage configuredBackground;
        private Material originalMaterial;
        private Texture originalTexture;
        private float[] sampledMultipliers = Array.Empty<float>();
        private Vector2[] uvOffsets = Array.Empty<Vector2>();
        private Vector3[] receiverPositions = Array.Empty<Vector3>();

        public SceneLightingState State => state;

        public void Configure(SceneLightingAsset asset, RawImage image, SceneLightingSourceBinding[] sources,
            SceneLightingImageReceiver[] imageReceivers = null, SceneLightingHostReceiver[] hostReceivers = null,
            SceneLitImage[] litLayers = null)
        {
            Release();
            lightingAsset = asset;
            background = image;
            bindings = sources ?? Array.Empty<SceneLightingSourceBinding>();
            receivers = imageReceivers ?? Array.Empty<SceneLightingImageReceiver>();
            hosts = hostReceivers ?? Array.Empty<SceneLightingHostReceiver>();
            litImages = litLayers ?? Array.Empty<SceneLitImage>();
            if (isActiveAndEnabled) ApplyAtTime(Time.unscaledTimeAsDouble);
        }

        public float GetLightIntensity(int index)
        {
            CheckIndex(index);
            EnsureArrays();
            return intensityMultipliers[index];
        }

        /** A multiplier belongs to this presentation; shared lighting assets retain their authored settings. */
        public void SetLightIntensity(int index, float multiplier)
        {
            CheckIndex(index);
            if (!SceneLightingAsset.Finite(multiplier) || multiplier < 0f)
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            EnsureArrays();
            intensityMultipliers[index] = multiplier;
            if (isActiveAndEnabled) ApplyAtTime(state.SampleTime);
        }

        public void ApplyAtTime(double time)
        {
            if (!isActiveAndEnabled) return;
            if (lightingAsset == null || background == null)
            {
                Release();
                state.Sample(null, time);
                return;
            }
            lightingAsset.Validate();
            EnsureArrays();
            if (configuredBackground != background || compositor != null && compositor.Material.shader != lightingAsset.Shader)
                Release();
            if (compositor == null)
            {
                configuredBackground = background;
                originalMaterial = background.material;
                originalTexture = background.texture;
                compositor = new SceneLightingMaterial(lightingAsset.Shader);
            }
            for (int i = 0; i < lightingAsset.Lights.Length; ++i)
            {
                sampledMultipliers[i] = intensityMultipliers[i];
                uvOffsets[i] = lightingAsset.Lights[i].UvOffset;
                receiverPositions[i] = lightingAsset.Lights[i].ReceiverPosition;
            }
            foreach (SceneLightingSourceBinding binding in bindings)
            {
                if (binding == null) continue;
                CheckIndex(binding.LightIndex);
                if (!binding.Enabled || binding.Source != null && !binding.Source.gameObject.activeInHierarchy)
                    sampledMultipliers[binding.LightIndex] = 0f;
                if (binding.Source == null) continue;
                Vector2 delta = GetSourceOffset(binding.Source, binding.ReferencePosition);
                uvOffsets[binding.LightIndex] += delta;
                receiverPositions[binding.LightIndex] += binding.ReceiverOffsetX * delta.x + binding.ReceiverOffsetY * delta.y;
            }
            state.Sample(lightingAsset, time, sampledMultipliers, uvOffsets, receiverPositions);
            compositor.Apply(state);
            background.texture = lightingAsset.BaseTexture;
            background.material = compositor.Material;
            foreach (SceneLightingSourceBinding binding in bindings)
            {
                if (binding == null || binding.EmissiveImages == null) continue;
                SceneLightLayer light = lightingAsset.Lights[binding.LightIndex];
                float relativeIntensity = light.Intensity > 0f ? state.GetIntensity(binding.LightIndex) / light.Intensity : 0f;
                float emission = Mathf.Clamp01(Mathf.Pow(relativeIntensity, light.SourceResponse));
                foreach (RawImage image in binding.EmissiveImages)
                {
                    if (image == null) continue;
                    Color color = OriginalColor(image);
                    color.a *= emission;
                    image.color = color;
                }
            }
            foreach (SceneLightingImageReceiver receiver in receivers)
            {
                if (receiver == null || receiver.Image == null) continue;
                Color original = OriginalColor(receiver.Image);
                // Images lit per pixel already receive these lights and keep their authored color.
                if (receiver.Image.TryGetComponent(out SceneLitImage lit) && lit.isActiveAndEnabled && lit.Layer != null && lit.Layer.Configured)
                {
                    receiver.Image.color = original;
                    continue;
                }
                Vector2 delta = receiver.Source != null ? GetSourceOffset(receiver.Source, receiver.ReferencePosition) : Vector2.zero;
                Vector3 position = receiver.Position + receiver.ReceiverOffsetX * delta.x + receiver.ReceiverOffsetY * delta.y;
                Color color = (original.linear * state.EvaluateReceiver(position, receiver.Normal)).gamma;
                color.a = original.a;
                receiver.Image.color = color;
            }
            foreach (SceneLightingHostReceiver receiver in hosts)
            {
                if (receiver == null) continue;
                if (receiver.Portrait == null || receiver.Presentation == null || !(receiver.Portrait.transform is RectTransform))
                    throw new InvalidOperationException("Scene lighting hosts require a portrait, presentation and RectTransform.");
                receiver.Portrait.Build((RectTransform)receiver.Portrait.transform, receiver.Presentation);
                receiver.Portrait.SetSceneLighting(state, receiver.Position);
            }
            if (litImages != null)
                foreach (SceneLitImage image in litImages)
                    if (image != null) image.Bind(state);
        }

        private Color OriginalColor(RawImage image)
        {
            if (!originalColors.TryGetValue(image, out Color color))
                originalColors.Add(image, color = image.color);
            return color;
        }

        private Vector2 GetSourceOffset(RectTransform source, Vector2 referencePosition)
        {
            RectTransform frame = background.rectTransform;
            Rect rect = frame.rect;
            if (Mathf.Abs(rect.width) < .0001f || Mathf.Abs(rect.height) < .0001f) return Vector2.zero;
            Vector3 position = frame.InverseTransformPoint(source.position);
            return new Vector2((position.x - rect.xMin) / rect.width,
                (position.y - rect.yMin) / rect.height) - referencePosition;
        }

        private void EnsureArrays()
        {
            int count = lightingAsset.Lights.Length;
            if (intensityMultipliers == null || intensityMultipliers.Length != count)
            {
                float[] values = new float[count];
                for (int i = 0; i < count; ++i)
                    values[i] = intensityMultipliers != null && i < intensityMultipliers.Length ? intensityMultipliers[i] : 1f;
                intensityMultipliers = values;
            }
            if (sampledMultipliers.Length == count) return;
            sampledMultipliers = new float[count];
            uvOffsets = new Vector2[count];
            receiverPositions = new Vector3[count];
        }

        private void CheckIndex(int index)
        {
            if (lightingAsset == null || lightingAsset.Lights == null || index < 0 || index >= lightingAsset.Lights.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
        }

        private void OnEnable() => ApplyAtTime(Time.unscaledTimeAsDouble);
        private void LateUpdate() => ApplyAtTime(Time.unscaledTimeAsDouble);
        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= ApplyValidated;
#endif
            Release();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= ApplyValidated;
            UnityEditor.EditorApplication.delayCall += ApplyValidated;
#endif
        }

#if UNITY_EDITOR
        private void ApplyValidated()
        {
            if (this != null && isActiveAndEnabled) ApplyAtTime(Time.unscaledTimeAsDouble);
        }
#endif

        private void Release()
        {
            if (configuredBackground != null && compositor != null && configuredBackground.material == compositor.Material)
            {
                configuredBackground.material = originalMaterial;
                if (configuredBackground.texture == state.Asset?.BaseTexture) configuredBackground.texture = originalTexture;
            }
            foreach (var pair in originalColors)
                if (pair.Key != null) pair.Key.color = pair.Value;
            originalColors.Clear();
            foreach (SceneLightingHostReceiver receiver in hosts)
                if (receiver != null && receiver.Portrait != null) receiver.Portrait.SetSceneLighting(null, receiver.Position);
            SceneLitImage.Release(state);
            compositor?.Dispose();
            compositor = null;
            configuredBackground = null;
            originalMaterial = null;
            originalTexture = null;
        }
    }
}
