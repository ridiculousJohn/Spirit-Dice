using System;
using UnityEngine;

namespace Dice.Presentation
{
    public enum SpiritAudioCue { Spirit, Score, StageClear }

    /** Owns cosmetic audio sources and suspension without altering global audio or game randomness. */
    [DisallowMultipleComponent]
    public sealed class SpiritEncounterAudio : MonoBehaviour, IAmbientSoundTarget
    {
        // Seconds a roll lasts when the theme has no recording for its dice count.
        private const float UnrecordedRollDuration = .4f;
        // Seconds before a roll's dice launch when the encounter has no audio theme.
        private const float UnthemedRollLaunchTime = .15f;
        [SerializeField] private SpiritAudioTheme theme;
        [SerializeField] private bool muted;
        [SerializeField] private AudioSource ambienceSource;
        [SerializeField] private AudioSource creakSource;
        [SerializeField] private AudioSource rollSource;
        [SerializeField] private AudioSource[] cueSources = new AudioSource[4];
        [Tooltip("Voices for ambient prop and burst sounds, kept apart from the cue voices so a busy room never drops a scoring cue.")]
        [SerializeField] private AudioSource[] propSources = new AudioSource[3];
        private System.Random cosmeticRandom;
        private bool hasFocus;
        private bool paused;
        private float nextCreak;
        private float nextAmbientOneShot;
        private int nextCueSource;
        private int nextPropSource;
        private AudioClip lastSpiritClip;
        private AudioClip lastAmbientOneShotClip;

        public SpiritAudioTheme Theme => theme;
        public void SetTheme(SpiritAudioTheme selectedTheme)
        {
            if (theme == selectedTheme) return;
            StopPlayback();
            theme = selectedTheme;
            lastSpiritClip = null;
            lastAmbientOneShotClip = null;
            RefreshPlayback();
        }

        /** Plays an authored clip, such as a journey beat's or a rule presenter's, on the cue voices at the effects volume. */
        public void PlayCue(AudioClip clip) => PlayClip(clip);
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
            if (ambienceSource == null || creakSource == null || rollSource == null || cueSources == null || cueSources.Length == 0
                || propSources == null || propSources.Length == 0)
                throw new InvalidOperationException("Encounter audio requires authored audio sources.");
            foreach (AudioSource source in cueSources)
                if (source == null) throw new InvalidOperationException("An encounter audio cue source is missing.");
            foreach (AudioSource source in propSources)
                if (source == null || Array.IndexOf(cueSources, source) >= 0)
                    throw new InvalidOperationException("Encounter audio prop sources must be authored apart from the cue sources.");
        }

        /** Times a roll of the dice count and schedules its recording; the timing holds even when audio is muted or suspended. */
        public SpiritRollTiming BeginRoll(int diceCount)
        {
            Initialize();
            AudioClip clip = theme != null && theme.rollClips != null && diceCount > 0 && diceCount <= theme.rollClips.Length
                ? theme.rollClips[diceCount - 1] : null;
            SpiritRollContactTrack track = theme != null && theme.rollContactTracks != null
                && diceCount > 0 && diceCount <= theme.rollContactTracks.Length
                ? theme.rollContactTracks[diceCount - 1] : null;
            var timing = new SpiritRollTiming(clip != null ? clip.length : UnrecordedRollDuration, track,
                theme != null ? theme.rollLaunchTime : UnthemedRollLaunchTime, Time.unscaledTimeAsDouble, AudioSettings.dspTime);
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
                case SpiritAudioCue.Spirit: clip = ChooseSpiritClip(); break;
                case SpiritAudioCue.Score: clip = theme.score; break;
                case SpiritAudioCue.StageClear: clip = theme.stageClear; break;
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
            PlayOnVoice(cueSources, ref nextCueSource, clip, volume);
        }

        /** Plays an ambient prop or burst sound on the prop voices, under the same mute and suspension as every cue. */
        public void PlayAmbientSound(AudioClip clip, float volume)
        {
            if (!CanPlay || clip == null) return;
            PlayOnVoice(propSources, ref nextPropSource, clip, volume);
        }

        /** Plays the clip on one of the voices and returns it; nextVoice advances past the voice used. */
        public static AudioSource PlayOnVoice(AudioSource[] voices, ref int nextVoice, AudioClip clip, float volume)
        {
            // Reuse a free voice first; a fixed pool bounds overlapping cosmetic sounds.
            int selected = nextVoice % voices.Length;
            for (int i = 0; i < voices.Length; i++)
            {
                int candidate = (nextVoice + i) % voices.Length;
                if (!voices[candidate].isPlaying) { selected = candidate; break; }
            }
            var source = voices[selected];
            nextVoice = (selected + 1) % voices.Length;
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.Play();
            return source;
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
            if (propSources != null) foreach (var source in propSources) if (source != null) source.Stop();
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
