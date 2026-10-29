using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    /**
     * How a table's rule presenters receive snapshots and events, with test-created presenters that record what they
     * receive: every rule's presenter gets every event, the longest lead-in wins, and the presenters of one event run side by
     * side until the last ends. Playback knows no rule or presenter kind.
     */
    public sealed class RulePresenterDispatchTests
    {
        private static readonly EncounterRuleDefinition.Id FirstId = TestRules.IdOf("test.first");
        private static readonly EncounterRuleDefinition.Id SecondId = TestRules.IdOf("test.second");
        private static readonly EncounterRuleDefinition.Id ThirdId = TestRules.IdOf("test.third");
        private readonly List<Object> created = new List<Object>();
        private readonly List<string> log = new List<string>();
        private TestRulePresenterContext context;

        [SetUp]
        public void SetUp() => context = TestRulePresenterContext.Create();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                if (value != null) Object.DestroyImmediate(value);
            created.Clear();
            log.Clear();
            context.Dispose();
        }

        /** Checks Sync hands the snapshot and its animation choice to every rule's presenter in card order / fails if a presenter misses a snapshot or receives the wrong animation choice. */
        [Test]
        public void SyncShowsTheSnapshotThroughEveryRulesPresenter()
        {
            var presenters = Create(Card(FirstId, new Recording()), Card(SecondId, new Recording()));
            var snapshot = TestContent.Session().Snapshot;
            presenters.Sync(snapshot, true);
            presenters.Sync(null, false);
            Assert.That(log, Is.EqualTo(new[]
            {
                FirstId + " sync " + snapshot.Revision + " animated", SecondId + " sync " + snapshot.Revision + " animated",
                FirstId + " sync none still", SecondId + " sync none still"
            }));
        }

        /** Checks an event plays only the presenters that present it, in card order, and yields nothing when none does / fails if playback waits on a presenter that does not present the event or plays them out of order. */
        [Test]
        public void AnEventPlaysOnlyThePresentersThatPresentItInCardOrder()
        {
            var events = TestContent.Session().InitialResolution.Events;
            var rolled = events.OfType<DiceRolledEvent>().First();
            var started = events.OfType<HandStartedEvent>().First();
            var unpresented = TestContent.Session().InitialResolution.Events.OfType<DiceRolledEvent>().First();
            var presenters = Create(Card(FirstId, new Recording { Presents = value => value == rolled }),
                Card(SecondId, new Recording()),
                Card(ThirdId, new Recording { Presents = value => value == rolled || value == started }));

            Assert.That(presenters.Present(unpresented), Is.Null, "No presenter presents the event.");
            TestRulePresenterContext.Run(presenters.Present(started));
            Assert.That(log, Is.EqualTo(new[] { ThirdId + " step 0" }));
            log.Clear();
            TestRulePresenterContext.Run(presenters.Present(rolled));
            Assert.That(log, Is.EqualTo(new[] { FirstId + " step 0", ThirdId + " step 0" }));
        }

        /** Checks the lead-in before an event is the longest any presenter asks for, zero when none asks / fails if a shorter lead-in wins or a presenter's lead-in for another event leaks. */
        [Test]
        public void LeadInIsTheLongestAnyPresenterAsksFor()
        {
            var events = TestContent.Session().InitialResolution.Events;
            var rolled = events.OfType<DiceRolledEvent>().First();
            var started = events.OfType<HandStartedEvent>().First();
            var presenters = Create(Card(FirstId, new Recording { LeadIns = value => value == rolled ? .1f : 0f }),
                Card(SecondId, new Recording { LeadIns = value => value == rolled ? .4f : 0f }),
                Card(ThirdId, new Recording { LeadIns = value => value == started ? .2f : 0f }));
            Assert.That(presenters.LeadIn(rolled), Is.EqualTo(.4f));
            Assert.That(presenters.LeadIn(started), Is.EqualTo(.2f));
            Assert.That(presenters.LeadIn(events.OfType<StageStartedEvent>().First()), Is.Zero);

            var invalid = Create(Card(FirstId, new Recording { LeadIns = _ => -1f }));
            Assert.Throws<InvalidOperationException>(() => invalid.LeadIn(rolled));
        }

        /** Checks the presenters of one event start in the same step, run side by side and end only when the longest ends / fails if one waits for another or the event ends before every presenter. */
        [Test]
        public void PresentersOfOneEventRunSideBySideAndTheEventWaitsForAll()
        {
            var rolled = TestContent.Session().InitialResolution.Events.OfType<DiceRolledEvent>().First();
            var presenters = Create(Card(FirstId, new Recording { Presents = _ => true, Steps = 2 }),
                Card(SecondId, new Recording { Presents = _ => true, Steps = 4 }));
            var routine = presenters.Present(rolled);
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(log, Is.EqualTo(new[] { FirstId + " step 0", SecondId + " step 0" }), "Both presenters start at once.");
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(log.Skip(2), Is.EqualTo(new[] { FirstId + " step 1", SecondId + " step 1" }));
            Assert.That(routine.MoveNext(), Is.True, "The event waits for the longer presenter.");
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(log.Last(), Is.EqualTo(SecondId + " step 3"));
        }

        /** Checks a routine a presenter yields runs to its end inside the side-by-side run, and disposal ends what still runs / fails if nested routines are skipped or left running. */
        [Test]
        public void NestedRoutinesRunInsideTheEventAndDisposalEndsThem()
        {
            bool finished = false;
            IEnumerator Nested()
            {
                try
                {
                    log.Add("nested");
                    yield return null;
                    log.Add("nested end");
                }
                finally { finished = true; }
            }
            IEnumerator Outer()
            {
                yield return Nested();
                log.Add("outer end");
            }
            var run = new ConcurrentRoutines(new[] { Outer() });
            Assert.That(run.MoveNext(), Is.True);
            Assert.That(run.MoveNext(), Is.False);
            Assert.That(log, Is.EqualTo(new[] { "nested", "nested end", "outer end" }), "A finished nested routine resumes its parent at once.");

            finished = false;
            var interrupted = new ConcurrentRoutines(new[] { Outer() });
            Assert.That(interrupted.MoveNext(), Is.True);
            interrupted.Dispose();
            Assert.That(finished, Is.True, "Disposal ends the routine still running.");
        }

        /** Checks a card whose rule the encounter lacks, and a presenter asset given another kind of rule, fail at creation / fails if either is accepted. */
        [Test]
        public void PresentersFailAtCreationForAMissingOrDifferentRule()
        {
            var encounter = Encounter();
            var missing = new[] { Card(TestRules.IdOf("test.missing"), new Recording()) };
            Assert.Throws<InvalidOperationException>(() => EncounterRulePresenters.Create(missing, encounter, () => context));

            var interference = ScriptableObject.CreateInstance<DieInterferencePresenterAsset>();
            created.Add(interference);
            var free = TestRules.IdOf("first_reroll_free");
            Assert.Throws<InvalidOperationException>(() => interference.Create(Card(free, null), new FirstRerollFreeRuleDefinition(free),
                context));
        }

        /** Checks the card lookup finds the card of the named rule among the table's cards and nothing for a rule without one / fails if another card is returned. */
        [Test]
        public void ARuleIsPresentedByTheCardOfThatRule()
        {
            var first = Card(FirstId, null);
            var second = Card(SecondId, null);
            var cards = new[] { first, null, second };
            Assert.That(EncounterRulePresentationAsset.For(cards, SecondId), Is.SameAs(second));
            Assert.That(EncounterRulePresentationAsset.For(cards, FirstId), Is.SameAs(first));
            Assert.That(EncounterRulePresentationAsset.For(cards, ThirdId), Is.Null);
        }

        private EncounterRulePresenters Create(params EncounterRulePresentationAsset[] cards)
        {
            var presenters = EncounterRulePresenters.Create(cards, Encounter(), () => context);
            Assert.That(presenters.Presenters.Count, Is.EqualTo(cards.Count(card => card.Presenter != null)));
            return presenters;
        }

        /** A test encounter with the three test rules the cards present. */
        private static EncounterDefinition Encounter()
            => new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1000000, 20, 20) },
                rules: new EncounterRuleDefinition[]
                {
                    new DieBindingRuleDefinition(FirstId, false, false), new DieBindingRuleDefinition(SecondId, false, false),
                    new DieBindingRuleDefinition(ThirdId, false, false)
                });

        /** A rule card whose test presenter records its presenter's calls into this test's log. */
        private EncounterRulePresentationAsset Card(EncounterRuleDefinition.Id ruleId, Recording recording)
        {
            RecordingPresenterAsset asset = null;
            if (recording != null)
            {
                asset = ScriptableObject.CreateInstance<RecordingPresenterAsset>();
                asset.Log = log;
                asset.Recording = recording;
                created.Add(asset);
            }
            var rule = TestRules.Asset(ruleId);
            created.Add(rule);
            var card = ScriptableObject.CreateInstance<EncounterRulePresentationAsset>();
            created.Add(card);
            using var serialized = new SerializedObject(card);
            serialized.FindProperty("rule").objectReferenceValue = rule;
            serialized.FindProperty("presenter").objectReferenceValue = asset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        /** What a test presenter presents, the lead-ins it asks for and how many steps its routine takes. */
        private sealed class Recording
        {
            public Func<RuleEvent, bool> Presents = _ => false;
            public Func<RuleEvent, float> LeadIns = _ => 0f;
            public int Steps = 1;
        }

        private sealed class RecordingPresenterAsset : RulePresenterAsset
        {
            public List<string> Log;
            [NonSerialized] public Recording Recording;

            public override RulePresenter Create(EncounterRulePresentationAsset card, EncounterRuleDefinition rule,
                RulePresenterContext context)
                => new RecordingPresenter(this, rule.Identity);
        }

        private sealed class RecordingPresenter : RulePresenter
        {
            private readonly RecordingPresenterAsset asset;
            private readonly EncounterRuleDefinition.Id ruleId;

            public RecordingPresenter(RecordingPresenterAsset asset, EncounterRuleDefinition.Id ruleId)
            {
                this.asset = asset;
                this.ruleId = ruleId;
            }

            public override void Sync(EncounterSnapshot snapshot, bool animate)
                => asset.Log.Add(ruleId + " sync " + (snapshot == null ? "none" : snapshot.Revision.ToString()) +
                    (animate ? " animated" : " still"));

            public override float LeadIn(RuleEvent ruleEvent) => asset.Recording.LeadIns(ruleEvent);

            public override IEnumerator Present(RuleEvent ruleEvent)
                => asset.Recording.Presents(ruleEvent) ? Play() : null;

            private IEnumerator Play()
            {
                for (int step = 0; step < asset.Recording.Steps; ++step)
                {
                    if (step > 0) yield return null;
                    asset.Log.Add(ruleId + " step " + step);
                }
            }
        }
    }
}
