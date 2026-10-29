using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using Resolution = Dice.Engine.Resolution;

namespace Dice.Tests
{
    /**
     * The encounter-charms screen over synthetic hierarchies: how it lists an encounter's charms, and when Accept on a
     * journey's invitation opens it before play instead of starting the encounter at once.
     */
    public sealed class EncounterCharmsScreenTests
    {
        private SyntheticUi ui;
        private SyntheticCharms synthetic;

        [SetUp]
        public void SetUp()
        {
            ui = new SyntheticUi();
            synthetic = new SyntheticCharms();
        }

        [TearDown]
        public void TearDown()
        {
            ui.Dispose();
            synthetic.Dispose();
        }

        /** Checks the screen shows one row per family in authored order and narration only when a line is given / fails if a family's variants split into rows, rows from an earlier showing stay visible, or narration shows without a line. */
        [Test]
        public void ShowListsOneRowPerFamilyInAuthoredOrder()
        {
            var screen = ui.CharmsScreen(ui.Root);
            screen.Build(() => { });
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(100, 3, 3) });
            var catalog = new MechanicalCatalog("test.charms_screen", TestContent.Hands(), new[] { encounter }, new[]
            {
                Modifier("first.base", "family.first", 5), Modifier("second", "family.second", 6),
                Modifier("first.more", "family.first", 7)
            });

            var charms = synthetic.For(catalog);
            screen.Show(catalog, charms, TestModifiers.IdsOf("first.base", "second", "first.more"), "A spoken line.");
            Assert.That(screen.IsOpen, Is.True);
            Assert.That(VisibleFamilies(screen), Is.EqualTo(new[] { TestFamilies.Of("family.first"), TestFamilies.Of("family.second") }));
            Assert.That(screen.Narration.gameObject.activeSelf, Is.True);
            Assert.That(screen.Narration.text, Does.Contain("A spoken line."));

            screen.Show(catalog, charms, TestModifiers.IdsOf("second"));
            Assert.That(VisibleFamilies(screen), Is.EqualTo(new[] { TestFamilies.Of("family.second") }));
            Assert.That(screen.Narration.gameObject.activeSelf, Is.False);
        }

        /** Checks Accept opens the unnarrated screen and waits for Begin only when the encounter's additions are offered from the first clear / fails if such an encounter starts at Accept, Back starts it, or an encounter without first-clear additions shows the screen. */
        [Test]
        public void AcceptOpensTheScreenBeforePlayOnlyForAdditionsOfferedFromTheFirstClear()
        {
            var view = ui.Add<EncounterJourneyView>(ui.Root, "Journey");
            var accept = ui.NewButton(view.transform, "Accept");
            SyntheticUi.Set(view, "advanceButton", ui.NewButton(view.transform, "Advance"));
            SyntheticUi.Set(view, "acceptButton", accept);
            SyntheticUi.Set(view, "skipButton", ui.NewButton(view.transform, "Skip"));
            SyntheticUi.Set(view, "returnButton", ui.NewButton(view.transform, "Return"));
            var screen = ui.CharmsScreen(view.transform);
            SyntheticUi.Set(view, "<EncounterCharms>k__BackingField", screen);
            view.Build(null);
            var journey = ui.Own(ScriptableObject.CreateInstance<EncounterJourneyAsset>());
            TestJourneys.SetEncounterBeats(journey, null, synthetic.Encounter("encounter.test"));
            var controls = new TestJourneyControls { SelectedJourney = journey, JourneyPhase = EncounterJourneyPhase.Sequence };

            view.Bind(controls, Controls(EncounterCharmsIntroductionTests.Catalog(firstClearedStage: 1, additions: true)));
            accept.onClick.Invoke();
            Assert.That(screen.IsOpen, Is.True, "Additions offered from the first clear are introduced after Accept.");
            Assert.That(controls.Accepted, Is.Zero, "The encounter waits for Begin.");
            Assert.That(screen.Narration.gameObject.activeSelf, Is.False, "The pre-encounter screen has no narration.");
            screen.BackButton.onClick.Invoke();
            Assert.That(screen.IsOpen, Is.False, "Back returns to the invitation.");
            Assert.That(controls.Accepted, Is.Zero, "Back does not start the encounter.");
            accept.onClick.Invoke();
            screen.BeginButton.onClick.Invoke();
            Assert.That(controls.Accepted, Is.EqualTo(1), "Begin starts the encounter.");

            foreach (var catalog in new[]
            {
                EncounterCharmsIntroductionTests.Catalog(firstClearedStage: 3, additions: true),
                EncounterCharmsIntroductionTests.Catalog(firstClearedStage: 1, additions: false)
            })
            {
                controls.Accepted = 0;
                view.Bind(controls, Controls(catalog));
                accept.onClick.Invoke();
                Assert.That(controls.Accepted, Is.EqualTo(1), catalog.Version + ": Accept starts the encounter at once.");
                Assert.That(screen.IsOpen, Is.False, catalog.Version);
            }
        }

        private EncounterControls Controls(MechanicalCatalog catalog)
            => new EncounterControls(catalog) { ModifierPresentation = synthetic.For(catalog) };

        private static ModifierFamilyDefinition[] VisibleFamilies(SpiritEncounterCharmsScreen screen)
            => screen.Rows.Where(row => row.gameObject.activeSelf).Select(row => row.Family).ToArray();

        private static ModifierDefinition Modifier(string id, string family, int amount)
            => new ModifierDefinition(TestModifiers.IdOf(id), TestFamilies.Of(family), ModifierPhase.PlayHand, new AlwaysCondition(),
                new ModifierEffect[] { new AddBaseScoreEffect(amount) });

        /** Encounter controls that only supply the mechanical catalog. */
        private sealed class EncounterControls : ISpiritEncounterControls
        {
            public EncounterControls(MechanicalCatalog catalog) => Catalog = catalog;

            public EncounterSnapshot Snapshot => null;
            public MechanicalCatalog Catalog { get; }
            public ModifierPresentationCatalog ModifierPresentation { get; set; }
            public HandPreview Preview => null;
            public IReadOnlyList<ModifierInspection> ModifierInspections => Array.Empty<ModifierInspection>();
            public int SelectedMask => 0;
            public bool IsBusy => false;
            public SpiritStatus Status => SpiritStatus.None;
            public event Action StateChanged { add { } remove { } }
            public event Action<Resolution> Resolved { add { } remove { } }
            public void ToggleDie(int index) { }
            public void Reroll() { }
            public void PlayHand() { }
            public void InteractWithPiece(TablePieceDefinition.Id pieceId, int interactionIndex, PieceInteractionInput input) { }
            public void RefreshRewards() { }
            public void ChooseReward(long offerId, long optionId) { }
            public void StartNew() { }
            public void Save() { }
            public void Load() { }
            public void Abandon() { }
            public void CompletePresentation() { }
        }
    }
}
