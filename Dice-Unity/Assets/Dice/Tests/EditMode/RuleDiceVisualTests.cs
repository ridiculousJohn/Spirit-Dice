using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    /**
     * Rule-supplied dice visuals: the binding kind's states from events and snapshots, generic dispatch, and light flares.
     * Mechanism tests use a test-created binding visual and per-die prefab.
     */
    public sealed class RuleDiceVisualTests
    {
        private const string BindingId = "rule.test.binding";
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                if (value != null) Object.DestroyImmediate(value);
            created.Clear();
        }

        [Test]
        public void FlaresRiseHoldAndFallBackToNothing()
        {
            var flares = new SceneLightFlares();
            flares.Resize(2);
            Assert.That(flares.Level(1, 0), Is.Zero);
            flares.Start(1, .8f, .2f, .3f, 1f, 10);
            Assert.That(flares.Level(1, 10), Is.Zero, "A flare rises from the light's present raise.");
            Assert.That(flares.Level(1, 10.1), Is.EqualTo(.4f).Within(.0001f));
            Assert.That(flares.Level(1, 10.3), Is.EqualTo(.8f));
            Assert.That(flares.Level(1, 10.5), Is.EqualTo(.8f).Within(.0001f));
            Assert.That(flares.Level(1, 11), Is.EqualTo(.8f * .25f).Within(.0001f), "The fall eases out.");
            Assert.That(flares.Level(1, 11.6), Is.Zero);
            Assert.That(flares.Level(0, 10.3), Is.Zero, "Each light keeps its own flare.");

            flares.Start(1, 1f, .2f, 0f, 1f, 20);
            flares.Start(1, .2f, .4f, 0f, 1f, 20.2);
            Assert.That(flares.Level(1, 20.2), Is.EqualTo(1f), "A replacing flare starts from the level reached.");
            flares.Clear();
            Assert.That(flares.Level(1, 20.2), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => flares.Start(2, 1, 0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => flares.Start(0, -1, 0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => flares.Start(0, 1, float.NaN, 0, 0, 0));
        }

        [Test]
        public void ShellVisualDrivesItsShellsThroughTheNormalizedLevels()
        {
            var visual = Instantiate(ShellPrefab(), null);
            var shell = (DieBindingShellVisual)visual;
            shell.MarkShell.enabled = shell.RootShell.enabled = true;
            // Edit mode does not run Awake on instantiation; the visual's creation is simulated here.
            typeof(DieBindingVisual).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(visual, null);
            Assert.That(shell.MarkShell.enabled || shell.RootShell.enabled, Is.False, "A new visual shows nothing.");
            visual.AnimateMark(1f, .4f);
            Assert.That(visual.IsAnimating, Is.True);
            Assert.That(shell.MarkShell.enabled, Is.False);
            visual.Advance(.2f);
            Assert.That(visual.Mark, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(shell.MarkShell.enabled, Is.True);
            visual.Advance(.3f);
            Assert.That(visual.Mark, Is.EqualTo(1f));
            Assert.That(visual.IsAnimating, Is.False);

            visual.AnimateGrowth(1f, .5f);
            visual.AnimateMark(0f, .3f);
            visual.Advance(.25f);
            Assert.That(visual.Growth, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(shell.RootShell.enabled, Is.True);
            visual.Advance(.25f);
            Assert.That(visual.Mark, Is.Zero);
            Assert.That(visual.Growth, Is.EqualTo(1f));
            Assert.That(shell.MarkShell.enabled, Is.False);
            var block = new MaterialPropertyBlock();
            shell.RootShell.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_Coverage"), Is.EqualTo(1f));

            visual.Show(0f, 0f);
            Assert.That(shell.MarkShell.enabled || shell.RootShell.enabled, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => visual.Show(1.1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => visual.AnimateGrowth(1f, -1f));
        }

        [Test]
        public void BindingPresenterMarksBindsAndReleasesWithTheSpiritsReactionsAndFlares()
        {
            AssertBindingSequence(BindingAsset(), BindingId);
        }

        [Test]
        public void SyncShowsSnapshotsAtOnceAndIgnoresOtherRules()
        {
            var fixture = new PresenterFixture(this, BindingAsset(), BindingId);
            var session = EncounterSession.CreateEncounter(Catalog(), TestContent.EncounterId, 11);
            var reroll = session.Execute(new RerollDiceAction(1 << Bay(DiceValues.AllMask & ~session.Snapshot.MarkedMask)),
                session.Snapshot.Revision);
            fixture.Presenter.Sync(reroll.Snapshot, false);
            for (int bay = 0; bay < DiceValues.Count; ++bay) Assert.That(fixture.Presenter[bay].IsAnimating, Is.False);
            AssertLevels(fixture, reroll.Snapshot.MarkedMask, reroll.Snapshot.BoundMask, "A resumed state");
            fixture.Presenter.Sync(null, true);
            fixture.Settle();
            AssertLevels(fixture, 0, 0, "No snapshot");

            var other = EncounterSession.CreateEncounter(Catalog("rule.other.binding"), TestContent.EncounterId, 11);
            foreach (var ruleEvent in other.InitialResolution.Events.Where(value => !(value is HandScoredEvent)))
                Assert.That(fixture.Presenter.Present(ruleEvent), Is.Null, "Another rule's events are not this card's.");
            Assert.That(fixture.Presenter.Present(other.InitialResolution.Events.OfType<HandStartedEvent>().Single()), Is.Null);
            Assert.That(fixture.Presenter.Present(other.InitialResolution.Events.OfType<DiceRolledEvent>().Single()), Is.Null);
            Assert.That(fixture.Reactions, Is.Zero);
        }

        [Test]
        public void OnlyRuleCardsWithDiceVisualsBuildAContextAndPresentersWhoseDisposalRemovesTheirDieVisuals()
        {
            var plain = new[] { Card("rule.test.plain", null), null, Card("rule.test.other", null) };
            bool built = false;
            Assert.That(EncounterRuleDiceVisuals.Create(plain, () => { built = true; return null; }), Is.Null);
            Assert.That(built, Is.False, "Cards without dice visuals build no context.");

            var cards = new[] { Card("rule.test.plain", null), Card(BindingId, BindingAsset()) };
            var context = new RuleDiceVisualContext(Dice(), null, null);
            var visuals = EncounterRuleDiceVisuals.Create(cards, () => context);
            try
            {
                Assert.That(visuals.Presenters.Single(), Is.InstanceOf<DieBindingVisualPresenter>());
                foreach (var die in context.Dice) Assert.That(die.GetComponentsInChildren<DieBindingVisual>().Length, Is.EqualTo(1));
            }
            finally { visuals.Dispose(); }
            foreach (var die in context.Dice) Assert.That(die.GetComponentsInChildren<DieBindingVisual>(), Is.Empty);
        }

        /**
         * Marks, binds and releases through the asset's presenter on a session of a binding rule with the given id; the
         * expected flares are the asset's own mark and binding flares.
         */
        private void AssertBindingSequence(DieBindingVisualAsset asset, string ruleId)
        {
            string name = asset.name;
            var fixture = new PresenterFixture(this, asset, ruleId);
            var markFlare = asset.FlareLight >= 0 ? new[] { Flare(asset, "mark") } : Array.Empty<RecordedFlare>();
            var bindFlare = asset.FlareLight >= 0 ? new[] { Flare(asset, "bind") } : Array.Empty<RecordedFlare>();
            var session = EncounterSession.CreateEncounter(Catalog(ruleId), TestContent.EncounterId, 7);
            var opening = session.InitialResolution;
            int marked = Bay(session.Snapshot.MarkedMask);
            foreach (var ruleEvent in opening.Events)
            {
                var routine = fixture.Presenter.Present(ruleEvent);
                Assert.That(routine != null, Is.EqualTo(ruleEvent is DieMarkedEvent), name + ": " + ruleEvent.GetType().Name);
                Run(routine);
            }
            fixture.Settle();
            AssertLevels(fixture, 1 << marked, 0, name + ": the opening roll's mark");
            Assert.That(fixture.Reactions, Is.EqualTo(1), name);
            Assert.That(fixture.Lights.Flares, Is.EqualTo(markFlare), name + ": a mark plays the asset's mark flare.");

            var reroll = session.Execute(new RerollDiceAction(DiceValues.AllMask & ~(1 << marked)), session.Snapshot.Revision);
            Assert.That(reroll.Accepted, Is.True);
            var bound = reroll.Events.OfType<DieBoundEvent>().Single();
            fixture.Lights.Flares.Clear();
            var routineBound = fixture.Presenter.Present(bound);
            Assert.That(routineBound, Is.Not.Null, name);
            var die = fixture.Presenter[bound.Bay];
            Assert.That(die.GrowthTarget, Is.EqualTo(1f), name + ": the roots grow when the die binds.");
            Assert.That(die.MarkTarget, Is.Zero, name + ": the mark gives way to the roots.");
            Assert.That(fixture.Lights.Flares, Is.EqualTo(bindFlare), name + ": a binding plays the asset's binding flare.");
            Run(routineBound);
            Run(fixture.Presenter.Present(reroll.Events.OfType<DieMarkedEvent>().Single()));
            fixture.Settle();
            AssertLevels(fixture, reroll.Snapshot.MarkedMask, reroll.Snapshot.BoundMask, name + ": after the reroll");
            Assert.That(fixture.Reactions, Is.EqualTo(3), name);

            var played = session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
            Assert.That(played.Accepted, Is.True);
            bool released = false;
            foreach (var ruleEvent in played.Events)
            {
                var routine = fixture.Presenter.Present(ruleEvent);
                if (ruleEvent is HandScoredEvent)
                {
                    Assert.That(routine, Is.Not.Null, name + ": scoring releases the bindings.");
                    for (int bay = 0; bay < DiceValues.Count; ++bay)
                        Assert.That(fixture.Presenter[bay].MarkTarget + fixture.Presenter[bay].GrowthTarget, Is.Zero, name);
                    fixture.Settle();
                    AssertLevels(fixture, 0, 0, name + ": released after scoring");
                    released = true;
                }
                Run(routine);
            }
            Assert.That(released, Is.True, name);
            fixture.Settle();
            AssertLevels(fixture, played.Snapshot.MarkedMask, 0, name + ": the next hand's opening mark");
            fixture.Presenter.Dispose();
        }

        private static void AssertLevels(PresenterFixture fixture, int markedMask, int boundMask, string message)
        {
            for (int bay = 0; bay < DiceValues.Count; ++bay)
            {
                Assert.That(fixture.Presenter[bay].Mark, Is.EqualTo((markedMask & (1 << bay)) != 0 ? 1f : 0f), message + ": mark " + bay);
                Assert.That(fixture.Presenter[bay].Growth, Is.EqualTo((boundMask & (1 << bay)) != 0 ? 1f : 0f), message + ": roots " + bay);
            }
        }

        /** The asset's serialized mark or binding flare, as the presenter hands it to the scene lights. */
        private static RecordedFlare Flare(DieBindingVisualAsset asset, string prefix)
        {
            using var serialized = new SerializedObject(asset);
            float Value(string field)
            {
                var property = serialized.FindProperty(prefix + field);
                Assert.That(property, Is.Not.Null, prefix + field);
                return property.floatValue;
            }
            return new RecordedFlare(asset.FlareLight, Value("FlarePeak"), Value("FlareRise"), Value("FlareHold"), Value("FlareFall"));
        }

        /** Runs a playback routine to its end, skipping its realtime waits. */
        private static void Run(IEnumerator routine)
        {
            if (routine == null) return;
            int steps = 0;
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator nested && !(nested is CustomYieldInstruction)) Run(nested);
                Assert.That(++steps, Is.LessThan(100));
            }
        }

        private static int Bay(int mask)
        {
            for (int bay = 0; bay < DiceValues.Count; ++bay)
                if ((mask & (1 << bay)) != 0) return bay;
            throw new ArgumentException("No die in the mask.", nameof(mask));
        }

        private static MechanicalCatalog Catalog(string ruleId = BindingId)
        {
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1000000, 20, 20) },
                rules: new EncounterRuleDefinition[] { new DieBindingRuleDefinition(ruleId, true, true) });
            return new MechanicalCatalog("test.binding.visuals", TestContent.Hands(), new[] { encounter });
        }

        /** Five stand-in dice. */
        private Transform[] Dice()
        {
            var dice = new Transform[DiceValues.Count];
            for (int i = 0; i < dice.Length; ++i)
            {
                var die = new GameObject("Die " + i);
                created.Add(die);
                dice[i] = die.transform;
            }
            return dice;
        }

        /** A test shell visual with mark and root shells, standing in for an authored per-die prefab. */
        private DieBindingShellVisual ShellPrefab()
        {
            var root = new GameObject("Test Binding Shell");
            created.Add(root);
            var shell = root.AddComponent<DieBindingShellVisual>();
            var mark = new GameObject("Mark Shell");
            mark.transform.SetParent(root.transform, false);
            var roots = new GameObject("Root Shell");
            roots.transform.SetParent(root.transform, false);
            using var serialized = new SerializedObject(shell);
            serialized.FindProperty("markShell").objectReferenceValue = mark.AddComponent<MeshRenderer>();
            serialized.FindProperty("rootShell").objectReferenceValue = roots.AddComponent<MeshRenderer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return shell;
        }

        /** A test binding visual over the test shell prefab, with distinct mark and binding flares on light 1. */
        private DieBindingVisualAsset BindingAsset()
        {
            var asset = ScriptableObject.CreateInstance<DieBindingVisualAsset>();
            asset.name = "Test Binding Visual";
            created.Add(asset);
            using var serialized = new SerializedObject(asset);
            serialized.FindProperty("diePrefab").objectReferenceValue = ShellPrefab();
            serialized.FindProperty("flareLight").intValue = 1;
            serialized.FindProperty("markFlarePeak").floatValue = .25f;
            serialized.FindProperty("markFlareRise").floatValue = .1f;
            serialized.FindProperty("markFlareHold").floatValue = .05f;
            serialized.FindProperty("markFlareFall").floatValue = .6f;
            serialized.FindProperty("bindFlarePeak").floatValue = .9f;
            serialized.FindProperty("bindFlareRise").floatValue = .2f;
            serialized.FindProperty("bindFlareHold").floatValue = .4f;
            serialized.FindProperty("bindFlareFall").floatValue = 1.1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private EncounterRulePresentationAsset Card(string ruleId, RuleDiceVisualAsset diceVisual)
        {
            var card = ScriptableObject.CreateInstance<EncounterRulePresentationAsset>();
            created.Add(card);
            using var serialized = new SerializedObject(card);
            serialized.FindProperty("ruleId").stringValue = ruleId;
            serialized.FindProperty("diceVisual").objectReferenceValue = diceVisual;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        private T Instantiate<T>(T prefab, Transform parent) where T : Component
        {
            var instance = Object.Instantiate(prefab, parent, false);
            created.Add(instance.gameObject);
            return instance;
        }

        private readonly struct RecordedFlare : IEquatable<RecordedFlare>
        {
            public readonly int Index;
            public readonly float Peak, Rise, Hold, Fall;

            public RecordedFlare(int index, float peak, float rise, float hold, float fall)
            {
                Index = index;
                Peak = peak;
                Rise = rise;
                Hold = hold;
                Fall = fall;
            }

            public bool Equals(RecordedFlare other)
                => Index == other.Index && Peak == other.Peak && Rise == other.Rise && Hold == other.Hold && Fall == other.Fall;

            public override bool Equals(object obj) => obj is RecordedFlare other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Index, Peak, Rise, Hold, Fall);
            public override string ToString() => "light " + Index + " peak " + Peak + " rise " + Rise + " hold " + Hold + " fall " + Fall;
        }

        private sealed class RecordingLights : IRuleSceneLights
        {
            public readonly List<RecordedFlare> Flares = new List<RecordedFlare>();
            public int LightCount => SceneLightingAsset.MaximumLights;
            public void Flare(int index, float peak, float rise, float hold, float fall)
                => Flares.Add(new RecordedFlare(index, peak, rise, hold, fall));
        }

        /** Five stand-in dice with a binding visual's presenter created under them. */
        private sealed class PresenterFixture
        {
            public readonly RecordingLights Lights = new RecordingLights();
            public readonly RuleDiceVisualContext Context;
            public readonly DieBindingVisualPresenter Presenter;
            public int Reactions;

            public PresenterFixture(RuleDiceVisualTests owner, DieBindingVisualAsset asset, string ruleId)
            {
                Context = new RuleDiceVisualContext(owner.Dice(), () => ++Reactions, Lights);
                Presenter = (DieBindingVisualPresenter)asset.Create(ruleId, Context);
            }

            /** Lets every running animation finish. */
            public void Settle()
            {
                for (int bay = 0; bay < DiceValues.Count; ++bay) Presenter[bay].Advance(10f);
            }
        }
    }
}
