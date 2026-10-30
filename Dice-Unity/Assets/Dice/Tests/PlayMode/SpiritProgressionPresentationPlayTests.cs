using System.Collections;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class SpiritProgressionPresentationPlayTests
    {
        [UnityTest]
        public IEnumerator ExhaustedUnlockListShowsContinueAndCompletesTheEncounter()
        {
            yield return ContentReady.LoadApplication();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var definition = controller.Catalog.Modifiers.First(value => value.Availability.Access == ModifierAccess.PermanentUnlock);
            var configuration = Addressables.LoadAssetAsync<DiceApplicationConfig>(DiceBootstrap.ConfigurationAddress);
            yield return configuration;
            var table = ContentReady.DirectTable(configuration.Result, controller.Catalog, out var rules);
            Addressables.Release(configuration);
            var encounter = new EncounterDefinition(TestEncounters.IdOf("encounter.collected"), new[] { new StageDefinition(1, 1, 0) },
                rules: rules, unlockMilestones: new[] { new ModifierUnlockMilestone(1) },
                unlockRewardModifierIds: new[] { definition.Identity });
            var catalog = new MechanicalCatalog("collected.test", controller.Catalog.Hands, new[] { encounter }, new[] { definition });
            string path = ContentReady.SpiritSavePath();
            var profile = new PlayerProfile();
            profile.UnlockModifier(definition.Identity);
            profile.DiscoverFamily(definition.Family.Identity);
            PlayerProfileFileStore.Save(Path.Combine(Path.GetDirectoryName(path), PlayerProfileFileStore.FileName), profile);
            controller.Initialize(catalog, encounter.Identity, ContentReady.Seed, path);
            yield return ContentReady.Routed(controller);
            var view = ContentReady.EncounterView(controller);
            Assert.That(view.TablePresentation, Is.SameAs(table));
            controller.PlayHand();
            yield return ContentReady.WaitUntil(() => !controller.IsBusy, "The played hand's presentation did not complete.");
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingUnlock));
            var rewards = ContentReady.Find<Text>(controller, "RewardTitle").GetComponentInParent<SpiritUiContext>(true).Wording.rewards
                .For(RewardKind.ModifierUnlock);
            Assert.That(ContentReady.Find<Text>(controller, "RewardTitle").text, Is.EqualTo(rewards.emptyTitle));
            var confirm = ContentReady.Find<Button>(controller, "UpgradeHandButton");
            Assert.That(confirm.interactable, Is.True);
            Assert.That(confirm.GetComponentInChildren<Text>().text, Is.EqualTo(rewards.emptyConfirmButton));
            confirm.onClick.Invoke();
            yield return ContentReady.Routed();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(controller.Snapshot.NewlyUnlockedModifierIds, Is.Empty);
            yield return ContentReady.WaitUntil(() => !controller.IsBusy, "The encounter's completion did not finish presenting.");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
