using System;
using UnityEngine;

namespace Dice.Presentation
{
    public enum SpiritAudioCue { Roll, Spirit, Score, StageClear }

    /** Owns cosmetic audio sources and suspension without altering global audio or game randomness. */
    [DisallowMultipleComponent]
    public sealed class SpiritEncounterAudio : MonoBehaviour
    {
        [SerializeField] private SpiritAudioTheme theme;
        [SerializeField] private bool muted;
        private GameObject sourceOwner;
        private AudioSource ambienceSource;
        private AudioSource creakSource;
        private AudioSource rollSource;
        private readonly AudioSource[] cueSources = new AudioSource[4];
        private System.Random cosmeticRandom;
        private bool hasFocus;
        private bool paused;
        private float nextCreak;
        private int nextCueSource;

        public SpiritAudioTheme Theme => theme;
        public bool Muted
        {
            get => muted;
            set
            {
                if (muted == value) return;
                muted = value;
                RefreshPlayback();
            }
        }

        private bool CanPlay => isActiveAndEnabled && !muted && hasFocus && !paused && theme != null;

        private void Awake()
        {
            hasFocus = Application.isFocused;
            Initialize();
        }

        private void OnEnable()
        {
            hasFocus = Application.isFocused;
            Initialize();
            RefreshPlayback();
        }

        private void Initialize()
        {
            if (theme == null) theme = Resources.Load<SpiritAudioTheme>("SpiritAudio/SpiritAudioTheme");
            if (cosmeticRandom == null)
                cosmeticRandom = new System.Random();
            if (sourceOwner != null) return;

            sourceOwner = new GameObject("Spirit Audio Sources");
            sourceOwner.transform.SetParent(transform, false);
            ambienceSource = CreateSource(true);
            creakSource = CreateSource(false);
            rollSource = CreateSource(false);
            for (int i = 0; i < cueSources.Length; i++) cueSources[i] = CreateSource(false);
        }

        private AudioSource CreateSource(bool loop)
        {
            var source = sourceOwner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.loop = loop;
            source.priority = loop ? 192 : 128;
            return source;
        }

        /** Returns the visual roll duration even when audio is muted or suspended. */
        public float PlayRoll(int diceCount)
            => BeginRoll(diceCount).Duration;

        public SpiritRollTiming BeginRoll(int diceCount)
        {
            Initialize();
            AudioClip clip = theme != null && theme.rollClips != null && diceCount > 0 && diceCount <= theme.rollClips.Length
                ? theme.rollClips[diceCount - 1] : null;
            SpiritRollContactTrack track = theme != null && theme.rollContactTracks != null
                && diceCount > 0 && diceCount <= theme.rollContactTracks.Length
                ? theme.rollContactTracks[diceCount - 1] : null;
            var timing = new SpiritRollTiming(clip != null ? clip.length : .4f, track,
                theme != null ? theme.rollLaunchTime : .15f, Time.unscaledTimeAsDouble, AudioSettings.dspTime);
            if (CanPlay && clip != null)
            {
                rollSource.Stop();
                rollSource.clip = clip;
                rollSource.volume = Mathf.Clamp01(theme.effectsVolume);
                rollSource.timeSamples = Mathf.Clamp(Mathf.RoundToInt(timing.RecordingStartTime * clip.frequency),
                    0, Mathf.Max(0, clip.samples - 1));
                rollSource.PlayScheduled(timing.AudioStartDspTime);
            }
            return timing;
        }

        public void Play(SpiritAudioCue cue)
        {
            if (!CanPlay) return;
            AudioClip clip;
            switch (cue)
            {
                case SpiritAudioCue.Roll:
                    PlayRoll(5);
                    return;
                case SpiritAudioCue.Spirit: clip = theme.spirit; break;
                case SpiritAudioCue.Score: clip = theme.score; break;
                case SpiritAudioCue.StageClear: clip = theme.stageClear; break;
                default: return;
            }
            PlayClip(clip);
        }

        private void PlayClip(AudioClip clip)
        {
            if (!CanPlay || clip == null) return;

            // Reuse a free voice first; a fixed pool bounds overlapping cosmetic cues.
            int selected = nextCueSource;
            for (int i = 0; i < cueSources.Length; i++)
            {
                int candidate = (nextCueSource + i) % cueSources.Length;
                if (!cueSources[candidate].isPlaying) { selected = candidate; break; }
            }
            var source = cueSources[selected];
            nextCueSource = (selected + 1) % cueSources.Length;
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(theme.effectsVolume);
            source.Play();
        }

        private void Update()
        {
            if (!CanPlay || theme.creak == null || Time.unscaledTime < nextCreak) return;
            creakSource.clip = theme.creak;
            creakSource.volume = Mathf.Clamp01(theme.creakVolume);
            creakSource.Play();
            ScheduleCreak();
        }

        private void ScheduleCreak()
        {
            if (theme == null || cosmeticRandom == null) return;
            float minimum = Mathf.Max(1, theme.minimumCreakInterval);
            float maximum = Mathf.Max(minimum, theme.maximumCreakInterval);
            nextCreak = Time.unscaledTime + minimum + (float)cosmeticRandom.NextDouble() * (maximum - minimum);
        }

        private void RefreshPlayback()
        {
            if (!CanPlay) { StopPlayback(); return; }
            if (ambienceSource == null) return;
            ambienceSource.volume = Mathf.Clamp01(theme.ambienceVolume);
            if (ambienceSource.clip != theme.ambience)
            {
                ambienceSource.Stop();
                ambienceSource.clip = theme.ambience;
            }
            if (ambienceSource.clip != null && !ambienceSource.isPlaying) ambienceSource.Play();
            ScheduleCreak();
        }

        private void StopPlayback()
        {
            if (ambienceSource != null) ambienceSource.Stop();
            if (creakSource != null) creakSource.Stop();
            if (rollSource != null) rollSource.Stop();
            foreach (var source in cueSources) if (source != null) source.Stop();
        }

        private void OnApplicationFocus(bool focus)
        {
            hasFocus = focus;
            RefreshPlayback();
        }

        private void OnApplicationPause(bool pause)
        {
            paused = pause;
            RefreshPlayback();
        }

        private void OnDisable() => StopPlayback();

        private void OnDestroy()
        {
            StopPlayback();
            if (sourceOwner == null) return;
            if (Application.isPlaying) Destroy(sourceOwner);
            else DestroyImmediate(sourceOwner);
        }
    }
}
