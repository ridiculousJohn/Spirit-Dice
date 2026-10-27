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
        private const string WardId = "rule.test.ward";
        private const string PieceId = "piece.test";
        private const string InteractionId = "position";

        [Test]
        public void TheOpeningRollIsMarkedWithoutChangingItsFacesOrTheDiceStream()
        {
            for (ulong seed = 0; seed < 20; ++seed)
            {
                var session = Session(Catalog(), seed);
                var plain = Session(Catalog(binding: false), seed);
                var snapshot = session.Snapshot;
                Assert.That(Bits(snapshot.MarkedMask), Is.EqualTo(1));
                Assert.That(snapshot.BoundMask, Is.Zero);
                Assert.That(snapshot.LockedMask, Is.Zero);
                Assert.That(snapshot.CurrentDice, Is.EqualTo(plain.Snapshot.CurrentDice), "Marking never changes a face.");
                Assert.That(snapshot.InitialDice, Is.EqualTo(snapshot.CurrentDice));
                var events = session.InitialResolution.Events;
                var marked = events.OfType<DieMarkedEvent>().Single();
                Assert.That(marked.RuleId, Is.EqualTo(BindingId));
                Assert.That(1 << marked.Bay, Is.EqualTo(snapshot.MarkedMask));
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
                int marked = session.Snapshot.MarkedMask;
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
                Assert.That(result.Snapshot.BoundMask, Is.EqualTo(marked));
                Assert.That(result.Snapshot.LockedMask, Is.EqualTo(marked));
                Assert.That(Bits(result.Snapshot.MarkedMask), Is.EqualTo(1));
                Assert.That(result.Snapshot.MarkedMask & marked, Is.Zero, "Only unbound dice are marked.");
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
                int unbound = DiceValues.AllMask & ~session.Snapshot.BoundMask;
                var save = session.CaptureSave();
                long revision = session.Snapshot.Revision;
                if (session.Snapshot.BoundMask != 0)
                {
                    var rejected = session.Execute(new RerollDiceAction(session.Snapshot.BoundMask | unbound), revision);
                    Assert.That(rejected.Rejection, Is.EqualTo(CommandRejection.InvalidDiceSelection));
                    EncounterPersistenceTests.AssertGraphEqual(save, session.CaptureSave());
                }
                Execute(session, new RerollDiceAction(Lowest(unbound)));
                Assert.That(Bits(session.Snapshot.BoundMask), Is.EqualTo(reroll));
                Assert.That(Bits(session.Snapshot.MarkedMask), Is.EqualTo(reroll < DiceValues.Count ? 1 : 0));
                Restore(catalog, session.CaptureSave());
            }
            var snapshot = session.Snapshot;
            Assert.That(snapshot.BoundMask, Is.EqualTo(DiceValues.AllMask));
            Assert.That(snapshot.LockedMask, Is.EqualTo(DiceValues.AllMask));
            Assert.That(snapshot.MarkedMask, Is.Zero, "Nothing is marked once every die is bound.");
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
                new ModifierCondition(ModifierConditionKind.BoundFaceCountAtLeast, 1),
                new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 7) });
            var catalog = Catalog(modifiers: new[] { bonus });
            var session = Session(catalog, 8);
            Assert.That(session.InspectModifiersForPlayHand().Single().ConditionMet, Is.False, "No die is bound before a reroll.");
            Execute(session, new RerollDiceAction(session.Snapshot.MarkedMask));
            Assert.That(Bits(session.Snapshot.BoundMask), Is.EqualTo(1));
            Assert.That(session.InspectModifiersForPlayHand().Single().ConditionMet, Is.True);
            Assert.That(session.PreviewPlayHand().ModifierBaseScore, Is.EqualTo(7));
            var played = Execute(session, PlayHandAction.Instance);
            Assert.That(played.Events.OfType<ModifierActivatedEvent>().Single().DefinitionId, Is.EqualTo(bonus.Id));
            int handStarted = played.Events.IndexOf(played.Events.OfType<HandStartedEvent>().Single());
            Assert.That(played.Events.IndexOf(played.Events.OfType<ModifierActivatedEvent>().Single()), Is.LessThan(handStarted));
            Assert.That(played.Events.IndexOf(played.Events.OfType<DieMarkedEvent>().Single()), Is.GreaterThan(handStarted));
            Assert.That(played.Snapshot.BoundMask, Is.Zero);
            Assert.That(Bits(played.Snapshot.MarkedMask), Is.EqualTo(1));
            Assert.That(session.InspectModifiersForPlayHand().Single().ConditionMet, Is.False);
        }

        [Test]
        public void BindingsRemainOnTheScoredHandWhenTheStageClears()
        {
            var catalog = Catalog(stages: new[] { new StageDefinition(1, 3, 3, StageExitBehavior.AwaitRewards),
                new StageDefinition(1000000, 3, 3) });
            var session = Session(catalog, 2);
            Execute(session, new RerollDiceAction(session.Snapshot.MarkedMask));
            int bound = session.Snapshot.BoundMask;
            int marked = session.Snapshot.MarkedMask;
            var cleared = Execute(session, PlayHandAction.Instance);
            Assert.That(cleared.Snapshot.Phase, Is.EqualTo(EncounterPhase.AwaitingRewards));
            Assert.That(cleared.Snapshot.BoundMask, Is.EqualTo(bound));
            Assert.That(cleared.Snapshot.MarkedMask, Is.EqualTo(marked));
            Restore(catalog, session.CaptureSave());
        }

        [Test]
        public void AuthoredSchedulesControlWhenDiceAreMarked()
        {
            var openingOnly = Catalog(afterReroll: false);
            var session = Session(openingOnly, 4);
            Assert.That(Bits(session.Snapshot.MarkedMask), Is.EqualTo(1));
            Execute(session, new RerollDiceAction(Lowest(DiceValues.AllMask & ~session.Snapshot.MarkedMask)));
            Assert.That(Bits(session.Snapshot.BoundMask), Is.EqualTo(1));
            Assert.That(session.Snapshot.MarkedMask, Is.Zero);
            var unchanged = Execute(session, new RerollDiceAction(Lowest(DiceValues.AllMask & ~session.Snapshot.BoundMask)));
            Assert.That(unchanged.Events.OfType<DieBoundEvent>(), Is.Empty);
            Assert.That(unchanged.Events.OfType<DieMarkedEvent>(), Is.Empty);
            Restore(openingOnly, session.CaptureSave());

            var rerollOnly = Catalog(afterOpening: false);
            session = Session(rerollOnly, 4);
            Assert.That(session.Snapshot.MarkedMask, Is.Zero);
            Assert.That(session.InitialResolution.Events.OfType<DieMarkedEvent>(), Is.Empty);
            Restore(rerollOnly, session.CaptureSave());
            Execute(session, new RerollDiceAction(1));
            Assert.That(session.Snapshot.BoundMask, Is.Zero);
            Assert.That(Bits(session.Snapshot.MarkedMask), Is.EqualTo(1));
            Restore(rerollOnly, session.CaptureSave());
            Execute(session, new RerollDiceAction(Lowest(DiceValues.AllMask & ~session.Snapshot.BoundMask)));
            Assert.That(Bits(session.Snapshot.BoundMask), Is.EqualTo(1));
            Restore(rerollOnly, session.CaptureSave());
        }

        [Test]
        public void MidHandSaveRestoresMasksAndRuleStreamAndContinuesIdentically()
        {
            var catalog = Catalog();
            var session = Session(catalog, 21);
            Execute(session, new RerollDiceAction(session.Snapshot.MarkedMask));
            Execute(session, new RerollDiceAction(Lowest(DiceValues.AllMask & ~session.Snapshot.BoundMask)));
            var save = session.CaptureSave();
            var restored = Restore(catalog, Decode(catalog, EncounterSaveCodec.Serialize(save)));
            Assert.That(restored.Snapshot.BoundMask, Is.EqualTo(session.Snapshot.BoundMask));
            Assert.That(restored.Snapshot.MarkedMask, Is.EqualTo(session.Snapshot.MarkedMask));
            Assert.That(restored.InitialResolution.Events, Is.Empty);
            EncounterPersistenceTests.AssertGraphEqual(save, restored.CaptureSave());
            Assert.That(restored.CaptureSave().RuleRandomState, Is.EqualTo(save.RuleRandomState), "Restore consumes no randomness.");
            for (int i = 0; i < 4; ++i)
            {
                EncounterAction action = i == 2 ? (EncounterAction)PlayHandAction.Instance
                    : new RerollDiceAction(Lowest(DiceValues.AllMask & ~session.Snapshot.BoundMask));
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
            Execute(session, new RerollDiceAction(session.Snapshot.MarkedMask));
            int bound = session.Snapshot.BoundMask;
            int marked = session.Snapshot.MarkedMask;
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
            Execute(session, new RerollDiceAction(session.Snapshot.MarkedMask));
            byte[] bytes = EncounterSaveCodec.Serialize(session.CaptureSave());
            string text = Encoding.UTF8.GetString(bytes);
            Assert.That(text, Does.Not.Contain("$type"));
            var json = JObject.Parse(text);
            var entry = (JObject)((JArray)json[nameof(EncounterSaveData.RuleStates)]).Single();
            Assert.That((string)entry[nameof(EncounterSaveRuleData.RuleId)], Is.EqualTo(BindingId));
            var state = (JObject)entry[nameof(EncounterSaveRuleData.State)];
            Assert.That(state.Properties().Select(value => value.Name),
                Is.EquivalentTo(new[] { nameof(DieBindingSaveData.BoundMask), nameof(DieBindingSaveData.MarkedMask) }));
            Assert.That((int)state[nameof(DieBindingSaveData.BoundMask)], Is.EqualTo(session.Snapshot.BoundMask));
            Assert.That((int)state[nameof(DieBindingSaveData.MarkedMask)], Is.EqualTo(session.Snapshot.MarkedMask));
            var decoded = Decode(catalog, bytes);
            var typed = (DieBindingSaveData)decoded.RuleStates.Single().State;
            Assert.That(typed.BoundMask, Is.EqualTo(session.Snapshot.BoundMask));
            Assert.That(typed.MarkedMask, Is.EqualTo(session.Snapshot.MarkedMask));
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
        public void FormatSixDisturbancesMigrateIntoTheHauntingRuleState()
        {
            var catalog = WardedHauntingCatalog();
            var session = Session(catalog, 5);
            Execute(session, new RerollDiceAction(1));
            Assert.That(session.Snapshot.DisturbedMask, Is.Not.Zero);
            var current = session.CaptureSave();
            var haunting = (DieInterferenceSaveData)current.RuleStates.Single().State;
            Assert.That(current.RuleStates.Single().RuleId, Is.EqualTo(HauntingId), "Stateless rules save no entry.");
            var json = Downgrade(session, haunting.DisturbedMask, haunting.LatestDisturbedMask);
            var decoded = Decode(catalog, json);
            Assert.That(decoded.SaveFormatVersion, Is.EqualTo(6));
            var migrated = decoded.RuleStates.Single();
            Assert.That(migrated.RuleId, Is.EqualTo(HauntingId));
            Assert.That(((DieInterferenceSaveData)migrated.State).DisturbedMask, Is.EqualTo(haunting.DisturbedMask));
            Assert.That(((DieInterferenceSaveData)migrated.State).LatestDisturbedMask, Is.EqualTo(haunting.LatestDisturbedMask));
            var restored = Restore(catalog, decoded);
            Assert.That(restored.Snapshot.DisturbedMask, Is.EqualTo(session.Snapshot.DisturbedMask));
            var resaved = restored.CaptureSave();
            Assert.That(resaved.SaveFormatVersion, Is.EqualTo(EncounterSaveData.CurrentFormatVersion));
            Assert.That(resaved.RulesVersion, Is.EqualTo(EncounterSession.RulesVersion));
            EncounterPersistenceTests.AssertGraphEqual(current.RuleStates, resaved.RuleStates);
            Execute(session, new RerollDiceAction(2));
            Execute(restored, new RerollDiceAction(2));
            Assert.That(restored.Snapshot.CurrentDice, Is.EqualTo(session.Snapshot.CurrentDice));
            Assert.That(restored.Snapshot.DisturbedMask, Is.EqualTo(session.Snapshot.DisturbedMask));

            var undisturbed = Session(catalog, 5);
            var quiet = Decode(catalog, Downgrade(undisturbed, 0, 0));
            Assert.That(quiet.RuleStates, Is.Empty, "Zero masks are dropped.");
            var quietRestored = Restore(catalog, quiet);
            Assert.That(quietRestored.Snapshot.DisturbedMask, Is.Zero);
            EncounterPersistenceTests.AssertGraphEqual(undisturbed.CaptureSave().RuleStates, quietRestored.CaptureSave().RuleStates);
        }

        [Test]
        public void FormatSixDocumentsWithoutInterferenceLoadWithoutRuleStates()
        {
            var catalog = new MechanicalCatalog("test.plain.rules", TestContent.Hands(), new[]
            {
                new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1000000, 20, 20) },
                    rules: new[] { new FirstRerollFreeRuleDefinition() })
            });
            var session = Session(catalog, 12);
            Execute(session, new RerollDiceAction(3));
            var decoded = Decode(catalog, Downgrade(session, 0, 0));
            Assert.That(decoded.RuleStates, Is.Empty);
            var restored = Restore(catalog, decoded);
            Execute(session, new RerollDiceAction(5));
            Execute(restored, new RerollDiceAction(5));
            EncounterPersistenceTests.AssertGraphEqual(session.CaptureSave(), restored.CaptureSave());
            Assert.Throws<FormatException>(() => Decode(catalog, Downgrade(session, 1, 1)),
                "Disturbed dice require a die-interference rule.");
            var missing = Downgrade(session, 0, 0);
            missing.Remove("LatestDisturbedMask");
            Assert.Throws<FormatException>(() => Decode(catalog, missing));
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
            Assert.That(new FirstRerollFreeRuleDefinition().SaveDataType, Is.Null);
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

        private static MechanicalCatalog WardedHauntingCatalog()
        {
            var ward = new TablePieceDefinition(PieceId, new[]
                { new PositionInteractionDefinition(InteractionId, PiecePosition.Neutral, PieceMovementTiming.DuringHand) });
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1000000, 20, 20) },
                rules: new EncounterRuleDefinition[]
                {
                    new DieInterferenceRuleDefinition(HauntingId, 100, 1, 1, true, true),
                    new BayWardRuleDefinition(WardId, PieceId, InteractionId, new[] { DieInterferenceKind.ForcedReroll })
                }, pieces: new[] { ward });
            return new MechanicalCatalog("test.haunting", TestContent.Hands(), new[] { encounter });
        }

        /** Rewrites a current save as a format 6 document, which kept disturbed dice on the shared block. */
        private static JObject Downgrade(EncounterSession session, int disturbed, int latest)
        {
            var json = Json(session);
            json[nameof(EncounterSaveData.SaveFormatVersion)] = 6;
            json[nameof(EncounterSaveData.RulesVersion)] = 8;
            json.Remove(nameof(EncounterSaveData.RuleStates));
            json.Add("DisturbedMask", disturbed);
            json.Add("LatestDisturbedMask", latest);
            return json;
        }

        private static DieBindingSaveData Binding(EncounterSaveData save)
            => (DieBindingSaveData)save.RuleStates.Single(value => value.RuleId == BindingId).State;

        private static int Bits(int mask)
        {
            int count = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if ((mask & (1 << i)) != 0) ++count;
            return count;
        }

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
