using System;
using System.Linq;
using System.Text;
using Dice.App;
using Dice.Engine;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class DieBindingTests
    {
        private const string BindingId = "rule.test.binding";
        private const string HauntingId = "rule.test.haunting";

        [Test]
        public void TheOpeningRollIsMarkedWithoutChangingItsFacesOrTheDiceStream()
        {
            for (ulong seed = 0; seed < 20; ++seed)
            {
                var session = Session(Catalog(), seed);
                var plain = Session(Catalog(binding: false), seed);
                var snapshot = session.Snapshot;
                Assert.That(DiceValues.CountBays(snapshot.Binding().MarkedMask), Is.EqualTo(1));
                Assert.That(snapshot.Binding().BoundMask, Is.Zero);
                Assert.That(snapshot.LockedMask, Is.Zero);
                Assert.That(snapshot.CurrentDice, Is.EqualTo(plain.Snapshot.CurrentDice), "Marking never changes a face.");
                Assert.That(snapshot.InitialDice, Is.EqualTo(snapshot.CurrentDice));
                var events = session.InitialResolution.Events;
                var marked = events.OfType<DieMarkedEvent>().Single();
                Assert.That(marked.RuleId, Is.EqualTo(BindingId));
                Assert.That(1 << marked.Bay, Is.EqualTo(snapshot.Binding().MarkedMask));
                Assert.That(events.IndexOf(marked), Is.GreaterThan(events.IndexOf(events.OfType<DiceRolledEvent>().Single())));
                Assert.That(session.CaptureSave().DiceRandomState, Is.EqualTo(plain.CaptureSave().DiceRandomState));
                Assert.That(session.CaptureSave().RuleRandomState, Is.Not.Zero);
                Assert.That(plain.CaptureSave().RuleRandomState, Is.Zero);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TheMarkedDieBindsAtItsFaceAfterTheNextRerollWhetherOrNotItWasRerolled(bool rerollMarked)
        {
            for (ulong seed = 0; seed < 20; ++seed)
            {
                var catalog = Catalog();
                var session = Session(catalog, seed);
                int marked = session.Snapshot.Binding().MarkedMask;
                int selected = rerollMarked ? marked : Lowest(DiceValues.AllMask & ~marked);
                var before = session.Snapshot.CurrentDice;
                var result = Execute(session, new RerollDiceAction(selected));
                var events = result.Events;
                var bound = events.OfType<DieBoundEvent>().Single();
                var newMark = events.OfType<DieMarkedEvent>().Single();
                Assert.That(1 << bound.Bay, Is.EqualTo(marked));
                Assert.That(bound.RuleId, Is.EqualTo(BindingId));
                Assert.That(bound.Face, Is.EqualTo(result.Snapshot.CurrentDice[bound.Bay]));
                if (!rerollMarked) Assert.That(bound.Face, Is.EqualTo(before[bound.Bay]), "A kept die binds at its face.");
                Assert.That(events.IndexOf(bound), Is.GreaterThan(events.IndexOf(events.OfType<DiceRolledEvent>().Single())));
                Assert.That(events.IndexOf(newMark), Is.GreaterThan(events.IndexOf(bound)));
                Assert.That(result.Snapshot.Binding().BoundMask, Is.EqualTo(marked));
                Assert.That(result.Snapshot.LockedMask, Is.EqualTo(marked));
                Assert.That(DiceValues.CountBays(result.Snapshot.Binding().MarkedMask), Is.EqualTo(1));
                Assert.That(result.Snapshot.Binding().MarkedMask & marked, Is.Zero, "Only unbound dice are marked.");
                Assert.That(result.Snapshot.CanReroll, Is.True);
                Restore(catalog, session.CaptureSave());
            }
        }

        [Test]
        public void EachRerollBindsAnotherDieUntilAllAreBoundAndRerollsOfBoundDiceAreRejected()
        {
            var catalog = Catalog();
            var session = Session(catalog, 5);
            for (int reroll = 1; reroll <= DiceValues.Count; ++reroll)
            {
                int unbound = DiceValues.AllMask & ~session.Snapshot.Binding().BoundMask;
                var save = session.CaptureSave();
                long revision = session.Snapshot.Revision;
                if (session.Snapshot.Binding().BoundMask != 0)
                {
                    var rejected = session.Execute(new RerollDiceAction(session.Snapshot.Binding().BoundMask | unbound), revision);
                    Assert.That(rejected.Rejection, Is.EqualTo(CommandRejection.InvalidDiceSelection));
                    EncounterPersistenceTests.AssertGraphEqual(save, session.CaptureSave());
                }
                Execute(session, new RerollDiceAction(Lowest(unbound)));
                Assert.That(DiceValues.CountBays(session.Snapshot.Binding().BoundMask), Is.EqualTo(reroll));
                Assert.That(DiceValues.CountBays(session.Snapshot.Binding().MarkedMask), Is.EqualTo(reroll < DiceValues.Count ? 1 : 0));
                Restore(catalog, session.CaptureSave());
            }
            var snapshot = session.Snapshot;
            Assert.That(snapshot.Binding().BoundMask, Is.EqualTo(DiceValues.AllMask));
            Assert.That(snapshot.LockedMask, Is.EqualTo(DiceValues.AllMask));
            Assert.That(snapshot.Binding().MarkedMask, Is.Zero, "Nothing is marked once every die is bound.");
            Assert.That(snapshot.RerollsRemaining, Is.GreaterThan(0));
            Assert.That(snapshot.CanReroll, Is.False);
            for (int mask = 1; mask <= DiceValues.AllMask; ++mask)
                Assert.That(session.Execute(new RerollDiceAction(mask), snapshot.Revision).Rejection,
                    Is.EqualTo(CommandRejection.InvalidDiceSelection));
        }

        [Test]
        public void BindingsPersistThroughPlayHandAndClearWhenTheNextHandStarts()
        {
            var bonus = new ModifierDefinition("charm.bound", "family.bound", ModifierPhase.PlayHand,
                new BoundFaceCountAtLeastCondition(1),
                new ModifierEffect[] { new AddBaseScoreEffect(7) });
            var catalog = Catalog(modifiers: new[] { bonus });
            var session = Session(catalog, 8);
            Assert.That(session.InspectModifiersForPlayHand().Single().ConditionMet, Is.False, "No die is bound before a reroll.");
            Execute(session, new RerollDiceAction(session.Snapshot.Binding().MarkedMask));
            Assert.That(DiceValues.CountBays(session.Snapshot.Binding().BoundMask), Is.EqualTo(1));
            Assert.That(session.InspectModifiersForPlayHand().Single().ConditionMet, Is.True);
            Assert.That(session.PreviewPlayHand().ModifierBaseScore, Is.EqualTo(7));
            var played = Execute(session, PlayHandAction.Instance);
            Assert.That(played.Events.OfType<ModifierActivatedEvent>().Single().DefinitionId, Is.EqualTo(bonus.Id));
            int handStarted = played.Events.IndexOf(played.Events.OfType<HandStartedEvent>().Single());
            Assert.That(played.Events.IndexOf(played.Events.OfType<ModifierActivatedEvent>().Single()), Is.LessThan(handStarted));
            Assert.That(played.Events.IndexOf(played.Events.OfType<DieMarkedEvent>().Single()), Is.GreaterThan(handStarted));
            Assert.That(played.Snapshot.Binding().BoundMask, Is.Zero);
            Assert.That(DiceValues.CountBays(played.Snapshot.Binding().MarkedMask), Is.EqualTo(1));
            Assert.That(session.InspectModifiersForPlayHand().Single().ConditionMet, Is.False);
        }

        [Test]
        public void BindingsRemainOnTheScoredHandWhenTheStageClears()
        {
            var catalog = Catalog(stages: new[] { new StageDefinition(1, 3, 3, StageExitBehavior.AwaitRewards),
                new StageDefinition(1000000, 3, 3) });
            var session = Session(catalog, 2);
            Execute(session, new RerollDiceAction(session.Snapshot.Binding().MarkedMask));
            int bound = session.Snapshot.Binding().BoundMask;
            int marked = session.Snapshot.Binding().MarkedMask;
            var cleared = Execute(session, PlayHandAction.Instance);
            Assert.That(cleared.Snapshot.Phase, Is.EqualTo(EncounterPhase.AwaitingRewards));
            Assert.That(cleared.Snapshot.Binding().BoundMask, Is.EqualTo(bound));
            Assert.That(cleared.Snapshot.Binding().MarkedMask, Is.EqualTo(marked));
            Restore(catalog, session.CaptureSave());
        }

        [Test]
        public void AuthoredSchedulesControlWhenDiceAreMarked()
        {
            var openingOnly = Catalog(afterReroll: false);
            var session = Session(openingOnly, 4);
            Assert.That(DiceValues.CountBays(session.Snapshot.Binding().MarkedMask), Is.EqualTo(1));
            Execute(session, new RerollDiceAction(Lowest(DiceValues.AllMask & ~session.Snapshot.Binding().MarkedMask)));
            Assert.That(DiceValues.CountBays(session.Snapshot.Binding().BoundMask), Is.EqualTo(1));
            Assert.That(session.Snapshot.Binding().MarkedMask, Is.Zero);
            var unchanged = Execute(session, new RerollDiceAction(Lowest(DiceValues.AllMask & ~session.Snapshot.Binding().BoundMask)));
            Assert.That(unchanged.Events.OfType<DieBoundEvent>(), Is.Empty);
            Assert.That(unchanged.Events.OfType<DieMarkedEvent>(), Is.Empty);
            Restore(openingOnly, session.CaptureSave());

            var rerollOnly = Catalog(afterOpening: false);
            session = Session(rerollOnly, 4);
            Assert.That(session.Snapshot.Binding().MarkedMask, Is.Zero);
            Assert.That(session.InitialResolution.Events.OfType<DieMarkedEvent>(), Is.Empty);
            Restore(rerollOnly, session.CaptureSave());
            Execute(session, new RerollDiceAction(1));
            Assert.That(session.Snapshot.Binding().BoundMask, Is.Zero);
            Assert.That(DiceValues.CountBays(session.Snapshot.Binding().MarkedMask), Is.EqualTo(1));
            Restore(rerollOnly, session.CaptureSave());
            Execute(session, new RerollDiceAction(Lowest(DiceValues.AllMask & ~session.Snapshot.Binding().BoundMask)));
            Assert.That(DiceValues.CountBays(session.Snapshot.Binding().BoundMask), Is.EqualTo(1));
            Restore(rerollOnly, session.CaptureSave());
        }

        [Test]
        public void MidHandSaveRestoresMasksAndRuleStreamAndContinuesIdentically()
        {
            var catalog = Catalog();
            var session = Session(catalog, 21);
            Execute(session, new RerollDiceAction(session.Snapshot.Binding().MarkedMask));
            Execute(session, new RerollDiceAction(Lowest(DiceValues.AllMask & ~session.Snapshot.Binding().BoundMask)));
            var save = session.CaptureSave();
            var restored = Restore(catalog, Decode(catalog, EncounterSaveCodec.Serialize(save)));
            Assert.That(restored.Snapshot.Binding().BoundMask, Is.EqualTo(session.Snapshot.Binding().BoundMask));
            Assert.That(restored.Snapshot.Binding().MarkedMask, Is.EqualTo(session.Snapshot.Binding().MarkedMask));
            Assert.That(restored.InitialResolution.Events, Is.Empty);
            EncounterPersistenceTests.AssertGraphEqual(save, restored.CaptureSave());
            Assert.That(restored.CaptureSave().RuleRandomState, Is.EqualTo(save.RuleRandomState), "Restore consumes no randomness.");
            for (int i = 0; i < 4; ++i)
            {
                EncounterAction action = i == 2 ? (EncounterAction)PlayHandAction.Instance
                    : new RerollDiceAction(Lowest(DiceValues.AllMask & ~session.Snapshot.Binding().BoundMask));
                var original = Execute(session, action);
                var continued = Execute(restored, action);
                Assert.That(continued.Events.Select(value => value.GetType()), Is.EqualTo(original.Events.Select(value => value.GetType())));
                EncounterPersistenceTests.AssertGraphEqual(session.CaptureSave(), restored.CaptureSave());
            }
        }

        [Test]
        public void RestoreRejectsBindingStatesThatDisagreeWithTheHand()
        {
            var catalog = Catalog();
            var session = Session(catalog, 9);
            Execute(session, new RerollDiceAction(session.Snapshot.Binding().MarkedMask));
            int bound = session.Snapshot.Binding().BoundMask;
            int marked = session.Snapshot.Binding().MarkedMask;
            int spare = Lowest(DiceValues.AllMask & ~bound & ~marked);
            AssertInvalid(catalog, session.CaptureSave(), save => Binding(save).MarkedMask = bound);
            AssertInvalid(catalog, session.CaptureSave(), save => Binding(save).MarkedMask = marked | spare);
            AssertInvalid(catalog, session.CaptureSave(), save => Binding(save).MarkedMask = 0);
            AssertInvalid(catalog, session.CaptureSave(), save => Binding(save).BoundMask = 0);
            AssertInvalid(catalog, session.CaptureSave(), save => Binding(save).BoundMask = bound | spare);
            AssertInvalid(catalog, session.CaptureSave(), save => Binding(save).BoundMask = 32);
            AssertInvalid(catalog, session.CaptureSave(), save => save.RuleStates = Array.Empty<EncounterSaveRuleData>());
            AssertInvalid(catalog, session.CaptureSave(), save => save.RuleStates = null);
            AssertInvalid(catalog, session.CaptureSave(), save => save.RuleStates[0].RuleId = "rule.other");
            AssertInvalid(catalog, session.CaptureSave(), save => save.RuleStates[0].State = null);
            AssertInvalid(catalog, session.CaptureSave(), save => save.RuleStates[0].State = new DieInterferenceSaveData());
            AssertInvalid(catalog, session.CaptureSave(), save => save.RuleStates = new[] { save.RuleStates[0], save.RuleStates[0] });
            AssertInvalid(catalog, Session(catalog, 9).CaptureSave(), save => Binding(save).MarkedMask = 0);
            var plain = Catalog(binding: false);
            AssertInvalid(plain, Session(plain).CaptureSave(), save => save.RuleRandomState = 1);
            AssertInvalid(plain, Session(plain).CaptureSave(), save => save.RuleStates = new[]
                { new EncounterSaveRuleData { RuleId = BindingId, State = new DieBindingSaveData() } });
        }

        [Test]
        public void CodecWritesRuleStatesByTheirRuntimeTypeAndReadsThemThroughTheOwningRule()
        {
            var catalog = Catalog();
            var session = Session(catalog, 6);
            Execute(session, new RerollDiceAction(session.Snapshot.Binding().MarkedMask));
            byte[] bytes = EncounterSaveCodec.Serialize(session.CaptureSave());
            string text = Encoding.UTF8.GetString(bytes);
            Assert.That(text, Does.Not.Contain("$type"));
            var json = JObject.Parse(text);
            var entry = (JObject)((JArray)json[nameof(EncounterSaveData.RuleStates)]).Single();
            Assert.That((string)entry[nameof(EncounterSaveRuleData.RuleId)], Is.EqualTo(BindingId));
            var state = (JObject)entry[nameof(EncounterSaveRuleData.State)];
            Assert.That(state.Properties().Select(value => value.Name),
                Is.EquivalentTo(new[] { nameof(DieBindingSaveData.BoundMask), nameof(DieBindingSaveData.MarkedMask) }));
            Assert.That((int)state[nameof(DieBindingSaveData.BoundMask)], Is.EqualTo(session.Snapshot.Binding().BoundMask));
            Assert.That((int)state[nameof(DieBindingSaveData.MarkedMask)], Is.EqualTo(session.Snapshot.Binding().MarkedMask));
            var decoded = Decode(catalog, bytes);
            var typed = (DieBindingSaveData)decoded.RuleStates.Single().State;
            Assert.That(typed.BoundMask, Is.EqualTo(session.Snapshot.Binding().BoundMask));
            Assert.That(typed.MarkedMask, Is.EqualTo(session.Snapshot.Binding().MarkedMask));
            Assert.That(EncounterSaveCodec.Serialize(decoded), Is.EqualTo(bytes));
            EncounterPersistenceTests.AssertGraphEqual(session.CaptureSave(), decoded);
        }

        [Test]
        public void CodecRejectsUnknownMissingAndAliasedFieldsInsideARuleState()
        {
            var catalog = Catalog();
            var session = Session(catalog, 6);
            JObject State(JObject json) => (JObject)json[nameof(EncounterSaveData.RuleStates)][0][nameof(EncounterSaveRuleData.State)];

            var unknown = Json(session);
            State(unknown).Add("Extra", 1);
            Assert.Throws<FormatException>(() => Decode(catalog, unknown));
            var missing = Json(session);
            State(missing).Remove(nameof(DieBindingSaveData.MarkedMask));
            Assert.Throws<FormatException>(() => Decode(catalog, missing));
            var aliased = Json(session);
            State(aliased).Add("boundMask", 0);
            Assert.Throws<FormatException>(() => Decode(catalog, aliased));
            var text = Json(session);
            State(text)[nameof(DieBindingSaveData.BoundMask)] = "0";
            Assert.Throws<FormatException>(() => Decode(catalog, text));
            var entryField = Json(session);
            ((JObject)entryField[nameof(EncounterSaveData.RuleStates)][0]).Remove(nameof(EncounterSaveRuleData.State));
            Assert.Throws<FormatException>(() => Decode(catalog, entryField));

            var foreign = Json(session);
            foreign[nameof(EncounterSaveData.RuleStates)][0][nameof(EncounterSaveRuleData.RuleId)] = "rule.other";
            State(foreign).Add("Extra", 1);
            var result = EncounterSession.RestoreEncounter(catalog, Decode(catalog, foreign));
            Assert.That(result.Error, Is.EqualTo(EncounterRestoreError.InvalidState), result.Detail);
        }

        [Test]
        public void DieBindingSettingsAndRuleIdsAreAuthoredOnTheDefinition()
        {
            var rule = new DieBindingRuleDefinition(BindingId, true, false);
            Assert.That(rule.Id, Is.EqualTo(BindingId));
            Assert.That(rule.MarksAfterOpeningRoll, Is.True);
            Assert.That(rule.MarksAfterPlayerReroll, Is.False);
            Assert.That(rule.SaveDataType, Is.EqualTo(typeof(DieBindingSaveData)));
            Assert.That(new DieInterferenceRuleDefinition(HauntingId, 20, 1, 1, true, true).SaveDataType,
                Is.EqualTo(typeof(DieInterferenceSaveData)));
            Assert.That(new FirstRerollFreeRuleDefinition().SaveDataType, Is.EqualTo(typeof(FirstRerollFreeSaveData)));
            Assert.Throws<ArgumentException>(() => new DieBindingRuleDefinition(" ", true, true));
        }

        private static MechanicalCatalog Catalog(bool binding = true, bool afterOpening = true, bool afterReroll = true,
            StageDefinition[] stages = null, ModifierDefinition[] modifiers = null)
        {
            var rules = binding
                ? new EncounterRuleDefinition[] { new DieBindingRuleDefinition(BindingId, afterOpening, afterReroll) }
                : Array.Empty<EncounterRuleDefinition>();
            var encounter = new EncounterDefinition(TestContent.EncounterId, stages ?? new[] { new StageDefinition(1000000, 20, 20) },
                startingModifiers: modifiers?.Select(value => value.Id), rules: rules);
            return new MechanicalCatalog("test.binding", TestContent.Hands(), new[] { encounter }, modifiers);
        }

        private static DieBindingSaveData Binding(EncounterSaveData save)
            => (DieBindingSaveData)save.RuleStates.Single(value => value.RuleId == BindingId).State;

        private static int Lowest(int mask) => mask & -mask;

        private static EncounterSession Session(MechanicalCatalog catalog, ulong seed = 14)
            => EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, seed);

        private static Resolution Execute(EncounterSession session, EncounterAction action)
        {
            var result = session.Execute(action, session.Snapshot.Revision);
            Assert.That(result.Accepted, Is.True, result.Rejection.ToString());
            return result;
        }

        private static EncounterSession Restore(MechanicalCatalog catalog, EncounterSaveData save)
        {
            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.True, result.Error + ": " + result.Detail);
            return result.Session;
        }

        private static void AssertInvalid(MechanicalCatalog catalog, EncounterSaveData save, Action<EncounterSaveData> mutate)
        {
            mutate(save);
            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(EncounterRestoreError.InvalidState), result.Detail);
        }

        private static JObject Json(EncounterSession session)
            => JObject.Parse(Encoding.UTF8.GetString(EncounterSaveCodec.Serialize(session.CaptureSave())));

        private static EncounterSaveData Decode(MechanicalCatalog catalog, byte[] bytes) => EncounterSaveCodec.Deserialize(bytes, catalog);
        private static EncounterSaveData Decode(MechanicalCatalog catalog, JObject json)
            => EncounterSaveCodec.Deserialize(Encoding.UTF8.GetBytes(json.ToString()), catalog);
    }
}
