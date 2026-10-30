using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class ModifierInspectionTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void InspectionMatchesPreviewAndCommittedPlay(bool holds)
        {
            var definition = Modifier("inspected", ModifierPhase.PlayHand, new RecordingCondition(context => holds));
            var session = Session(Catalog(new[] { definition }));
            AssertInspectionMatchesPlay(session);
            session.Execute(new RerollDiceAction(5), session.Snapshot.Revision);
            AssertInspectionMatchesPlay(session);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CustomConditionsSeeTheHandBeingPlayedAndResourcesBeforeAllEffects(bool reverse)
        {
            var grantCondition = new RecordingCondition(context => true);
            var resourceCondition = new RecordingCondition(context =>
                context.HandsRemaining == 4 && context.RerollsRemaining == 2 &&
                context.SelectedMask == 5 && context.EverRerolledMask == 5 && context.RerollsSpentOnHand == 1);
            var grant = Modifier("grant", ModifierPhase.PlayHand, grantCondition,
                effects: new ModifierEffect[] { new AddRerollsEffect(2) });
            var resource = Modifier("resource", ModifierPhase.PlayHand, resourceCondition);
            var definitions = reverse ? new[] { resource, grant } : new[] { grant, resource };
            var session = Session(Catalog(definitions, new[] { new StageDefinition(1000000, 4, 3) }));
            session.Execute(new RerollDiceAction(5), 0);
            var snapshot = session.Snapshot;

            var inspection = session.InspectModifiersForPlayHand();
            Assert.That(inspection.Select(value => value.Status),
                Is.All.EqualTo(ModifierInspectionStatus.WouldActivate));
            foreach (var condition in new[] { grantCondition, resourceCondition })
            {
                Assert.That(condition.Contexts.Count, Is.EqualTo(1));
                var context = condition.Contexts[0];
                Assert.That(context.HandsRemaining, Is.EqualTo(snapshot.HandsRemaining));
                Assert.That(context.RerollsRemaining, Is.EqualTo(snapshot.RerollsRemaining));
                Assert.That(context.HandsPlayedInStage, Is.Zero);
                Assert.That(context.StartingHands, Is.EqualTo(4));
                Assert.That(context.CurrentDice, Is.EqualTo(snapshot.CurrentDice));
                Assert.That(context.InitialDice, Is.EqualTo(snapshot.InitialDice));
                Assert.That(context.BeforeRerollDice, Is.EqualTo(snapshot.DiceBeforeLastReroll));
                Assert.That(context.PreviousPlayedDice, Is.Null);
            }
            var preview = session.PreviewHandResolution();
            Assert.That(preview.RerollsRemaining, Is.EqualTo(4));
            Assert.That(preview.Events.OfType<ModifierActivatedEvent>().Select(value => value.InstanceId),
                Is.EqualTo(inspection.Select(value => value.InstanceId)));
            foreach (var condition in new[] { grantCondition, resourceCondition })
                Assert.That(condition.Contexts[1], Is.EqualTo(condition.Contexts[0]));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PlayHandLimitAndConditionAreReportedIndependently(bool conditionStillMet)
        {
            var condition = new RecordingCondition(context => conditionStillMet || context.HandsPlayedInStage == 0);
            var definition = Modifier("limited", ModifierPhase.PlayHand, condition, ModifierActivationLimit.OncePerStage);
            var session = Session(Catalog(new[] { definition }));
            Assert.That(session.InspectModifiersForPlayHand()[0].Status, Is.EqualTo(ModifierInspectionStatus.WouldActivate));
            session.Execute(PlayHandAction.Instance, 0);
            Assert.That(condition.Contexts.Count, Is.EqualTo(2));

            var inspection = session.InspectModifiersForPlayHand().Single();
            Assert.That(condition.Contexts.Count, Is.EqualTo(3));
            Assert.That(inspection.ActivationLimitReached, Is.True);
            Assert.That(inspection.ConditionMet, Is.EqualTo(conditionStillMet));
            Assert.That(inspection.Status, Is.EqualTo(ModifierInspectionStatus.ActivationLimitReached));
            Assert.That(session.PreviewHandResolution().Events.OfType<ModifierActivatedEvent>(), Is.Empty);
            Assert.That(condition.Contexts.Count, Is.EqualTo(3));
        }

        [Test]
        public void OtherPhasesAreNotEvaluatedAndShowActualActivationLimits()
        {
            var initial = new RecordingCondition(context => true);
            var reroll = new RecordingCondition(context => true);
            var definitions = new[]
            {
                Modifier("initial", ModifierPhase.InitialRoll, initial),
                Modifier("reroll", ModifierPhase.PlayerReroll, reroll),
                Modifier("never", ModifierPhase.PlayHand, new NeverCondition())
            };
            var session = Session(Catalog(definitions));
            var inspection = session.InspectModifiersForPlayHand();
            Assert.That(inspection.Select(value => value.Phase), Is.EqualTo(definitions.Select(value => value.Phase)));
            Assert.That(inspection.Select(value => value.Status), Is.EqualTo(new[]
            {
                ModifierInspectionStatus.ActivationLimitReached, ModifierInspectionStatus.DifferentPhase,
                ModifierInspectionStatus.ConditionNotMet
            }));
            Assert.That(inspection.Select(value => value.ConditionMet), Is.EqualTo(new bool?[] { null, null, false }));
            Assert.That(inspection.Select(value => value.ActivationLimitReached), Is.EqualTo(new[] { true, false, false }));
            Assert.That(initial.Contexts.Count, Is.EqualTo(1));
            Assert.That(reroll.Contexts, Is.Empty);

            session.Execute(new RerollDiceAction(1), 0);
            inspection = session.InspectModifiersForPlayHand();
            Assert.That(inspection[1].Status, Is.EqualTo(ModifierInspectionStatus.ActivationLimitReached));
            Assert.That(inspection[1].ConditionMet, Is.Null);
            Assert.That(inspection[1].ActivationLimitReached, Is.True);
            Assert.That(initial.Contexts.Count, Is.EqualTo(1));
            Assert.That(reroll.Contexts.Count, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NonPlayingEncountersReturnUnknownConditionsWithoutEvaluation(bool completed)
        {
            var initial = new RecordingCondition(context => true);
            var play = new RecordingCondition(context => true);
            var definitions = new[]
            {
                Modifier("initial", ModifierPhase.InitialRoll, initial),
                Modifier("play", ModifierPhase.PlayHand, play)
            };
            var session = Session(Catalog(definitions, new[]
            {
                new StageDefinition(1, 3, 0, StageExitBehavior.AwaitRewards), new StageDefinition(1000000, 3, 0)
            }));
            session.Execute(completed ? (EncounterAction)AbandonEncounterAction.Instance : PlayHandAction.Instance, 0);
            Assert.That(session.Snapshot.Phase,
                Is.EqualTo(completed ? EncounterPhase.Completed : EncounterPhase.AwaitingRewards));
            int playEvaluations = play.Contexts.Count;
            var save = session.CaptureSave();

            var inspection = session.InspectModifiersForPlayHand();
            Assert.That(inspection.Select(value => value.Status),
                Is.All.EqualTo(ModifierInspectionStatus.EncounterNotPlaying));
            Assert.That(inspection.Select(value => value.ConditionMet), Is.All.Null);
            Assert.That(inspection[0].ActivationLimitReached, Is.True);
            Assert.That(inspection[1].ActivationLimitReached, Is.EqualTo(!completed));
            Assert.That(initial.Contexts.Count, Is.EqualTo(1));
            Assert.That(play.Contexts.Count, Is.EqualTo(playEvaluations));
            EncounterPersistenceTests.AssertGraphEqual(save, session.CaptureSave());
        }

        [Test]
        public void DuplicateDefinitionsKeepTheirInstanceIdentityAndInspectionIsImmutable()
        {
            var definition = Modifier("duplicate", ModifierPhase.PlayHand, new AlwaysCondition());
            var session = Session(Catalog(new[] { definition }, inventory: TestModifiers.IdsOf("duplicate", "duplicate")));
            var inspection = session.InspectModifiersForPlayHand();
            Assert.That(((ICollection<ModifierInspection>)inspection).IsReadOnly, Is.True);
            Assert.That(inspection.Select(value => value.InstanceId), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(inspection.Select(value => value.DefinitionId), Is.EqualTo(TestModifiers.IdsOf("duplicate", "duplicate")));
            var preview = session.PreviewHandResolution();
            Assert.That(preview.Events.OfType<ModifierActivatedEvent>().Select(value => value.InstanceId),
                Is.EqualTo(inspection.Select(value => value.InstanceId)));
            session.Execute(PlayHandAction.Instance, 0);
            Assert.That(inspection.Select(value => value.ConditionMet), Is.All.True);
            Assert.That(inspection.Select(value => value.ActivationLimitReached), Is.All.False);
            Assert.That(inspection.Select(value => value.Status), Is.All.EqualTo(ModifierInspectionStatus.WouldActivate));
        }

        [Test]
        public void RepeatedInspectionPreservesSaveCountersAndFutureRandomStreams()
        {
            var definition = Modifier("resources", ModifierPhase.PlayHand, new AlwaysCondition(),
                effects: new ModifierEffect[] { new AddRerollsEffect(3) });
            var catalog = Catalog(new[] { definition });
            var session = Session(catalog);
            var control = Session(catalog);
            session.Execute(new RerollDiceAction(3), 0);
            control.Execute(new RerollDiceAction(3), 0);
            var snapshot = session.Snapshot;
            var save = session.CaptureSave();
            for (int i = 0; i < 5; ++i)
            {
                session.InspectModifiersForPlayHand();
                EncounterSessionTests.AssertSnapshotsEqual(snapshot, session.Snapshot);
                EncounterPersistenceTests.AssertGraphEqual(save, session.CaptureSave());
            }
            EncounterSessionTests.AssertSnapshotsEqual(control.Execute(PlayHandAction.Instance, 1).Snapshot,
                session.Execute(PlayHandAction.Instance, 1).Snapshot);
            EncounterSessionTests.AssertSnapshotsEqual(control.Execute(new RerollDiceAction(31), 2).Snapshot,
                session.Execute(new RerollDiceAction(31), 2).Snapshot);
            EncounterPersistenceTests.AssertGraphEqual(control.CaptureSave(), session.CaptureSave());
        }

        [Test]
        public void InspectionDoesNotApplyEffectsThatWouldOverflowResolution()
        {
            var definition = Modifier("overflow", ModifierPhase.PlayHand, new AlwaysCondition(),
                effects: new ModifierEffect[] { new AddBaseScoreEffect(long.MaxValue) });
            var session = Session(Catalog(new[] { definition }));
            var save = session.CaptureSave();
            Assert.That(session.InspectModifiersForPlayHand().Single().Status,
                Is.EqualTo(ModifierInspectionStatus.WouldActivate));
            EncounterPersistenceTests.AssertGraphEqual(save, session.CaptureSave());
            Assert.Throws<OverflowException>(() => session.PreviewHandResolution());
        }


        private static void AssertInspectionMatchesPlay(EncounterSession session)
        {
            var inspection = session.InspectModifiersForPlayHand().Single();
            Assert.That(inspection.ConditionMet, Is.Not.Null);
            Assert.That(inspection.ActivationLimitReached, Is.False);
            Assert.That(inspection.Status, Is.EqualTo(inspection.ConditionMet.Value
                ? ModifierInspectionStatus.WouldActivate : ModifierInspectionStatus.ConditionNotMet));
            var expected = inspection.Status == ModifierInspectionStatus.WouldActivate
                ? new[] { inspection.InstanceId } : Array.Empty<long>();
            Assert.That(session.PreviewHandResolution().Events.OfType<ModifierActivatedEvent>().Select(value => value.InstanceId),
                Is.EqualTo(expected));
            var committed = session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
            Assert.That(committed.Events.OfType<ModifierActivatedEvent>().Where(value => value.Phase == ModifierPhase.PlayHand)
                .Select(value => value.InstanceId), Is.EqualTo(expected));
        }

        private static ModifierDefinition Modifier(string id, ModifierPhase phase, IModifierCondition condition,
            ModifierActivationLimit limit = ModifierActivationLimit.OncePerHand, ModifierEffect[] effects = null)
            => new ModifierDefinition(TestModifiers.IdOf(id), TestFamilies.Of("test." + id), phase, condition,
                effects ?? new ModifierEffect[] { new AddBaseScoreEffect(7) }, limit);

        private static MechanicalCatalog Catalog(ModifierDefinition[] definitions, IEnumerable<StageDefinition> stages = null,
            IEnumerable<ModifierDefinition.Id> inventory = null)
        {
            var encounter = new EncounterDefinition(TestContent.EncounterId,
                stages ?? new[] { new StageDefinition(1000000, 20, 20) },
                startingModifiers: inventory ?? definitions.Select(value => value.Identity));
            return new MechanicalCatalog("test.inspection", TestContent.Hands(), new[] { encounter }, definitions);
        }

        private static EncounterSession Session(MechanicalCatalog catalog)
            => EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 14);
    }
}
