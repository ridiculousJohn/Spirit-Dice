using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class IntegrationBoundaryTests
    {
        [Test]
        public void EngineAssemblyHasNoUnityOrApplicationDependencies()
        {
            var references = typeof(EncounterSession).Assembly.GetReferencedAssemblies().Select(value => value.Name).ToArray();
            Assert.That(references.Any(value => value.StartsWith("Unity") || value.StartsWith("Dice.")), Is.False,
                string.Join(", ", references));
        }

        [Test]
        public void ChangingPresentersDoesNotChangeMechanics()
        {
            var catalog = TestContent.Catalog();
            var application = new EncounterApplication(catalog, TestContent.EncounterId, 52);
            var control = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 52);
            var firstPresenter = new RecordingPresenter();
            var secondPresenter = new RecordingPresenter();
            application.AttachPresenter(firstPresenter);
            Assert.That(firstPresenter.Snapshot.Revision, Is.Zero);
            var reroll = new RerollDiceAction(5);
            application.Submit(reroll, 0);
            control.Execute(reroll, 0);
            application.AttachPresenter(secondPresenter);
            Assert.That(secondPresenter.Snapshot.Revision, Is.EqualTo(1));
            application.Submit(PlayHandAction.Instance, 1);
            control.Execute(PlayHandAction.Instance, 1);
            Assert.That(firstPresenter.ResolutionCount, Is.EqualTo(1));
            Assert.That(secondPresenter.ResolutionCount, Is.EqualTo(1));
            EncounterSessionTests.AssertSnapshotsEqual(control.Snapshot, application.Snapshot);
            application.AttachPresenter(null);
            application.Submit(PlayHandAction.Instance, 2);
            control.Execute(PlayHandAction.Instance, 2);
            EncounterSessionTests.AssertSnapshotsEqual(control.Snapshot, application.Snapshot);
            Assert.That(secondPresenter.ResolutionCount, Is.EqualTo(1));
        }

        private sealed class RecordingPresenter : IEncounterPresenter
        {
            internal EncounterSnapshot Snapshot;
            internal int ResolutionCount;
            public void Synchronize(EncounterSnapshot snapshot) => Snapshot = snapshot;
            public void Present(Resolution resolution)
            {
                Snapshot = resolution.Snapshot;
                ++ResolutionCount;
            }
        }
    }
}
