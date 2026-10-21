using System;
using UnityEngine;

namespace Dice.Presentation
{
    public enum SpiritAudioCue { Roll, Spirit, Score, StageClear, Disturbance, DisturbanceBlocked }

    /** Owns cosmetic audio sources and suspension without altering global audio or game randomness. */
    [DisallowMultipleComponent]
    public sealed class SpiritEncounterAudio : MonoBehaviour
    {
        [SerializeField] private SpiritAudioTheme theme;
        [SerializeField] private bool muted;
        [SerializeField] private AudioSource ambienceSource;
        [SerializeField] private AudioSource creakSource;
        [SerializeField] private AudioSource rollSource;
        [SerializeField] private AudioSource[] cueSources = new AudioSource[4];
        private System.Random cosmeticRandom;
        private bool hasFocus;
        private bool paused;
        private float nextCreak;
        private float nextAmbientOneShot;
        private int nextCueSource;
        private AudioClip lastSpiritClip;
        private AudioClip lastDisturbanceClip;
        private AudioClip lastBlockedClip;
        private AudioClip lastAmbientOneShotClip;

        public SpiritAudioTheme Theme => theme;
        public void SetTheme(SpiritAudioTheme selectedTheme)
        {
            if (theme == selectedTheme) return;
            StopPlayback();
            theme = selectedTheme;
            lastSpiritClip = null;
            lastDisturbanceClip = null;
            lastBlockedClip = null;
            lastAmbientOneShotClip = null;
            RefreshPlayback();
        }

        public void PlayJourneyCue(AudioClip clip) => PlayClip(clip);
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
        private bool HasAmbientOneShots => theme != null && theme.ambientOneShotClips != null && theme.ambientOneShotClips.Length > 0;

        private void Awake()
        {
            hasFocus = Application.isFocused;
            Initialize();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            hasFocus = Application.isFocused;
            Initialize();
            RefreshPlayback();
        }

        private void Initialize()
        {
            if (cosmeticRandom == null) cosmeticRandom = new System.Random();
            if (ambienceSource == null || creakSource == null || rollSource == null || cueSources.Length == 0)
                throw new InvalidOperationException("Encounter audio requires authored audio sources.");
            foreach (AudioSource source in cueSources)
                if (source == null) throw new InvalidOperationException("An encounter audio cue source is missing.");
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
                case SpiritAudioCue.Spirit: clip = ChooseSpiritClip(); break;
                case SpiritAudioCue.Score: clip = theme.score; break;
                case SpiritAudioCue.StageClear: clip = theme.stageClear; break;
                case SpiritAudioCue.Disturbance: clip = lastDisturbanceClip = ChooseClip(theme.disturbanceClips, lastDisturbanceClip, null, cosmeticRandom); break;
                case SpiritAudioCue.DisturbanceBlocked: clip = lastBlockedClip = ChooseClip(theme.disturbanceBlockedClips, lastBlockedClip, null, cosmeticRandom); break;
                default: return;
            }
            PlayClip(clip);
        }

        private AudioClip ChooseSpiritClip()
        {
            lastSpiritClip = ChooseClip(theme.spiritClips, lastSpiritClip, theme.spirit, cosmeticRandom);
            return lastSpiritClip;
        }

        /** Picks a random assigned clip other than the previous one unless it is the only one; returns fallback when none are assigned. */
        public static AudioClip ChooseClip(AudioClip[] clips, AudioClip previous, AudioClip fallback, System.Random random)
        {
            AudioClip selected = null;
            int candidates = 0;
            if (clips != null)
            {
                // Reservoir selection skips empty slots and the previous clip without allocating a list.
                foreach (var clip in clips)
                    if (clip != null && clip != previous && random.Next(++candidates) == 0)
                        selected = clip;
                if (selected == null)
                    foreach (var clip in clips)
                        if (clip != null) { selected = clip; break; }
            }
            return selected != null ? selected : fallback;
        }

        /** Returns the delay before a scheduled room one-shot: uniform between the authored bounds, at least one second. */
        public static float NextInterval(float minimumInterval, float maximumInterval, System.Random random)
        {
            float minimum = Mathf.Max(1, minimumInterval);
            float maximum = Mathf.Max(minimum, maximumInterval);
            return minimum + (float)random.NextDouble() * (maximum - minimum);
        }

        private void PlayClip(AudioClip clip)
        {
            if (CanPlay) PlayClip(clip, theme.effectsVolume);
        }

        private void PlayClip(AudioClip clip, float volume)
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
            source.volume = Mathf.Clamp01(volume);
            source.Play();
        }

        private void Update()
        {
            if (!CanPlay) return;
            if (theme.creak != null && Time.unscaledTime >= nextCreak)
            {
                creakSource.clip = theme.creak;
                creakSource.volume = Mathf.Clamp01(theme.creakVolume);
                creakSource.Play();
                ScheduleCreak();
            }
            if (HasAmbientOneShots && Time.unscaledTime >= nextAmbientOneShot)
            {
                lastAmbientOneShotClip = ChooseClip(theme.ambientOneShotClips, lastAmbientOneShotClip, null, cosmeticRandom);
                // Ambient one-shots use the bounded cue voices, so mute and suspension stop them with the other cues.
                PlayClip(lastAmbientOneShotClip, theme.ambientOneShotVolume);
                ScheduleAmbientOneShot();
            }
        }

        private void ScheduleCreak()
        {
            if (theme == null || cosmeticRandom == null) return;
            nextCreak = Time.unscaledTime + NextInterval(theme.minimumCreakInterval, theme.maximumCreakInterval, cosmeticRandom);
        }

        private void ScheduleAmbientOneShot()
        {
            // Themes without ambient one-shots leave the cosmetic random sequence to the creak.
            if (!HasAmbientOneShots || cosmeticRandom == null) return;
            nextAmbientOneShot = Time.unscaledTime + NextInterval(theme.minimumAmbientOneShotInterval,
                theme.maximumAmbientOneShotInterval, cosmeticRandom);
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
            ScheduleAmbientOneShot();
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

        private void OnDestroy() => StopPlayback();

    }
}
