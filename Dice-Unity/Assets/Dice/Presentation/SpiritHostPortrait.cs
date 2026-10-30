using System;
using UnityEngine;

namespace Dice.Presentation
{
    public enum SpiritPresentationState { Idle = 0, Waiting = 1, Arrival = 2, Reaction = 3, Victory = 4, Defeat = 5, Farewell = 6 }

    /** A host evaluates authored timelines independently of encounter mechanics. */
    public sealed class SpiritHostPortrait : MonoBehaviour
    {
        private sealed class LayerRuntime
        {
            public SpiritPortraitImage Image;
            public SpiritSmokeMaterial Smoke;
            public SceneLitLayerMaterial Lit;
            public Material AuthoredMaterial;
            public Color Tint;
            public float[] Defaults;
            public float[] Values;
            public float[] EntryPose;
            public float[] EntryBaseline;
            public float[] Weights;
        }

        /** A layer's pose holds one value per property before its deformation channels, which follow in order. */
        private const int DeformationOffset = (int)SpiritPortraitProperty.Deformation;
        /** Longer frame steps, such as a resume after a pause, advance the pose without emitting the gestures they skip, in seconds. */
        private const float MaximumGestureStep = 2f;
        private SpiritHostPresentationAsset presentation;
        [SerializeField] private RectTransform portraitRoot;
        [SerializeField] private SpiritPortraitImage[] authoredLayers;
        private bool initialized;
        private Vector2 referenceSize;
        private Vector2 portraitOffset;
        private Vector3 portraitScale;
        private LayerRuntime[] layers;
        private double[] idleAges;
        private float[][] entryValues;
        private readonly float[] durations = new float[(int)SpiritPresentationState.Farewell + 1];
        private readonly bool[] includesIdle = new bool[(int)SpiritPresentationState.Farewell + 1];
        private double stateAge;
        // Continuous effects retain their phase across dialogue and pose resets.
        private double effectAge;
        // Internal so EditMode tests read the time continuous effects are sampled at.
        internal double EffectAge => effectAge;
        private float arrivalProgress;
        private Color sceneLightingTint = Color.white;
        private bool hasSceneLighting;
        private SceneLightingState sceneLighting;
        private Vector3 sceneLightingPosition;

        public bool IsReacting => State == SpiritPresentationState.Reaction;
        public SpiritPresentationState State { get; private set; }
        public float Progress => SpiritHostPresentationAsset.DirectedState(State) ? arrivalProgress
            : durations[(int)State] > 0f ? Mathf.Clamp01((float)(stateAge / durations[(int)State])) : 0f;
        public float GestureDuration => durations[(int)SpiritPresentationState.Reaction];
        public event Action SpiritGesture;

        public float GetDuration(SpiritPresentationState state)
        {
            if (!SpiritHostPresentationAsset.ValidState(state)) throw new ArgumentOutOfRangeException(nameof(state));
            return durations[(int)state];
        }

        public void SetSceneLighting(SceneLightingState state, Vector3 position)
        {
            hasSceneLighting = state != null && state.Asset != null;
            sceneLightingTint = state != null ? state.EvaluateReceiver(position, Vector3.back) : Color.white;
            sceneLighting = hasSceneLighting ? state : null;
            sceneLightingPosition = position;
            ApplyLitLayers();
            ApplyPose();
        }

        /** Lit layers use the shared sample per pixel; without one they show their authored material. */
        private void ApplyLitLayers()
        {
            if (layers == null) return;
            foreach (LayerRuntime layer in layers)
            {
                if (layer.Lit == null) continue;
                Material material = layer.AuthoredMaterial;
                if (sceneLighting != null)
                {
                    layer.Lit.Apply(sceneLighting, sceneLightingPosition, layer.Image.uvRect);
                    material = layer.Lit.Material;
                }
                if (layer.Image.material != material) layer.Image.material = material;
            }
        }

        public void Build(SpiritHostPresentationAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (initialized) return;
            if (portraitRoot == null || authoredLayers == null || authoredLayers.Length != asset.Layers.Length)
                throw new InvalidOperationException("The host portrait requires its authored image layers.");
            asset.Validate();
            // Whether a lit layer needs the host's lit-layer shader depends on the material authored on the portrait's image,
            // so it is checked here, before anything is built.
            for (int i = 0; i < asset.Layers.Length; ++i)
            {
                SpiritPortraitLayer source = asset.Layers[i];
                var image = authoredLayers[i];
                if (image == null) throw new InvalidOperationException("An authored portrait layer is missing.");
                if (source.Lighting != null && source.Lighting.Configured && !SceneLitLayerMaterial.UsesLitShader(image.material) &&
                    !SceneLitLayerMaterial.IsLitShader(asset.LitLayerShader))
                    throw new InvalidOperationException("The lit layer " + source.Id + " of the host " + asset.name + " has an " +
                        "authored material without a lit-layer shader and requires the host's Lit Layer Shader, a shader with " +
                        "scene lit layer properties.");
            }
            presentation = asset;
            referenceSize = portraitRoot.rect.size;
            portraitOffset = portraitRoot.anchoredPosition;
            portraitScale = portraitRoot.localScale;
            layers = new LayerRuntime[asset.Layers.Length];
            idleAges = new double[asset.Sequences.Length];
            entryValues = new float[asset.Sequences.Length][];
            includesIdle[(int)SpiritPresentationState.Waiting] = true;
            for (int i = 0; i < asset.Sequences.Length; ++i)
            {
                SpiritPresentationSequence sequence = asset.Sequences[i];
                durations[(int)sequence.State] = Mathf.Max(durations[(int)sequence.State], sequence.Duration);
                includesIdle[(int)sequence.State] |= sequence.IncludeIdle;
                entryValues[i] = new float[sequence.Tracks.Length];
            }

            for (int i = 0; i < layers.Length; ++i)
            {
                SpiritPortraitLayer source = asset.Layers[i];
                var image = authoredLayers[i];
                SpiritSmokeMaterial smoke = source.Smoke != null && source.Smoke.Enabled
                    ? new SpiritSmokeMaterial(asset.SmokeShader, image.material) : null;
                if (smoke != null) image.material = smoke.Material;
                SceneLitLayerMaterial lit = source.Lighting != null && source.Lighting.Configured
                    ? new SceneLitLayerMaterial(source.Lighting, asset.LitLayerShader, image.material) : null;
                int channels = image.Weights.Count;
                if (channels != (source.Mesh != null ? source.Mesh.Shapes.Length : 0))
                    throw new InvalidOperationException("Authored portrait deformation channels must match the animation data.");
                var defaults = new float[DeformationOffset + channels];
                defaults[(int)SpiritPortraitProperty.PositionX] = image.rectTransform.anchoredPosition.x;
                defaults[(int)SpiritPortraitProperty.PositionY] = image.rectTransform.anchoredPosition.y;
                defaults[(int)SpiritPortraitProperty.Width] = image.rectTransform.sizeDelta.x;
                defaults[(int)SpiritPortraitProperty.Height] = image.rectTransform.sizeDelta.y;
                defaults[(int)SpiritPortraitProperty.Rotation] = Mathf.DeltaAngle(0, image.rectTransform.localEulerAngles.z);
                defaults[(int)SpiritPortraitProperty.Alpha] = image.color.a;
                for (int channel = 0; channel < channels; ++channel) defaults[DeformationOffset + channel] = image.Weights[channel];
                layers[i] = new LayerRuntime
                {
                    Image = image, Smoke = smoke, Lit = lit, AuthoredMaterial = image.material, Tint = image.color, Defaults = defaults, Values = new float[defaults.Length],
                    EntryPose = new float[defaults.Length], EntryBaseline = new float[defaults.Length],
                    Weights = new float[channels]
                };
            }
            initialized = true;
            ApplyLitLayers();
            ResetPose();
        }

        public SpiritPortraitImage GetLayer(string id)
        {
            if (presentation == null) return null;
            for (int i = 0; i < layers.Length; ++i)
                if (string.Equals(presentation.Layers[i].Id, id, StringComparison.Ordinal)) return layers[i].Image;
            return null;
        }

        public float ReadLayerValue(int layer, SpiritPortraitProperty property, int channel = 0)
        {
            if (layers == null || layer < 0 || layer >= layers.Length) throw new ArgumentOutOfRangeException(nameof(layer));
            if (property < SpiritPortraitProperty.PositionX || property > SpiritPortraitProperty.Deformation)
                throw new ArgumentOutOfRangeException(nameof(property));
            if (channel < 0 || (property == SpiritPortraitProperty.Deformation ? channel >= layers[layer].Weights.Length : channel != 0))
                throw new ArgumentOutOfRangeException(nameof(channel));
            return layers[layer].Values[ValueIndex(property, channel)];
        }

        public void React()
        {
            if (!Visible || State != SpiritPresentationState.Idle && State != SpiritPresentationState.Reaction || GestureDuration <= 0f) return;
            CaptureEntryPose();
            for (int i = 0; i < presentation.Sequences.Length; ++i)
                if (presentation.Sequences[i].State == SpiritPresentationState.Idle && presentation.Sequences[i].RestartOnReaction)
                    idleAges[i] = 0d;

            CaptureEntryValues(SpiritPresentationState.Reaction);
            State = SpiritPresentationState.Reaction;
            stateAge = 0d;
            ApplyPose();
            EmitStartGestures(State);
        }

        public void ResetPose()
        {
            State = SpiritPresentationState.Idle;
            stateAge = 0d;
            arrivalProgress = 0f;
            if (idleAges == null) return;
            Array.Clear(idleAges, 0, idleAges.Length);
            for (int i = 0; i < entryValues.Length; ++i) Array.Clear(entryValues[i], 0, entryValues[i].Length);
            ApplyPose();
        }

        public void SetVisible(bool visible)
        {
            if (portraitRoot == null) return;
            if (!visible) ResetPose();
            portraitRoot.gameObject.SetActive(visible);
            if (visible) ApplyPose();
        }

        public void SetState(SpiritPresentationState state, float progress)
        {
            if (!SpiritHostPresentationAsset.ValidState(state)) throw new ArgumentOutOfRangeException(nameof(state));
            if (presentation == null || !FiniteValue.Is(progress)) return;
            progress = Mathf.Clamp01(progress);
            if (state == SpiritPresentationState.Reaction)
            {
                React();
                return;
            }
            if (state == SpiritPresentationState.Idle && IsReacting)
            {
                ApplyPose();
                return;
            }
            bool changed = State != state;
            float previousProgress = arrivalProgress;
            if (changed)
            {
                CaptureEntryPose();
                State = state;
                stateAge = 0d;
                arrivalProgress = 0f;
                Array.Clear(idleAges, 0, idleAges.Length);
                for (int i = 0; i < entryValues.Length; ++i) Array.Clear(entryValues[i], 0, entryValues[i].Length);
                CaptureEntryValues(state);
            }
            if (SpiritHostPresentationAsset.DirectedState(state)) arrivalProgress = progress;
            ApplyPose();
            if (!Visible) return;
            if (changed) EmitStartGestures(state);
            else if (SpiritHostPresentationAsset.DirectedState(state) && progress > previousProgress)
            {
                for (int i = 0; i < presentation.Sequences.Length; ++i)
                {
                    SpiritPresentationSequence sequence = presentation.Sequences[i];
                    if (sequence.State == state)
                        EmitCrossedGestures(sequence, previousProgress * sequence.Duration, progress * sequence.Duration);
                }
            }
        }

        private bool Visible => initialized && portraitRoot != null && portraitRoot.gameObject.activeInHierarchy;

        private void Update() => Advance(Time.unscaledDeltaTime);

        /** Evaluate an absolute idle time for authoring, including the continuous effect phase. */
        public void Sample(float seconds)
        {
            if (!Visible || !FiniteValue.Is(seconds) || seconds < 0f) return;
            effectAge = 0d;
            ResetPose();
            Advance(seconds);
        }

        public void Advance(float deltaTime)
        {
            if (!Visible || !FiniteValue.Is(deltaTime) || deltaTime < 0f) return;
            effectAge += deltaTime;
            if (SpiritHostPresentationAsset.DirectedState(State))
            {
                ApplyPose();
                return;
            }
            SpiritPresentationState previousState = State;
            double previousAge = stateAge;
            stateAge += deltaTime;
            bool emit = deltaTime <= MaximumGestureStep;
            for (int i = 0; i < presentation.Sequences.Length; ++i)
            {
                SpiritPresentationSequence sequence = presentation.Sequences[i];
                if (sequence.State == SpiritPresentationState.Idle)
                {
                    double previousIdleAge = idleAges[i];
                    idleAges[i] += deltaTime;
                    if (emit && previousState != SpiritPresentationState.Reaction
                        && (previousState == SpiritPresentationState.Idle || includesIdle[(int)previousState]))
                        EmitCrossedGestures(sequence, previousIdleAge, idleAges[i]);
                }
                else if (emit && sequence.State == previousState)
                    EmitCrossedGestures(sequence, previousAge, stateAge);
            }
            if (IsReacting && stateAge >= GestureDuration)
            {
                State = SpiritPresentationState.Idle;
                stateAge = 0d;
            }
            ApplyPose();
        }

        private void ApplyPose()
        {
            if (presentation == null) return;
            RectTransform parent = (RectTransform)portraitRoot.parent;
            Vector2 fit = new Vector2(parent.rect.width / referenceSize.x, parent.rect.height / referenceSize.y);
            portraitRoot.localScale = Vector3.Scale(portraitScale, new Vector3(fit.x, fit.y, 1));
            portraitRoot.anchoredPosition = Vector2.Scale(portraitOffset, fit);
            ResetValues(false);
            if (State == SpiritPresentationState.Idle || includesIdle[(int)State])
                SampleSequences(SpiritPresentationState.Idle, false);
            if (State != SpiritPresentationState.Idle) SampleSequences(State, false);
            for (int i = 0; i < layers.Length; ++i)
            {
                LayerRuntime layer = layers[i];
                float[] values = layer.Values;
                RectTransform rect = layer.Image.rectTransform;
                rect.anchoredPosition = new Vector2(values[(int)SpiritPortraitProperty.PositionX],
                    values[(int)SpiritPortraitProperty.PositionY]);
                rect.sizeDelta = new Vector2(values[(int)SpiritPortraitProperty.Width], values[(int)SpiritPortraitProperty.Height]);
                rect.localRotation = Quaternion.Euler(0f, 0f, values[(int)SpiritPortraitProperty.Rotation]);
                Color tint = layer.Tint;
                if (hasSceneLighting && layer.Lit == null && !presentation.Layers[i].Silhouette && presentation.Layers[i].ReceiveSceneLighting)
                    tint = (tint.linear * sceneLightingTint).gamma;
                tint.a = Mathf.Clamp01(values[(int)SpiritPortraitProperty.Alpha]);
                layer.Image.color = tint;
                if (layer.Weights.Length > 0) Array.Copy(values, DeformationOffset, layer.Weights, 0, layer.Weights.Length);
                layer.Image.SetWeights(layer.Weights);
                layer.Smoke?.ApplyTime(effectAge, layer.Image);
            }
        }

        private void ResetValues(bool baseline)
        {
            for (int i = 0; i < layers.Length; ++i)
                Array.Copy(layers[i].Defaults, baseline ? layers[i].EntryBaseline : layers[i].Values, layers[i].Defaults.Length);
        }

        private void CaptureEntryPose()
        {
            for (int i = 0; i < layers.Length; ++i)
                Array.Copy(layers[i].Values, layers[i].EntryPose, layers[i].Values.Length);
        }

        private void CaptureEntryValues(SpiritPresentationState state)
        {
            // Additive entry values exclude the baseline that the destination will evaluate again.
            ResetValues(true);
            if (state != SpiritPresentationState.Idle && includesIdle[(int)state]) SampleSequences(SpiritPresentationState.Idle, true);
            for (int i = 0; i < presentation.Sequences.Length; ++i)
            {
                SpiritPresentationSequence sequence = presentation.Sequences[i];
                if (sequence.State != state) continue;
                for (int j = 0; j < sequence.Tracks.Length; ++j)
                {
                    SpiritPortraitTrack track = sequence.Tracks[j];
                    int value = ValueIndex(track.Property, track.Channel);
                    entryValues[i][j] = layers[track.Layer].EntryPose[value] - track.Curve.Evaluate(0f)
                        - (track.Blend == SpiritTrackBlend.Add ? layers[track.Layer].EntryBaseline[value] : 0f);
                }
            }
        }

        private void SampleSequences(SpiritPresentationState state, bool baseline)
        {
            for (int i = 0; i < presentation.Sequences.Length; ++i)
            {
                SpiritPresentationSequence sequence = presentation.Sequences[i];
                if (sequence.State != state) continue;
                double age = SpiritHostPresentationAsset.DirectedState(State) ? arrivalProgress * durations[(int)State]
                    : state == SpiritPresentationState.Idle ? idleAges[i] : stateAge;
                float time = SpiritHostPresentationAsset.DirectedState(state) ? arrivalProgress * sequence.Duration
                    : sequence.Loop ? (float)(age % sequence.Duration) : (float)Math.Min(age, sequence.Duration);
                for (int j = 0; j < sequence.Tracks.Length; ++j)
                {
                    SpiritPortraitTrack track = sequence.Tracks[j];
                    float value = track.Curve.Evaluate(time);
                    if (!baseline && track.EntryWeight != null && track.EntryWeight.length > 0)
                        value += entryValues[i][j] * track.EntryWeight.Evaluate(time);
                    float[] target = baseline ? layers[track.Layer].EntryBaseline : layers[track.Layer].Values;
                    int index = ValueIndex(track.Property, track.Channel);
                    if (track.Blend == SpiritTrackBlend.Add) target[index] += value;
                    else target[index] = value;
                }
            }
        }

        private static int ValueIndex(SpiritPortraitProperty property, int channel)
            => property == SpiritPortraitProperty.Deformation ? DeformationOffset + channel : (int)property;

        private void EmitStartGestures(SpiritPresentationState state)
        {
            for (int i = 0; i < presentation.Sequences.Length; ++i)
            {
                SpiritPresentationSequence sequence = presentation.Sequences[i];
                if (sequence.State != state) continue;
                if (sequence.GestureOnStart) SpiritGesture?.Invoke();
                if (sequence.GestureTimes == null) continue;
                for (int j = 0; j < sequence.GestureTimes.Length; ++j)
                    if (sequence.GestureTimes[j] == 0f) SpiritGesture?.Invoke();
            }
        }

        private void EmitCrossedGestures(SpiritPresentationSequence sequence, double previous, double current)
        {
            if (sequence.GestureTimes == null || current <= previous) return;
            for (int i = 0; i < sequence.GestureTimes.Length; ++i)
            {
                float time = sequence.GestureTimes[i];
                if (sequence.Loop)
                {
                    double firstCycle = Math.Floor((previous - time) / sequence.Duration) + 1d;
                    double lastCycle = Math.Floor((current - time) / sequence.Duration);
                    // Resume spikes must not replay a backlog of cosmetic audio.
                    int count = (int)Math.Min(4d, Math.Max(0d, lastCycle - firstCycle + 1d));
                    for (int j = 0; j < count; ++j) SpiritGesture?.Invoke();
                }
                else if (previous < time && current >= time) SpiritGesture?.Invoke();
            }
        }

        private void OnDestroy()
        {
            if (layers != null)
                foreach (LayerRuntime layer in layers)
                {
                    if (layer != null && layer.Smoke != null)
                    {
                        if (layer.Image != null) layer.Image.material = null;
                        layer.Smoke.Dispose();
                    }
                    if (layer != null && layer.Lit != null)
                    {
                        if (layer.Image != null && layer.Image.material == layer.Lit.Material) layer.Image.material = layer.AuthoredMaterial;
                        layer.Lit.Dispose();
                    }
                }
        }
    }
}
