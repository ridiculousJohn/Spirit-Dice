using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    /**
     * The in-encounter charms introduction over a synthetic screen: it covers the first charm offer that can contain the
     * encounter's additions until Continue, stays dismissed for that stage's refreshed offer, and shows again once its
     * dismissal is reset, as a loaded or resumed run does. The snapshots come from a session of a synthetic encounter whose
     * additions are first offered at the stage 3 clear.
     */
    public sealed class EncounterCharmsIntroductionDismissalTests
    {
        private const string Line = "A line spoken over the offer.";
        private SyntheticUi ui;
        private SpiritEncounterCharmsIntroduction introduction;
        private MechanicalCatalog catalog;
        private EncounterJourneyAsset journey;
        private EncounterSnapshot playing;
        private EncounterSnapshot introduced;
        private EncounterSnapshot refreshed;
        private EncounterSnapshot chosen;
        private readonly SyntheticCharms synthetic = new SyntheticCharms();
        private ModifierPresentationCatalog charms;

        [SetUp]
        public void SetUp()
        {
            ui = new SyntheticUi();
            introduction = ui.Add<SpiritEncounterCharmsIntroduction>(ui.Root, "Charms Introduction");
            SyntheticUi.Set(introduction, "screen", ui.CharmsScreen(introduction.transform));
            introduction.Build();
            journey = ui.Own(ScriptableObject.CreateInstance<EncounterJourneyAsset>());
            SyntheticUi.Set(journey, "encounterId", TestContent.EncounterId);
            SyntheticUi.Set(journey, "encounterCharmsIntroductionText", Line);

            catalog = EncounterCharmsIntroductionTests.Catalog(firstClearedStage: 3, additions: true);
            charms = synthetic.For(catalog);
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 11);
            playing = session.Snapshot;
            for (int cleared = 1; cleared <= 3; ++cleared)
            {
                EncounterCharmsIntroductionTests.Execute(session, PlayHandAction.Instance);
                if (session.Snapshot.Phase == EncounterPhase.ChoosingUnlock)
                    EncounterCharmsIntroductionTests.ContinueAfterUnlock(session);
                if (cleared == 3) break;
                var offer = session.Snapshot.PendingReward;
                EncounterCharmsIntroductionTests.Execute(session, new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId));
                var upgrade = session.Snapshot.PendingReward;
                EncounterCharmsIntroductionTests.Execute(session,
                    new SelectHandUpgradeAction(upgrade.OfferId, upgrade.Options[0].OptionId));
            }
            introduced = session.Snapshot;
            EncounterCharmsIntroductionTests.Execute(session, new RefreshModifierOfferAction(introduced.PendingReward.OfferId));
            refreshed = session.Snapshot;
            var pending = refreshed.PendingReward;
            EncounterCharmsIntroductionTests.Execute(session, new SelectModifierAction(pending.OfferId, pending.Options[0].OptionId));
            chosen = session.Snapshot;
        }

        [TearDown]
        public void TearDown()
        {
            ui.Dispose();
            synthetic.Dispose();
        }

        /** Checks Continue dismisses the introduction for its offer's stage, refreshed offer included / fails if it does not open over that offer with the journey's line and the encounter's family, opens while playing, or reopens after Continue. */
        [Test]
        public void ContinueDismissesTheIntroductionForItsOfferAndARefreshedOffer()
        {
            introduction.Refresh(catalog, charms, playing, journey);
            Assert.That(introduction.IsOpen, Is.False, "Nothing is introduced while playing.");

            introduction.Refresh(catalog, charms, introduced, journey);
            Assert.That(introduction.IsOpen, Is.True, "The first offer that can contain the additions is introduced.");
            var screen = introduction.Screen;
            Assert.That(screen.Narration.gameObject.activeSelf, Is.True);
            Assert.That(screen.Narration.text, Does.Contain(Line));
            var additions = catalog.GetEncounter(TestContent.EncounterId).EncounterModifierIds
                .Select(id => catalog.GetModifier(id).FamilyId).Distinct();
            Assert.That(screen.Rows.Where(row => row.gameObject.activeSelf).Select(row => row.FamilyId), Is.EqualTo(additions));

            screen.BeginButton.onClick.Invoke();
            Assert.That(introduction.IsOpen, Is.False, "Continue dismisses the introduction.");
            introduction.Refresh(catalog, charms, introduced, journey);
            Assert.That(introduction.IsOpen, Is.False, "The dismissed offer is not introduced again.");
            introduction.Refresh(catalog, charms, refreshed, journey);
            Assert.That(introduction.IsOpen, Is.False, "A refreshed offer for the same stage stays dismissed.");
        }

        /** Checks a reset dismissal shows the introduction again while its offer is pending, and taking a charm leaves it closed / fails if a loaded or resumed run stays dismissed or the introduction outlives its offer. */
        [Test]
        public void ResettingTheDismissalIntroducesThePendingOfferAgain()
        {
            introduction.Refresh(catalog, charms, introduced, journey);
            introduction.Screen.BeginButton.onClick.Invoke();
            introduction.ResetDismissal();
            Assert.That(introduction.IsOpen, Is.False, "Resetting hides the introduction until the run is shown again.");

            introduction.Refresh(catalog, charms, refreshed, journey);
            Assert.That(introduction.IsOpen, Is.True, "The pending offer is introduced again after the reset.");
            introduction.Refresh(catalog, charms, chosen, journey);
            Assert.That(introduction.IsOpen, Is.False, "Taking a charm moves on without the introduction.");
        }
    }
}
