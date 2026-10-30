using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Presents application navigation for the selected campaign save slot. */
    public sealed class SpiritMainMenu : MonoBehaviour
    {
        private ISpiritMainMenuControls controls;

        [field: SerializeField] public RectTransform Root { get; private set; }
        [field: SerializeField] public Text Title { get; private set; }
        [field: SerializeField] public Text Status { get; private set; }
        [field: SerializeField] public Button ContinueButton { get; private set; }
        [field: SerializeField] public Button ChapterButton { get; private set; }
        [field: SerializeField] public Button NewGameButton { get; private set; }
        [field: SerializeField] public Button OptionsButton { get; private set; }
        [field: SerializeField] public Button SlotSelectorButton { get; private set; }
        [field: SerializeField] public SpiritSaveSlotPicker SlotPicker { get; private set; }

        public void Build()
        {
            if (Root == null || Title == null || Status == null || ContinueButton == null ||
                ChapterButton == null || NewGameButton == null || OptionsButton == null ||
                SlotSelectorButton == null || SlotPicker == null)
                throw new InvalidOperationException("The main menu requires authored UI references.");
            SpiritUi.RequireWording(transform);
            SlotPicker.Build();
            ContinueButton.onClick.RemoveListener(ContinueGame);
            ContinueButton.onClick.AddListener(ContinueGame);
            SlotSelectorButton.onClick.RemoveListener(SelectSaveSlot);
            SlotSelectorButton.onClick.AddListener(SelectSaveSlot);
            ChapterButton.onClick.RemoveListener(OpenChapter);
            ChapterButton.onClick.AddListener(OpenChapter);
            NewGameButton.onClick.RemoveListener(StartNewGame);
            NewGameButton.onClick.AddListener(StartNewGame);
            Hide();
        }

        public void Bind(ISpiritMainMenuControls menuControls)
        {
            SlotPicker.Bind(menuControls);
            controls = menuControls;
        }

        public void Render(SpiritStatus status)
        {
            Root.gameObject.SetActive(true);
            ContinueButton.interactable = controls != null && controls.CanContinueGame;
            ChapterButton.interactable = controls != null && controls.CanOpenSavedChapter;
            NewGameButton.interactable = controls != null && controls.CanStartNewGame;
            SlotSelectorButton.interactable = controls != null && controls.CanSelectSaveSlot;
            OptionsButton.interactable = controls != null;
            Status.text = SpiritStatusText.Describe(status, SpiritUi.GetWording(transform).status);
            SlotPicker.Refresh();
        }

        public void Hide()
        {
            SlotPicker.Close();
            Root.gameObject.SetActive(false);
        }

        private void ContinueGame() => controls?.ContinueGame();
        private void SelectSaveSlot() => SlotPicker.Show(false);
        private void OpenChapter() => controls?.OpenSavedChapter();
        private void StartNewGame() => SlotPicker.Show(true);

        private void OnDisable()
        {
            SlotPicker.Close();
        }
    }
}
