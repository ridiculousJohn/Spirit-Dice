using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;

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
        public void ImportedStandardCatalogMatchesReferenceValuesAndKeepsRewardBoundary()
        {
            var asset = AssetDatabase.LoadAssetAtPath<MechanicalCatalogAsset>("Assets/Dice/Content/Mechanical/Standard.asset");
            Assert.That(asset, Is.Not.Null);
            MechanicalCatalog catalog = asset.Build();
            EncounterDefinition encounter = catalog.GetEncounter("encounter.standard");
            long[] thresholds = { 160, 225, 320, 410, 520, 635, 750, 860, 975, 1090,
                1820, 2045, 2275, 2545, 2820, 3090, 3365, 3635, 3955, 4275, 5455, 5910, 6365, 7045, 7725 };
            Assert.That(encounter.Stages.Select(value => value.Threshold), Is.EqualTo(thresholds));
            for (int i = 0; i < encounter.Stages.Count; ++i)
            {
                int expectedBudget = i < 10 ? 3 : i < 20 ? 4 : 5;
                Assert.That(encounter.Stages[i].StartingHands, Is.EqualTo(expectedBudget));
                Assert.That(encounter.Stages[i].StartingRerolls, Is.EqualTo(expectedBudget));
                Assert.That(encounter.Stages[i].ExitBehavior, Is.EqualTo(StageExitBehavior.AwaitRewards));
            }
            var reference = TestContent.Hands();
            for (int i = 0; i < reference.Length; ++i)
            {
                Assert.That(catalog.Hands[i].BaseScore, Is.EqualTo(reference[i].BaseScore));
                Assert.That(catalog.Hands[i].Multiplier, Is.EqualTo(reference[i].Multiplier));
                Assert.That(catalog.Hands[i].BaseScorePerUpgrade, Is.EqualTo(5));
                Assert.That(catalog.Hands[i].MultiplierPerUpgrade, Is.EqualTo(reference[i].MultiplierPerUpgrade));
                Assert.That(catalog.Hands[i].MaximumUpgrades, Is.EqualTo(6));
            }
            var application = EncounterApplication.Create(asset, "encounter.standard", 0);
            Assert.That(application.Snapshot.StartingThreshold, Is.EqualTo(160));
            Assert.That(application.Snapshot.HandsRemaining, Is.EqualTo(3));
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
