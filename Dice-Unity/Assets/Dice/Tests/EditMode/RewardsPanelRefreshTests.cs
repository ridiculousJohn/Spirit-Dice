using System;
using System.Collections.Generic;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Resolution = Dice.Engine.Resolution;

namespace Dice.Tests
{
    /** A rewards panel over a synthetic hierarchy and charm card template, rendering a run's pending charm offer. */
    public sealed class RewardsPanelRefreshTests
    {
        private SyntheticUi ui;
        private SyntheticCharms synthetic;
        private SpiritRewardsPanel panel;
        private Text refreshLabel;

        [SetUp]
        public void SetUp()
        {
            ui = new SyntheticUi();
            synthetic = new SyntheticCharms();
            panel = ui.Add<SpiritRewardsPanel>(ui.Root, "Rewards");
            Transform parent = panel.transform;
            foreach (string label in new[] { "Title", "Instruction" })
                SyntheticUi.Set(panel, "<" + label + ">k__BackingField", ui.Add<Text>(parent, label));
            SyntheticUi.Set(panel, "<Content>k__BackingField", ui.Rect(parent, "Content"));
            var refresh = ui.NewButton(parent, "Refresh");
            refreshLabel = ui.Add<Text>(refresh.transform, "Label");
            SyntheticUi.Set(panel, "<RefreshButton>k__BackingField", refresh);
            SyntheticUi.Set(panel, "<ConfirmButton>k__BackingField", ui.NewButton(parent, "Confirm"));
            SyntheticUi.Set(panel, "cardPrefab", CardTemplate());
            panel.Build(() => false);
        }

        [TearDown]
        public void TearDown()
        {
            ui.Dispose();
            synthetic.Dispose();
        }

        /** Checks the refresh button counts and allows the refreshes the snapshot states are available, and is disabled when none are / fails if the button divides the points itself, ignores the stated count or stays enabled without a refresh. */
        [Test]
        public void RefreshButtonShowsAndAllowsTheRefreshesTheSnapshotStates()
        {
            var catalog = RewardTestContent.Catalog(policy: RewardTestContent.Policy(startingRefreshes: 3, refreshesPerClear: 2,
                refreshCost: 2));
            panel.Bind(new Controls(catalog, synthetic.For(catalog)));
            var session = RewardTestContent.Session(catalog);
            session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
            Assert.That(session.Snapshot.RewardRefreshesAvailable, Is.EqualTo(2), "Fixture refreshes");
            panel.Render(session.Snapshot);
            Assert.That(panel.RefreshButton.gameObject.activeSelf, Is.True);
            Assert.That(panel.RefreshButton.interactable, Is.True);
            Assert.That(refreshLabel.text, Is.EqualTo("<rewards.refreshButton>2"));

            for (int i = 0; i < 2; ++i)
                session.Execute(new RefreshModifierOfferAction(session.Snapshot.PendingReward.OfferId), session.Snapshot.Revision);
            Assert.That(session.Snapshot.RewardRefreshesAvailable, Is.Zero, "Fixture refreshes");
            panel.Render(session.Snapshot);
            Assert.That(panel.RefreshButton.interactable, Is.False);
            Assert.That(refreshLabel.text, Is.EqualTo("<rewards.refreshButton>0"));
        }

        /** A charm card template, inactive outside the panel, holding what a charm offer binds. */
        private SpiritRewardCard CardTemplate()
        {
            var button = ui.NewButton(ui.Root, "Reward Card Template");
            var card = button.gameObject.AddComponent<SpiritRewardCard>();
            var modifierRoot = ui.Rect(card.transform, "Modifier");
            SyntheticUi.Set(card, "<Button>k__BackingField", button);
            SyntheticUi.Set(card, "modifierRoot", modifierRoot.gameObject);
            SyntheticUi.Set(card, "handRoot", ui.Rect(card.transform, "Hand").gameObject);
            SyntheticUi.Set(card, "modifierBackground", ui.Add<Image>(modifierRoot, "Background"));
            SyntheticUi.Set(card, "selectionMarker", ui.Add<Image>(card.transform, "Selection Marker"));
            foreach (string text in new[] { "title", "condition", "effects" })
                SyntheticUi.Set(card, text, ui.Add<Text>(modifierRoot, text));
            SyntheticUi.Set(card, "charmIcon", ui.Add<Image>(modifierRoot, "Charm Icon"));
            SyntheticUi.Set(card, "charmCategory", ui.CategoryBadge(modifierRoot));
            card.gameObject.SetActive(false);
            return card;
        }

        /** Controls that supply the catalog and charm presentation the panel renders; the panel calls no action here. */
        private sealed class Controls : ISpiritEncounterControls
        {
            public Controls(MechanicalCatalog catalog, ModifierPresentationCatalog charms)
            {
                Catalog = catalog;
                ModifierPresentation = charms;
            }

            public EncounterSnapshot Snapshot => null;
            public MechanicalCatalog Catalog { get; }
            public ModifierPresentationCatalog ModifierPresentation { get; }
            public HandPreview Preview => null;
            public IReadOnlyList<ModifierInspection> ModifierInspections => Array.Empty<ModifierInspection>();
            public int SelectedMask => 0;
            public bool IsBusy => false;
            public SpiritStatus Status => SpiritStatus.None;
            public event Action StateChanged { add { } remove { } }
            public event Action<Resolution> Resolved { add { } remove { } }
            public void ToggleDie(int index) => throw new NotSupportedException();
            public void Reroll() => throw new NotSupportedException();
            public void PlayHand() => throw new NotSupportedException();
            public void InteractWithPiece(string pieceId, string interactionId, PieceInteractionInput input) => throw new NotSupportedException();
            public void RefreshRewards() => throw new NotSupportedException();
            public void ChooseReward(long offerId, long optionId) => throw new NotSupportedException();
            public void StartNew() => throw new NotSupportedException();
            public void Save() => throw new NotSupportedException();
            public void Load() => throw new NotSupportedException();
            public void Abandon() => throw new NotSupportedException();
            public void CompletePresentation() => throw new NotSupportedException();
        }
    }
}
