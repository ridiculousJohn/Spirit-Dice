using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Presents application navigation for the selected campaign save slot. */
    public sealed class SpiritMainMenu : MonoBehaviour
    {
        private ISpiritMainMenuControls controls;

        public RectTransform Root { get; private set; }
        public Text Title { get; private set; }
        public Text Status { get; private set; }
        public Button ContinueButton { get; private set; }
        public Button ChapterButton { get; private set; }
        public Button NewGameButton { get; private set; }
        public Button OptionsButton { get; private set; }
        public Button SlotSelectorButton { get; private set; }
        public SpiritSaveSlotPicker SlotPicker { get; private set; }

        public void Build(RectTransform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (Root != null) throw new InvalidOperationException("The main menu has already been built.");

            var theme = SpiritUi.GetTheme(transform);
            Root = SpiritUi.Panel("MainMenu", parent, Vector2.zero, Vector2.one,
                theme.panelColor).rectTransform;
            Title = SpiritUi.Heading("MainMenuTitle", Root, "Spirit Dice", 48,
                new Vector2(.06f, .76f), new Vector2(.94f, .89f), TextAnchor.MiddleCenter, theme.amber);
            ContinueButton = SpiritUi.Button("ContinueGameButton", Root, "Continue",
                new Vector2(.16f, .65f), new Vector2(.84f, .73f), () => controls?.ContinueGame(), compact: true);
            SlotSelectorButton = SpiritUi.Button("MainMenuSaveSlotButton", Root, "Load Game",
                new Vector2(.16f, .55f), new Vector2(.84f, .63f), () => SlotPicker.Show(false), compact: true);
            ChapterButton = SpiritUi.Button("OpenSavedChapterButton", Root, "Chapter",
                new Vector2(.16f, .45f), new Vector2(.84f, .53f), () => controls?.OpenSavedChapter(), compact: true);
            NewGameButton = SpiritUi.Button("NewGameButton", Root, "New Game",
                new Vector2(.16f, .35f), new Vector2(.84f, .43f), () => SlotPicker.Show(true), compact: true);
            OptionsButton = SpiritUi.Button("MainMenuOptionsButton", Root, "Options",
                new Vector2(.16f, .25f), new Vector2(.84f, .33f), () => { }, compact: true);
            Status = SpiritUi.Label("MainMenuStatus", Root, "", 17,
                new Vector2(.08f, .12f), new Vector2(.92f, .22f), TextAnchor.MiddleCenter, theme.muted);

            SlotPicker = gameObject.AddComponent<SpiritSaveSlotPicker>();
            SlotPicker.Build(Root);
            Hide();
        }

        public void Bind(ISpiritMainMenuControls menuControls)
        {
            SlotPicker.Bind(menuControls);
            controls = menuControls;
        }

        public void Render(string status)
        {
            Root.gameObject.SetActive(true);
            ContinueButton.interactable = controls != null && controls.CanContinueGame;
            ChapterButton.interactable = controls != null && controls.CanOpenSavedChapter;
            NewGameButton.interactable = controls != null && controls.CanStartNewGame;
            SlotSelectorButton.interactable = controls != null && controls.CanSelectSaveSlot;
            OptionsButton.interactable = controls != null;
            Status.text = status ?? "";
            SlotPicker.Refresh();
        }

        public void Hide()
        {
            SlotPicker?.Close();
            if (Root != null) Root.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            SlotPicker?.Close();
        }
    }
}
