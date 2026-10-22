using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dice.Tests
{
    public sealed class SpiritAmbientAudioPlayTests
    {
        private readonly List<Object> resources = new List<Object>();
        private GameObject root;
        private SpiritEncounterAudio sound;
        private AudioSource[] cues;
        private AudioSource[] props;
        private AudioClip[] ambientClips;
        private GameObject listener;

        /**
         * Playing voices log a warning without an audio listener. One listener serves the whole fixture, apart from
         * each test's root, unless an earlier fixture's scene already supplies one.
         */
        [OneTimeSetUp]
        public void ProvideListener()
        {
            if (Object.FindAnyObjectByType<AudioListener>() == null)
                listener = new GameObject("AmbientAudioTestListener", typeof(AudioListener));
        }

        [OneTimeTearDown]
        public void RemoveListener()
        {
            if (listener != null) Object.Destroy(listener);
        }

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("AmbientAudioTest");
            resources.Add(root);
            root.SetActive(false);
            sound = root.AddComponent<SpiritEncounterAudio>();
            Set("ambienceSource", Source("Ambience"));
            Set("creakSource", Source("Creak"));
            Set("rollSource", Source("Roll"));
            cues = Enumerable.Range(0, 4).Select(index => Source("Cue" + index)).ToArray();
            Set("cueSources", cues);
            props = Enumerable.Range(0, 3).Select(index => Source("Prop" + index)).ToArray();
            Set("propSources", props);
            ambientClips = Enumerable.Range(1, 3).Select(index => Clip("ambient-" + index)).ToArray();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var resource in resources)
                if (resource != null) Object.Destroy(resource);
            resources.Clear();
        }

        [UnityTest]
        public IEnumerator AmbientOneShotsFollowTheCreakScheduleAndSuspension()
        {
            var theme = Theme(ambientClips);
            Set("theme", theme);
            root.SetActive(true);
            sound.SendMessage("OnApplicationFocus", true);
            AssertScheduled();
            Assert.That(cues.Any(source => source.isPlaying), Is.False, "Nothing plays before the first interval.");

            var previous = new List<AudioClip>();
            for (int draw = 0; draw < 6; draw++)
            {
                Due();
                yield return null;
                var playing = cues.Where(source => source.isPlaying && ambientClips.Contains(source.clip)).ToArray();
                Assert.That(playing, Has.Length.EqualTo(1), "A due one-shot must play on one cue voice.");
                Assert.That(playing[0].volume, Is.EqualTo(theme.ambientOneShotVolume).Within(.0001f));
                if (previous.Count > 0) Assert.That(playing[0].clip, Is.Not.SameAs(previous[previous.Count - 1]));
                previous.Add(playing[0].clip);
                AssertScheduled();
                foreach (var source in cues) source.Stop();
            }

            sound.Muted = true;
            AssertSilentWhileDue();
            yield return null;
            AssertSilent();
            sound.Muted = false;
            AssertScheduled();

            sound.SendMessage("OnApplicationFocus", false);
            AssertSilentWhileDue();
            yield return null;
            AssertSilent();
            sound.SendMessage("OnApplicationFocus", true);
            AssertScheduled();

            sound.SendMessage("OnApplicationPause", true);
            AssertSilentWhileDue();
            yield return null;
            AssertSilent();
            sound.SendMessage("OnApplicationPause", false);
            AssertScheduled();

            Due();
            yield return null;
            Assert.That(cues.Any(source => source.isPlaying), Is.True);
            sound.enabled = false;
            AssertSilent();
            Due();
            yield return null;
            AssertSilent();
            sound.enabled = true;
            // Re-enabling samples the editor's real focus; restore test focus only when the editor lacks it.
            if (!Application.isFocused) sound.SendMessage("OnApplicationFocus", true);
            AssertScheduled();

            Due();
            yield return null;
            Assert.That(cues.Any(source => source.isPlaying), Is.True);
            Object.Destroy(sound);
            yield return null;
            AssertSilent();
            Assert.That(theme != null && ambientClips.All(clip => clip != null), Is.True,
                "Destroying the component must not unload the theme or its clips.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ThemesWithoutAmbientOneShotsLeaveTheCueVoicesIdle()
        {
            Set("theme", Theme(null));
            root.SetActive(true);
            sound.SendMessage("OnApplicationFocus", true);
            for (int frame = 0; frame < 3; frame++)
            {
                Due();
                yield return null;
                AssertSilent();
            }
            sound.SetTheme(Theme(new AudioClip[0]));
            Due();
            yield return null;
            AssertSilent();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PropSoundsUseTheirOwnVoicesAndFollowMuteAndSuspension()
        {
            Set("theme", Theme(null));
            root.SetActive(true);
            sound.SendMessage("OnApplicationFocus", true);
            var knock = ambientClips[0];
            for (int play = 0; play < 5; play++) sound.PlayAmbientSound(knock, .4f);
            yield return null;
            Assert.That(props.Count(source => source.isPlaying && source.clip == knock), Is.EqualTo(props.Length),
                "Overlapping prop sounds fill the prop voices.");
            Assert.That(props.All(source => Mathf.Approximately(source.volume, .4f)), Is.True);
            Assert.That(cues.Any(source => source.isPlaying || source.clip != null), Is.False, "Prop sounds never take a cue voice.");

            sound.Muted = true;
            Assert.That(props.Any(source => source.isPlaying), Is.False, "Muting stops the prop voices.");
            sound.PlayAmbientSound(knock, .4f);
            yield return null;
            Assert.That(props.Any(source => source.isPlaying), Is.False, "Muted prop sounds stay silent.");
            sound.Muted = false;

            sound.SendMessage("OnApplicationPause", true);
            sound.PlayAmbientSound(knock, .4f);
            yield return null;
            Assert.That(props.Any(source => source.isPlaying), Is.False, "Suspended prop sounds stay silent.");
            sound.SendMessage("OnApplicationPause", false);
            sound.PlayAmbientSound(knock, .4f);
            yield return null;
            Assert.That(props.Count(source => source.isPlaying), Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        private SpiritAudioTheme Theme(AudioClip[] clips)
        {
            var theme = ScriptableObject.CreateInstance<SpiritAudioTheme>();
            resources.Add(theme);
            theme.ambientOneShotClips = clips;
            theme.ambientOneShotVolume = .3f;
            theme.minimumAmbientOneShotInterval = 25;
            theme.maximumAmbientOneShotInterval = 60;
            return theme;
        }

        private void AssertScheduled()
        {
            float delay = (float)Field("nextAmbientOneShot") - Time.unscaledTime;
            Assert.That(delay, Is.InRange(24f, 60f), "One-shots must be scheduled within the authored interval.");
        }

        private void AssertSilentWhileDue()
        {
            AssertSilent();
            Due();
        }

        private void AssertSilent()
        {
            foreach (var source in cues)
                Assert.That(source == null || !source.isPlaying, Is.True, "Suspended audio must stay silent.");
        }

        private void Due() => Set("nextAmbientOneShot", 0f);

        private AudioSource Source(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        private AudioClip Clip(string name)
        {
            var clip = AudioClip.Create(name, 44100, 1, 44100, false);
            resources.Add(clip);
            return clip;
        }

        private object Field(string name)
            => typeof(SpiritEncounterAudio).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sound);

        private void Set(string name, object value)
            => typeof(SpiritEncounterAudio).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sound, value);
    }
}
