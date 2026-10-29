using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class ModifierPolymorphismTests
    {
        [Test]
        public void DefinitionRejectsNullInterfaceConditionAndUncastNullSelectsThatOverload()
        {
            IModifierCondition condition = null;
            var typedException = Assert.Throws<ArgumentNullException>(() =>
                new ModifierDefinition(TestModifiers.IdOf("custom.null"), TestFamilies.Of("test.null"), ModifierPhase.PlayHand,
                    condition, Array.Empty<ModifierEffect>()));
            Assert.That(typedException.ParamName, Is.EqualTo("condition"));

            var literalException = Assert.Throws<ArgumentNullException>(() =>
                new ModifierDefinition(TestModifiers.IdOf("custom.null"), TestFamilies.Of("test.null"), ModifierPhase.PlayHand,
                    null, Array.Empty<ModifierEffect>()));
            Assert.That(literalException.ParamName, Is.EqualTo("condition"));
        }

        [Test]
        public void UnregisteredConditionsResolveThroughThePublicInterface()
        {
            var matching = new RecordingCondition(context => context.SelectedMask == 5 &&
                context.EverRerolledMask == 5 && context.RerollsSpentOnHand == 1);
            var failing = new RecordingCondition(context => context.SelectedMask == 2);
            var definitions = new[]
            {
                Modifier("custom.match", ModifierPhase.PlayerReroll, matching,
                    ModifierActivationLimit.Unlimited, new AddBaseScoreEffect(17)),
                Modifier("custom.fail", ModifierPhase.PlayerReroll, failing,
                    ModifierActivationLimit.Unlimited, new AddMultiplierEffect(9))
            };
            var session = Session(Catalog(definitions));
            Assert.That(definitions[0].Condition, Is.SameAs(matching));
            Assert.That(matching.Contexts, Is.Empty);
            Assert.That(failing.Contexts, Is.Empty);

            var result = session.Execute(new RerollDiceAction(5), 0);
            Assert.That(result.Accepted, Is.True);
            Assert.That(matching.Contexts.Count, Is.EqualTo(1));
            Assert.That(failing.Contexts.Count, Is.EqualTo(1));
            Assert.That(result.Events.OfType<ModifierActivatedEvent>().Select(value => value.DefinitionId),
                Is.EqualTo(TestModifiers.IdsOf("custom.match")));
            Assert.That(result.Snapshot.ModifierBaseScore, Is.EqualTo(17));
            Assert.That(result.Snapshot.ModifierMultiplier, Is.Zero);
            Assert.That(result.Snapshot.Modifiers.Select(value => value.TotalActivations),
                Is.EqualTo(new long[] { 1, 0 }));
        }

        [TestCase(ModifierActivationLimit.OncePerHand, 2)]
        [TestCase(ModifierActivationLimit.OncePerStage, 1)]
        public void PhaseAndActivationLimitChecksSkipCustomConditionEvaluation(
            ModifierActivationLimit limit, int evaluationsAfterNextHand)
        {
            var condition = new RecordingCondition(context => true);
            var definition = Modifier("custom.limited", ModifierPhase.PlayerReroll, condition, limit,
                new AddBaseScoreEffect(7));
            var session = Session(Catalog(new[] { definition }));
            Assert.That(condition.Contexts, Is.Empty);
            session.PreviewHandResolution();
            Assert.That(condition.Contexts, Is.Empty);

            var first = session.Execute(new RerollDiceAction(1), 0);
            Assert.That(first.Events.OfType<ModifierActivatedEvent>().Count(), Is.EqualTo(1));
            Assert.That(condition.Contexts.Count, Is.EqualTo(1));
            var second = session.Execute(new RerollDiceAction(1), 1);
            Assert.That(second.Events.OfType<ModifierActivatedEvent>(), Is.Empty);
            Assert.That(condition.Contexts.Count, Is.EqualTo(1));

            session.Execute(PlayHandAction.Instance, 2);
            Assert.That(condition.Contexts.Count, Is.EqualTo(1));
            var nextHand = session.Execute(new RerollDiceAction(1), 3);
            Assert.That(condition.Contexts.Count, Is.EqualTo(evaluationsAfterNextHand));
            Assert.That(nextHand.Events.OfType<ModifierActivatedEvent>().Count(),
                Is.EqualTo(evaluationsAfterNextHand - 1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CustomConditionsObserveOneResourceSnapshotBeforePhaseEffects(bool reverse)
        {
            var grantCondition = new RecordingCondition(context => true);
            var exhaustedCondition = new RecordingCondition(context =>
                context.HandsRemaining == 2 && context.RerollsRemaining == 0);
            var grant = Modifier("custom.grant", ModifierPhase.PlayHand, grantCondition,
                ModifierActivationLimit.Unlimited, new AddRerollsEffect(2));
            var exhausted = Modifier("custom.exhausted", ModifierPhase.PlayHand, exhaustedCondition,
                ModifierActivationLimit.Unlimited, new AddBaseScoreEffect(17));
            var definitions = reverse ? new[] { exhausted, grant } : new[] { grant, exhausted };
            var session = Session(Catalog(definitions, new StageDefinition(1000000, 3, 0)));

            var result = session.Execute(PlayHandAction.Instance, 0);
            Assert.That(result.Events.OfType<ModifierActivatedEvent>().Select(value => value.DefinitionId),
                Is.EqualTo(definitions.Select(value => value.Identity)));
            Assert.That(result.Events.OfType<HandScoredEvent>().Single().Score.ModifierBaseScore, Is.EqualTo(17));
            Assert.That(result.Snapshot.RerollsRemaining, Is.EqualTo(2));
            foreach (var condition in new[] { grantCondition, exhaustedCondition })
            {
                Assert.That(condition.Contexts.Count, Is.EqualTo(1));
                Assert.That(condition.Contexts[0].HandsRemaining, Is.EqualTo(2));
                Assert.That(condition.Contexts[0].RerollsRemaining, Is.Zero);
                Assert.That(condition.Contexts[0].HandsPlayedInStage, Is.Zero);
            }
        }

        [Test]
        public void CustomConditionPreviewPreservesStateAndMatchesCommittedPlayAndFutureDice()
        {
            var condition = new RecordingCondition(context =>
                context.HandsRemaining == 19 && context.RerollsRemaining == 20);
            var definition = Modifier("custom.preview", ModifierPhase.PlayHand, condition,
                ModifierActivationLimit.OncePerHand,
                new AddBaseScoreEffect(11),
                new AddRerollsEffect(3));
            var catalog = Catalog(new[] { definition });
            var session = Session(catalog);
            var control = Session(catalog);
            var before = session.Snapshot;
            var save = session.CaptureSave();
            HandPreview preview = null;
            for (int i = 0; i < 3; ++i)
            {
                preview = session.PreviewHandResolution();
                Assert.That(preview.Score.ModifierBaseScore, Is.EqualTo(11));
                Assert.That(preview.RerollsRemaining, Is.EqualTo(23));
                Assert.That(preview.Modifiers[0].TotalActivations, Is.EqualTo(1));
                EncounterSessionTests.AssertSnapshotsEqual(before, session.Snapshot);
                EncounterPersistenceTests.AssertGraphEqual(save, session.CaptureSave());
            }
            Assert.That(condition.Contexts.Count, Is.EqualTo(3));
            var committed = session.Execute(PlayHandAction.Instance, 0);
            Assert.That(condition.Contexts.Count, Is.EqualTo(4));
            var score = committed.Events.OfType<HandScoredEvent>().Single().Score;
            Assert.That(score.HandScore, Is.EqualTo(preview.Score.HandScore));
            Assert.That(score.ModifierBaseScore, Is.EqualTo(preview.Score.ModifierBaseScore));
            Assert.That(committed.Snapshot.RerollsRemaining, Is.EqualTo(preview.RerollsRemaining));
            Assert.That(committed.Snapshot.Modifiers[0].TotalActivations, Is.EqualTo(1));
            EncounterSessionTests.AssertSnapshotsEqual(control.Execute(PlayHandAction.Instance, 0).Snapshot,
                committed.Snapshot);
            EncounterSessionTests.AssertSnapshotsEqual(control.Execute(new RerollDiceAction(31), 1).Snapshot,
                session.Execute(new RerollDiceAction(31), 1).Snapshot);
        }

        [Test]
        public void CustomConditionsRestoreByDefinitionIdWithoutReevaluatingStoredActivations()
        {
            var initial = new RecordingCondition(context => true);
            var reroll = new RecordingCondition(context => context.SelectedMask == 3);
            var definitions = new[]
            {
                Modifier("custom.initial", ModifierPhase.InitialRoll, initial,
                    ModifierActivationLimit.OncePerHand, new AddBaseScoreEffect(5)),
                Modifier("custom.reroll", ModifierPhase.PlayerReroll, reroll,
                    ModifierActivationLimit.OncePerHand, new AddMultiplierEffect(2))
            };
            var catalog = Catalog(definitions);
            var session = Session(catalog);
            session.Execute(new RerollDiceAction(3), 0);
            var save = session.CaptureSave();
            Assert.That(save.Modifiers.Select(value => value.DefinitionId),
                Is.EqualTo(definitions.Select(value => value.Identity)));
            Assert.That(initial.Contexts.Count, Is.EqualTo(1));
            Assert.That(reroll.Contexts.Count, Is.EqualTo(1));

            var result = EncounterSession.RestoreEncounter(catalog, save);
            Assert.That(result.Success, Is.True, result.Error + ": " + result.Detail);
            var restored = result.Session;
            Assert.That(initial.Contexts.Count, Is.EqualTo(1));
            Assert.That(reroll.Contexts.Count, Is.EqualTo(1));
            Assert.That(restored.InitialResolution.Events, Is.Empty);
            EncounterPersistenceTests.AssertGraphEqual(save, restored.CaptureSave());
            EncounterSessionTests.AssertSnapshotsEqual(session.Snapshot, restored.Snapshot);

            var resumedReroll = restored.Execute(new RerollDiceAction(3), 1);
            Assert.That(resumedReroll.Events.OfType<ModifierActivatedEvent>(), Is.Empty);
            Assert.That(reroll.Contexts.Count, Is.EqualTo(1));
            EncounterSessionTests.AssertSnapshotsEqual(session.Execute(new RerollDiceAction(3), 1).Snapshot,
                resumedReroll.Snapshot);
            EncounterSessionTests.AssertSnapshotsEqual(session.Execute(PlayHandAction.Instance, 2).Snapshot,
                restored.Execute(PlayHandAction.Instance, 2).Snapshot);
            Assert.That(initial.Contexts.Count, Is.EqualTo(3));
            EncounterSessionTests.AssertSnapshotsEqual(session.Execute(new RerollDiceAction(3), 3).Snapshot,
                restored.Execute(new RerollDiceAction(3), 3).Snapshot);
            Assert.That(reroll.Contexts.Count, Is.EqualTo(3));
            EncounterPersistenceTests.AssertGraphEqual(session.CaptureSave(), restored.CaptureSave());
        }

        private static ModifierDefinition Modifier(string id, ModifierPhase phase, IModifierCondition condition,
            ModifierActivationLimit limit, params ModifierEffect[] effects)
            => new ModifierDefinition(TestModifiers.IdOf(id), TestFamilies.Of("test." + id), phase, condition, effects, limit);

        private static MechanicalCatalog Catalog(ModifierDefinition[] definitions, StageDefinition stage = null)
        {
            var encounter = new EncounterDefinition(TestContent.EncounterId,
                new[] { stage ?? new StageDefinition(1000000, 20, 20) },
                startingModifiers: definitions.Select(value => value.Identity));
            return new MechanicalCatalog("test.polymorphism", TestContent.Hands(), new[] { encounter }, definitions);
        }

        private static EncounterSession Session(MechanicalCatalog catalog)
            => EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 14);
    }

    /** A condition that records every context it evaluates and answers with the given predicate. */
    // Production conditions must be pure. Recording is test-only instrumentation of interface dispatch.
    internal sealed class RecordingCondition : IModifierCondition
    {
        private readonly Func<ModifierContext, bool> predicate;
        internal List<ModifierContext> Contexts { get; } = new List<ModifierContext>();

        internal RecordingCondition(Func<ModifierContext, bool> predicate) => this.predicate = predicate;

        public bool Evaluate(in ModifierContext context)
        {
            Contexts.Add(context);
            return predicate(context);
        }
    }
}
