using System;
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
     * The die interference rule's presenter and the bay ward rule's presenter, fed the disturbances and repelled attempts
     * of a minimal test encounter whose haunting always acts and whose wards' pieces stand on the bays they protect.
     */
    public sealed class InterferencePresenterTests
    {
        private const string HauntingId = "rule.test.haunting";
        private const string OtherHauntingId = "rule.test.other_haunting";
        private const string WardId = "rule.test.ward";
        private const string PieceId = "piece.test";
        private const string InteractionId = "position";
        // Rerolling every die but the first leaves the first bay as the only target after a reroll.
        private const int RerollAllButFirst = 0b11110;
        private const float Hold = .3f;
        private const float Beat = .25f;
        private readonly List<Object> created = new List<Object>();
        private TestRulePresenterContext context;

        [SetUp]
        public void SetUp() => context = TestRulePresenterContext.Create();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                if (value != null) Object.DestroyImmediate(value);
            created.Clear();
            context.Dispose();
        }

        /** Checks a disturbance after a reroll needs no lead-in, reacts, sounds, jolts the room, moves the die by its effect, then holds / fails if any step is missing, out of order or uses the wrong movement. */
        [TestCase(DieInterferenceKind.Flip)]
        [TestCase(DieInterferenceKind.ForcedReroll)]
        public void ADisturbanceMovesItsDieThenHolds(DieInterferenceKind kind)
        {
            var catalog = Catalog(kind, wardedBays: 0);
            var disturbed = Execute(catalog, new RerollDiceAction(RerollAllButFirst)).OfType<DieDisturbedEvent>().Single();
            Assert.That(disturbed.Trigger, Is.EqualTo(InterferenceTrigger.PlayerReroll));
            var presenter = Haunting(catalog, HauntingId);
            Assert.That(presenter.LeadIn(disturbed), Is.Zero, "A disturbance after a reroll follows the roll at once.");
            var waits = TestRulePresenterContext.Waits(presenter.Present(disturbed));
            string move = (kind == DieInterferenceKind.Flip ? "flip " : "reroll ") + disturbed.Bay + " to " + disturbed.After;
            Assert.That(context.Log, Is.EqualTo(new[] { "react", "cue giggle", "jolt", move, "landed" }));
            Assert.That(waits, Is.EqualTo(new[] { Hold }));
        }

        /** Checks a disturbance or repelled attempt after Play Hand asks for the beat as its lead-in / fails if Play Hand interference is not preceded by the beat. */
        [Test]
        public void InterferenceAfterPlayHandLeadsInWithTheBeat()
        {
            var catalog = Catalog(DieInterferenceKind.Flip, wardedBays: 0, afterReroll: false);
            var disturbed = Execute(catalog, PlayHandAction.Instance).OfType<DieDisturbedEvent>().Single();
            Assert.That(disturbed.Trigger, Is.EqualTo(InterferenceTrigger.PlayHand));
            Assert.That(Haunting(catalog, HauntingId).LeadIn(disturbed), Is.EqualTo(Beat));

            // With every bay warded, the attempt after Play Hand is always repelled.
            var warded = Catalog(DieInterferenceKind.Flip, wardedBays: DiceValues.Count, afterReroll: false);
            var blocked = Execute(warded, PlayHandAction.Instance).OfType<DisturbanceBlockedEvent>().Single();
            Assert.That(blocked.Trigger, Is.EqualTo(InterferenceTrigger.PlayHand));
            Assert.That(Haunting(warded, HauntingId).LeadIn(blocked), Is.EqualTo(Beat));
        }

        /** Checks a repelled attempt reacts and sounds the blocked clip, then holds without moving a die or jolting the room / fails if a repelled attempt moves a die or jolts. */
        [Test]
        public void ARepelledAttemptReactsAndHolds()
        {
            var catalog = Catalog(DieInterferenceKind.Flip, wardedBays: 1);
            var blocked = Execute(catalog, new RerollDiceAction(RerollAllButFirst)).OfType<DisturbanceBlockedEvent>().Single();
            var presenter = Haunting(catalog, HauntingId);
            Assert.That(presenter.LeadIn(blocked), Is.Zero);
            var waits = TestRulePresenterContext.Waits(presenter.Present(blocked));
            Assert.That(context.Log, Is.EqualTo(new[] { "react", "cue grumble" }));
            Assert.That(waits, Is.EqualTo(new[] { Hold }));
        }

        /** Checks the presenter presents only its own rule's interference and nothing else / fails if it presents another rule's disturbance or a core event. */
        [Test]
        public void TheInterferencePresenterPresentsOnlyItsOwnRule()
        {
            var catalog = Catalog(DieInterferenceKind.Flip, wardedBays: 0);
            var events = Execute(catalog, new RerollDiceAction(RerollAllButFirst));
            var other = Haunting(Catalog(DieInterferenceKind.Flip, wardedBays: 0, hauntingId: OtherHauntingId), OtherHauntingId);
            foreach (var ruleEvent in events)
            {
                Assert.That(other.Present(ruleEvent), Is.Null, ruleEvent.GetType().Name);
                Assert.That(other.LeadIn(ruleEvent), Is.Zero, ruleEvent.GetType().Name);
            }
            var own = Haunting(catalog, HauntingId);
            foreach (var ruleEvent in events.Where(value => !(value is DieDisturbedEvent)))
                Assert.That(own.Present(ruleEvent), Is.Null, ruleEvent.GetType().Name);
            Assert.That(context.Log, Is.Empty);
        }

        /** Checks the ward's piece flares for an attempt the ward repels and the presentation waits for the flare, and nothing else flares it / fails if the ward ignores its block, flares another piece or flares for a disturbance. */
        [Test]
        public void TheWardFlaresItsPieceOnlyForAttemptsItRepels()
        {
            var catalog = Catalog(DieInterferenceKind.Flip, wardedBays: 1);
            var blocked = Execute(catalog, new RerollDiceAction(RerollAllButFirst)).OfType<DisturbanceBlockedEvent>().Single();
            Assert.That(blocked.ProtectingRuleId, Is.EqualTo(WardId));
            var ward = Ward(catalog, WardId);
            context.FlareDuration = .7f;
            Assert.That(ward.LeadIn(blocked), Is.Zero);
            var waits = TestRulePresenterContext.Waits(ward.Present(blocked));
            Assert.That(context.Log, Is.EqualTo(new[] { "flare " + PieceId + " " + InteractionId }));
            Assert.That(waits, Is.EqualTo(new[] { .7f }));

            context.Log.Clear();
            var otherWard = Ward(Catalog(DieInterferenceKind.Flip, wardedBays: 1, wardId: "rule.test.other_ward"), "rule.test.other_ward");
            Assert.That(otherWard.Present(blocked), Is.Null, "Another ward's presenter does not flare for this ward's block.");
            var disturbed = Execute(Catalog(DieInterferenceKind.Flip, wardedBays: 0), new RerollDiceAction(RerollAllButFirst))
                .OfType<DieDisturbedEvent>().Single();
            Assert.That(ward.Present(disturbed), Is.Null, "A disturbance that was not repelled flares nothing.");
            Assert.That(context.Log, Is.Empty);
        }

        /** Checks a ward presenter fails at creation, naming its rule, when the table shows no prop for the ward's piece / fails if it is created with nothing to flare. */
        [Test]
        public void AWardPresenterFailsAtCreationWithoutItsPiecesProp()
        {
            var catalog = Catalog(DieInterferenceKind.Flip, wardedBays: 1);
            context.ShowsPieces = false;
            var failure = Assert.Throws<InvalidOperationException>(() => Ward(catalog, WardId));
            Assert.That(failure.Message, Does.Contain(WardId));
        }

        private DieInterferencePresenter Haunting(MechanicalCatalog catalog, string ruleId)
        {
            var asset = ScriptableObject.CreateInstance<DieInterferencePresenterAsset>();
            created.Add(asset);
            using var serialized = new SerializedObject(asset);
            SetClips(serialized.FindProperty("disturbanceClips"), Clip("giggle"));
            SetClips(serialized.FindProperty("blockedClips"), Clip("grumble"));
            serialized.FindProperty("hold").floatValue = Hold;
            serialized.FindProperty("playHandBeat").floatValue = Beat;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return (DieInterferencePresenter)asset.Create(Card(ruleId), Rule(catalog, ruleId), context);
        }

        private BayWardPresenter Ward(MechanicalCatalog catalog, string ruleId)
        {
            var asset = ScriptableObject.CreateInstance<BayWardPresenterAsset>();
            created.Add(asset);
            return (BayWardPresenter)asset.Create(Card(ruleId), Rule(catalog, ruleId), context);
        }

        private EncounterRulePresentationAsset Card(string ruleId)
        {
            var card = ScriptableObject.CreateInstance<EncounterRulePresentationAsset>();
            created.Add(card);
            using var serialized = new SerializedObject(card);
            serialized.FindProperty("ruleId").stringValue = ruleId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        private AudioClip Clip(string name)
        {
            var clip = AudioClip.Create(name, 64, 1, 8000, false);
            created.Add(clip);
            return clip;
        }

        private static void SetClips(SerializedProperty property, AudioClip clip)
        {
            property.arraySize = 1;
            property.GetArrayElementAtIndex(0).objectReferenceValue = clip;
        }

        private static EncounterRuleDefinition Rule(MechanicalCatalog catalog, string ruleId)
            => catalog.GetEncounter(TestContent.EncounterId).Rules.Single(rule => rule.Id == ruleId);

        private static IReadOnlyList<RuleEvent> Execute(MechanicalCatalog catalog, EncounterAction action)
        {
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 3);
            var result = session.Execute(action, session.Snapshot.Revision);
            Assert.That(result.Accepted, Is.True, result.Rejection.ToString());
            return result.Events;
        }

        /**
         * A test encounter whose haunting always acts with the one effect, after rerolls unless told otherwise and after
         * Play Hand, with a ward standing on each of the first warded bays: the first bay's ward and piece have the given
         * ids, each further bay's ward and piece add the bay's number to them.
         */
        private static MechanicalCatalog Catalog(DieInterferenceKind kind, int wardedBays, bool afterReroll = true,
            string hauntingId = HauntingId, string wardId = WardId)
        {
            var rules = new List<EncounterRuleDefinition>
            {
                new DieInterferenceRuleDefinition(hauntingId, 100, kind == DieInterferenceKind.ForcedReroll ? 1 : 0,
                    kind == DieInterferenceKind.Flip ? 1 : 0, afterReroll, true)
            };
            var pieces = new List<TablePieceDefinition>();
            for (int bay = 0; bay < wardedBays; ++bay)
            {
                string piece = bay == 0 ? PieceId : PieceId + "." + bay;
                rules.Add(new BayWardRuleDefinition(bay == 0 ? wardId : wardId + "." + bay, piece, InteractionId, new[] { kind }));
                pieces.Add(new TablePieceDefinition(piece, new[] { new PositionInteractionDefinition(InteractionId,
                    PiecePosition.Bay(bay), PieceMovementTiming.DuringHand) }));
            }
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1000000, 20, 20) },
                rules: rules, pieces: pieces);
            return new MechanicalCatalog("test.interference.presenters", TestContent.Hands(), new[] { encounter });
        }
    }
}
