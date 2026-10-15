using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    public enum SpiritPortraitProperty { PositionX, PositionY, Width, Height, Rotation, Alpha, Deformation }
    public enum SpiritTrackBlend { Replace, Add }

    [Serializable]
    public sealed class SpiritPortraitSmoke
    {
        public bool Enabled;
        [Tooltip("Optional linear motion mask: black remains fixed, white receives smoke motion.")]
        public Texture2D MotionMask;
        [Tooltip("Optional linear, repeating RG flow noise. An omitted texture uses generated noise.")]
        public Texture2D FlowNoise;
        [Range(0f, .05f)] public float Distortion = .035f;
        [Min(0f)] public float Speed = .7f;
        [Range(0f, 1f)] public float Opacity = .6f;
        public Vector2 Flow = new Vector2(0f, .018f);
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
        public Shader SilhouetteShader;
        public Shader SmokeShader;
        public Vector2 ReferenceSize = new Vector2(540f, 960f);
        public float Scale = 1f;
        public Vector2 Offset;
        public SpiritPortraitLayer[] Layers = Array.Empty<SpiritPortraitLayer>();
        public SpiritPresentationSequence[] Sequences = Array.Empty<SpiritPresentationSequence>();
        public Vector2[] SigilLines = Array.Empty<Vector2>();
        public float SigilWidth = .035f;
        public Color SigilColor = new Color32(210, 166, 88, 255);

        public SpiritHostPortrait Create(RectTransform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            Validate();
            var host = SpiritUi.Full("HostPortrait", parent);
            var portrait = host.gameObject.AddComponent<SpiritHostPortrait>();
            portrait.Build(host, this);
            return portrait;
        }

        public Graphic CreateSigil(string name, RectTransform parent)
        {
            Graphic sigil;
            if (SigilLines != null && SigilLines.Length > 0)
            {
                var lines = SpiritUi.Full(name, parent).gameObject.AddComponent<SpiritHostSigilGraphic>();
                lines.Configure(SigilLines, SigilWidth);
                sigil = lines;
            }
            else sigil = SpiritUi.Full(name, parent).gameObject.AddComponent<SpiritModifierSigilGraphic>();
            sigil.color = SigilColor;
            sigil.raycastTarget = false;
            return sigil;
        }

        public void Validate()
        {
            Require(Finite(ReferenceSize) && ReferenceSize.x > 0f && ReferenceSize.y > 0f, "ReferenceSize must be positive and finite.");
            Require(Finite(Scale) && Scale > 0f && Finite(Offset), "Scale and Offset must be finite, with positive Scale.");
            Require(Layers != null && Layers.Length > 0, "At least one layer is required.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Layers.Length; ++i)
            {
                SpiritPortraitLayer layer = Layers[i];
                Require(layer != null && !string.IsNullOrWhiteSpace(layer.Id) && ids.Add(layer.Id), "Layer ids must be nonempty and unique.");
                Require(Finite(layer.Position) && Finite(layer.Size) && layer.Size.x > 0f && layer.Size.y > 0f, "Layer " + layer.Id + " has invalid bounds.");
                Require(Finite(layer.Pivot) && layer.Pivot.x >= 0f && layer.Pivot.x <= 1f && layer.Pivot.y >= 0f && layer.Pivot.y <= 1f,
                    "Layer " + layer.Id + " has invalid Pivot.");
                Require(Finite(layer.Rotation) && Finite(layer.Tint.r) && Finite(layer.Tint.g) && Finite(layer.Tint.b) && Finite(layer.Tint.a), "Layer " + layer.Id + " has invalid tint or rotation.");
                if (layer.Smoke != null && layer.Smoke.Enabled)
                {
                    Require(!layer.Silhouette, "Layer " + layer.Id + " cannot use both silhouette and smoke materials.");
                    Require(Finite(layer.Smoke.Distortion) && layer.Smoke.Distortion >= 0f && layer.Smoke.Distortion <= .05f,
                        "Layer " + layer.Id + " smoke distortion must be between zero and 0.05.");
                    Require(Finite(layer.Smoke.Speed) && layer.Smoke.Speed >= 0f && Finite(layer.Smoke.Flow),
                        "Layer " + layer.Id + " smoke motion must be finite, with nonnegative speed.");
                    Require(Finite(layer.Smoke.Opacity) && layer.Smoke.Opacity >= 0f && layer.Smoke.Opacity <= 1f,
                        "Layer " + layer.Id + " smoke opacity must be between zero and one.");
                }
                if (layer.Mesh != null) layer.Mesh.Validate();
                int channels = layer.Mesh != null && layer.Mesh.Shapes != null ? layer.Mesh.Shapes.Length : 0;
                Require(layer.Weights == null || layer.Weights.Length == 0 || layer.Weights.Length == channels, "Layer " + layer.Id + " weights must match its mesh shapes.");
                if (layer.Weights != null)
                    for (int j = 0; j < layer.Weights.Length; ++j) Require(Finite(layer.Weights[j]), "Layer weights must be finite.");
            }
            Require(Sequences != null, "Sequences cannot be null.");
            ids.Clear();
            foreach (SpiritPresentationSequence sequence in Sequences)
            {
                Require(sequence != null && !string.IsNullOrWhiteSpace(sequence.Id) && ids.Add(sequence.Id), "Sequence ids must be nonempty and unique.");
                Require(ValidState(sequence.State), "Sequence " + sequence.Id + " has invalid State.");
                Require(Finite(sequence.Duration) && sequence.Duration > 0f, "Sequence " + sequence.Id + " Duration must be positive and finite.");
                Require(sequence.State != SpiritPresentationState.Reaction || !sequence.Loop, "Reaction sequences cannot loop.");
                Require(sequence.State < SpiritPresentationState.Victory || !sequence.Loop, "Outcome sequences cannot loop.");
                if (sequence.GestureTimes != null)
                    foreach (float time in sequence.GestureTimes)
                        Require(Finite(time) && time >= 0f && time <= sequence.Duration, "GestureTimes must be within their sequence duration.");
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
            Require(Finite(SigilWidth) && SigilWidth > 0f, "SigilWidth must be positive and finite.");
            Require(SigilLines == null || SigilLines.Length % 2 == 0, "SigilLines must contain endpoint pairs.");
            if (SigilLines != null)
                foreach (Vector2 point in SigilLines) Require(Finite(point), "SigilLines must be finite.");
        }

        private static void ValidateCurve(AnimationCurve curve, bool required)
        {
            Require(!required || curve != null && curve.length > 0, "A track requires a nonempty Curve.");
            if (curve == null) return;
            foreach (Keyframe key in curve.keys)
                Require(Finite(key.time) && Finite(key.value) && !float.IsNaN(key.inTangent) && !float.IsNaN(key.outTangent)
                    && Finite(key.inWeight) && Finite(key.outWeight), "Curve keys contain invalid values.");
        }

        internal static bool ValidState(SpiritPresentationState state) => state >= SpiritPresentationState.Idle && state <= SpiritPresentationState.Farewell;
        internal static bool DirectedState(SpiritPresentationState state) => state == SpiritPresentationState.Arrival
            || state >= SpiritPresentationState.Victory && state <= SpiritPresentationState.Farewell;
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        internal static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Invalid spirit presentation: " + message);
        }
    }
}
