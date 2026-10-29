using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    /** Main menu and save slot picker choices, bound to test-only menu controls on a minimal test hierarchy. */
    public sealed class SaveSlotChoiceTests
    {
        private SyntheticUi ui;
        private MenuControls controls;
        private SpiritMainMenu menu;
        private SpiritSaveSlotPicker picker;

        [SetUp]
        public void SetUp()
        {
            ui = new SyntheticUi();
            controls = new MenuControls();
            picker = BuildPicker(ui.Root);
            menu = BuildMenu(ui.Root, picker);
            menu.Build();
            menu.Bind(controls);
        }

        [TearDown]
        public void TearDown()
        {
            ui.Dispose();
        }

        /** Checks the menu enables Continue and Chapter only when the controls offer them; fails if either is enabled without its save. */
        [Test]
        public void MenuEnablesContinueAndChapterOnlyWhenOffered()
        {
            menu.Render(SpiritStatus.None);
            Assert.That(menu.ContinueButton.interactable, Is.False);
            Assert.That(menu.ChapterButton.interactable, Is.False);
            Assert.That(menu.NewGameButton.interactable, Is.True);

            controls.CanContinueGame = true;
            controls.CanOpenSavedChapter = true;
            menu.Render(SpiritStatus.None);
            Assert.That(menu.ContinueButton.interactable, Is.True);
            Assert.That(menu.ChapterButton.interactable, Is.True);
        }

        /** Checks New Game confirms before replacing an occupied slot and starts an empty slot directly; fails if a campaign is replaced unconfirmed or an empty slot asks. */
        [Test]
        public void NewGameConfirmsOnlyBeforeReplacingAnOccupiedSlot()
        {
            controls.Occupied.Add(1);
            picker.Show(true);
            picker.SlotButtons[0].onClick.Invoke();
            Assert.That(picker.IsConfirmingReplacement, Is.True);
            picker.CancelReplaceSaveButton.onClick.Invoke();
            Assert.That(picker.IsConfirmingReplacement, Is.False);
            Assert.That(picker.IsOpen, Is.True);
            Assert.That(controls.Calls, Is.Empty);

            picker.SlotButtons[0].onClick.Invoke();
            picker.ConfirmReplaceSaveButton.onClick.Invoke();
            Assert.That(controls.Calls, Is.EqualTo(new[] { "new game 1" }));
            Assert.That(picker.IsOpen, Is.False);

            picker.Show(true);
            picker.SlotButtons[1].onClick.Invoke();
            Assert.That(picker.IsConfirmingReplacement, Is.False);
            Assert.That(controls.Calls, Is.EqualTo(new[] { "new game 1", "new game 2" }));
        }

        /** Checks Load Game offers and selects only occupied slots; fails if an empty slot is enabled or selected. */
        [Test]
        public void LoadGameOffersOnlyOccupiedSlots()
        {
            controls.Occupied.Add(2);
            picker.Show(false);
            Assert.That(picker.SlotButtons.Select(button => button.interactable), Is.EqualTo(new[] { false, true, false }));
            picker.SlotButtons[0].onClick.Invoke();
            Assert.That(controls.Calls, Is.Empty);
            picker.SlotButtons[1].onClick.Invoke();
            Assert.That(controls.Calls, Is.EqualTo(new[] { "select 2" }));
        }

        /** Checks recovery confirms before using an occupied slot and recovers into an empty slot directly; fails if a slot is taken unconfirmed or an empty slot asks. */
        [Test]
        public void RecoveryConfirmsOnlyBeforeUsingAnOccupiedSlot()
        {
            controls.Occupied.Add(1);
            var preserved = Guid.NewGuid();
            controls.Recoverable.Add(preserved);
            ChooseRecoveryCandidate(0);
            Assert.That(picker.Mode, Is.EqualTo(SpiritSaveSlotPicker.PickerMode.Recover));
            picker.SlotButtons[0].onClick.Invoke();
            Assert.That(picker.IsConfirmingReplacement, Is.True);
            Assert.That(controls.Calls, Is.Empty);
            picker.ConfirmReplaceSaveButton.onClick.Invoke();
            Assert.That(controls.Calls, Is.EqualTo(new[] { "recover " + preserved + " 1" }));

            ChooseRecoveryCandidate(0);
            picker.SlotButtons[1].onClick.Invoke();
            Assert.That(picker.IsConfirmingReplacement, Is.False);
            Assert.That(controls.Calls, Is.EqualTo(new[] { "recover " + preserved + " 1", "recover " + preserved + " 2" }));
        }

        /** Checks recovery paging reaches a campaign beyond the first page; fails if later campaigns cannot be chosen. */
        [Test]
        public void RecoveryPagingReachesEveryPreservedCampaign()
        {
            for (int index = 1; index <= picker.RecoveryPicker.CandidateButtons.Count + 1; ++index)
                controls.Recoverable.Add(Guid.NewGuid());
            Guid last = controls.Recoverable.Last();
            picker.Show(false);
            picker.RecoverSavesButton.onClick.Invoke();
            var recovery = picker.RecoveryPicker;
            Assert.That(recovery.PreviousPageButton.interactable, Is.False);
            Assert.That(recovery.NextPageButton.interactable, Is.True);

            recovery.NextPageButton.onClick.Invoke();
            Assert.That(recovery.NextPageButton.interactable, Is.False);
            Assert.That(recovery.CandidateButtons.Count(button => button.gameObject.activeSelf), Is.EqualTo(1));
            recovery.CandidateButtons[0].onClick.Invoke();
            picker.SlotButtons[0].onClick.Invoke();
            Assert.That(controls.Calls, Is.EqualTo(new[] { "recover " + last + " 1" }));
        }

        // Opens Load Game, then Recover saves, then chooses a candidate on the first page.
        private void ChooseRecoveryCandidate(int candidate)
        {
            picker.Show(false);
            picker.RecoverSavesButton.onClick.Invoke();
            Assert.That(picker.RecoveryPicker.IsOpen, Is.True);
            picker.RecoveryPicker.CandidateButtons[candidate].onClick.Invoke();
        }

        private SpiritMainMenu BuildMenu(Transform parent, SpiritSaveSlotPicker slotPicker)
        {
            var menuRoot = ui.Rect(parent, "MainMenu");
            var result = menuRoot.gameObject.AddComponent<SpiritMainMenu>();
            SyntheticUi.Set(result, "<Root>k__BackingField", menuRoot);
            SyntheticUi.Set(result, "<Title>k__BackingField", ui.Label(menuRoot, "Title"));
            SyntheticUi.Set(result, "<Status>k__BackingField", ui.Label(menuRoot, "Status"));
            SyntheticUi.Set(result, "<ContinueButton>k__BackingField", ui.LabeledButton(menuRoot, "Continue"));
            SyntheticUi.Set(result, "<ChapterButton>k__BackingField", ui.LabeledButton(menuRoot, "Chapter"));
            SyntheticUi.Set(result, "<NewGameButton>k__BackingField", ui.LabeledButton(menuRoot, "NewGame"));
            SyntheticUi.Set(result, "<OptionsButton>k__BackingField", ui.LabeledButton(menuRoot, "Options"));
            SyntheticUi.Set(result, "<SlotSelectorButton>k__BackingField", ui.LabeledButton(menuRoot, "LoadGame"));
            SyntheticUi.Set(result, "<SlotPicker>k__BackingField", slotPicker);
            return result;
        }

        private SpiritSaveSlotPicker BuildPicker(Transform parent)
        {
            var pickerRoot = ui.Rect(parent, "SlotPicker");
            var recoveryRoot = ui.Rect(pickerRoot, "RecoveryPicker");
            var recovery = recoveryRoot.gameObject.AddComponent<SpiritCampaignRecoveryPicker>();
            SyntheticUi.Set(recovery, "<Root>k__BackingField", recoveryRoot);
            SyntheticUi.Set(recovery, "<CloseButton>k__BackingField", ui.LabeledButton(recoveryRoot, "Close"));
            SyntheticUi.Set(recovery, "<PreviousPageButton>k__BackingField", ui.LabeledButton(recoveryRoot, "Previous"));
            SyntheticUi.Set(recovery, "<NextPageButton>k__BackingField", ui.LabeledButton(recoveryRoot, "Next"));
            SyntheticUi.Set(recovery, "<PageLabel>k__BackingField", ui.Label(recoveryRoot, "Page"));
            SyntheticUi.Set(recovery, "candidateButtons", Enumerable.Range(0, 3).Select(index => ui.LabeledButton(recoveryRoot, "Candidate" + index)).ToArray());
            SyntheticUi.Set(recovery, "summaries", Labels(recoveryRoot, "Summary"));
            SyntheticUi.Set(recovery, "details", Labels(recoveryRoot, "Detail"));
            SyntheticUi.Set(recovery, "identifiers", Labels(recoveryRoot, "Identifier"));

            var confirmation = ui.Rect(pickerRoot, "Confirmation");
            var result = pickerRoot.gameObject.AddComponent<SpiritSaveSlotPicker>();
            SyntheticUi.Set(result, "<Root>k__BackingField", pickerRoot);
            SyntheticUi.Set(result, "<Title>k__BackingField", ui.Label(pickerRoot, "Title"));
            SyntheticUi.Set(result, "<Subtitle>k__BackingField", ui.Label(pickerRoot, "Subtitle"));
            SyntheticUi.Set(result, "<CloseButton>k__BackingField", ui.LabeledButton(pickerRoot, "Close"));
            SyntheticUi.Set(result, "<RecoverSavesButton>k__BackingField", ui.LabeledButton(pickerRoot, "RecoverSaves"));
            SyntheticUi.Set(result, "<RecoveryPicker>k__BackingField", recovery);
            SyntheticUi.Set(result, "<Confirmation>k__BackingField", confirmation);
            SyntheticUi.Set(result, "<ConfirmationTitle>k__BackingField", ui.Label(confirmation, "Title"));
            SyntheticUi.Set(result, "<ConfirmationText>k__BackingField", ui.Label(confirmation, "Text"));
            SyntheticUi.Set(result, "<ConfirmReplaceSaveButton>k__BackingField", ui.LabeledButton(confirmation, "Confirm"));
            SyntheticUi.Set(result, "<CancelReplaceSaveButton>k__BackingField", ui.LabeledButton(confirmation, "Cancel"));
            SyntheticUi.Set(result, "slotButtons", Enumerable.Range(0, 3).Select(index => ui.LabeledButton(pickerRoot, "Slot" + index)).ToArray());
            SyntheticUi.Set(result, "slotSummaries", Labels(pickerRoot, "SlotSummary"));
            return result;
        }

        private Text[] Labels(Transform parent, string name)
            => Enumerable.Range(0, 3).Select(index => ui.Label(parent, name + index)).ToArray();

        /** Menu controls that offer every action and record what the menu asks of them. */
        private sealed class MenuControls : ISpiritMainMenuControls
        {
            public readonly HashSet<int> Occupied = new HashSet<int>();
            public readonly List<Guid> Recoverable = new List<Guid>();
            public readonly List<string> Calls = new List<string>();
            public bool CanContinueGame { get; set; }
            public bool CanOpenSavedChapter { get; set; }
            public bool CanStartNewGame => true;
            public bool CanSelectSaveSlot => true;
            public int ActiveSaveSlot => 1;
            public IReadOnlyList<SpiritSaveSlotInfo> SaveSlots => Enumerable.Range(1, 3)
                .Select(slot => new SpiritSaveSlotInfo(slot, Occupied.Contains(slot), true, 0, 0)).ToArray();
            public IReadOnlyList<SpiritRecoveryCampaignInfo> RecoveryCampaigns => Recoverable
                .Select(id => new SpiritRecoveryCampaignInfo(id, 0, 0, default, 0)).ToArray();

            public void ContinueGame() => Calls.Add("continue");
            public void OpenSavedChapter() => Calls.Add("chapter");
            public void SelectSaveSlot(int slot) => Calls.Add("select " + slot);
            public void NewGame(int slot) => Calls.Add("new game " + slot);
            public void RecoverCampaign(Guid campaignId, int slot) => Calls.Add("recover " + campaignId + " " + slot);
            public void ShowMainMenu() => Calls.Add("main menu");
        }
    }
}
