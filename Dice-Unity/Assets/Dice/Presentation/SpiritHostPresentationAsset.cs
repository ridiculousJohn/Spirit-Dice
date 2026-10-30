using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    public enum SpiritPortraitProperty { PositionX, PositionY, Width, Height, Rotation, Alpha, Deformation }
    public enum SpiritTrackBlend { Replace, Add }

    /**
     * Whether a layer animates as smoke. The layer's authored image material, which uses the host's smoke shader, holds
     * the smoke's flow noise, motion mask and values.
     */
    [Serializable]
    public sealed class SpiritPortraitSmoke
    {
        public bool Enabled;
    }

    [Serializable]
    public sealed class SpiritPortraitLayer
    {
        public string Id;
        public Texture2D Texture;
        public SpiritPortraitMeshAsset Mesh;
        public Vector2 Position;
        public Vector2 Size;
        public Vector2 Pivot = new Vector2(.5f, .5f);
        public float Rotation;
        public Color Tint = Color.white;
        public bool Silhouette;
        [Tooltip("Respond to authored environment lighting. Disable for self-lit spirit details.")]
        public bool ReceiveSceneLighting = true;
        public SpiritPortraitSmoke Smoke = new SpiritPortraitSmoke();
        public float[] Weights = Array.Empty<float>();
        [Tooltip("Optional per-pixel scene lighting from unlit, reference and depth textures. Layers without lights keep the uniform receiver tint.")]
        public SceneLitLayer Lighting = new SceneLitLayer();
    }

    [Serializable]
    public sealed class SpiritPortraitTrack
    {
        public int Layer;
        public SpiritPortraitProperty Property;
        public int Channel;
        public SpiritTrackBlend Blend;
        public AnimationCurve Curve;
        public AnimationCurve EntryWeight;
    }

    [Serializable]
    public sealed class SpiritPresentationSequence
    {
        public string Id;
        public SpiritPresentationState State;
        public float Duration = 1f;
        public bool Loop;
        public bool IncludeIdle;
        public bool GestureOnStart;
        public float[] GestureTimes = Array.Empty<float>();
        public SpiritPortraitTrack[] Tracks = Array.Empty<SpiritPortraitTrack>();
        public bool RestartOnReaction;
    }

    /** Authored layers and timelines describe a host without coupling presentation to its identity. */
    [CreateAssetMenu(menuName = "Dice/Presentation/Spirit Host")]
    public sealed class SpiritHostPresentationAsset : ScriptableObject
    {
        public SpiritHostPortrait PortraitPrefab;
        public Image SigilPrefab;
        public Shader SmokeShader;
        [Tooltip("Lit-layer shader for lit layers whose authored material does not already use one.")]
        public Shader LitLayerShader;
        public Vector2 ReferenceSize = new Vector2(540f, 960f);
        public float Scale = 1f;
        public Vector2 Offset;
        public SpiritPortraitLayer[] Layers = Array.Empty<SpiritPortraitLayer>();
        public SpiritPresentationSequence[] Sequences = Array.Empty<SpiritPresentationSequence>();

        public Image CreateSigil(string name, RectTransform parent)
        {
            if (SigilPrefab == null) throw new InvalidOperationException("The host requires an authored sigil prefab.");
            Image sigil = Instantiate(SigilPrefab, parent, false);
            sigil.name = name;
            return sigil;
        }

        /** Removes a host sigil created earlier and creates the given host's in its place; no host leaves none. */
        public static Graphic ReplaceSigil(Graphic current, SpiritHostPresentationAsset presentation, string name,
            RectTransform parent)
        {
            if (current != null)
            {
                current.gameObject.SetActive(false);
                OwnedObject.Release(current.gameObject);
            }
            return presentation != null ? presentation.CreateSigil(name, parent) : null;
        }

        public void Validate()
        {
            Require(FiniteValue.Is(ReferenceSize) && ReferenceSize.x > 0f && ReferenceSize.y > 0f, "ReferenceSize must be positive and finite.");
            Require(FiniteValue.Is(Scale) && Scale > 0f && FiniteValue.Is(Offset), "Scale and Offset must be finite, with positive Scale.");
            Require(Layers != null && Layers.Length > 0, "At least one layer is required.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Layers.Length; ++i)
            {
                SpiritPortraitLayer layer = Layers[i];
                Require(layer != null && !string.IsNullOrWhiteSpace(layer.Id) && ids.Add(layer.Id), "Layer ids must be nonempty and unique.");
                Require(FiniteValue.Is(layer.Position) && FiniteValue.Is(layer.Size) && layer.Size.x > 0f && layer.Size.y > 0f, "Layer " + layer.Id + " has invalid bounds.");
                Require(FiniteValue.Is(layer.Pivot) && layer.Pivot.x >= 0f && layer.Pivot.x <= 1f && layer.Pivot.y >= 0f && layer.Pivot.y <= 1f,
                    "Layer " + layer.Id + " has invalid Pivot.");
                Require(FiniteValue.Is(layer.Rotation) && FiniteValue.Is(layer.Tint), "Layer " + layer.Id + " has invalid tint or rotation.");
                if (layer.Smoke != null && layer.Smoke.Enabled)
                {
                    Require(!layer.Silhouette, "Layer " + layer.Id + " cannot use both silhouette and smoke materials.");
                    Require(SmokeShader != null, "Layer " + layer.Id + " animates as smoke and requires the host's Smoke Shader.");
                }
                if (layer.Lighting != null && layer.Lighting.Configured)
                {
                    Require(!layer.Silhouette && (layer.Smoke == null || !layer.Smoke.Enabled) && layer.ReceiveSceneLighting,
                        "Layer " + layer.Id + " per-pixel lighting requires a scene-lit layer without silhouette or smoke materials.");
                    try { layer.Lighting.Validate(); }
                    catch (InvalidOperationException error) { Require(false, "Layer " + layer.Id + ": " + error.Message); }
                }
                if (layer.Mesh != null) layer.Mesh.Validate();
                int channels = layer.Mesh != null && layer.Mesh.Shapes != null ? layer.Mesh.Shapes.Length : 0;
                Require(layer.Weights == null || layer.Weights.Length == 0 || layer.Weights.Length == channels, "Layer " + layer.Id + " weights must match its mesh shapes.");
                if (layer.Weights != null)
                    for (int j = 0; j < layer.Weights.Length; ++j) Require(FiniteValue.Is(layer.Weights[j]), "Layer weights must be finite.");
            }
            Require(Sequences != null, "Sequences cannot be null.");
            ids.Clear();
            foreach (SpiritPresentationSequence sequence in Sequences)
            {
                Require(sequence != null && !string.IsNullOrWhiteSpace(sequence.Id) && ids.Add(sequence.Id), "Sequence ids must be nonempty and unique.");
                Require(ValidState(sequence.State), "Sequence " + sequence.Id + " has invalid State.");
                Require(FiniteValue.Is(sequence.Duration) && sequence.Duration > 0f, "Sequence " + sequence.Id + " Duration must be positive and finite.");
                Require(sequence.State != SpiritPresentationState.Reaction || !sequence.Loop, "Reaction sequences cannot loop.");
                Require(sequence.State < SpiritPresentationState.Victory || !sequence.Loop, "Outcome sequences cannot loop.");
                if (sequence.GestureTimes != null)
                    foreach (float time in sequence.GestureTimes)
                        Require(FiniteValue.Is(time) && time >= 0f && time <= sequence.Duration, "GestureTimes must be within their sequence duration.");
                Require(sequence.Tracks != null, "Sequence Tracks cannot be null.");
                foreach (SpiritPortraitTrack track in sequence.Tracks)
                {
                    Require(track != null && track.Layer >= 0 && track.Layer < Layers.Length, "Track Layer is out of range.");
                    Require(track.Property >= SpiritPortraitProperty.PositionX && track.Property <= SpiritPortraitProperty.Deformation, "Track Property is invalid.");
                    Require(track.Blend == SpiritTrackBlend.Replace || track.Blend == SpiritTrackBlend.Add, "Track Blend is invalid.");
                    int channels = Layers[track.Layer].Mesh != null ? Layers[track.Layer].Mesh.Shapes.Length : 0;
                    Require(track.Channel >= 0 && (track.Property == SpiritPortraitProperty.Deformation ? track.Channel < channels : track.Channel == 0), "Track Channel is out of range.");
                    ValidateCurve(track.Curve, true);
                    ValidateCurve(track.EntryWeight, false);
                }
            }
        }

        private void ValidateCurve(AnimationCurve curve, bool required)
        {
            Require(!required || curve != null && curve.length > 0, "A track requires a nonempty Curve.");
            if (curve == null) return;
            foreach (Keyframe key in curve.keys)
                Require(FiniteValue.Is(key.time) && FiniteValue.Is(key.value) && !float.IsNaN(key.inTangent) && !float.IsNaN(key.outTangent)
                    && FiniteValue.Is(key.inWeight) && FiniteValue.Is(key.outWeight), "Curve keys contain invalid values.");
        }

        internal static bool ValidState(SpiritPresentationState state) => state >= SpiritPresentationState.Idle && state <= SpiritPresentationState.Farewell;
        internal static bool DirectedState(SpiritPresentationState state) => state == SpiritPresentationState.Arrival
            || state >= SpiritPresentationState.Victory && state <= SpiritPresentationState.Farewell;
        private void Require(bool condition, string message) => Require(condition, this, message);

        /** Fails validation of the given presentation asset, naming it. */
        internal static void Require(bool condition, UnityEngine.Object asset, string message)
        {
            if (!condition) throw new InvalidOperationException("Invalid spirit presentation " + asset.name + ": " + message);
        }
    }
}
