using System.Collections.Generic;
using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class SpiritEncounterAudioTests
    {
        private const string MerchantAudioPath = "Assets/Dice/Content/Presentation/HauntedMerchant/MerchantAudio.asset";
        private readonly List<Object> resources = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var resource in resources)
                if (resource != null) Object.DestroyImmediate(resource);
            resources.Clear();
        }

        [Test]
        public void ClipChoiceNeverRepeatsImmediatelyAndReachesEveryAssignedClip()
        {
            var clips = new[] { Clip("first"), null, Clip("second"), Clip("third") };
            var random = new System.Random(4127);
            var counts = new Dictionary<AudioClip, int>();
            AudioClip previous = null;
            for (int draw = 0; draw < 300; draw++)
            {
                var selected = SpiritEncounterAudio.ChooseClip(clips, previous, null, random);
                Assert.That(selected, Is.Not.Null, "Empty slots must be skipped.");
                Assert.That(selected, Is.Not.SameAs(previous), "A clip must not play twice in a row.");
                counts[selected] = counts.TryGetValue(selected, out int count) ? count + 1 : 1;
                previous = selected;
            }
            Assert.That(counts.Keys, Is.EquivalentTo(clips.Where(clip => clip != null)));
            foreach (var count in counts.Values) Assert.That(count, Is.GreaterThan(60));
        }

        [Test]
        public void SingleClipRepeatsAndUnassignedListsUseTheFallback()
        {
            var only = Clip("only");
            var fallback = Clip("fallback");
            var random = new System.Random(5);
            Assert.That(SpiritEncounterAudio.ChooseClip(new[] { null, only }, only, null, random), Is.SameAs(only));
            Assert.That(SpiritEncounterAudio.ChooseClip(null, null, fallback, random), Is.SameAs(fallback));
            Assert.That(SpiritEncounterAudio.ChooseClip(new AudioClip[0], null, fallback, random), Is.SameAs(fallback));
            Assert.That(SpiritEncounterAudio.ChooseClip(new AudioClip[] { null }, null, null, random), Is.Null);
        }

        [Test]
        public void ScheduledIntervalsSpanTheAuthoredRange()
        {
            var random = new System.Random(911);
            float shortest = float.MaxValue, longest = float.MinValue;
            for (int draw = 0; draw < 1000; draw++)
            {
                float interval = SpiritEncounterAudio.NextInterval(25, 60, random);
                Assert.That(interval, Is.InRange(25f, 60f));
                shortest = Mathf.Min(shortest, interval);
                longest = Mathf.Max(longest, interval);
            }
            Assert.That(shortest, Is.LessThan(27f));
            Assert.That(longest, Is.GreaterThan(58f));
            Assert.That(SpiritEncounterAudio.NextInterval(0, .5f, random), Is.EqualTo(1f), "Intervals must be at least one second.");
            Assert.That(SpiritEncounterAudio.NextInterval(40, 30, random), Is.EqualTo(40f), "An inverted range uses its minimum.");
        }

        [Test]
        public void IntervalUsesOneCosmeticDrawLikeTheCreakSchedule()
        {
            var random = new System.Random(77);
            var reference = new System.Random(77);
            for (int draw = 0; draw < 20; draw++)
            {
                float interval = SpiritEncounterAudio.NextInterval(17, 38, random);
                Assert.That(interval, Is.EqualTo(17 + (float)reference.NextDouble() * 21).Within(.0001f));
            }
            Assert.That(random.Next(), Is.EqualTo(reference.Next()), "Scheduling must consume exactly one draw.");
        }

        [Test]
        public void MerchantThemeAssignsPoltergeistClipsAndQuietAmbientOneShots()
        {
            var theme = AssetDatabase.LoadAssetAtPath<SpiritAudioTheme>(MerchantAudioPath);
            Assert.That(theme, Is.Not.Null);
            Assert.That(theme.disturbanceClips.Select(clip => clip.name),
                Is.EqualTo(new[] { "poltergeist_giggle_01", "poltergeist_giggle_02" }));
            Assert.That(theme.disturbanceBlockedClips.Select(clip => clip.name), Is.EqualTo(new[] { "poltergeist_grumble_01" }));
            Assert.That(theme.ambientOneShotClips.Select(clip => clip.name), Is.EqualTo(new[]
                { "poltergeist_ambient_giggle_01", "poltergeist_ambient_giggle_02", "poltergeist_ambient_giggle_03" }));
            foreach (var clip in theme.disturbanceClips.Concat(theme.disturbanceBlockedClips).Concat(theme.ambientOneShotClips))
            {
                Assert.That(clip.channels, Is.EqualTo(1), clip.name);
                Assert.That(clip.frequency, Is.EqualTo(44100), clip.name);
                Assert.That(clip.length, Is.InRange(.5f, 3f), clip.name);
            }
            Assert.That(theme.ambientOneShotVolume, Is.GreaterThan(0f).And.LessThan(theme.effectsVolume));
            Assert.That(theme.minimumAmbientOneShotInterval, Is.EqualTo(25f));
            Assert.That(theme.maximumAmbientOneShotInterval, Is.EqualTo(60f));
        }

        [Test]
        public void SharedThemeHasNoAmbientOneShots()
        {
            var theme = AssetDatabase.LoadAssetAtPath<SpiritAudioTheme>("Assets/Dice/Presentation/Shared/SpiritAudio/SpiritAudioTheme.asset");
            Assert.That(theme, Is.Not.Null);
            Assert.That(theme.ambientOneShotClips == null || theme.ambientOneShotClips.Length == 0, Is.True,
                "Themes without authored ambient one-shots keep their previous audio.");
        }

        private AudioClip Clip(string name)
        {
            var clip = AudioClip.Create(name, 4410, 1, 44100, false);
            resources.Add(clip);
            return clip;
        }
    }
}
