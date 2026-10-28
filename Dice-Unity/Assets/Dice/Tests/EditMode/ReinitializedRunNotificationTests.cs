using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Engine;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    /** Notifications the controller publishes while it starts a run from another catalog. */
    public sealed class ReinitializedRunNotificationTests
    {
        private const string EncounterId = "encounter.reinitialize";
        private const string FirstCharm = "modifier.reinitialize.first";
        private const string SecondCharm = "modifier.reinitialize.second";
        private GameObject host;
        private SpiritEncounterController controller;
        private string artifactRoot;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Reinitialized run test");
            host.SetActive(false);
            controller = host.AddComponent<SpiritEncounterController>();
            artifactRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..",
                "Docs", "work", "spirit-file-tests", Guid.NewGuid().ToString("N")));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
            if (Directory.Exists(artifactRoot)) TestContext.WriteLine("Retained spirit test files: " + artifactRoot);
        }

        /**
         * Checks that every state and journey notification published while the controller re-initializes with another
         * catalog already exposes that catalog, the new run's charm and only that charm's ready contribution. Fails if an
         * observer sees the previous catalog, the previous run or the previous run's ready charm.
         */
        [Test]
        public void ReinitializingWithAnotherCatalogNotifiesOnlyTheNewRun()
        {
            var first = OneCharmCatalog(FirstCharm);
            var second = OneCharmCatalog(SecondCharm);
            controller.Initialize(first, EncounterId, 60, Path.Combine(artifactRoot, "first.json"));
            Assert.That(Describe(first), Is.EqualTo(Expected(FirstCharm)), "The first run readies its own charm.");

            var observed = new List<string>();
            Action observe = () => observed.Add(Describe(second));
            controller.StateChanged += observe;
            controller.JourneyChanged += observe;
            controller.Initialize(second, EncounterId, 60, Path.Combine(artifactRoot, "second.json"));
            controller.StateChanged -= observe;
            controller.JourneyChanged -= observe;

            Assert.That(observed, Is.Not.Empty, "Re-initialization must publish the new run.");
            Assert.That(observed, Has.All.EqualTo(Expected(SecondCharm)));
        }

        /** A one-stage encounter that starts with one charm whose Play Hand bonus is always ready. */
        private static MechanicalCatalog OneCharmCatalog(string modifierId)
        {
            var definition = new ModifierDefinition(modifierId, TestFamilies.Of(modifierId), ModifierPhase.PlayHand,
                new AlwaysCondition(), new ModifierEffect[] { new AddBaseScoreEffect(1) });
            var encounter = new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 3, 3) },
                startingModifiers: new[] { modifierId });
            return new MechanicalCatalog("reinitialize." + modifierId, TestContent.Hands(), new[] { encounter }, new[] { definition });
        }

        private static string Expected(string charm) => "expected catalog / run " + charm + " / ready " + charm;

        /** The catalog, run charms and ready Play Hand contributions an observer reads from the controller right now. */
        private string Describe(MechanicalCatalog expected)
        {
            string catalog = ReferenceEquals(controller.Catalog, expected) ? "expected catalog" : "another catalog";
            string run = controller.Snapshot == null ? "none"
                : string.Join(",", controller.Snapshot.Modifiers.Select(instance => instance.DefinitionId));
            string ready = controller.Preview == null ? "none"
                : string.Join(",", controller.Preview.Score.ModifierContributions
                    .Where(contribution => contribution.Phase == ModifierPhase.PlayHand)
                    .Select(contribution => contribution.DefinitionId));
            return catalog + " / run " + run + " / ready " + ready;
        }
    }
}
