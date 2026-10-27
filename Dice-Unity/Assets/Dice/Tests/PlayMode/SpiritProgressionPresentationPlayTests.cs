using System;
using System.Collections;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class SpiritProgressionPresentationPlayTests
    {
        [UnityTest]
        public IEnumerator ExhaustedUnlockListShowsContinueAndCompletesTheEncounter()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = ContentReady.EncounterView(controller);
            var definition = controller.Catalog.Modifiers.First(value => value.Availability.Access == ModifierAccess.PermanentUnlock);
            var encounter = new EncounterDefinition("encounter.collected", new[] { new StageDefinition(1, 1, 0) },
                unlockMilestones: new[] { new ModifierUnlockMilestone(1, Array.Empty<string>()) },
                unlockRewardModifierIds: new[] { definition.Id });
            var catalog = new MechanicalCatalog("collected.test", controller.Catalog.Hands, new[] { encounter }, new[] { definition });
            string path = ContentReady.SpiritSavePath();
            var profile = new PlayerProfile();
            profile.UnlockModifier(definition.Id);
            profile.DiscoverFamily(definition.FamilyId);
            PlayerProfileFileStore.Save(Path.Combine(Path.GetDirectoryName(path), "spirit-profile.json"), profile);
            controller.Initialize(catalog, encounter.Id, 60, path);
            yield return ContentReady.Routed(controller);
            view = ContentReady.EncounterView(controller);
            var portrait = ContentReady.Portrait();
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(443, 912);
            controller.PlayHand();
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingUnlock));
            Assert.That(Find<Text>(view, "RewardTitle").text, Is.EqualTo("All charms collected"));
            var confirm = Find<Button>(view, "UpgradeHandButton");
            Assert.That(confirm.interactable, Is.True);
            Assert.That(confirm.GetComponentInChildren<Text>().text, Is.EqualTo("Continue"));
            confirm.onClick.Invoke();
            yield return ContentReady.Routed();
            view = ContentReady.EncounterView(controller);
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(controller.Snapshot.NewlyUnlockedModifierIds, Is.Empty);
            deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private static T Find<T>(Component root, string name) where T : Component
        {
            var component = root.GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name);
            Assert.That(component, Is.Not.Null, name);
            return component;
        }
    }
}
