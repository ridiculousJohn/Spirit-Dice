using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dice.Presentation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    public sealed class EncounterAmbientActivityTests
    {
        private const float Step = 1f / 60f;
        private const string TablePath = "Assets/Dice/Content/Presentation/HauntedMerchant/MerchantUnquietCartTable.asset";
        private const string CupPrefabPath = "Assets/Dice/Content/Presentation/Ambient/HangingCup.prefab";
        private const string PreviewScenePath = "Assets/Scenes/AmbientMotionPreview.unity";
        private const string PropsFolder = "Assets/Dice/Art/HauntedMerchant/Props/";
        private const string RelightFolder = "Assets/Dice/Art/HauntedMerchant/Relight/";
        private const string ResponsePath = "Assets/Dice/Art/HauntedMerchant/MerchantLanternResponse.png";
        private const string LitShaderPath = "Assets/Dice/Presentation/Shared/SceneLitImage.shader";
        private const string PropAudioFolder = "Assets/Dice/Presentation/Shared/SpiritAudio/MerchantProps/";
        private const string MerchantAudioPath = "Assets/Dice/Content/Presentation/HauntedMerchant/MerchantAudio.asset";
        private const float PlateWidth = 941f, PlateHeight = 1672f;
        private static readonly (string Id, string Name)[] PropActors =
        {
            ("hanging-pot", "HangingPot"), ("hanging-pan", "HangingPan"), ("shelf-bottle", "ShelfBottle"), ("trunk-lid", "TrunkLid"),
        };
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                if (value != null) Object.DestroyImmediate(value);
            created.Clear();
        }

        [Test]
        public void IdleOutagesFollowTheSeededScheduleAndRestoreExactlyFullIntensity()
        {
            var actor = new AmbientLightActor { LightIndex = 1 };
            var asset = Activity(new[] { actor }, Array.Empty<AmbientPropActor>());
            var lighting = new RecordingLighting(2);
            var activity = new EncounterAmbientActivity(asset, lighting, Array.Empty<EncounterAmbientPropBinding>());
            activity.Begin(12345);
            Assert.That(lighting.Values[1], Is.EqualTo(1f), "The light starts on.");
            Run(activity, lighting, 900f);

            Assert.That(lighting.Writes.Where(write => write.Index == 0), Is.Empty, "Only the authored light is driven.");
            var writes = lighting.Writes.Where(write => write.Index == 1).ToList();
            Assert.That(writes.All(write => write.Value >= 0f && write.Value <= 1f), Is.True);
            var outages = new List<(double Start, double End, double Dark)>();
            double start = -1, dark = 0;
            for (int i = 0; i < writes.Count; ++i)
            {
                if (start < 0 && writes[i].Value < 1f) start = writes[i].Time;
                if (writes[i].Value == 0f && i + 1 < writes.Count) dark = writes[i + 1].Time - writes[i].Time;
                if (start >= 0 && writes[i].Value == 1f)
                {
                    outages.Add((start, writes[i].Time, dark));
                    start = -1;
                }
            }
            Assert.That(outages.Count, Is.GreaterThanOrEqualTo(15));
            double previousEnd = 0;
            foreach (var outage in outages)
            {
                Assert.That(outage.Start - previousEnd, Is.InRange(20 - 2 * Step, 40 + 2 * Step), "Idle interval");
                Assert.That(outage.Dark, Is.InRange(1.5f - 2 * Step, 4f + 2 * Step), "Dark duration");
                Assert.That(outage.End - outage.Start, Is.LessThanOrEqualTo(4f + 12 * .14f + 2 * Step), "Outage length");
                previousEnd = outage.End;
            }
            Assert.That(lighting.Values[1] == 1f || start >= 0, Is.True, "Between outages the light rests at exactly one.");

            var repeat = new RecordingLighting(2);
            var again = new EncounterAmbientActivity(asset, repeat, Array.Empty<EncounterAmbientPropBinding>());
            again.Begin(12345);
            Run(again, repeat, 900f);
            Assert.That(repeat.Writes, Is.EqualTo(lighting.Writes), "The presentation random is seeded at activation.");
            var other = new RecordingLighting(2);
            var different = new EncounterAmbientActivity(asset, other, Array.Empty<EncounterAmbientPropBinding>());
            different.Begin(54321);
            Run(different, other, 900f);
            Assert.That(other.Writes, Is.Not.EqualTo(lighting.Writes));
        }

        [Test]
        public void StoppingDuringAnOutageRestoresFullIntensityAndRestPoses()
        {
            var light = new AmbientLightActor { LightIndex = 0, IdleInterval = new AmbientRange(1f, 1f) };
            var prop = Prop("Cup");
            var asset = Activity(new[] { light }, new[] { PropActor("cup", Swing(10f)) });
            var lighting = new RecordingLighting(1);
            var activity = new EncounterAmbientActivity(asset, lighting, new[] { Binding("cup", prop) });
            activity.Begin(9);
            RunUntil(activity, lighting, () => lighting.Values[0] == 0f, 10f);
            activity.Disturb();
            Run(activity, lighting, .2f);
            Assert.That(prop.localRotation, Is.Not.EqualTo(Quaternion.identity));
            activity.Stop();
            Assert.That(lighting.Values[0], Is.EqualTo(1f));
            Assert.That(prop.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(prop.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(activity.IsRunning, Is.False);
            Assert.That(activity.IsAtRest, Is.True);
            int writes = lighting.Writes.Count;
            Run(activity, lighting, 60f);
            Assert.That(lighting.Writes.Count, Is.EqualTo(writes), "A stopped activity stays still.");
        }

        [Test]
        public void DisturbanceStuttersTheLightAndJoltsExactlyOneProp()
        {
            var light = new AmbientLightActor { LightIndex = 0, IdleInterval = new AmbientRange(1000f, 1000f) };
            var swing = Swing(10f);
            string[] ids = { "a", "b", "c" };
            var asset = Activity(new[] { light }, ids.Select(id => PropActor(id, swing)).ToArray());
            var struck = new HashSet<string>();
            for (int seed = 1; seed <= 8; ++seed)
            {
                var props = ids.Select(Prop).ToArray();
                var lighting = new RecordingLighting(1);
                var activity = new EncounterAmbientActivity(asset, lighting, ids.Select((id, i) => Binding(id, props[i])).ToArray());
                activity.Disturb();
                Assert.That(lighting.Writes, Is.Empty, "Nothing happens before the first hand.");
                activity.Begin(seed);
                Run(activity, lighting, 1f);
                int before = lighting.Writes.Count;
                activity.Disturb();
                Run(activity, lighting, .1f);
                var moved = props.Where(prop => prop.localRotation != Quaternion.identity).ToArray();
                Assert.That(moved, Has.Length.EqualTo(1), "Seed " + seed);
                struck.Add(moved[0].name);
                Run(activity, lighting, 1f);
                var stutter = lighting.Writes.Skip(before).ToList();
                Assert.That(stutter.Min(write => write.Value), Is.LessThan(1f), "The light stutters.");
                Assert.That(stutter.Last().Value, Is.EqualTo(1f));
                for (int i = 0; i + 1 < stutter.Count; ++i)
                    Assert.That(stutter[i + 1].Time - stutter[i].Time, Is.LessThanOrEqualTo(.14f + 2 * Step), "A stutter never holds dark.");
                Assert.That(stutter.Last().Time - stutter.First().Time, Is.LessThanOrEqualTo(4 * .14f + 2 * Step));
                Run(activity, lighting, 30f);
                Assert.That(props.All(prop => prop.localRotation == Quaternion.identity && prop.localPosition == Vector3.zero), Is.True);
                Assert.That(activity.IsAtRest, Is.True);
            }
            Assert.That(struck.Count, Is.GreaterThan(1), "The jolted prop is chosen at random.");
        }

        [Test]
        public void VictorySettlesLightsAndPropsAndStopsIdleEvents()
        {
            var light = new AmbientLightActor { LightIndex = 0, IdleInterval = new AmbientRange(2f, 3f) };
            var prop = Prop("Cup");
            var asset = Activity(new[] { light },
                new[] { PropActor("cup", Swing(10f), new AmbientRange(1f, 2f)) });
            var lighting = new RecordingLighting(1);
            var activity = new EncounterAmbientActivity(asset, lighting, new[] { Binding("cup", prop) });
            activity.Begin(7);
            RunUntil(activity, lighting, () => lighting.Values[0] == 0f, 10f);
            activity.Disturb();
            Run(activity, lighting, .2f);
            Assert.That(prop.localRotation, Is.Not.EqualTo(Quaternion.identity));

            int settled = lighting.Writes.Count;
            activity.Settle();
            Assert.That(activity.IsRunning, Is.False);
            Run(activity, lighting, 1f + 2 * Step);
            var recovery = lighting.Writes.Skip(settled).Select(write => write.Value).ToArray();
            Assert.That(recovery, Is.Ordered, "Lights return steadily, without flicker.");
            Assert.That(lighting.Values[0], Is.EqualTo(1f));
            Run(activity, lighting, 5f);
            Assert.That(prop.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(prop.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(activity.IsAtRest, Is.True);

            int writes = lighting.Writes.Count;
            activity.Disturb();
            Run(activity, lighting, 120f);
            Assert.That(lighting.Writes.Count, Is.EqualTo(writes), "Idle events and disturbances stop after victory.");
            Assert.That(prop.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void DefeatKnocksOnePropHardOnceAndStops()
        {
            var light = new AmbientLightActor { LightIndex = 0, IdleInterval = new AmbientRange(1000f, 1000f) };
            var swing = Swing(10f);
            var asset = Activity(new[] { light }, new[] { PropActor("a", swing), PropActor("b", swing) });
            float disturbed = PeakAfter(asset, activity => activity.Disturb());
            float knocked = PeakAfter(asset, activity => activity.Knock());
            Assert.That(knocked / disturbed, Is.EqualTo(asset.DefeatStrength / asset.DisturbanceStrength).Within(.001f),
                "The final knock strikes harder than a disturbance.");

            var props = new[] { Prop("a"), Prop("b") };
            var lighting = new RecordingLighting(1);
            var knock = new EncounterAmbientActivity(asset, lighting, new[] { Binding("a", props[0]), Binding("b", props[1]) });
            knock.Begin(4);
            Run(knock, lighting, .5f);
            knock.Knock();
            Assert.That(knock.IsRunning, Is.False);
            var peaks = new float[2];
            for (float elapsed = 0; elapsed < 60f; elapsed += Step)
            {
                lighting.Clock += Step;
                knock.Tick(Step);
                for (int i = 0; i < props.Length; ++i)
                    peaks[i] = Mathf.Max(peaks[i], Mathf.Abs(Mathf.DeltaAngle(0f, props[i].localEulerAngles.z)));
            }
            Assert.That(peaks.Count(peak => peak > 0f), Is.EqualTo(1), "Exactly one prop knocks.");
            Assert.That(props.All(prop => prop.localRotation == Quaternion.identity), Is.True);
            Assert.That(lighting.Values[0], Is.EqualTo(1f));
            Assert.That(knock.IsAtRest, Is.True);
            knock.Knock();
            knock.Disturb();
            int writes = lighting.Writes.Count;
            Run(knock, lighting, 60f);
            Assert.That(props.All(prop => prop.localRotation == Quaternion.identity), Is.True, "The knock happens once.");
            Assert.That(lighting.Writes.Count, Is.EqualTo(writes));
        }

        [Test]
        public void PropSoundsPlayFromTheStrikesTierWithoutImmediateRepeats()
        {
            var idle = Clips("idle", 3);
            var hit = Clips("hit", 3);
            var defeat = Clips("defeat", 2);
            var actor = PropActor("cup", Swing(10f), new AmbientRange(1f, 1f));
            actor.IdleClips = idle;
            actor.IdleVolume = .2f;
            actor.HitClips = hit;
            actor.HitVolume = .5f;
            actor.DefeatClips = defeat;
            actor.DefeatVolume = .8f;
            var asset = Activity(Array.Empty<AmbientLightActor>(), new[] { actor });
            var lighting = new RecordingLighting(0);
            var sound = new RecordingSound();
            var activity = new EncounterAmbientActivity(asset, lighting, new[] { Binding("cup", Prop("Cup")) }, sound);
            activity.Begin(31);
            Run(activity, lighting, 120.5f);
            Assert.That(sound.Plays.Count, Is.InRange(118, 121), "Each idle jolt sounds once.");
            AssertTier(sound.Plays, idle, .2f, "Idle");
            Assert.That(idle.All(clip => sound.Plays.Count(play => play.Clip == clip) > 20), Is.True, "Idle jolts reach every idle clip.");

            sound.Plays.Clear();
            for (int i = 0; i < 60; ++i) activity.Disturb();
            Assert.That(sound.Plays, Has.Count.EqualTo(60), "Each disturbance jolt sounds once.");
            AssertTier(sound.Plays, hit, .5f, "Hit");
            Assert.That(hit.All(clip => sound.Plays.Any(play => play.Clip == clip)), Is.True, "Disturbances reach every hit clip.");

            sound.Plays.Clear();
            activity.Knock();
            Assert.That(sound.Plays, Has.Count.EqualTo(1), "The final knock sounds once.");
            AssertTier(sound.Plays, defeat, .8f, "Defeat");
            activity.Disturb();
            activity.Knock();
            Run(activity, lighting, 30f);
            Assert.That(sound.Plays, Has.Count.EqualTo(1), "Nothing sounds after the knock.");
        }

        [Test]
        public void DefeatFallsBackToTheHitClipsAndPropsWithoutClipsStaySilent()
        {
            var hit = Clips("hit", 2);
            var voiced = PropActor("a", Swing(10f));
            voiced.HitClips = hit;
            voiced.DefeatVolume = .7f;
            var sound = new RecordingSound();
            var lighting = new RecordingLighting(0);
            var activity = new EncounterAmbientActivity(Activity(Array.Empty<AmbientLightActor>(), new[] { voiced }), lighting,
                new[] { Binding("a", Prop("a")) }, sound);
            activity.Begin(8);
            activity.Disturb();
            activity.Knock();
            Assert.That(sound.Plays, Has.Count.EqualTo(2));
            Assert.That(hit, Has.Member(sound.Plays[1].Clip), "A prop without defeat clips knocks with a hit clip.");
            Assert.That(sound.Plays[1].Clip, Is.Not.SameAs(sound.Plays[0].Clip), "The knock does not repeat the last hit.");
            Assert.That(sound.Plays[1].Volume, Is.EqualTo(.7f), "The knock plays at the defeat volume.");

            var silent = new RecordingSound();
            var quiet = new EncounterAmbientActivity(Activity(Array.Empty<AmbientLightActor>(),
                new[] { PropActor("b", Swing(10f), new AmbientRange(1f, 2f)) }), lighting, new[] { Binding("b", Prop("b")) }, silent);
            quiet.Begin(8);
            Run(quiet, lighting, 20f);
            quiet.Disturb();
            quiet.Knock();
            Assert.That(silent.Plays, Is.Empty, "Props and activities without clips stay silent.");
        }

        [Test]
        public void EachDisturbancePlaysOneBurstBesideTheJoltedPropsHit()
        {
            var bursts = Clips("burst", 3);
            var hit = Clips("hit", 2);
            string[] ids = { "a", "b", "c" };
            var actors = ids.Select(id => PropActor(id, Swing(10f))).ToArray();
            foreach (var actor in actors) actor.HitClips = hit;
            var asset = Activity(Array.Empty<AmbientLightActor>(), actors);
            Set(asset, "burstClips", bursts);
            Set(asset, "burstVolume", .25f);
            var lighting = new RecordingLighting(0);
            var sound = new RecordingSound();
            var activity = new EncounterAmbientActivity(asset, lighting, ids.Select(id => Binding(id, Prop(id))).ToArray(), sound);
            activity.Disturb();
            Assert.That(sound.Plays, Is.Empty, "Nothing sounds before the first hand.");
            activity.Begin(77);
            var burstPlays = new List<(AudioClip Clip, float Volume)>();
            for (int i = 0; i < 40; ++i)
            {
                int before = sound.Plays.Count;
                activity.Disturb();
                var played = sound.Plays.Skip(before).ToList();
                Assert.That(played.Count(play => bursts.Contains(play.Clip)), Is.EqualTo(1), "One burst per disturbance.");
                Assert.That(played.Count(play => hit.Contains(play.Clip)), Is.EqualTo(1), "The jolted prop sounds its hit.");
                Assert.That(played, Has.Count.EqualTo(2));
                burstPlays.AddRange(played.Where(play => bursts.Contains(play.Clip)));
                Run(activity, lighting, .5f);
            }
            AssertTier(burstPlays, bursts, .25f, "Burst");
            Assert.That(bursts.All(clip => burstPlays.Any(play => play.Clip == clip)), Is.True, "Bursts reach every clip.");

            var empty = new RecordingSound();
            var bare = Activity(Array.Empty<AmbientLightActor>(), Array.Empty<AmbientPropActor>());
            Set(bare, "burstClips", bursts);
            var room = new EncounterAmbientActivity(bare, lighting, Array.Empty<EncounterAmbientPropBinding>(), empty);
            room.Begin(3);
            room.Disturb();
            Assert.That(empty.Plays, Has.Count.EqualTo(1), "A disturbance bursts even with no props to jolt.");
            room.Settle();
            room.Disturb();
            Assert.That(empty.Plays, Has.Count.EqualTo(1), "No bursts after the encounter ends.");
        }

        [Test]
        public void SoundChoicesLeaveTheLightsAndPropsOnTheirSeededSchedule()
        {
            var light = new AmbientLightActor { LightIndex = 0, IdleInterval = new AmbientRange(2f, 3f) };
            var actor = PropActor("cup", Swing(10f), new AmbientRange(1f, 2f));
            actor.IdleClips = Clips("idle", 3);
            actor.HitClips = Clips("hit", 3);
            var asset = Activity(new[] { light }, new[] { actor });
            Set(asset, "burstClips", Clips("burst", 2));
            List<(double Time, int Index, float Value)> Record(IAmbientSoundTarget target, List<float> angles)
            {
                var prop = Prop("Cup");
                var lighting = new RecordingLighting(1);
                var activity = new EncounterAmbientActivity(asset, lighting, new[] { Binding("cup", prop) }, target);
                activity.Begin(2024);
                for (int second = 0; second < 40; ++second)
                {
                    if (second % 7 == 3) activity.Disturb();
                    Run(activity, lighting, 1f);
                    angles.Add(prop.localEulerAngles.z);
                }
                return lighting.Writes;
            }
            var sound = new RecordingSound();
            List<float> silentAngles = new List<float>(), voicedAngles = new List<float>();
            var silent = Record(null, silentAngles);
            var voiced = Record(sound, voicedAngles);
            Assert.That(sound.Plays.Count, Is.GreaterThan(20));
            Assert.That(voiced, Is.EqualTo(silent), "Sound choices never draw from the lights' random.");
            Assert.That(voicedAngles, Is.EqualTo(silentAngles), "Sound choices never draw from the props' random.");
        }

        [Test]
        public void MerchantPropsSoundWithTheInstalledClipsQuieterWhenIdle()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            var activity = table.AmbientActivity;
            Assert.DoesNotThrow(activity.Validate);
            string[] Names(string prefix, int count) => Enumerable.Range(1, count).Select(index => $"{prefix}_{index:D2}").ToArray();
            var cookware = (Idle: Names("merchant_cookware_idle", 4), Hit: Names("merchant_cookware_hit", 6), Defeat: new string[0]);
            var expected = new Dictionary<string, (string[] Idle, string[] Hit, string[] Defeat)>
            {
                ["hanging-cup"] = cookware, ["hanging-pot"] = cookware, ["hanging-pan"] = cookware,
                ["shelf-bottle"] = (Names("merchant_bottle_idle", 2), Names("merchant_bottle_hit", 1), new string[0]),
                ["trunk-lid"] = (Names("merchant_lid_idle", 2), Names("merchant_lid_hit", 2), Names("merchant_lid_defeat", 1)),
            };
            Assert.That(activity.Props.Select(actor => actor.Id), Is.EquivalentTo(expected.Keys));
            var referenced = new HashSet<AudioClip>();
            float idlePeak = 0f, hitPeak = 1f;
            foreach (var actor in activity.Props)
            {
                var clips = expected[actor.Id];
                Assert.That(actor.IdleClips.Select(clip => clip.name), Is.EqualTo(clips.Idle), actor.Id);
                Assert.That(actor.HitClips.Select(clip => clip.name), Is.EqualTo(clips.Hit), actor.Id);
                Assert.That(actor.DefeatClips.Select(clip => clip.name), Is.EqualTo(clips.Defeat), actor.Id);
                Assert.That(actor.IdleVolume, Is.GreaterThan(0f).And.LessThan(actor.HitVolume), actor.Id + " idles quieter than it is hit.");
                Assert.That(actor.DefeatVolume, Is.GreaterThanOrEqualTo(actor.HitVolume), actor.Id);
                referenced.UnionWith(actor.IdleClips.Concat(actor.HitClips).Concat(actor.DefeatClips));
                foreach (var clip in actor.IdleClips) idlePeak = Mathf.Max(idlePeak, Peak(clip));
                foreach (var clip in actor.HitClips) hitPeak = Mathf.Min(hitPeak, Peak(clip));
            }
            Assert.That(idlePeak, Is.LessThan(hitPeak / 2f), "Idle clips are normalized well below the hits.");
            Assert.That(activity.BurstClips.Select(clip => clip.name), Is.EqualTo(Names("poltergeist_burst", 3)));
            referenced.UnionWith(activity.BurstClips);
            var giggles = AssetDatabase.LoadAssetAtPath<SpiritAudioTheme>(MerchantAudioPath);
            Assert.That(activity.BurstVolume, Is.GreaterThan(0f).And.LessThan(giggles.effectsVolume), "Bursts sit under the giggles.");
            Assert.That(activity.BurstClips.Max(Peak), Is.LessThan(giggles.disturbanceClips.Min(Peak)));
            foreach (var clip in referenced)
            {
                Assert.That(AssetDatabase.GetAssetPath(clip), Does.StartWith(PropAudioFolder), clip.name);
                Assert.That(clip.channels, Is.EqualTo(1), clip.name);
                Assert.That(clip.frequency, Is.EqualTo(44100), clip.name);
                Assert.That(clip.length, Is.InRange(.2f, 2.5f), clip.name);
            }
            var installed = Directory.GetFiles(PropAudioFolder, "*.wav").Select(file => Path.GetFileNameWithoutExtension(file));
            Assert.That(referenced.Select(clip => clip.name), Is.EquivalentTo(installed), "The merchant uses every installed prop clip.");
        }

        private static void AssertTier(IReadOnlyList<(AudioClip Clip, float Volume)> plays, AudioClip[] tier, float volume, string name)
        {
            for (int i = 0; i < plays.Count; ++i)
            {
                Assert.That(tier, Has.Member(plays[i].Clip), name + " plays its own clips.");
                Assert.That(plays[i].Volume, Is.EqualTo(volume), name + " volume");
                if (i > 0) Assert.That(plays[i].Clip, Is.Not.SameAs(plays[i - 1].Clip), name + " never repeats a clip immediately.");
            }
        }

        private static float Peak(AudioClip clip)
        {
            Assert.That(clip.LoadAudioData(), Is.True, clip.name);
            var samples = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(samples, 0), Is.True, clip.name);
            return samples.Max(Mathf.Abs);
        }

        private AudioClip[] Clips(string prefix, int count)
            => Enumerable.Range(1, count).Select(index => Track(AudioClip.Create(prefix + index, 4410, 1, 44100, false))).ToArray();

        private static IEnumerable<TestCaseData> MotionKinds()
        {
            yield return new TestCaseData("Swing", Vector3.zero, Vector3.zero)
                .SetName("SwingMovesWhileStruckAndReturnsExactlyToItsRecordedRest");
            // The default wobble contacts sit a quarter of the width in from each side of the base.
            yield return new TestCaseData("Wobble", new Vector3(-10f, -30f, 0f), new Vector3(10f, -30f, 0f))
                .SetName("WobbleMovesWhileStruckAndReturnsExactlyToItsRecordedRest");
            yield return new TestCaseData("Thump", Vector3.zero, Vector3.zero)
                .SetName("ThumpMovesWhileStruckAndReturnsExactlyToItsRecordedRest");
        }

        [TestCaseSource(nameof(MotionKinds))]
        public void MotionKindMovesWhileStruckAndReturnsExactlyToItsRecordedRest(string kind, Vector3 leftPivot, Vector3 rightPivot)
        {
            AmbientMotionAsset asset = kind == "Swing" ? Swing(10f)
                : kind == "Wobble" ? (AmbientMotionAsset)Track(ScriptableObject.CreateInstance<AmbientWobbleMotionAsset>())
                : Track(ScriptableObject.CreateInstance<AmbientThumpMotionAsset>());
            var restPosition = new Vector3(12.5f, -40.25f, 0f);
            var restRotation = Quaternion.Euler(0f, 0f, 7f);
            // The motion runs out on its own, is damped by a settle, or is dropped by a rest mid-motion.
            foreach (string ending in new[] { "decay", "settle", "rest" })
            {
                var prop = Prop(kind + " " + ending);
                prop.anchoredPosition3D = restPosition;
                prop.localRotation = restRotation;
                var motion = asset.Create(prop);
                motion.Strike(.1, 1.5f);
                float peakAngle = 0f, peakOffset = 0f, leftmost = 0f, rightmost = 0f;
                bool interrupted = false;
                for (double time = 0; time < 15; time += Step)
                {
                    if (!interrupted && ending != "decay" && time >= .3)
                    {
                        Assert.That(motion.IsOffset, Is.True, kind + " is mid-motion when it is interrupted.");
                        if (ending == "settle") motion.Settle(time, .05f);
                        else
                        {
                            motion.Rest();
                            Assert.That(motion.IsMoving || motion.IsOffset, Is.False, kind + " rests at once.");
                            Assert.That(Vector3.Distance(prop.anchoredPosition3D, restPosition), Is.LessThan(.0001f), kind + " rest position");
                            Assert.That(Quaternion.Angle(prop.localRotation, restRotation), Is.LessThan(.0001f), kind + " rest rotation");
                        }
                        interrupted = true;
                    }
                    motion.Apply(time);
                    if (!motion.IsOffset) continue;

                    // A counterclockwise turn stands on the left contact, a clockwise one on the right.
                    float turn = Mathf.DeltaAngle(0f, (Quaternion.Inverse(restRotation) * prop.localRotation).eulerAngles.z);
                    Vector3 pivot = turn >= 0f ? leftPivot : rightPivot;
                    leftmost = Mathf.Max(leftmost, turn);
                    rightmost = Mathf.Min(rightmost, turn);
                    Vector3 offset = prop.anchoredPosition3D + prop.localRotation * pivot - (restPosition + restRotation * pivot);
                    peakAngle = Mathf.Max(peakAngle, Mathf.Abs(turn));
                    peakOffset = Mathf.Max(peakOffset, offset.magnitude);
                    if (kind == "Thump") Assert.That(offset.y, Is.GreaterThanOrEqualTo(-.0001f), "A thump hops upward.");
                }
                Assert.That(peakAngle, Is.GreaterThan(1f), kind + " moves the prop before its " + ending + ".");
                if (kind != "Thump") Assert.That(peakOffset, Is.LessThan(.001f), kind + " keeps its pivot in place.");
                if (ending == "decay")
                {
                    if (kind != "Thump") Assert.That(leftmost > 1f && rightmost < -1f, Is.True, kind + " turns both ways.");
                    else Assert.That(peakOffset, Is.GreaterThan(5f));
                }
                Assert.That(motion.IsMoving || motion.IsOffset, Is.False, kind + " " + ending);
                Assert.That(Vector3.Distance(prop.anchoredPosition3D, restPosition), Is.LessThan(.0001f),
                    kind + " returns to its recorded rest position after its " + ending + ": " + prop.anchoredPosition3D.ToString("F5"));
                Assert.That(Quaternion.Angle(prop.localRotation, restRotation), Is.LessThan(.0001f),
                    kind + " returns to its recorded rest rotation after its " + ending + ".");

                // The next motion records the rest pose the prop holds when it starts.
                var nextPosition = new Vector3(-8f, 21.75f, 0f);
                var nextRotation = Quaternion.Euler(0f, 0f, -4f);
                prop.anchoredPosition3D = nextPosition;
                prop.localRotation = nextRotation;
                motion.Strike(15, 1.5f);
                bool moved = false;
                for (double time = 15; time < 30; time += Step)
                {
                    motion.Apply(time);
                    moved |= Quaternion.Angle(prop.localRotation, nextRotation) > 1f;
                }
                Assert.That(moved, Is.True, kind + " moves again.");
                Assert.That(motion.IsMoving || motion.IsOffset, Is.False);
                Assert.That(Vector3.Distance(prop.anchoredPosition3D, nextPosition), Is.LessThan(.0001f), kind + " next rest position");
                Assert.That(Quaternion.Angle(prop.localRotation, nextRotation), Is.LessThan(.0001f), kind + " next rest rotation");
            }
        }

        [Test]
        public void HangingCupRootSwingComposesWithItsPrefabsChildSway()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CupPrefabPath);
            var clip = prefab.GetComponent<Animation>().clip;
            Assert.That(clip, Is.Not.Null);
            var cup = Track(Object.Instantiate(prefab));
            var reference = Track(Object.Instantiate(prefab));
            var root = (RectTransform)cup.transform;
            root.anchoredPosition3D = new Vector3(30f, -50f, 0f);
            Vector3 restPosition = root.anchoredPosition3D;
            Quaternion restRotation = root.localRotation;
            Transform swing = root.Find("Swing"), referenceSwing = reference.transform.Find("Swing");
            Assert.That(swing, Is.Not.Null, "The prefab's idle sway animates its Swing child.");
            var motion = Swing(10f).Create(root);
            motion.Strike(.5, 1.5f);
            float peakRoot = 0f, peakChild = 0f;
            for (double time = 0; time < 15; time += Step)
            {
                // The prefab's clip sways the child every frame while the ambient motion swings the root.
                float clipTime = (float)(time % clip.length);
                clip.SampleAnimation(cup, clipTime);
                motion.Apply(time);
                clip.SampleAnimation(reference, clipTime);
                Assert.That(Quaternion.Angle(swing.localRotation, referenceSwing.localRotation), Is.LessThan(.0001f),
                    "The child keeps the clip's sway at " + time);
                Assert.That(Vector3.Distance(swing.localPosition, referenceSwing.localPosition), Is.LessThan(.0001f));
                peakRoot = Mathf.Max(peakRoot, Quaternion.Angle(root.localRotation, restRotation));
                peakChild = Mathf.Max(peakChild, Quaternion.Angle(swing.localRotation, Quaternion.identity));
            }
            Assert.That(peakRoot, Is.GreaterThan(3f), "The strike swings the cup's root.");
            Assert.That(peakChild, Is.GreaterThan(2f), "The clip sways the child.");
            Assert.That(motion.IsMoving || motion.IsOffset, Is.False);
            Assert.That(Vector3.Distance(root.anchoredPosition3D, restPosition), Is.LessThan(.0001f), "The root returns to rest.");
            Assert.That(Quaternion.Angle(root.localRotation, restRotation), Is.LessThan(.0001f));
            clip.SampleAnimation(cup, 1f);
            clip.SampleAnimation(reference, 1f);
            Assert.That(Quaternion.Angle(swing.localRotation, referenceSwing.localRotation), Is.LessThan(.0001f),
                "The child holds whatever pose the clip sets.");
            Assert.That(Quaternion.Angle(swing.localRotation, Quaternion.identity), Is.GreaterThan(2f));
        }

        [Test]
        public void MerchantPropsReturnExactlyToRestAfterRepeatedStrikesUnderPerFrameLayout()
        {
            // The merchant's scene frame: point anchors with a fractional pivot, so a prop's local position is
            // re-derived from its anchored position with float rounding whenever its rect transform updates.
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            var frame = (RectTransform)Track(new GameObject("Merchant Scene", typeof(RectTransform))).transform;
            frame.anchorMin = frame.anchorMax = Vector2.zero;
            frame.pivot = new Vector2(87.26366f / 540f, 0f);
            frame.sizeDelta = new Vector2(540f, 960f);
            frame.anchoredPosition = new Vector2(87.26366f, 0f);
            frame.localScale = new Vector3(1.12f, 1.12f, 1f);
            var placements = new[] { (Id: "shelf-bottle", Anchored: new Vector2(131.700317f, -220.019135f)),
                (Id: "trunk-lid", Anchored: new Vector2(122.231667f, -234.832535f)) };
            var props = new List<(AmbientMotion Motion, RectTransform Rect, Vector2 Anchored, float Strength)>();
            foreach (var placement in placements)
            {
                var actor = table.AmbientActivity.Props.Single(candidate => candidate.Id == placement.Id);
                var rect = Prop(placement.Id);
                rect.SetParent(frame, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(.5f, 0f);
                rect.anchoredPosition = placement.Anchored;
                props.Add((actor.Motion.Create(rect), rect, placement.Anchored, actor.Strength * table.AmbientActivity.DisturbanceStrength));
            }
            // A local position written near the bottle's rest often reads back differently once its rect transform updates.
            var bottle = props[0].Rect;
            Vector3 restLocal = bottle.localPosition;
            int rederived = 0;
            for (int i = 1; i <= 100; ++i)
            {
                bottle.localPosition = restLocal + new Vector3(i * .0137f, i * .0291f, 0f);
                Vector3 written = bottle.localPosition;
                bottle.ForceUpdateRectTransforms();
                if (!bottle.localPosition.Equals(written)) ++rederived;
            }
            bottle.anchoredPosition = props[0].Anchored;
            Assert.That(rederived, Is.GreaterThan(0), "The layout update re-derives local positions with rounding.");
            var drift = new List<string>();
            double time = 0;
            for (int round = 0; round < 3; ++round)
            {
                foreach (var prop in props) prop.Motion.Strike(time, prop.Strength);
                for (double end = time + 5; time < end; time += Step)
                    foreach (var prop in props)
                    {
                        prop.Motion.Apply(time);
                        // The canvas updates every prop's rect transform each frame after the motion writes it.
                        prop.Rect.ForceUpdateRectTransforms();
                    }
                foreach (var prop in props)
                {
                    Assert.That(prop.Motion.IsMoving || prop.Motion.IsOffset, Is.False, prop.Rect.name);
                    float distance = Vector2.Distance(prop.Rect.anchoredPosition, prop.Anchored);
                    float angle = Quaternion.Angle(prop.Rect.localRotation, Quaternion.identity);
                    if (distance >= .0001f || angle >= .0001f)
                        drift.Add($"round {round} {prop.Rect.name} {(prop.Rect.anchoredPosition - prop.Anchored).ToString("F4")} {angle:F4} deg");
                }
            }
            Assert.That(drift, Is.Empty, "Every strike returns the props exactly to their anchored rest pose.");
        }

        [Test]
        public void PropsReturnToTheirAnchoredRestPoseWhenTheirFrameResizesMidMotion()
        {
            // Like the merchant props: anchored at the top-left corner of a frame that the view resizes to the screen.
            var frame = (RectTransform)Track(new GameObject("Frame", typeof(RectTransform))).transform;
            frame.sizeDelta = new Vector2(540f, 960f);
            var wobbled = Prop("Bottle");
            var thumped = Prop("Lid");
            var rest = new Dictionary<RectTransform, Vector2>
            {
                [wobbled] = new Vector2(200f, -500f), [thumped] = new Vector2(320f, -700f),
            };
            foreach (var prop in rest)
            {
                prop.Key.SetParent(frame, false);
                prop.Key.anchorMin = prop.Key.anchorMax = new Vector2(0f, 1f);
                prop.Key.pivot = new Vector2(.5f, 0f);
                prop.Key.anchoredPosition = prop.Value;
            }
            var motions = new[]
            {
                Track(ScriptableObject.CreateInstance<AmbientWobbleMotionAsset>()).Create(wobbled),
                Track(ScriptableObject.CreateInstance<AmbientThumpMotionAsset>()).Create(thumped),
            };
            foreach (var motion in motions) motion.Strike(.1, 1.5f);
            bool resized = false;
            for (double time = 0; time < 5; time += Step)
            {
                if (!resized && time >= .2)
                {
                    Assert.That(motions.All(motion => motion.IsOffset), Is.True, "Both props are mid-motion when the frame resizes.");
                    Vector3 before = wobbled.localPosition;
                    frame.sizeDelta = new Vector2(443f, 900f);
                    foreach (var rect in frame.GetComponentsInChildren<RectTransform>(true)) rect.ForceUpdateRectTransforms();
                    Assert.That(Vector3.Distance(wobbled.localPosition - before, new Vector3(48.5f, -30f, 0f)), Is.LessThan(.0001f),
                        "The resize moves the props' local positions with their corner anchor.");
                    resized = true;
                }
                foreach (var motion in motions) motion.Apply(time);
            }
            Assert.That(resized, Is.True);
            foreach (var motion in motions)
            {
                var prop = (RectTransform)motion.Target;
                Assert.That(motion.IsMoving || motion.IsOffset, Is.False, prop.name);
                Assert.That(Vector2.Distance(prop.anchoredPosition, rest[prop]), Is.LessThan(.0001f),
                    prop.name + " returns to its anchored rest position, not one displaced by the resize: " + prop.anchoredPosition.ToString("F4"));
                Assert.That(prop.anchoredPosition3D.z, Is.EqualTo(0f).Within(.0001f), prop.name);
                Assert.That(Quaternion.Angle(prop.localRotation, Quaternion.identity), Is.LessThan(.0001f), prop.name);
            }
        }

        [Test]
        public void MerchantTableAuthorsLanternOutagesAndTheHangingCupSwing()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            Assert.That(table, Is.Not.Null);
            var activity = table.AmbientActivity;
            Assert.That(activity, Is.Not.Null);
            Assert.DoesNotThrow(activity.Validate);
            var lantern = activity.Lights.Single();
            Assert.That(lantern.LightIndex, Is.Zero, "Light 0 is the merchant's fixed lantern.");
            Assert.That(table.SceneLighting.Lights.Length, Is.GreaterThan(lantern.LightIndex));
            Assert.That(table.PieceLights.All(pieceLight => pieceLight.LightIndex != lantern.LightIndex), Is.True,
                "The lantern is not the candle's piece light.");
            Assert.That(lantern.IdleInterval.Min, Is.EqualTo(20f));
            Assert.That(lantern.IdleInterval.Max, Is.EqualTo(40f));
            Assert.That(lantern.DarkDuration.Min, Is.EqualTo(1.5f));
            Assert.That(lantern.DarkDuration.Max, Is.EqualTo(4f));
            Assert.That(lantern.StutterFlickers.Max, Is.LessThan(lantern.SputterFlickers.Max + 1));
            var cup = activity.Props.Single(actor => actor.Id == "hanging-cup");
            var swing = cup.Motion as AmbientSwingMotionAsset;
            Assert.That(swing, Is.Not.Null, "The hanging cup swings.");
            Assert.That(swing.Amplitude * cup.Strength * activity.DisturbanceStrength, Is.GreaterThan(3f),
                "A disturbance swings harder than the cup's three-degree idle sway.");
            Assert.That(cup.IdleInterval.Min, Is.GreaterThan(0f));
            Assert.That(activity.DefeatStrength, Is.GreaterThan(activity.DisturbanceStrength));
        }

        [Test]
        public void MerchantSceneShowsTheHangingCupAtItsPreviewPlacementBoundToTheActivity()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            Vector2 previewPosition, previewParentSize, previewAnchor;
            var preview = EditorSceneManager.OpenPreviewScene(PreviewScenePath);
            try
            {
                var previewCup = (RectTransform)FindCup(preview.GetRootGameObjects()).transform;
                previewPosition = previewCup.anchoredPosition;
                previewAnchor = previewCup.anchorMin;
                previewParentSize = ((RectTransform)previewCup.parent).sizeDelta;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }

            var scene = EditorSceneManager.OpenPreviewScene(AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID));
            try
            {
                var roots = scene.GetRootGameObjects();
                var view = roots.SelectMany(root => root.GetComponentsInChildren<SpiritEncounterView>(true)).Single();
                var bindings = (List<EncounterAmbientPropBinding>)typeof(SpiritEncounterView)
                    .GetField("ambientProps", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                foreach (var actor in table.AmbientActivity.Props)
                    Assert.That(bindings.Any(binding => binding.ActorId == actor.Id && binding.Target != null), Is.True, actor.Id);
                var cup = (RectTransform)bindings.Single(binding => binding.ActorId == "hanging-cup").Target;
                Assert.That(cup.gameObject, Is.SameAs(FindCup(roots)));
                Assert.That(PrefabUtility.GetOutermostPrefabInstanceRoot(cup.gameObject), Is.SameAs(cup.gameObject),
                    "The swing turns the cup's root about its pin, over the prefab's own sway.");
                Assert.That(cup.gameObject.activeSelf, Is.True);
                Assert.That(cup.IsChildOf(view.Portrait), Is.True);
                Assert.That(cup.anchorMin, Is.EqualTo(previewAnchor));
                Assert.That(cup.anchorMax, Is.EqualTo(previewAnchor));
                Assert.That(cup.anchoredPosition, Is.EqualTo(previewPosition));
                var parent = (RectTransform)cup.parent;
                Assert.That(parent.sizeDelta, Is.EqualTo(previewParentSize), "The cup's frame matches the preview plate.");
                // The canopy draws first, the cup over it, and the merchant over both; his layers are clear at the cup.
                Assert.That(parent.Find("Canopy").GetSiblingIndex(), Is.LessThan(cup.GetSiblingIndex()));
                Assert.That(parent.Find("Merchant").GetSiblingIndex(), Is.GreaterThan(cup.GetSiblingIndex()));
                Assert.DoesNotThrow(() => new EncounterAmbientActivity(table.AmbientActivity,
                    new RecordingLighting(table.SceneLighting.Lights.Length), bindings));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void MerchantTableAuthorsTheMovablePropsWithTheirMotionKinds()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            var activity = table.AmbientActivity;
            Assert.DoesNotThrow(activity.Validate);
            var cuts = ReadPropCuts();
            Assert.That(activity.Props.Select(actor => actor.Id),
                Is.EquivalentTo(PropActors.Select(prop => prop.Id).Append("hanging-cup")), "The cup stays an actor beside the props.");
            float eventsPerSecond = 0f;
            foreach (var actor in activity.Props)
            {
                // Each prop idles every 8 to 20 seconds, so together something moves every few seconds.
                Assert.That(actor.IdleInterval.Min, Is.GreaterThanOrEqualTo(8f), actor.Id);
                Assert.That(actor.IdleInterval.Max, Is.LessThanOrEqualTo(20f), actor.Id);
                eventsPerSecond += 2f / (actor.IdleInterval.Min + actor.IdleInterval.Max);
            }
            Assert.That(1f / eventsPerSecond, Is.InRange(2f, 4f), "Mean seconds between idle prop events.");
            foreach (var (id, name) in PropActors)
            {
                var actor = activity.Props.Single(candidate => candidate.Id == id);
                var cut = cuts.Single(candidate => candidate.Name == name);
                Assert.That(actor.Strength, Is.GreaterThan(0f), id);
                if (name == "ShelfBottle")
                {
                    var wobble = actor.Motion as AmbientWobbleMotionAsset;
                    Assert.That(wobble, Is.Not.Null, "The bottle rocks on its base.");
                    Vector2[] contacts = cut.Contacts.Select(contact => cut.Normalized(contact)).ToArray();
                    Assert.That(Vector2.Distance(wobble.LeftContact, contacts[0]), Is.LessThan(.0001f));
                    Assert.That(Vector2.Distance(wobble.RightContact, contacts[1]), Is.LessThan(.0001f));
                }
                else if (name == "TrunkLid") Assert.That(actor.Motion, Is.InstanceOf<AmbientThumpMotionAsset>(), "The lid thumps.");
                else Assert.That(actor.Motion, Is.InstanceOf<AmbientSwingMotionAsset>(), name + " swings from its ring.");

                // Idle swings and rocks read clearly at 7 to 10 degrees and the lid visibly hops;
                // a disturbance still hits harder than any idle motion.
                bool lift = name == "TrunkLid";
                float suggested = lift ? cut.SuggestedLift * 960f / 1672f : cut.SuggestedDegrees;
                float idleLow = PeakResponse(actor.Motion, actor.Strength * actor.IdleStrength.Min, lift);
                float idle = PeakResponse(actor.Motion, actor.Strength * actor.IdleStrength.Max, lift);
                float disturbance = PeakResponse(actor.Motion, actor.Strength * activity.DisturbanceStrength, lift);
                if (lift) Assert.That(idleLow / suggested, Is.GreaterThanOrEqualTo(2f), name + " idle hop " + idleLow);
                else
                {
                    Assert.That(idleLow, Is.GreaterThanOrEqualTo(7f), name + " idle peak " + idleLow);
                    Assert.That(idle, Is.LessThanOrEqualTo(10.5f), name + " idle peak " + idle);
                }
                Assert.That(disturbance / idle, Is.GreaterThanOrEqualTo(1.3f), name + " disturbance peak " + disturbance);
            }
        }

        [Test]
        public void MerchantScenePlacesTheMovablePropsOverTheirRemovedRegionsBoundToTheActivity()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            var cuts = ReadPropCuts();
            var scene = EditorSceneManager.OpenPreviewScene(AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID));
            try
            {
                var roots = scene.GetRootGameObjects();
                var view = roots.SelectMany(root => root.GetComponentsInChildren<SpiritEncounterView>(true)).Single();
                var bindings = (List<EncounterAmbientPropBinding>)typeof(SpiritEncounterView)
                    .GetField("ambientProps", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                Transform frame = FindCup(roots).transform.parent;
                Assert.That(frame.name, Is.EqualTo("Merchant Scene"));
                foreach (var (id, name) in PropActors)
                    Assert.That(bindings.Single(binding => binding.ActorId == id).Target, Is.SameAs(frame.Find(name)), id);
                foreach (float width in new[] { 540f, 443f })
                {
                    view.Portrait.anchorMin = view.Portrait.anchorMax = new Vector2(.5f, .5f);
                    view.Portrait.sizeDelta = new Vector2(width, 960);
                    view.Portrait.anchoredPosition = Vector2.zero;
                    // A preview scene neither sizes nor scales its overlay canvas, so the test gives it unit scale and recalculates rects.
                    view.Canvas.transform.localScale = Vector3.one;
                    foreach (var rect in view.GetComponentsInChildren<RectTransform>(true)) rect.ForceUpdateRectTransforms();
                    RectTransform front = view.TableImage.rectTransform, back = view.RoomImage.rectTransform;
                    // Props register to the encounter's background images, which show the room texture across both halves.
                    AssertPropsPlaced(frame, cuts, table, world =>
                    {
                        Vector2 uv = SpiritTablePieceLayout.TextureCoordinate(front.InverseTransformPoint(world), front.rect,
                            view.TableImage.uvRect, back.InverseTransformPoint(world), back.rect, view.RoomImage.uvRect);
                        return new Vector2(uv.x * PlateWidth, (1f - uv.y) * PlateHeight);
                    });
                }
                Assert.That(frame.Find("Canopy").GetSiblingIndex(), Is.LessThan(FindCup(roots).transform.GetSiblingIndex()));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void PreviewScenePlacesTheMovablePropsOverTheirRemovedRegions()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            var cuts = ReadPropCuts();
            var scene = EditorSceneManager.OpenPreviewScene(PreviewScenePath);
            try
            {
                var roots = scene.GetRootGameObjects();
                Transform frame = FindCup(roots).transform.parent;
                var plate = frame.GetComponent<RawImage>();
                Assert.That(plate, Is.Not.Null, "The preview's props register to its background plate.");
                Assert.That(plate.uvRect, Is.EqualTo(new Rect(0, 0, 1, 1)));
                foreach (var rect in frame.GetComponentsInChildren<RectTransform>(true)) rect.ForceUpdateRectTransforms();
                AssertPropsPlaced(frame, cuts, table, world =>
                {
                    Rect bounds = plate.rectTransform.rect;
                    Vector3 local = plate.rectTransform.InverseTransformPoint(world);
                    return new Vector2((local.x - bounds.xMin) / bounds.width * PlateWidth,
                        (bounds.yMax - local.y) / bounds.height * PlateHeight);
                });
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void MerchantScenesLightTheHangingCupWithTheLanternAtItsRestRect()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            foreach (string path in new[] { AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID), PreviewScenePath })
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var cup = (RectTransform)FindCup(scene.GetRootGameObjects()).transform;
                    var frame = (RectTransform)cup.parent;
                    var image = cup.GetComponentsInChildren<RawImage>(true).Single();
                    Assert.That(image.rectTransform.parent.name, Is.EqualTo("Swing"), path);
                    Assert.That(image.raycastTarget, Is.False, path);
                    Assert.That(image.uvRect, Is.EqualTo(new Rect(0, 0, 1, 1)), path);
                    Assert.That(AssetDatabase.GetAssetPath(image.texture), Is.EqualTo(RelightFolder + "HangingCupUnlit.png"), path);
                    Assert.That(image.material.shader, Is.SameAs(AssetDatabase.LoadAssetAtPath<Shader>(LitShaderPath)), path);
                    Assert.That(image.material.GetTexture("_LayerMask"), Is.Null, "The cup has no ownership mask.");
                    Assert.That(image.material.GetFloat("_InvertMask"), Is.EqualTo(1), "Without a mask the inverted selection covers the cup.");
                    Assert.That(image.material.GetTexture("_SourceTex"), Is.SameAs(image.texture), path);

                    // The reusable prefab keeps only its animation and image; the scene instance adds the lighting.
                    var lit = image.GetComponent<SceneLitImage>();
                    Assert.That(lit, Is.Not.Null, path);
                    Assert.That(PrefabUtility.IsAddedComponentOverride(lit), Is.True, path);
                    Assert.That(lit.enabled, Is.True, path);
                    Assert.That(lit.Lighting, Is.SameAs(table.SceneLighting), path);
                    Assert.DoesNotThrow(lit.Layer.Validate);
                    Assert.That(AssetDatabase.GetAssetPath(lit.Layer.BaseTexture), Is.EqualTo(RelightFolder + "HangingCupUnlit.png"), path);
                    var lantern = lit.Layer.Lights.Single();
                    Assert.That(lantern.LightIndex, Is.Zero, "Only the lantern lights the cup; no computed candle light.");
                    Assert.That(lantern.Mode, Is.EqualTo(SceneLitLightMode.Reference), path);
                    Assert.That(AssetDatabase.GetAssetPath(lantern.Reference), Is.EqualTo(RelightFolder + "HangingCupLanternLit.png"), path);
                    Assert.That(AssetDatabase.GetAssetPath(lantern.ResponseMap), Is.EqualTo(ResponsePath), path);

                    // The response map is a smooth distance field, so the swinging cup samples it at its rest rect.
                    Assert.That(cup.Find("Swing").localRotation, Is.EqualTo(Quaternion.identity), "The scene holds the cup at rest.");
                    foreach (var rect in frame.GetComponentsInChildren<RectTransform>(true)) rect.ForceUpdateRectTransforms();
                    Rect bounds = frame.rect;
                    Assert.That(bounds.size, Is.EqualTo(new Vector2(540, 960)), path + " frame is the plate's 540 x 960 frame.");
                    // Frame-local, so the result does not depend on the scale a preview scene gives its canvas.
                    Vector2 Plate(Vector2 point)
                    {
                        Vector3 local = point;
                        for (Transform node = image.rectTransform; node != frame; node = node.parent)
                            local = Matrix4x4.TRS(node.localPosition, node.localRotation, node.localScale).MultiplyPoint3x4(local);
                        return new Vector2((local.x - bounds.xMin) / bounds.width * PlateWidth, (bounds.yMax - local.y) / bounds.height * PlateHeight);
                    }
                    Rect imageRect = image.rectTransform.rect;
                    Vector2 topLeft = Plate(new Vector2(imageRect.xMin, imageRect.yMax)), bottomRight = Plate(new Vector2(imageRect.xMax, imageRect.yMin));
                    var mapRect = new Rect(topLeft.x / PlateWidth, 1f - bottomRight.y / PlateHeight,
                        (bottomRight.x - topLeft.x) / PlateWidth, (bottomRight.y - topLeft.y) / PlateHeight);
                    Assert.That(Vector4.Distance(new Vector4(lit.Layer.MapRect.x, lit.Layer.MapRect.y, lit.Layer.MapRect.width,
                        lit.Layer.MapRect.height), new Vector4(mapRect.x, mapRect.y, mapRect.width, mapRect.height)), Is.LessThan(.00001f),
                        path + " samples the plate's response map under the cup's rest rect " + mapRect.ToString("F6"));
                    // The material carries the same rectangle for an image no component lights.
                    Vector4 materialRect = image.material.GetVector("_SceneLitMapRect");
                    Assert.That(Vector4.Distance(materialRect, new Vector4(mapRect.x, mapRect.y, mapRect.width, mapRect.height)),
                        Is.LessThan(.00001f), path);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }

        [Test]
        public void MovablePropsCompositeOverThePatchedPlatesIntoTheInstalledPlates()
        {
            foreach (var plates in new[] { (Cut: "Unlit", Patched: "MerchantAmbientUnlitPropsRemoved.png", Installed: "MerchantAmbientUnlit.png"),
                (Cut: "LanternLit", Patched: "MerchantLanternOnlyPropsRemoved.png", Installed: "MerchantLanternOnly.png") })
            {
                var patched = Decode(PropsFolder + plates.Patched);
                var installed = Decode(PropsFolder + "../" + plates.Installed);
                try
                {
                    Assert.That(new Vector2Int(installed.width, installed.height), Is.EqualTo(new Vector2Int((int)PlateWidth, (int)PlateHeight)));
                    Color32[] before = installed.GetPixels32(), after = patched.GetPixels32();
                    var cuts = ReadPropCuts().OrderBy(cut => cut.DrawOrder).ToList();
                    // Plate coverage of every piece drawn after each piece; the cut completes an earlier piece where a later one hides it.
                    var coveredLater = new bool[cuts.Count][];
                    var covered = new bool[before.Length];
                    for (int index = cuts.Count - 1; index >= 0; --index)
                    {
                        coveredLater[index] = (bool[])covered.Clone();
                        var cover = Decode(PropsFolder + cuts[index].Name + plates.Cut + ".png");
                        try
                        {
                            Color32[] coverPixels = cover.GetPixels32();
                            for (int row = 0; row < cover.height; ++row)
                                for (int column = 0; column < cover.width; ++column)
                                    if (coverPixels[row * cover.width + column].a > 0)
                                        covered[(installed.height - 1 - (cuts[index].Crop.y + cover.height - 1 - row)) * installed.width
                                            + cuts[index].Crop.x + column] = true;
                        }
                        finally { Object.DestroyImmediate(cover); }
                    }
                    for (int index = 0; index < cuts.Count; ++index)
                    {
                        var cut = cuts[index];
                        var piece = Decode(PropsFolder + cut.Name + plates.Cut + ".png");
                        try
                        {
                            Assert.That(new Vector2Int(piece.width, piece.height), Is.EqualTo(cut.Crop.size), cut.Name);
                            Color32[] pixels = piece.GetPixels32();
                            int opaque = 0, removed = 0;
                            for (int row = 0; row < piece.height; ++row)
                                for (int column = 0; column < piece.width; ++column)
                                {
                                    Color32 pixel = pixels[row * piece.width + column];
                                    if (pixel.a < 255) continue;
                                    // Texture rows start at the bottom; crop rows start at the top.
                                    int plateIndex = (installed.height - 1 - (cut.Crop.y + piece.height - 1 - row)) * installed.width
                                        + cut.Crop.x + column;
                                    // Completed pixels behind a later piece show only through its soft edge and need not match the plate.
                                    if (coveredLater[index][plateIndex]) continue;
                                    Color32 expected = before[plateIndex];
                                    Assert.That(Math.Max(Math.Abs(pixel.r - expected.r), Math.Max(Math.Abs(pixel.g - expected.g),
                                        Math.Abs(pixel.b - expected.b))), Is.LessThanOrEqualTo(2),
                                        $"{cut.Name} {plates.Cut} at crop ({column}, {piece.height - 1 - row})");
                                    ++opaque;
                                    Color32 behind = after[plateIndex];
                                    if (Math.Max(Math.Abs(behind.r - expected.r), Math.Max(Math.Abs(behind.g - expected.g),
                                        Math.Abs(behind.b - expected.b))) > 2) ++removed;
                                }
                            Assert.That(opaque, Is.GreaterThan(500), cut.Name);
                            Assert.That(removed, Is.GreaterThan(opaque / 2), cut.Name + " is removed from the patched plate.");
                        }
                        finally { Object.DestroyImmediate(piece); }
                    }
                }
                finally
                {
                    Object.DestroyImmediate(patched);
                    Object.DestroyImmediate(installed);
                }
            }
        }

        private static void AssertPropsPlaced(Transform frame, IReadOnlyList<PropCut> cuts, EncounterTablePresentationAsset table,
            Func<Vector3, Vector2> plate)
        {
            Transform canopy = frame.Find("Canopy"), merchant = frame.Find("Merchant");
            Transform cup = frame.Cast<Transform>().Single(child =>
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject) == CupPrefabPath);
            Assert.That(canopy, Is.Not.Null);
            Assert.That(merchant, Is.Not.Null);
            int previous = -1;
            foreach (var cut in cuts.OrderBy(candidate => candidate.DrawOrder))
            {
                var prop = frame.Find(cut.Name) as RectTransform;
                Assert.That(prop, Is.Not.Null, cut.Name);
                Assert.That(prop.gameObject.activeSelf, Is.True, cut.Name);
                // Props draw over the background in the cut's order (the pot behind the pan), under the canopy, cup and merchant.
                Assert.That(prop.GetSiblingIndex(), Is.GreaterThan(previous), cut.Name);
                previous = prop.GetSiblingIndex();
                Assert.That(prop.GetSiblingIndex(), Is.LessThan(canopy.GetSiblingIndex()), cut.Name);
                Assert.That(prop.GetSiblingIndex(), Is.LessThan(cup.GetSiblingIndex()), cut.Name);
                Assert.That(prop.GetSiblingIndex(), Is.LessThan(merchant.GetSiblingIndex()), cut.Name);

                var image = prop.GetComponent<RawImage>();
                Assert.That(image, Is.Not.Null, cut.Name);
                Assert.That(image.raycastTarget, Is.False, cut.Name + " must not intercept input.");
                Assert.That(image.uvRect, Is.EqualTo(new Rect(0, 0, 1, 1)));
                Assert.That(AssetDatabase.GetAssetPath(image.texture), Is.EqualTo(PropsFolder + cut.Name + "Unlit.png"));
                Assert.That(image.material.shader, Is.SameAs(AssetDatabase.LoadAssetAtPath<Shader>(LitShaderPath)), cut.Name);
                Assert.That(image.material.GetTexture("_LayerMask"), Is.Null, "Props have no ownership mask.");
                Assert.That(image.material.GetFloat("_InvertMask"), Is.EqualTo(1), "Without a mask the inverted selection covers the prop.");
                Assert.That(image.material.GetTexture("_SourceTex"), Is.SameAs(image.texture));

                var lit = prop.GetComponent<SceneLitImage>();
                Assert.That(lit, Is.Not.Null, cut.Name);
                Assert.That(lit.enabled, Is.True);
                Assert.That(lit.Lighting, Is.SameAs(table.SceneLighting));
                Assert.DoesNotThrow(lit.Layer.Validate);
                Assert.That(AssetDatabase.GetAssetPath(lit.Layer.BaseTexture), Is.EqualTo(PropsFolder + cut.Name + "Unlit.png"));
                var lantern = lit.Layer.Lights.Single();
                Assert.That(lantern.LightIndex, Is.Zero, "Only the lantern lights the props; no computed candle light.");
                Assert.That(lantern.Mode, Is.EqualTo(SceneLitLightMode.Reference));
                Assert.That(AssetDatabase.GetAssetPath(lantern.Reference), Is.EqualTo(PropsFolder + cut.Name + "LanternLit.png"));
                Assert.That(AssetDatabase.GetAssetPath(lantern.ResponseMap), Is.EqualTo(ResponsePath));
                var mapRect = new Rect(cut.Crop.x / PlateWidth, 1f - (cut.Crop.y + cut.Crop.height) / PlateHeight,
                    cut.Crop.width / PlateWidth, cut.Crop.height / PlateHeight);
                Assert.That(Vector4.Distance(new Vector4(lit.Layer.MapRect.x, lit.Layer.MapRect.y, lit.Layer.MapRect.width,
                    lit.Layer.MapRect.height), new Vector4(mapRect.x, mapRect.y, mapRect.width, mapRect.height)), Is.LessThan(.00001f),
                    cut.Name + " samples the plate's response map under its crop.");

                // The pivot is the cut's hang point, base centre or hinge, so motion turns and lifts about it.
                Assert.That(Vector2.Distance(prop.pivot, cut.Normalized(cut.Pivot)), Is.LessThan(.00001f), cut.Name);
                Assert.That(Vector2.Distance(plate(prop.position), cut.Pivot), Is.LessThan(.1f), cut.Name + " pivot");
                var corners = new Vector3[4];
                prop.GetWorldCorners(corners);
                Vector2 topLeft = plate(corners[1]), bottomRight = plate(corners[3]);
                Assert.That(Vector2.Distance(topLeft, cut.Crop.position), Is.LessThan(.1f), cut.Name + " top-left " + topLeft);
                Assert.That(Vector2.Distance(bottomRight, cut.Crop.max), Is.LessThan(.1f), cut.Name + " bottom-right " + bottomRight);

                // Every solid pixel of the cut lands inside the placed rectangle, so the prop covers its removed region at rest.
                var piece = Decode(PropsFolder + cut.Name + "Unlit.png");
                try
                {
                    Color32[] pixels = piece.GetPixels32();
                    int solid = 0;
                    for (int row = 0; row < piece.height; ++row)
                        for (int column = 0; column < piece.width; ++column)
                        {
                            if (pixels[row * piece.width + column].a <= 127) continue;
                            ++solid;
                            var point = new Vector2(cut.Crop.x + column + .5f, cut.Crop.y + piece.height - 1 - row + .5f);
                            Assert.That(point.x > topLeft.x && point.x < bottomRight.x && point.y > topLeft.y && point.y < bottomRight.y,
                                Is.True, cut.Name + " pixel " + point);
                        }
                    Assert.That(solid, Is.GreaterThan(500), cut.Name);
                }
                finally { Object.DestroyImmediate(piece); }
            }
        }

        private static float PeakResponse(AmbientMotionAsset asset, float strength, bool lift)
        {
            var target = new GameObject("Peak", typeof(RectTransform));
            try
            {
                ((RectTransform)target.transform).sizeDelta = new Vector2(40f, 60f);
                var motion = asset.Create(target.transform);
                motion.Strike(0, strength);
                float peak = 0f;
                for (double time = 0; time < 10; time += Step / 4f)
                {
                    motion.Apply(time);
                    peak = Mathf.Max(peak, lift ? target.transform.localPosition.y
                        : Mathf.Abs(Mathf.DeltaAngle(0f, target.transform.localEulerAngles.z)));
                }
                return peak;
            }
            finally { Object.DestroyImmediate(target); }
        }

        private static Texture2D Decode(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
            return texture;
        }

        private static List<PropCut> ReadPropCuts()
        {
            var json = JObject.Parse(File.ReadAllText(PropsFolder + "props.json"));
            Assert.That(json["source_plates"]["size"].Values<float>(), Is.EqualTo(new[] { PlateWidth, PlateHeight }));
            var cuts = new List<PropCut>();
            foreach (var piece in json["pieces"])
            {
                var crop = piece["crop_rect"];
                var pivot = piece["pivot"]["plate"].Values<float>().ToArray();
                var contacts = piece["pivot"]["base_contacts_plate"];
                var motion = piece["motion"];
                cuts.Add(new PropCut
                {
                    Name = (string)piece["name"],
                    DrawOrder = (int)piece["draw_order"],
                    Crop = new RectInt((int)crop["x"], (int)crop["y"], (int)crop["w"], (int)crop["h"]),
                    Pivot = new Vector2(pivot[0], pivot[1]),
                    Contacts = contacts == null ? Array.Empty<Vector2>() : contacts
                        .Select(point => new Vector2((float)point[0], (float)point[1])).ToArray(),
                    SuggestedDegrees = (float?)motion["rotate_degrees"] ?? 0f,
                    SuggestedLift = (float?)motion["lift_px"] ?? 0f,
                });
            }
            Assert.That(cuts.Select(cut => cut.Name), Is.EquivalentTo(PropActors.Select(prop => prop.Name)));
            return cuts;
        }

        private sealed class PropCut
        {
            public string Name;
            public int DrawOrder;
            /** Plate pixels, top-left origin. */
            public RectInt Crop;
            public Vector2 Pivot;
            public Vector2[] Contacts;
            public float SuggestedDegrees;
            public float SuggestedLift;

            /** Plate point as normalized bottom-left coordinates of the crop, like a RectTransform pivot. */
            public Vector2 Normalized(Vector2 point)
                => new Vector2((point.x - Crop.x) / Crop.width, 1f - (point.y - Crop.y) / Crop.height);
        }

        private static GameObject FindCup(GameObject[] roots)
            => roots.SelectMany(root => root.GetComponentsInChildren<Animation>(true))
                .Single(animation => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(animation.gameObject) == CupPrefabPath)
                .gameObject;

        private float PeakAfter(EncounterAmbientActivityAsset asset, Action<EncounterAmbientActivity> strike)
        {
            var props = new[] { Prop("a"), Prop("b") };
            var lighting = new RecordingLighting(1);
            var activity = new EncounterAmbientActivity(asset, lighting, new[] { Binding("a", props[0]), Binding("b", props[1]) });
            activity.Begin(4);
            Run(activity, lighting, .5f);
            strike(activity);
            Run(activity, lighting, .25f);
            return props.Max(prop => Mathf.Abs(Mathf.DeltaAngle(0f, prop.localEulerAngles.z)));
        }

        private static void Run(EncounterAmbientActivity activity, RecordingLighting lighting, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Step);
            for (int i = 0; i < steps; ++i)
            {
                lighting.Clock += Step;
                activity.Tick(Step);
            }
        }

        private static void RunUntil(EncounterAmbientActivity activity, RecordingLighting lighting, Func<bool> condition, float limit)
        {
            for (float elapsed = 0; elapsed < limit; elapsed += Step)
            {
                lighting.Clock += Step;
                activity.Tick(Step);
                if (condition()) return;
            }
            Assert.Fail("The condition was not reached within " + limit + " seconds.");
        }

        private EncounterAmbientActivityAsset Activity(AmbientLightActor[] lights, AmbientPropActor[] props)
        {
            var asset = Track(ScriptableObject.CreateInstance<EncounterAmbientActivityAsset>());
            Set(asset, "lights", lights);
            Set(asset, "props", props);
            return asset;
        }

        private AmbientSwingMotionAsset Swing(float amplitude)
        {
            var asset = Track(ScriptableObject.CreateInstance<AmbientSwingMotionAsset>());
            Set(asset, "amplitude", amplitude);
            return asset;
        }

        private static AmbientPropActor PropActor(string id, AmbientMotionAsset motion, AmbientRange? interval = null)
            => new AmbientPropActor { Id = id, Motion = motion, IdleInterval = interval ?? new AmbientRange(1000f, 1000f) };

        private static EncounterAmbientPropBinding Binding(string id, Transform target)
            => new EncounterAmbientPropBinding { ActorId = id, Target = target };

        private RectTransform Prop(string name)
        {
            var prop = Track(new GameObject(name, typeof(RectTransform)));
            var rect = (RectTransform)prop.transform;
            rect.sizeDelta = new Vector2(40f, 60f);
            return rect;
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private sealed class RecordingSound : IAmbientSoundTarget
        {
            public readonly List<(AudioClip Clip, float Volume)> Plays = new List<(AudioClip Clip, float Volume)>();

            public void PlayAmbientSound(AudioClip clip, float volume)
            {
                Assert.That(clip, Is.Not.Null);
                Plays.Add((clip, volume));
            }
        }

        private sealed class RecordingLighting : IAmbientLightingTarget
        {
            public readonly float[] Values;
            public readonly List<(double Time, int Index, float Value)> Writes = new List<(double Time, int Index, float Value)>();
            public double Clock;

            public RecordingLighting(int count) => Values = Enumerable.Repeat(1f, count).ToArray();

            public int LightCount => Values.Length;
            public float GetLightIntensity(int index) => Values[index];

            public void SetLightIntensity(int index, float multiplier)
            {
                Assert.That(float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f, Is.False);
                Values[index] = multiplier;
                Writes.Add((Clock, index, multiplier));
            }
        }
    }
}
