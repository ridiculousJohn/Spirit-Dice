using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    /**
     * How a table's rule dice visuals pass snapshots and events to their presenters, with test-created visuals that record
     * what they receive: playback hands every event to every rule's visuals and knows no rule or visual kind.
     */
    public sealed class RuleDiceVisualDispatchTests
    {
        private readonly List<Object> created = new List<Object>();
        private readonly List<string> log = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                if (value != null) Object.DestroyImmediate(value);
            created.Clear();
            log.Clear();
        }

        /** Checks Sync hands the snapshot and its animation choice to every rule's presenter in card order / fails if a rule's visuals miss a snapshot or receive the wrong animation choice. */
        [Test]
        public void SyncShowsTheSnapshotThroughEveryRulesPresenter()
        {
            var visuals = Create(Card("rule.first", _ => false), Card("rule.second", _ => false));
            var snapshot = TestContent.Session().Snapshot;
            visuals.Sync(snapshot, true);
            visuals.Sync(null, false);
            Assert.That(log, Is.EqualTo(new[]
            {
                "rule.first sync " + snapshot.Revision + " animated", "rule.second sync " + snapshot.Revision + " animated",
                "rule.first sync none still", "rule.second sync none still"
            }));
        }

        /** Checks an event plays only the presenters that present it, in card order, and yields nothing when none does / fails if playback waits on a rule that does not present the event or plays the rules out of order. */
        [Test]
        public void AnEventPlaysOnlyThePresentersThatPresentItInCardOrder()
        {
            var events = TestContent.Session().InitialResolution.Events;
            var rolled = events.OfType<DiceRolledEvent>().First();
            var started = events.OfType<HandStartedEvent>().First();
            var unpresented = TestContent.Session().InitialResolution.Events.OfType<DiceRolledEvent>().First();
            var visuals = Create(Card("rule.first", value => value == rolled), Card("rule.unused", _ => false),
                Card("rule.second", value => value == rolled || value == started));

            Assert.That(visuals.Present(unpresented), Is.Null, "No presenter presents the event.");
            Run(visuals.Present(started));
            Assert.That(log, Is.EqualTo(new[] { "rule.second present" }));
            log.Clear();
            Run(visuals.Present(rolled));
            Assert.That(log, Is.EqualTo(new[] { "rule.first present", "rule.second present" }));
        }

        private EncounterRuleDiceVisuals Create(params EncounterRulePresentationAsset[] cards)
        {
            var dice = new Transform[DiceValues.Count];
            for (int i = 0; i < dice.Length; ++i)
            {
                var die = new GameObject("Die " + i);
                created.Add(die);
                dice[i] = die.transform;
            }
            var visuals = EncounterRuleDiceVisuals.Create(cards, () => new RuleDiceVisualContext(dice, null, null));
            Assert.That(visuals.Presenters.Count, Is.EqualTo(cards.Length));
            return visuals;
        }

        /** A rule card whose test visual records its presenter's calls and presents the events the filter accepts. */
        private EncounterRulePresentationAsset Card(string ruleId, Func<RuleEvent, bool> presents)
        {
            var visual = ScriptableObject.CreateInstance<RecordingVisual>();
            visual.Log = log;
            visual.Presents = presents;
            created.Add(visual);
            var card = ScriptableObject.CreateInstance<EncounterRulePresentationAsset>();
            created.Add(card);
            SetField(card, "ruleId", ruleId);
            SetField(card, "diceVisual", visual);
            return card;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        /** Runs a playback routine to its end, including the routines it yields. */
        private static void Run(IEnumerator routine)
        {
            Assert.That(routine, Is.Not.Null);
            while (routine.MoveNext())
                if (routine.Current is IEnumerator nested) Run(nested);
        }

        private sealed class RecordingVisual : RuleDiceVisualAsset
        {
            public List<string> Log;
            public Func<RuleEvent, bool> Presents;

            public override RuleDiceVisualPresenter Create(string ruleId, RuleDiceVisualContext context)
                => new RecordingPresenter(this, ruleId);
        }

        private sealed class RecordingPresenter : RuleDiceVisualPresenter
        {
            private readonly RecordingVisual visual;
            private readonly string ruleId;

            public RecordingPresenter(RecordingVisual visual, string ruleId)
            {
                this.visual = visual;
                this.ruleId = ruleId;
            }

            public override void Sync(EncounterSnapshot snapshot, bool animate)
                => visual.Log.Add(ruleId + " sync " + (snapshot == null ? "none" : snapshot.Revision.ToString()) +
                    (animate ? " animated" : " still"));

            public override IEnumerator Present(RuleEvent ruleEvent) => visual.Presents(ruleEvent) ? Play() : null;

            public override void Dispose() { }

            private IEnumerator Play()
            {
                visual.Log.Add(ruleId + " present");
                yield break;
            }
        }
    }
}
