using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    /** Ambient activity and the ambient motion kinds, driven with synthetic actors, props and recording targets. */
    public sealed class EncounterAmbientActivityTests
    {
        internal const float Step = 1f / 60f;
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                if (value != null) Object.DestroyImmediate(value);
            created.Clear();
        }

        // Activity

        [Test]
        public void LightOutageSputtersOutStaysDarkAndReturnsToExactlyFull()
        {
            var actor = new AmbientLightActor
            {
                LightIndex = 1, IdleInterval = new AmbientRange(2f, 2f), DarkDuration = new AmbientRange(1f, 1f),
                SputterFlickers = new AmbientCount(2, 2), FlickerDuration = new AmbientRange(.25f, .25f),
                DimLevel = new AmbientRange(.2f, .2f), BrightLevel = new AmbientRange(.6f, .6f),
            };
            var lighting = new RecordingLighting(2);
            var activity = new EncounterAmbientActivity(Activity(new[] { actor }, Array.Empty<AmbientPropActor>()), lighting,
                Array.Empty<EncounterAmbientPropBinding>());
            activity.Begin(1);
            Run(activity, lighting, 5.5f);

            Assert.That(lighting.Writes.Where(write => write.Index == 0), Is.Empty, "Only the actor's light is driven.");
            var writes = lighting.Writes.Where(write => write.Index == 1).ToList();
            // Full, then dim and bright flickers out, dark, bright and dim flickers back, and exactly full again.
            Assert.That(writes.Select(write => write.Value), Is.EqualTo(new[] { 1f, .2f, .6f, 0f, .6f, .2f, 1f }));
            Assert.That(writes[1].Time, Is.EqualTo(2.0).Within(Step), "The outage starts after its idle interval.");
            Assert.That(writes[4].Time - writes[3].Time, Is.EqualTo(1.0).Within(Step), "The light stays dark for its dark duration.");
            Assert.That(writes[6].Time, Is.EqualTo(4.0).Within(Step), "Each flicker holds for its flicker duration.");

            Run(activity, lighting, .6f);
            var next = lighting.Writes.Where(write => write.Index == 1).Skip(writes.Count).ToList();
            Assert.That(next, Has.Count.EqualTo(1));
            Assert.That(next[0].Time, Is.EqualTo(6.0).Within(Step), "The next outage is timed from the moment the light came back.");
        }

        [Test]
        public void TheActivityRandomIsSeededWhenItBegins()
        {
            var actor = new AmbientLightActor { LightIndex = 0, IdleInterval = new AmbientRange(1f, 3f) };
            var asset = Activity(new[] { actor }, Array.Empty<AmbientPropActor>());
            List<(double Time, int Index, float Value)> Record(int seed)
            {
                var lighting = new RecordingLighting(1);
                var activity = new EncounterAmbientActivity(asset, lighting, Array.Empty<EncounterAmbientPropBinding>());
                activity.Begin(seed);
                Run(activity, lighting, 30f);
                return lighting.Writes;
            }
            Assert.That(Record(12345), Is.EqualTo(Record(12345)), "The same seed repeats the schedule.");
            Assert.That(Record(54321), Is.Not.EqualTo(Record(12345)), "Another seed changes it.");
        }

        [Test]
        public void JoltStuttersTheLightAndStrikesOneRandomProp()
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
                activity.Jolt();
                Assert.That(lighting.Writes, Is.Empty, "Nothing happens before the first hand.");
                activity.Begin(seed);
                Run(activity, lighting, .1f);
                int before = lighting.Writes.Count;
                activity.Jolt();
                Run(activity, lighting, .1f);
                var moved = props.Where(prop => prop.localRotation != Quaternion.identity).ToArray();
                Assert.That(moved, Has.Length.EqualTo(1), "Seed " + seed);
                struck.Add(moved[0].name);
                Run(activity, lighting, 1f);
                var stutter = lighting.Writes.Skip(before).ToList();
                Assert.That(stutter.Min(write => write.Value), Is.LessThan(1f), "The light stutters.");
                Assert.That(stutter.Last().Value, Is.EqualTo(1f), "The stutter ends at full intensity.");
            }
            Assert.That(struck.Count, Is.GreaterThan(1), "The jolted prop is chosen at random.");
        }

        [Test]
        public void SettleReturnsLightsSteadilyAndPropsToRestAndStopsIdleEvents()
        {
            var light = new AmbientLightActor { LightIndex = 0, IdleInterval = new AmbientRange(2f, 3f) };
            var prop = Prop("Cup");
            var asset = Activity(new[] { light }, new[] { PropActor("cup", Swing(10f), new AmbientRange(1f, 2f)) });
            var lighting = new RecordingLighting(1);
            var activity = new EncounterAmbientActivity(asset, lighting, new[] { Binding("cup", prop) });
            activity.Begin(7);
            RunUntil(activity, lighting, () => lighting.Values[0] == 0f, 10f);
            activity.Jolt();
            Run(activity, lighting, .2f);
            Assert.That(prop.localRotation, Is.Not.EqualTo(Quaternion.identity));

            int settled = lighting.Writes.Count;
            activity.Settle();
            Assert.That(activity.IsRunning, Is.False);
            Run(activity, lighting, asset.LightRecovery + 2 * Step);
            var recovery = lighting.Writes.Skip(settled).Select(write => write.Value).ToArray();
            Assert.That(recovery, Is.Ordered, "Lights return steadily, without flicker.");
            Assert.That(lighting.Values[0], Is.EqualTo(1f));
            Run(activity, lighting, 5f);
            Assert.That(prop.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(prop.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(activity.IsAtRest, Is.True);

            int writes = lighting.Writes.Count;
            activity.Jolt();
            Run(activity, lighting, 120f);
            Assert.That(lighting.Writes.Count, Is.EqualTo(writes), "Idle events and jolts stop after settling.");
            Assert.That(prop.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void KnockStrikesOnePropAtTheDefeatStrengthOnceAndStops()
        {
            var swing = Swing(10f);
            var asset = Activity(Array.Empty<AmbientLightActor>(), new[] { PropActor("a", swing), PropActor("b", swing) });
            float disturbed = PeakAfter(asset, activity => activity.Jolt());
            float knocked = PeakAfter(asset, activity => activity.Knock());
            Assert.That(knocked / disturbed, Is.EqualTo(asset.DefeatStrength / asset.JoltStrength).Within(.001f),
                "The knock strikes at the defeat strength rather than the jolt strength.");

            var props = new[] { Prop("a"), Prop("b") };
            var lighting = new RecordingLighting(0);
            var knock = new EncounterAmbientActivity(asset, lighting, new[] { Binding("a", props[0]), Binding("b", props[1]) });
            knock.Begin(4);
            knock.Knock();
            Assert.That(knock.IsRunning, Is.False);
            var peaks = new float[2];
            for (int frame = 0; frame < 30 / Step; ++frame)
            {
                lighting.Clock += Step;
                knock.Tick(Step);
                for (int i = 0; i < props.Length; ++i)
                    peaks[i] = Mathf.Max(peaks[i], Mathf.Abs(Mathf.DeltaAngle(0f, props[i].localEulerAngles.z)));
            }
            Assert.That(peaks.Count(peak => peak > 0f), Is.EqualTo(1), "Exactly one prop knocks.");
            Assert.That(props.All(prop => prop.localRotation == Quaternion.identity), Is.True);
            Assert.That(knock.IsAtRest, Is.True);
            knock.Knock();
            knock.Jolt();
            Run(knock, lighting, 1f);
            Assert.That(props.All(prop => prop.localRotation == Quaternion.identity), Is.True, "The knock happens once.");
        }

        [Test]
        public void StopRestoresFullLightAndRestPosesAtOnce()
        {
            var light = new AmbientLightActor { LightIndex = 0, IdleInterval = new AmbientRange(1f, 1f) };
            var prop = Prop("Cup");
            var asset = Activity(new[] { light }, new[] { PropActor("cup", Swing(10f)) });
            var lighting = new RecordingLighting(1);
            var activity = new EncounterAmbientActivity(asset, lighting, new[] { Binding("cup", prop) });
            activity.Begin(9);
            RunUntil(activity, lighting, () => lighting.Values[0] == 0f, 10f);
            activity.Jolt();
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
        public void EachStrikePlaysAClipOfItsTierAtItsVolumeWithoutImmediateRepeats()
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
            var lighting = new RecordingLighting(0);
            var sound = new RecordingSound();
            var activity = new EncounterAmbientActivity(Activity(Array.Empty<AmbientLightActor>(), new[] { actor }), lighting,
                new[] { Binding("cup", Prop("Cup")) }, sound);
            activity.Begin(31);
            activity.Tick(3.5f);
            Assert.That(sound.Plays, Has.Count.EqualTo(1), "Idle jolts that fall due together in one long step sound once.");
            Run(activity, lighting, 5f);
            Assert.That(sound.Plays, Has.Count.EqualTo(6), "Each idle jolt sounds once.");
            AssertTier(sound.Plays, idle, .2f, "Idle");

            sound.Plays.Clear();
            for (int i = 0; i < 10; ++i) activity.Jolt();
            Assert.That(sound.Plays, Has.Count.EqualTo(10), "Each jolt sounds once.");
            AssertTier(sound.Plays, hit, .5f, "Hit");

            sound.Plays.Clear();
            activity.Knock();
            activity.Jolt();
            activity.Knock();
            Run(activity, lighting, 30f);
            Assert.That(sound.Plays, Has.Count.EqualTo(1), "The final knock sounds once and nothing sounds after it.");
            AssertTier(sound.Plays, defeat, .8f, "Defeat");
        }

        /** Checks the encounter's end knocks or settles as the asset authors for each outcome / fails if the outcome's authored ending is ignored. */
        [Test]
        public void TheEndingFollowsTheAssetsEndingForTheOutcome()
        {
            var actor = PropActor("a", Swing(10f));
            actor.DefeatClips = Clips("knock", 1);
            var asset = Activity(Array.Empty<AmbientLightActor>(), new[] { actor });
            SyntheticUi.Set(asset, "wonEnding", AmbientEnding.Knock);
            SyntheticUi.Set(asset, "lostEnding", AmbientEnding.Settle);
            SyntheticUi.Set(asset, "abandonedEnding", AmbientEnding.Knock);
            foreach (var (outcome, knocks) in new[] { (EncounterOutcome.Won, true), (EncounterOutcome.Lost, false),
                (EncounterOutcome.Abandoned, true), (EncounterOutcome.None, false) })
            {
                var sound = new RecordingSound();
                var activity = new EncounterAmbientActivity(asset, new RecordingLighting(0), new[] { Binding("a", Prop("a")) }, sound);
                activity.Begin(5);
                activity.End(outcome);
                Assert.That(activity.IsRunning, Is.False, outcome.ToString());
                Assert.That(sound.Plays.Count, Is.EqualTo(knocks ? 1 : 0), outcome + (knocks ? " knocks." : " settles."));
            }
        }

        [Test]
        public void KnockFallsBackToAHitClipAndPropsWithoutClipsStaySilent()
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
            activity.Jolt();
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
            quiet.Jolt();
            quiet.Knock();
            Assert.That(silent.Plays, Is.Empty, "Props and activities without clips stay silent.");
        }

        [Test]
        public void EachJoltPlaysOneBurstBesideTheStruckPropsHit()
        {
            var bursts = Clips("burst", 3);
            var hit = Clips("hit", 2);
            string[] ids = { "a", "b", "c" };
            var actors = ids.Select(id => PropActor(id, Swing(10f))).ToArray();
            foreach (var actor in actors) actor.HitClips = hit;
            var asset = Activity(Array.Empty<AmbientLightActor>(), actors);
            SyntheticUi.Set(asset, "burstClips", bursts);
            SyntheticUi.Set(asset, "burstVolume", .25f);
            var lighting = new RecordingLighting(0);
            var sound = new RecordingSound();
            var activity = new EncounterAmbientActivity(asset, lighting, ids.Select(id => Binding(id, Prop(id))).ToArray(), sound);
            activity.Jolt();
            Assert.That(sound.Plays, Is.Empty, "Nothing sounds before the first hand.");
            activity.Begin(77);
            var burstPlays = new List<(AudioClip Clip, float Volume)>();
            for (int i = 0; i < 10; ++i)
            {
                int before = sound.Plays.Count;
                activity.Jolt();
                var played = sound.Plays.Skip(before).ToList();
                Assert.That(played.Count(play => bursts.Contains(play.Clip)), Is.EqualTo(1), "One burst per jolt.");
                Assert.That(played.Count(play => hit.Contains(play.Clip)), Is.EqualTo(1), "The jolted prop sounds its hit.");
                Assert.That(played, Has.Count.EqualTo(2));
                burstPlays.AddRange(played.Where(play => bursts.Contains(play.Clip)));
                Run(activity, lighting, .5f);
            }
            AssertTier(burstPlays, bursts, .25f, "Burst");

            var empty = new RecordingSound();
            var bare = Activity(Array.Empty<AmbientLightActor>(), Array.Empty<AmbientPropActor>());
            SyntheticUi.Set(bare, "burstClips", bursts);
            var room = new EncounterAmbientActivity(bare, lighting, Array.Empty<EncounterAmbientPropBinding>(), empty);
            room.Begin(3);
            room.Jolt();
            Assert.That(empty.Plays, Has.Count.EqualTo(1), "A jolt bursts even with no props to jolt.");
            room.Settle();
            room.Jolt();
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
            SyntheticUi.Set(asset, "burstClips", Clips("burst", 2));
            List<(double Time, int Index, float Value)> Record(IAmbientSoundTarget target, List<float> angles)
            {
                var prop = Prop("Cup");
                var lighting = new RecordingLighting(1);
                var activity = new EncounterAmbientActivity(asset, lighting, new[] { Binding("cup", prop) }, target);
                activity.Begin(2024);
                for (int second = 0; second < 40; ++second)
                {
                    if (second % 7 == 3) activity.Jolt();
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
        public void APropGroupGivesOneRandomMemberATurnAtATimeTimedFromTheLastComingToRest()
        {
            // Each member's turn is a two second run that starts away from its rest, so a member is out exactly while offset.
            var path = Path(10f, 0f, new AmbientPathMotionAsset.Waypoint(new Vector2(5f, 0f)),
                new AmbientPathMotionAsset.Waypoint(new Vector2(25f, 0f)));
            string[] ids = { "a", "b", "c" };
            var group = new AmbientPropGroup { IdleInterval = new AmbientRange(3f, 3f), Props = ids.Select(id => PropActor(id, path)).ToArray() };
            var asset = Activity(Array.Empty<AmbientLightActor>(), Array.Empty<AmbientPropActor>());
            SyntheticUi.Set(asset, "propGroups", new[] { group });
            List<(string Id, double Start, double End)> Record(int seed, bool jolt)
            {
                var props = ids.Select(Prop).ToArray();
                var lighting = new RecordingLighting(0);
                var activity = new EncounterAmbientActivity(asset, lighting, ids.Select((id, i) => Binding(id, props[i])).ToArray());
                activity.Begin(seed);
                var turns = new List<(string Id, double Start, double End)>();
                for (int frame = 1; frame <= 60 / Step; ++frame)
                {
                    lighting.Clock += Step;
                    activity.Tick(Step);
                    if (jolt && frame % 20 == 0) activity.Jolt();
                    var outside = props.Where(prop => prop.anchoredPosition3D != Vector3.zero).ToArray();
                    Assert.That(outside.Length, Is.LessThanOrEqualTo(1), "Never two members at once, at " + lighting.Clock);
                    bool wasOut = turns.Count > 0 && double.IsNaN(turns[turns.Count - 1].End);
                    if (outside.Length == 1 && !wasOut) turns.Add((outside[0].name, lighting.Clock, double.NaN));
                    else if (outside.Length == 0 && wasOut) turns[turns.Count - 1] = (turns[turns.Count - 1].Id, turns[turns.Count - 1].Start, lighting.Clock);
                }
                return turns;
            }

            var seeded = Record(11, false);
            Assert.That(seeded.Count, Is.GreaterThan(8));
            Assert.That(seeded[0].Start, Is.EqualTo(3.0).Within(Step), "The first turn falls due after the group's interval.");
            for (int i = 0; i < seeded.Count; ++i)
            {
                if (double.IsNaN(seeded[i].End)) continue;
                Assert.That(seeded[i].End - seeded[i].Start, Is.EqualTo(2.0).Within(2 * Step), "Turn " + i + " runs its path.");
                if (i > 0) Assert.That(seeded[i].Start - seeded[i - 1].End, Is.EqualTo(3.0).Within(2 * Step),
                    "Turn " + i + " falls due the interval after the last member came to rest.");
            }
            Assert.That(seeded.Select(turn => turn.Id).Distinct().Count(), Is.GreaterThan(1), "The member is chosen at random.");
            Assert.That(Record(11, false), Is.EqualTo(seeded), "The activity's seeded random chooses the members.");
            Assert.That(Record(12, false).Select(turn => turn.Id), Is.Not.EqualTo(seeded.Select(turn => turn.Id)), "Another seed chooses others.");
            Assert.That(Record(11, true), Is.EqualTo(seeded), "Jolts never strike a group's members or change its turns.");
        }

        // Ambient motion

        [Test]
        public void MotionWritesBackTheRestPoseItRecordedWhenItsStrikeDecays()
        {
            RectTransform prop = Prop("Prop");
            AmbientMotion motion = Swing(10f).Create(prop);
            motion.Strike(0, 0f);
            Assert.That(motion.IsMoving, Is.False, "A zero-strength strike does nothing.");
            // The second strike starts from a different rest pose, which the motion records afresh.
            foreach (var (position, angle, start) in new[] { (new Vector3(12.5f, -40.25f, 0f), 7f, 0.0), (new Vector3(-8f, 21.75f, 0f), -4f, 20.0) })
            {
                Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
                prop.anchoredPosition3D = position;
                prop.localRotation = rotation;
                motion.Strike(start, 1.5f);
                float peak = 0f;
                for (double time = start; time < start + 20; time += Step)
                {
                    motion.Apply(time);
                    peak = Mathf.Max(peak, Quaternion.Angle(prop.localRotation, rotation));
                }
                Assert.That(peak, Is.GreaterThan(1f), "The strike moves the prop.");
                Assert.That(motion.IsMoving || motion.IsOffset, Is.False, "The strike has decayed.");
                AssertAt(prop, position, rotation, "The prop returns to the rest pose it held when struck.");
            }
        }

        [Test]
        public void SettleDampsAMovingPropToRestBeforeItsStrikeDecays()
        {
            AmbientSwingMotionAsset swing = Swing(10f);
            RectTransform settledProp = Prop("Settled"), freeProp = Prop("Free");
            AmbientMotion settled = swing.Create(settledProp), free = swing.Create(freeProp);
            settled.Strike(0, 1.5f);
            free.Strike(0, 1.5f);
            for (double time = 0; time < .3; time += Step)
            {
                settled.Apply(time);
                free.Apply(time);
            }
            Assert.That(settled.IsOffset, Is.True, "The prop is moving when it settles.");
            settled.Settle(.3, .05f);
            for (double time = .3; time < 1.3; time += Step)
            {
                settled.Apply(time);
                free.Apply(time);
            }
            Assert.That(settled.IsMoving || settled.IsOffset, Is.False);
            AssertAt(settledProp, Vector3.zero, Quaternion.identity, "The settled prop rests.");
            Assert.That(free.IsMoving, Is.True, "Without settling the same strike is still moving.");
        }

        [Test]
        public void RestDropsTheMotionAndRestoresTheRestPoseAtOnce()
        {
            RectTransform prop = Prop("Prop");
            AmbientMotion motion = Swing(10f).Create(prop);
            motion.Strike(0, 1.5f);
            for (double time = 0; time < .3; time += Step) motion.Apply(time);
            Assert.That(Quaternion.Angle(prop.localRotation, Quaternion.identity), Is.GreaterThan(1f), "The prop is moving when it rests.");
            motion.Rest();
            Assert.That(motion.IsMoving || motion.IsOffset, Is.False);
            AssertAt(prop, Vector3.zero, Quaternion.identity, "Rest restores the pose at once.");
            motion.Apply(.4);
            AssertAt(prop, Vector3.zero, Quaternion.identity, "A rested motion no longer moves the prop.");
        }

        [Test]
        public void SwingTurnsBothWaysAboutTheTargetsPivot()
        {
            RectTransform prop = Prop("Hanging");
            var rest = new Vector3(30f, -50f, 0f);
            prop.anchoredPosition3D = rest;
            AmbientMotion motion = Swing(10f).Create(prop);
            motion.Strike(0, 1.5f);
            float counterclockwise = 0f, clockwise = 0f, drift = 0f;
            for (double time = 0; time < 3; time += Step)
            {
                motion.Apply(time);
                float turn = Mathf.DeltaAngle(0f, prop.localEulerAngles.z);
                counterclockwise = Mathf.Max(counterclockwise, turn);
                clockwise = Mathf.Min(clockwise, turn);
                drift = Mathf.Max(drift, Vector3.Distance(prop.anchoredPosition3D, rest));
            }
            Assert.That(counterclockwise, Is.GreaterThan(1f));
            Assert.That(clockwise, Is.LessThan(-1f));
            Assert.That(drift, Is.LessThan(.0001f), "The pivot stays in place.");
        }

        [Test]
        public void WobbleStandsOnItsLeftContactTurningCounterclockwiseAndItsRightTurningClockwise()
        {
            AmbientWobbleMotionAsset wobble = Track(ScriptableObject.CreateInstance<AmbientWobbleMotionAsset>());
            RectTransform prop = Prop("Standing");
            Rect bounds = prop.rect;
            Vector3 Contact(Vector2 normalized)
                => new Vector3(bounds.xMin + normalized.x * bounds.width, bounds.yMin + normalized.y * bounds.height, 0f);
            // Where a point of the prop's rect sits in its parent.
            Vector3 Placed(Vector3 local) => prop.anchoredPosition3D + prop.localRotation * local;
            Vector3 left = Contact(wobble.LeftContact), right = Contact(wobble.RightContact);
            Vector3 leftRest = Placed(left), rightRest = Placed(right);
            AmbientMotion motion = wobble.Create(prop);
            motion.Strike(0, 1.5f);
            bool turnedCounterclockwise = false, turnedClockwise = false;
            for (double time = 0; time < 2; time += Step)
            {
                motion.Apply(time);
                float turn = Mathf.DeltaAngle(0f, prop.localEulerAngles.z);
                if (Mathf.Abs(turn) < 1f) continue;
                bool onLeft = turn > 0f;
                Assert.That(Vector3.Distance(Placed(onLeft ? left : right), onLeft ? leftRest : rightRest), Is.LessThan(.001f),
                    "The standing contact stays put at " + time);
                Assert.That(Vector3.Distance(Placed(onLeft ? right : left), onLeft ? rightRest : leftRest), Is.GreaterThan(.1f),
                    "The other contact lifts at " + time);
                turnedCounterclockwise |= onLeft;
                turnedClockwise |= !onLeft;
            }
            Assert.That(turnedCounterclockwise && turnedClockwise, Is.True, "The prop rocks both ways.");
        }

        [Test]
        public void ThumpHopsUpwardWithTiltsThatAlternateSides()
        {
            RectTransform prop = Prop("Lid");
            var rest = new Vector3(5f, 7f, 0f);
            prop.anchoredPosition3D = rest;
            AmbientMotion motion = Track(ScriptableObject.CreateInstance<AmbientThumpMotionAsset>()).Create(prop);
            motion.Strike(0, 1.5f);
            float highest = 0f, lowest = 0f, counterclockwise = 0f, clockwise = 0f;
            for (double time = 0; time < 2; time += Step / 4f)
            {
                motion.Apply(time);
                float lift = prop.anchoredPosition3D.y - rest.y;
                highest = Mathf.Max(highest, lift);
                lowest = Mathf.Min(lowest, lift);
                float turn = Mathf.DeltaAngle(0f, prop.localEulerAngles.z);
                counterclockwise = Mathf.Max(counterclockwise, turn);
                clockwise = Mathf.Min(clockwise, turn);
            }
            Assert.That(highest, Is.GreaterThan(1f), "The prop hops.");
            Assert.That(lowest, Is.GreaterThanOrEqualTo(-.0001f), "The prop never drops below its rest.");
            Assert.That(counterclockwise, Is.GreaterThan(0f), "One bounce tilts one way.");
            Assert.That(clockwise, Is.LessThan(0f), "The next bounce tilts the other way.");
            Assert.That(motion.IsMoving || motion.IsOffset, Is.False);
            AssertAt(prop, rest, Quaternion.identity, "The prop lands at its rest.");
        }

        [Test]
        public void PathRunsItsWaypointsAtItsSpeedFacingItsTravelWaitingWhereAuthoredAndRestsAtTheEnd()
        {
            // Legs of 30, 40 and 50 units at 10 units per second with a one second wait at the second point: 13 seconds.
            // The art faces up at rest, so travelling right turns it by -90 degrees.
            var path = Path(10f, -90f, new AmbientPathMotionAsset.Waypoint(Vector2.zero),
                new AmbientPathMotionAsset.Waypoint(new Vector2(30f, 0f), 1f), new AmbientPathMotionAsset.Waypoint(new Vector2(30f, 40f)),
                new AmbientPathMotionAsset.Waypoint(Vector2.zero));
            RectTransform prop = Prop("Creature");
            var rest = new Vector3(12f, -7f, 0f);
            Quaternion restRotation = Quaternion.Euler(0f, 0f, 10f);
            prop.anchoredPosition3D = rest;
            prop.localRotation = restRotation;
            AmbientMotion motion = path.Create(prop);
            void At(double time, Vector2 offset, float turn, string message)
            {
                motion.Apply(time);
                AssertAt(prop, rest + (Vector3)offset, restRotation * Quaternion.Euler(0f, 0f, turn), message);
            }
            motion.Strike(2, .4f);
            At(2, Vector2.zero, -90f, "The run starts at its first point facing its first leg.");
            At(3.5, new Vector2(15f, 0f), -90f, "Halfway along the first leg.");
            At(5.5, new Vector2(30f, 0f), -90f, "Waiting at the second point keeps the facing it arrived with.");
            motion.Strike(5.5, 1f);
            motion.Settle(5.5, .05f);
            At(8, new Vector2(30f, 20f), 0f, "A strike or settle during the run leaves it on its path; halfway up the second leg.");
            At(12.5, new Vector2(15f, 20f), Mathf.Atan2(-40f, -30f) * Mathf.Rad2Deg - 90f, "Halfway along the last leg.");
            Assert.That(motion.IsMoving, Is.True);
            motion.Apply(15);
            Assert.That(motion.IsMoving || motion.IsOffset, Is.False, "The run has ended.");
            AssertAt(prop, rest, restRotation, "The prop returns to its rest pose when the run ends.");

            RectTransform hidden = Prop("Hidden");
            var activity = new EncounterAmbientActivity(Activity(Array.Empty<AmbientLightActor>(), new[] { PropActor("creature", path) }),
                new RecordingLighting(0), new[] { Binding("creature", hidden) });
            activity.Begin(3);
            activity.Jolt();
            activity.Tick(.5f);
            activity.Knock();
            activity.Tick(.5f);
            AssertAt(hidden, Vector3.zero, Quaternion.identity, "Jolts and knocks pass a path by.");
        }

        [Test]
        public void MotionReturnsToTheAnchoredRestPoseWhenItsFrameResizesMidMotion()
        {
            // A prop anchored at the top-left corner of a frame that resizes while the prop moves.
            var frame = (RectTransform)Track(new GameObject("Frame", typeof(RectTransform))).transform;
            var original = new Vector2(540f, 960f);
            frame.sizeDelta = original;
            RectTransform prop = Prop("Prop");
            prop.SetParent(frame, false);
            prop.anchorMin = prop.anchorMax = new Vector2(0f, 1f);
            prop.pivot = new Vector2(.5f, 0f);
            var rest = new Vector2(200f, -500f);
            prop.anchoredPosition = rest;
            var resized = new Vector2(443f, 900f);
            AmbientMotion motion = Track(ScriptableObject.CreateInstance<AmbientWobbleMotionAsset>()).Create(prop);
            motion.Strike(.1, 1.5f);
            bool done = false;
            for (double time = 0; time < 5; time += Step)
            {
                if (!done && time >= .2)
                {
                    Assert.That(motion.IsOffset, Is.True, "The prop is mid-motion when the frame resizes.");
                    Vector3 before = prop.localPosition;
                    frame.sizeDelta = resized;
                    foreach (var rect in frame.GetComponentsInChildren<RectTransform>(true)) rect.ForceUpdateRectTransforms();
                    var corner = new Vector3((original.x - resized.x) / 2f, -(original.y - resized.y) / 2f, 0f);
                    Assert.That(Vector3.Distance(prop.localPosition - before, corner), Is.LessThan(.0001f),
                        "The resize moves the prop's local position with its corner anchor.");
                    done = true;
                }
                motion.Apply(time);
            }
            Assert.That(done, Is.True);
            Assert.That(motion.IsMoving || motion.IsOffset, Is.False);
            AssertAt(prop, rest, Quaternion.identity, "The prop returns to its anchored rest, not one displaced by the resize.");
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

        private static void AssertAt(RectTransform prop, Vector3 position, Quaternion rotation, string message)
        {
            Assert.That(Vector3.Distance(prop.anchoredPosition3D, position), Is.LessThan(.0001f),
                message + " Position " + prop.anchoredPosition3D.ToString("F5"));
            Assert.That(Quaternion.Angle(prop.localRotation, rotation), Is.LessThan(.0001f), message + " Rotation");
        }

        internal static float Peak(AudioClip clip)
        {
            Assert.That(clip.LoadAudioData(), Is.True, clip.name);
            var samples = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(samples, 0), Is.True, clip.name);
            return samples.Max(Mathf.Abs);
        }

        private AudioClip[] Clips(string prefix, int count)
            => Enumerable.Range(1, count).Select(index => Track(AudioClip.Create(prefix + index, 4410, 1, 44100, false))).ToArray();

        private float PeakAfter(EncounterAmbientActivityAsset asset, Action<EncounterAmbientActivity> strike)
        {
            var props = new[] { Prop("a"), Prop("b") };
            var lighting = new RecordingLighting(0);
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
            SyntheticUi.Set(asset, "lights", lights);
            SyntheticUi.Set(asset, "props", props);
            return asset;
        }

        private AmbientSwingMotionAsset Swing(float amplitude)
        {
            var asset = Track(ScriptableObject.CreateInstance<AmbientSwingMotionAsset>());
            SyntheticUi.Set(asset, "amplitude", amplitude);
            return asset;
        }

        private AmbientPathMotionAsset Path(float speed, float facingOffset, params AmbientPathMotionAsset.Waypoint[] waypoints)
        {
            var asset = Track(ScriptableObject.CreateInstance<AmbientPathMotionAsset>());
            SyntheticUi.Set(asset, "waypoints", waypoints);
            SyntheticUi.Set(asset, "speed", speed);
            SyntheticUi.Set(asset, "facingOffset", facingOffset);
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
