using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    /** The charm popup over a synthetic hierarchy, showing one charm instance of a one-charm session. */
    public sealed class CharmPopupBindingTests
    {
        private SyntheticUi ui;
        private SpiritCharmPopup popup;
        private Button backdrop;
        private ModifierDefinition charm;
        private EncounterSnapshot snapshot;
        private readonly SyntheticCharms synthetic = new SyntheticCharms();
        private ModifierPresentationCatalog charms;
        private SpiritUiTheme theme;

        [SetUp]
        public void SetUp()
        {
            ui = new SyntheticUi();
            popup = ui.Add<SpiritCharmPopup>(ui.Root, "Charm Popup");
            backdrop = ui.NewButton(popup.transform, "Backdrop");
            var panel = ui.Rect(popup.transform, "Panel");
            var statusRow = ui.Rect(panel, "Status Row");
            var badge = ui.Add<SpiritCharmStatusBadge>(statusRow, "Status Badge");
            SyntheticUi.Set(badge, "<Icon>k__BackingField", ui.Add<Image>(badge.transform, "Icon"));
            theme = ui.Root.GetComponent<SpiritUiContext>().Theme;
            theme.readyStatusSymbol = Symbol();
            theme.waitingConditionStatusSymbol = Symbol();
            theme.usedStatusSymbol = Symbol();
            theme.onRollStatusSymbol = Symbol();
            theme.inactiveStatusSymbol = Symbol();
            theme.keptStatusSymbol = Symbol();
            SyntheticUi.Set(popup, "dismissButton", backdrop);
            SyntheticUi.Set(popup, "statusRow", statusRow);
            SyntheticUi.Set(popup, "<Panel>k__BackingField", panel);
            SyntheticUi.Set(popup, "<Title>k__BackingField", ui.Add<Text>(panel, "Title"));
            SyntheticUi.Set(popup, "<Icon>k__BackingField", ui.Add<Image>(panel, "Icon"));
            SyntheticUi.Set(popup, "<Category>k__BackingField", ui.CategoryBadge(panel));
            SyntheticUi.Set(popup, "<Effect>k__BackingField", ui.Add<Text>(panel, "Effect"));
            SyntheticUi.Set(popup, "<Description>k__BackingField", ui.Add<Text>(panel, "Description"));
            SyntheticUi.Set(popup, "<Status>k__BackingField", ui.Add<Text>(statusRow, "Status"));
            SyntheticUi.Set(popup, "<Badge>k__BackingField", badge);
            SyntheticUi.Set(popup, "<CloseButton>k__BackingField", ui.NewButton(panel, "Close"));
            popup.Build();
            popup.Close();

            charm = new ModifierDefinition(TestModifiers.IdOf("test.popup"), TestFamilies.Of("test.popup"), ModifierPhase.PlayHand, new AlwaysCondition(),
                new ModifierEffect[] { new AddBaseScoreEffect(5) });
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(100000, 3, 3) },
                startingModifiers: new[] { charm.Identity });
            var catalog = new MechanicalCatalog("test.popup", TestContent.Hands(), new[] { encounter }, new[] { charm });
            snapshot = EncounterSession.CreateEncounter(catalog, encounter.Identity, 42).Snapshot;
            charms = synthetic.For(catalog);
        }

        [TearDown]
        public void TearDown()
        {
            ui.Dispose();
            synthetic.Dispose();
        }

        /** Checks showing a charm opens the popup for that instance and both the close button and the backdrop dismiss it / fails if the popup stays open or keeps its instance after either dismissal. */
        [Test]
        public void ShowOpensTheInstanceAndCloseOrBackdropDismissesIt()
        {
            Assert.That(popup.IsOpen, Is.False);
            foreach (var dismiss in new[] { popup.CloseButton, backdrop })
            {
                Show();
                Assert.That(popup.IsOpen, Is.True);
                Assert.That(popup.InstanceId, Is.EqualTo(snapshot.Modifiers[0].InstanceId));
                dismiss.onClick.Invoke();
                Assert.That(popup.IsOpen, Is.False, dismiss.name + " dismisses the popup.");
                Assert.That(popup.InstanceId, Is.EqualTo(-1), dismiss.name + " clears the inspected instance.");
            }
        }

        /** Checks showing a charm keeps the hierarchy's font sizes, panel anchors and objects / fails if binding restyles, repositions or rebuilds the authored popup. */
        [Test]
        public void ShowKeepsTheHierarchysTypographyPlacementAndObjects()
        {
            popup.Title.fontSize = 31;
            popup.Panel.anchorMin = new Vector2(.13f, .5f);
            popup.Panel.anchorMax = new Vector2(.87f, .5f);
            int objects = popup.GetComponentsInChildren<Transform>(true).Length;

            Show();

            Assert.That(popup.Title.fontSize, Is.EqualTo(31));
            Assert.That(popup.Panel.anchorMin, Is.EqualTo(new Vector2(.13f, .5f)));
            Assert.That(popup.Panel.anchorMax, Is.EqualTo(new Vector2(.87f, .5f)));
            Assert.That(popup.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
        }

        /** Checks the badge shows the theme's symbol for the charm's status and fails when the theme lacks it / fails if another state's symbol shows or a missing symbol passes silently. */
        [Test]
        public void BadgeShowsTheThemesSymbolForTheStatus()
        {
            Show();
            Assert.That(popup.Badge.Presentation.State, Is.EqualTo(SpiritCharmStatusState.Kept));
            Assert.That(popup.Badge.Icon.sprite, Is.SameAs(theme.keptStatusSymbol));
            Show(EncounterPhase.AwaitingRewards);
            Assert.That(popup.Badge.Presentation.State, Is.EqualTo(SpiritCharmStatusState.Inactive));
            Assert.That(popup.Badge.Icon.sprite, Is.SameAs(theme.inactiveStatusSymbol));

            theme.keptStatusSymbol = null;
            Assert.Throws<System.InvalidOperationException>(() => Show());
        }

        private Sprite Symbol() => ui.Own(Sprite.Create(ui.Own(new Texture2D(2, 2)), new Rect(0, 0, 2, 2), Vector2.one * .5f));

        private void Show() => Show(snapshot.Phase);

        private void Show(EncounterPhase phase)
        {
            var instance = snapshot.Modifiers[0];
            popup.Show(instance.InstanceId, charms, charm, SpiritCharmStatusPresentation.Create(instance, charm, phase, null), phase);
        }
    }
}
