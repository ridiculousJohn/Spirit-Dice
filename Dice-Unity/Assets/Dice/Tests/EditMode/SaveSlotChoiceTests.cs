using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    /** Main menu and save slot picker choices, bound to test-only menu controls on a minimal test hierarchy. */
    public sealed class SaveSlotChoiceTests
    {
        private GameObject root;
        private SpiritUiTheme theme;
        private SpiritUiWording wording;
        private MenuControls controls;
        private SpiritMainMenu menu;
        private SpiritSaveSlotPicker picker;

        [SetUp]
        public void SetUp()
        {
            theme = ScriptableObject.CreateInstance<SpiritUiTheme>();
            wording = SyntheticWording.Create();
            root = new GameObject("Save slot choice test", typeof(RectTransform), typeof(SpiritUiContext));
            root.GetComponent<SpiritUiContext>().Configure(theme, wording);
            controls = new MenuControls();
            picker = BuildPicker(root.transform);
            menu = BuildMenu(root.transform, picker);
            menu.Build((RectTransform)root.transform);
            menu.Bind(controls);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(theme);
            Object.DestroyImmediate(wording);
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
            controls.Recoverable.Add("campaign.preserved");
            ChooseRecoveryCandidate(0);
            Assert.That(picker.Mode, Is.EqualTo(SpiritSaveSlotPicker.PickerMode.Recover));
            picker.SlotButtons[0].onClick.Invoke();
            Assert.That(picker.IsConfirmingReplacement, Is.True);
            Assert.That(controls.Calls, Is.Empty);
            picker.ConfirmReplaceSaveButton.onClick.Invoke();
            Assert.That(controls.Calls, Is.EqualTo(new[] { "recover campaign.preserved 1" }));

            ChooseRecoveryCandidate(0);
            picker.SlotButtons[1].onClick.Invoke();
            Assert.That(picker.IsConfirmingReplacement, Is.False);
            Assert.That(controls.Calls, Is.EqualTo(new[] { "recover campaign.preserved 1", "recover campaign.preserved 2" }));
        }

        /** Checks recovery paging reaches a campaign beyond the first page; fails if later campaigns cannot be chosen. */
        [Test]
        public void RecoveryPagingReachesEveryPreservedCampaign()
        {
            for (int index = 1; index <= picker.RecoveryPicker.CandidateButtons.Count + 1; ++index)
                controls.Recoverable.Add("campaign." + index);
            string last = controls.Recoverable.Last();
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

        private static SpiritMainMenu BuildMenu(Transform parent, SpiritSaveSlotPicker slotPicker)
        {
            var menuRoot = Rect(parent, "MainMenu");
            var result = menuRoot.gameObject.AddComponent<SpiritMainMenu>();
            SetProperty(result, "Root", menuRoot);
            SetProperty(result, "Title", Label(menuRoot, "Title"));
            SetProperty(result, "Status", Label(menuRoot, "Status"));
            SetProperty(result, "ContinueButton", NewButton(menuRoot, "Continue"));
            SetProperty(result, "ChapterButton", NewButton(menuRoot, "Chapter"));
            SetProperty(result, "NewGameButton", NewButton(menuRoot, "NewGame"));
            SetProperty(result, "OptionsButton", NewButton(menuRoot, "Options"));
            SetProperty(result, "SlotSelectorButton", NewButton(menuRoot, "LoadGame"));
            SetProperty(result, "SlotPicker", slotPicker);
            return result;
        }

        private static SpiritSaveSlotPicker BuildPicker(Transform parent)
        {
            var pickerRoot = Rect(parent, "SlotPicker");
            var recoveryRoot = Rect(pickerRoot, "RecoveryPicker");
            var recovery = recoveryRoot.gameObject.AddComponent<SpiritCampaignRecoveryPicker>();
            SetProperty(recovery, "Root", recoveryRoot);
            SetProperty(recovery, "CloseButton", NewButton(recoveryRoot, "Close"));
            SetProperty(recovery, "PreviousPageButton", NewButton(recoveryRoot, "Previous"));
            SetProperty(recovery, "NextPageButton", NewButton(recoveryRoot, "Next"));
            SetProperty(recovery, "PageLabel", Label(recoveryRoot, "Page"));
            Set(recovery, "candidateButtons", Enumerable.Range(0, 3).Select(index => NewButton(recoveryRoot, "Candidate" + index)).ToArray());
            Set(recovery, "summaries", Labels(recoveryRoot, "Summary"));
            Set(recovery, "details", Labels(recoveryRoot, "Detail"));
            Set(recovery, "identifiers", Labels(recoveryRoot, "Identifier"));

            var confirmation = Rect(pickerRoot, "Confirmation");
            var result = pickerRoot.gameObject.AddComponent<SpiritSaveSlotPicker>();
            SetProperty(result, "Root", pickerRoot);
            SetProperty(result, "Title", Label(pickerRoot, "Title"));
            SetProperty(result, "Subtitle", Label(pickerRoot, "Subtitle"));
            SetProperty(result, "CloseButton", NewButton(pickerRoot, "Close"));
            SetProperty(result, "RecoverSavesButton", NewButton(pickerRoot, "RecoverSaves"));
            SetProperty(result, "RecoveryPicker", recovery);
            SetProperty(result, "Confirmation", confirmation);
            SetProperty(result, "ConfirmationTitle", Label(confirmation, "Title"));
            SetProperty(result, "ConfirmationText", Label(confirmation, "Text"));
            SetProperty(result, "ConfirmReplaceSaveButton", NewButton(confirmation, "Confirm"));
            SetProperty(result, "CancelReplaceSaveButton", NewButton(confirmation, "Cancel"));
            Set(result, "slotButtons", Enumerable.Range(0, 3).Select(index => NewButton(pickerRoot, "Slot" + index)).ToArray());
            Set(result, "slotSummaries", Labels(pickerRoot, "SlotSummary"));
            return result;
        }

        private static RectTransform Rect(Transform parent, string name)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            return (RectTransform)item.transform;
        }

        private static Text Label(Transform parent, string name) => Rect(parent, name).gameObject.AddComponent<Text>();

        private static Text[] Labels(Transform parent, string name)
            => Enumerable.Range(0, 3).Select(index => Label(parent, name + index)).ToArray();

        private static Button NewButton(Transform parent, string name)
        {
            var button = Rect(parent, name).gameObject.AddComponent<Button>();
            Label(button.transform, "Label");
            return button;
        }

        private static void SetProperty(object target, string name, object value)
            => target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value });

        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        /** Menu controls that offer every action and record what the menu asks of them. */
        private sealed class MenuControls : ISpiritMainMenuControls
        {
            public readonly HashSet<int> Occupied = new HashSet<int>();
            public readonly List<string> Recoverable = new List<string>();
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
            public void RecoverCampaign(string campaignId, int slot) => Calls.Add("recover " + campaignId + " " + slot);
            public void ShowMainMenu() => Calls.Add("main menu");
        }
    }
}
