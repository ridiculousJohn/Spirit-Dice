using System;
using UnityEngine;

namespace Dice.Presentation
{
    public enum SpiritPresentationState { Idle = 0, Waiting = 1, Arrival = 2, Reaction = 3 }

    /** A host evaluates authored timelines independently of encounter mechanics. */
    public sealed class SpiritHostPortrait : MonoBehaviour
    {
        private sealed class LayerRuntime
        {
            public SpiritPortraitImage Image;
            public SpiritSmokeMaterial Smoke;
            public float[] Defaults;
            public float[] Values;
            public float[] EntryPose;
            public float[] EntryBaseline;
            public float[] Weights;
        }

        private SpiritHostPresentationAsset presentation;
        private RectTransform portraitRoot;
        private LayerRuntime[] layers;
        private double[] idleAges;
        private float[][] entryValues;
        private readonly float[] durations = new float[4];
        private readonly bool[] includesIdle = new bool[4];
        private Material silhouetteMaterial;
        private double stateAge;
        // Continuous effects retain their phase across dialogue and pose resets.
        private double effectAge;
        private float arrivalProgress;
        private Color sceneLightingTint = Color.white;
        private bool hasSceneLighting;

        public bool IsReacting => State == SpiritPresentationState.Reaction;
        public SpiritPresentationState State { get; private set; }
        public float Progress => State == SpiritPresentationState.Arrival ? arrivalProgress
            : durations[(int)State] > 0f ? Mathf.Clamp01((float)(stateAge / durations[(int)State])) : 0f;
        public float GestureDuration => durations[(int)SpiritPresentationState.Reaction];
        public event Action SpiritGesture;

        public void SetSceneLighting(SceneLightingState state, Vector3 position)
        {
            hasSceneLighting = state != null && state.Asset != null;
            sceneLightingTint = state != null ? state.EvaluateReceiver(position, Vector3.back) : Color.white;
            ApplyPose();
        }

        public void Build(RectTransform parent, SpiritHostPresentationAsset asset)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (portraitRoot != null) return;
            asset.Validate();
            presentation = asset;
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

            portraitRoot = SpiritUi.Rect("Spirit Portrait", parent, Vector2.zero, Vector2.zero);
            portraitRoot.pivot = Vector2.zero;
            portraitRoot.sizeDelta = asset.ReferenceSize;
            for (int i = 0; i < layers.Length; ++i)
            {
                SpiritPortraitLayer source = asset.Layers[i];
                var rect = SpiritUi.Rect(source.Id, portraitRoot, Vector2.zero, Vector2.zero);
                rect.pivot = source.Pivot;
                var image = rect.gameObject.AddComponent<SpiritPortraitImage>();
                image.Configure(source.Mesh);
                image.texture = source.Texture;
                image.raycastTarget = false;
                if (source.Silhouette)
                {
                    if (silhouetteMaterial == null)
                    {
                        if (asset.SilhouetteShader == null) throw new InvalidOperationException("Spirit silhouette shader is unavailable.");
                        silhouetteMaterial = new Material(asset.SilhouetteShader) { name = "Spirit Portrait Silhouette", hideFlags = HideFlags.DontSave };
                    }
                    image.material = silhouetteMaterial;
                }
                SpiritSmokeMaterial smoke = source.Smoke != null && source.Smoke.Enabled ? new SpiritSmokeMaterial(source.Smoke, asset.SmokeShader) : null;
                if (smoke != null) image.material = smoke.Material;
                image.enabled = source.Texture != null && (!source.Silhouette || silhouetteMaterial != null);
                int channels = source.Mesh != null ? source.Mesh.Shapes.Length : 0;
                var defaults = new float[6 + channels];
                defaults[0] = source.Position.x;
                defaults[1] = source.Position.y;
                defaults[2] = source.Size.x;
                defaults[3] = source.Size.y;
                defaults[4] = source.Rotation;
                defaults[5] = source.Tint.a;
                if (source.Weights != null && source.Weights.Length > 0)
                    Array.Copy(source.Weights, 0, defaults, 6, channels);
                layers[i] = new LayerRuntime
                {
                    Image = image, Smoke = smoke, Defaults = defaults, Values = new float[defaults.Length],
                    EntryPose = new float[defaults.Length], EntryBaseline = new float[defaults.Length],
                    Weights = new float[channels]
                };
            }
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
            for (int i = 0; i < layers.Length; ++i)
                Array.Copy(layers[i].Values, layers[i].EntryPose, layers[i].Values.Length);
            for (int i = 0; i < presentation.Sequences.Length; ++i)
                if (presentation.Sequences[i].State == SpiritPresentationState.Idle && presentation.Sequences[i].RestartOnReaction)
                    idleAges[i] = 0d;

            // Additive entry values exclude the baseline that the reaction will evaluate again.
            ResetValues(true);
            if (includesIdle[(int)SpiritPresentationState.Reaction]) SampleSequences(SpiritPresentationState.Idle, true);
            for (int i = 0; i < presentation.Sequences.Length; ++i)
            {
                SpiritPresentationSequence sequence = presentation.Sequences[i];
                if (sequence.State != SpiritPresentationState.Reaction) continue;
                for (int j = 0; j < sequence.Tracks.Length; ++j)
                {
                    SpiritPortraitTrack track = sequence.Tracks[j];
                    int value = ValueIndex(track.Property, track.Channel);
                    entryValues[i][j] = layers[track.Layer].EntryPose[value] - track.Curve.Evaluate(0f)
                        - (track.Blend == SpiritTrackBlend.Add ? layers[track.Layer].EntryBaseline[value] : 0f);
                }
            }
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
            if (presentation == null || !SpiritHostPresentationAsset.Finite(progress)) return;
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
                State = state;
                stateAge = 0d;
                Array.Clear(idleAges, 0, idleAges.Length);
                for (int i = 0; i < entryValues.Length; ++i) Array.Clear(entryValues[i], 0, entryValues[i].Length);
            }
            if (state == SpiritPresentationState.Arrival) arrivalProgress = progress;
            ApplyPose();
            if (!Visible) return;
            if (changed) EmitStartGestures(state);
            else if (state == SpiritPresentationState.Arrival && progress > previousProgress)
            {
                for (int i = 0; i < presentation.Sequences.Length; ++i)
                {
                    SpiritPresentationSequence sequence = presentation.Sequences[i];
                    if (sequence.State == state)
                        EmitCrossedGestures(sequence, previousProgress * sequence.Duration, progress * sequence.Duration);
                }
            }
        }

        private bool Visible => portraitRoot != null && portraitRoot.gameObject.activeInHierarchy;

        private void Update() => Advance(Time.unscaledDeltaTime);

        /** Evaluate an absolute idle time for authoring, including the continuous effect phase. */
        public void Sample(float seconds)
        {
            if (!Visible || !SpiritHostPresentationAsset.Finite(seconds) || seconds < 0f) return;
            effectAge = 0d;
            ResetPose();
            Advance(seconds);
        }

        public void Advance(float deltaTime)
        {
            if (!Visible || !SpiritHostPresentationAsset.Finite(deltaTime) || deltaTime < 0f) return;
            effectAge += deltaTime;
            if (State == SpiritPresentationState.Arrival)
            {
                ApplyPose();
                return;
            }
            SpiritPresentationState previousState = State;
            double previousAge = stateAge;
            stateAge += deltaTime;
            bool emit = deltaTime <= 2f;
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
            Vector2 fit = new Vector2(parent.rect.width / presentation.ReferenceSize.x, parent.rect.height / presentation.ReferenceSize.y);
            portraitRoot.localScale = new Vector3(fit.x * presentation.Scale, fit.y * presentation.Scale, 1f);
            portraitRoot.anchoredPosition = Vector2.Scale(presentation.Offset, fit);
            ResetValues(false);
            if (State == SpiritPresentationState.Idle || includesIdle[(int)State])
                SampleSequences(SpiritPresentationState.Idle, false);
            if (State != SpiritPresentationState.Idle) SampleSequences(State, false);
            for (int i = 0; i < layers.Length; ++i)
            {
                LayerRuntime layer = layers[i];
                float[] values = layer.Values;
                RectTransform rect = layer.Image.rectTransform;
                rect.anchoredPosition = new Vector2(values[0], values[1]);
                rect.sizeDelta = new Vector2(values[2], values[3]);
                rect.localRotation = Quaternion.Euler(0f, 0f, values[4]);
                Color tint = presentation.Layers[i].Tint;
                if (hasSceneLighting && !presentation.Layers[i].Silhouette && presentation.Layers[i].ReceiveSceneLighting)
                    tint = (tint.linear * sceneLightingTint).gamma;
                tint.a = Mathf.Clamp01(values[5]);
                layer.Image.color = tint;
                if (layer.Weights.Length > 0) Array.Copy(values, 6, layer.Weights, 0, layer.Weights.Length);
                layer.Image.SetWeights(layer.Weights);
                layer.Smoke?.ApplyTime(effectAge, layer.Image);
            }
        }

        private void ResetValues(bool baseline)
        {
            for (int i = 0; i < layers.Length; ++i)
                Array.Copy(layers[i].Defaults, baseline ? layers[i].EntryBaseline : layers[i].Values, layers[i].Defaults.Length);
        }

        private void SampleSequences(SpiritPresentationState state, bool baseline)
        {
            for (int i = 0; i < presentation.Sequences.Length; ++i)
            {
                SpiritPresentationSequence sequence = presentation.Sequences[i];
                if (sequence.State != state) continue;
                double age = State == SpiritPresentationState.Arrival ? arrivalProgress * durations[(int)SpiritPresentationState.Arrival]
                    : state == SpiritPresentationState.Idle ? idleAges[i] : stateAge;
                float time = state == SpiritPresentationState.Arrival ? arrivalProgress * sequence.Duration
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
            => property == SpiritPortraitProperty.Deformation ? 6 + channel : (int)property;

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
                    if (layer != null && layer.Smoke != null)
                    {
                        if (layer.Image != null) layer.Image.material = null;
                        layer.Smoke.Dispose();
                    }
            if (portraitRoot != null) DestroyOwned(portraitRoot.gameObject);
            if (silhouetteMaterial != null) DestroyOwned(silhouetteMaterial);
        }

        private static void DestroyOwned(UnityEngine.Object owned)
        {
            if (Application.isPlaying) Destroy(owned);
            else DestroyImmediate(owned);
        }
    }
}
