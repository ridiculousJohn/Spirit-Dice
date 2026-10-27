using System.Collections.Generic;
using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class SpiritEncounterAudioTests
    {
        private readonly List<Object> resources = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var resource in resources)
                if (resource != null) Object.DestroyImmediate(resource);
            resources.Clear();
        }

        /** Checks clip choice skips empty slots, never repeats the previous clip and reaches every assigned clip / fails if a slot is empty, a clip plays twice in a row or one is never chosen. */
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

        /** Checks a lone clip may repeat and missing or empty lists fall back / fails if a single clip goes silent or an empty list ignores its fallback. */
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

        /** Checks an interval maps exactly one cosmetic draw uniformly onto its range / fails if scheduling consumes extra draws or leaves the range. */
        [Test]
        public void IntervalMapsOneCosmeticDrawOntoItsRange()
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

        /** Checks intervals last at least one second and an inverted range uses its minimum / fails if a short or inverted range schedules too soon. */
        [Test]
        public void IntervalsLastAtLeastOneSecondAndInvertedRangesUseTheirMinimum()
        {
            var random = new System.Random(911);
            Assert.That(SpiritEncounterAudio.NextInterval(0, .5f, random), Is.EqualTo(1f));
            Assert.That(SpiritEncounterAudio.NextInterval(40, 30, random), Is.EqualTo(40f));
        }

        /** Checks a voice pool rotates through its voices and clamps volume to one / fails if a voice is skipped or reused out of turn, or a volume above one is kept. */
        [Test]
        public void VoicePoolRotatesThroughItsVoicesAndClampsVolume()
        {
            var root = new GameObject("Voices");
            resources.Add(root);
            var voices = Enumerable.Range(0, 3).Select(index =>
            {
                var child = new GameObject("Voice " + index);
                child.transform.SetParent(root.transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                return source;
            }).ToArray();
            // A long clip keeps any voice the editor really plays busy for the whole loop.
            var clip = AudioClip.Create("knock", 44100 * 5, 1, 44100, false);
            resources.Add(clip);
            int next = 0;
            for (int play = 0; play < 9; play++)
            {
                var source = SpiritEncounterAudio.PlayOnVoice(voices, ref next, clip, 1.5f);
                Assert.That(source, Is.SameAs(voices[play % voices.Length]), "Voices rotate through the pool.");
                Assert.That(source.clip, Is.SameAs(clip));
                Assert.That(source.volume, Is.EqualTo(1f), "Volumes clamp to one.");
                Assert.That(next, Is.EqualTo((play + 1) % voices.Length));
            }
            foreach (var source in voices) source.Stop();
        }

        private AudioClip Clip(string name)
        {
            var clip = AudioClip.Create(name, 4410, 1, 44100, false);
            resources.Add(clip);
            return clip;
        }
    }
}
