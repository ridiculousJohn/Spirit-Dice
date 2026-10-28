using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class SpiritCharmStatusPresentationTests
    {
        [Test]
        public void AvailableUsesDoNotImplyThatTheConditionIsReady()
        {
            var ready = Modifier("ready", ModifierPhase.PlayHand, new AlwaysCondition());
            var waiting = Modifier("waiting", ModifierPhase.PlayHand, new NeverCondition());
            var session = Session(ready, waiting);
            AssertStatus(session, ready, SpiritCharmStatusState.Ready, 1, "Ready");
            AssertStatus(session, waiting, SpiritCharmStatusState.WaitingCondition, 1, "Waiting");
            Assert.That(session.PreviewHandResolution().Events.OfType<ModifierActivatedEvent>()
                .Select(value => value.DefinitionId), Is.EqualTo(new[] { ready.Id }));
        }

        [Test]
        public void AConsumedStageUseTakesPriorityWhenItsConditionStopsMatching()
        {
            var first = Modifier("first", ModifierPhase.PlayHand, new FirstHandCondition(),
                ModifierActivationLimit.OncePerStage);
            var session = Session(first);
            AssertStatus(session, first, SpiritCharmStatusState.Ready, 1);
            Assert.That(session.Execute(PlayHandAction.Instance, 0).Accepted, Is.True);
            var inspection = session.InspectModifiersForPlayHand().Single();
            Assert.That(inspection.ConditionMet, Is.False);
            Assert.That(inspection.ActivationLimitReached, Is.True);
            Assert.That(session.Snapshot.Modifiers.Single().ActivationsThisHand, Is.Zero);
            AssertStatus(session, first, SpiritCharmStatusState.Used, 0, "Activated this stage");
        }

        [Test]
        public void RollCharmsKeepUnknownConditionsAndShowTheirActualConsumedUses()
        {
            var initial = Modifier("initial", ModifierPhase.InitialRoll, new AlwaysCondition());
            var initialWaiting = Modifier("initial.waiting", ModifierPhase.InitialRoll, new NeverCondition());
            var reroll = Modifier("reroll", ModifierPhase.PlayerReroll, new AlwaysCondition());
            var session = Session(initial, initialWaiting, reroll);
            Assert.That(session.InspectModifiersForPlayHand().Select(value => value.ConditionMet), Is.All.Null);
            AssertStatus(session, initial, SpiritCharmStatusState.Used, 0, "Activated this hand");
            AssertStatus(session, initialWaiting, SpiritCharmStatusState.OnRoll, 1, "On the initial roll");
            AssertStatus(session, reroll, SpiritCharmStatusState.OnRoll, 1, "After a reroll");
            Assert.That(session.Execute(new RerollDiceAction(1), 0).Accepted, Is.True);
            AssertStatus(session, reroll, SpiritCharmStatusState.Used, 0);
        }

        [Test]
        public void HandUsesResetOnTheNextHandWhileStageUsesRemainConsumed()
        {
            var hand = Modifier("hand", ModifierPhase.PlayerReroll, new AlwaysCondition());
            var stage = Modifier("stage", ModifierPhase.PlayerReroll, new AlwaysCondition(),
                ModifierActivationLimit.OncePerStage);
            var session = Session(hand, stage);
            Assert.That(session.Execute(new RerollDiceAction(1), 0).Accepted, Is.True);
            AssertStatus(session, hand, SpiritCharmStatusState.Used, 0);
            AssertStatus(session, stage, SpiritCharmStatusState.Used, 0);
            Assert.That(session.Execute(PlayHandAction.Instance, 1).Accepted, Is.True);
            Assert.That(session.Snapshot.HandIndex, Is.EqualTo(1));
            AssertStatus(session, hand, SpiritCharmStatusState.OnRoll, 1);
            AssertStatus(session, stage, SpiritCharmStatusState.Used, 0);
            Assert.That(session.Execute(new RerollDiceAction(1), 2).Accepted, Is.True);
            Assert.That(session.Snapshot.Modifiers.Select(value => value.TotalActivations),
                Is.EqualTo(new long[] { 2, 1 }));
        }

        [Test]
        public void StageUsesResetOnAnActualStageTransition()
        {
            var stage = Modifier("stage", ModifierPhase.PlayerReroll, new AlwaysCondition(),
                ModifierActivationLimit.OncePerStage);
            var encounter = new EncounterDefinition(TestContent.EncounterId,
                new[] { new StageDefinition(1, 3, 3), new StageDefinition(1000000, 3, 3) },
                startingModifiers: new[] { stage.Id });
            var catalog = new MechanicalCatalog("test.charm.status.stage", TestContent.Hands(),
                new[] { encounter }, new[] { stage });
            var session = EncounterSession.CreateEncounter(catalog, encounter.Id, 14);
            Assert.That(session.Execute(new RerollDiceAction(1), 0).Accepted, Is.True);
            AssertStatus(session, stage, SpiritCharmStatusState.Used, 0);
            Assert.That(session.Execute(PlayHandAction.Instance, 1).Accepted, Is.True);
            Assert.That(session.Snapshot.StageIndex, Is.EqualTo(1));
            Assert.That(session.Snapshot.Modifiers.Single().TotalActivations, Is.EqualTo(1));
            AssertStatus(session, stage, SpiritCharmStatusState.OnRoll, 1);
        }

        [Test]
        public void UnlimitedUsesRemainUnlimitedAfterRepeatedActivations()
        {
            var unlimited = Modifier("unlimited", ModifierPhase.PlayerReroll, new AlwaysCondition(),
                ModifierActivationLimit.Unlimited);
            var session = Session(unlimited);
            for (int reroll = 0; reroll < 2; ++reroll)
            {
                Assert.That(session.Execute(new RerollDiceAction(1), session.Snapshot.Revision).Accepted, Is.True);
                AssertStatus(session, unlimited, SpiritCharmStatusState.OnRoll, null);
            }
            Assert.That(session.Snapshot.Modifiers.Single().ActivationsThisHand, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NonPlayingEncountersPreserveUsesWithoutClaimingReadiness(bool completed)
        {
            var initial = Modifier("initial", ModifierPhase.InitialRoll, new AlwaysCondition());
            var waiting = Modifier("waiting", ModifierPhase.PlayHand, new NeverCondition());
            var encounter = new EncounterDefinition(TestContent.EncounterId,
                new[] { new StageDefinition(1, 3, 3, StageExitBehavior.AwaitRewards),
                    new StageDefinition(1000000, 3, 3) },
                startingModifiers: new[] { initial.Id, waiting.Id });
            var catalog = new MechanicalCatalog("test.charm.status.inactive", TestContent.Hands(),
                new[] { encounter }, new[] { initial, waiting });
            var session = EncounterSession.CreateEncounter(catalog, encounter.Id, 14);
            Assert.That(session.Execute(completed ? (EncounterAction)AbandonEncounterAction.Instance :
                PlayHandAction.Instance, 0).Accepted, Is.True);
            Assert.That(session.Snapshot.Phase,
                Is.EqualTo(completed ? EncounterPhase.Completed : EncounterPhase.AwaitingRewards));
            AssertStatus(session, initial, SpiritCharmStatusState.Used, 0);
            AssertStatus(session, waiting, SpiritCharmStatusState.Inactive, 1,
                completed ? "Encounter ended" : "Between stages");
        }

        [Test]
        public void MissingInspectionKeepsTheCharmWithoutGuessingItsCondition()
        {
            var definition = Modifier("uninspected", ModifierPhase.PlayHand, new AlwaysCondition());
            var session = Session(definition);
            var status = SpiritCharmStatusPresentation.Create(session.Snapshot.Modifiers.Single(), definition,
                session.Snapshot.Phase, null);
            Assert.That(status.State, Is.EqualTo(SpiritCharmStatusState.Kept));
            Assert.That(status.RemainingUses, Is.EqualTo(1));
            Assert.That(status.GetDetailText(definition.Phase, session.Snapshot.Phase), Is.Empty);
        }

        private static void AssertStatus(EncounterSession session, ModifierDefinition definition,
            SpiritCharmStatusState state, int? remaining, string detailText = null)
        {
            var instance = session.Snapshot.Modifiers.Single(value => value.DefinitionId == definition.Id);
            var inspection = session.InspectModifiersForPlayHand().Single(value => value.InstanceId == instance.InstanceId);
            var status = SpiritCharmStatusPresentation.Create(instance, definition, session.Snapshot.Phase, inspection);
            Assert.That(status.State, Is.EqualTo(state), definition.Id);
            Assert.That(status.RemainingUses, Is.EqualTo(remaining), definition.Id);
            Assert.That(status.ActivationLimit, Is.EqualTo(definition.ActivationLimit), definition.Id);
            if (detailText != null)
                Assert.That(status.GetDetailText(definition.Phase, session.Snapshot.Phase), Is.EqualTo(detailText), definition.Id);
        }

        private static ModifierDefinition Modifier(string id, ModifierPhase phase, IModifierCondition condition,
            ModifierActivationLimit limit = ModifierActivationLimit.OncePerHand)
            => new ModifierDefinition(id, "test." + id, phase, condition,
                new ModifierEffect[] { new AddBaseScoreEffect(7) }, limit);

        private static EncounterSession Session(params ModifierDefinition[] definitions)
        {
            var encounter = new EncounterDefinition(TestContent.EncounterId,
                new[] { new StageDefinition(1000000, 4, 4) },
                startingModifiers: definitions.Select(value => value.Id));
            var catalog = new MechanicalCatalog("test.charm.status", TestContent.Hands(),
                new[] { encounter }, definitions);
            return EncounterSession.CreateEncounter(catalog, encounter.Id, 14);
        }
    }
}
