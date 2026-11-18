using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /** Binds the authored front-end screens to the retained campaign state. */
    public sealed class SpiritFrontendView : MonoBehaviour
    {
        [field: SerializeField] public RectTransform Portrait { get; private set; }
        [field: SerializeField] public SpiritMainMenu MainMenu { get; private set; }
        [field: SerializeField] public SpiritInvestigationBoard Board { get; private set; }
        [field: SerializeField] public SpiritCollectionView Collection { get; private set; }
        [field: SerializeField] public SpiritBoardRewardsPanel BoardRewards { get; private set; }
        private ISpiritEncounterControls encounter;
        private IEncounterJourneyControls journey;
        // Whether the board showed at the last refresh, so arriving at it is told apart from refreshing it.
        private bool showingBoard;

        public void Bind(ISpiritEncounterControls encounterControls, IEncounterJourneyControls journeyControls,
            ISpiritMainMenuControls menuControls)
        {
            if (menuControls == null) throw new ArgumentNullException(nameof(menuControls));
            Unbind();
            encounter = encounterControls;
            journey = journeyControls;
            MainMenu.Build();
            MainMenu.Bind(menuControls);
            Board.Build(journey.ResumeEncounter, ShowCollection);
            Board.BindMainMenu(menuControls);
            Collection.Build(Collection.Hide);
            if (BoardRewards == null) throw new InvalidOperationException("The front end requires its authored board rewards panel.");
            BoardRewards.Build();
            showingBoard = false;
            encounter.StateChanged += Refresh;
            journey.JourneyChanged += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            bool menu = journey.JourneyPhase == EncounterJourneyPhase.MainMenu;
            bool board = journey.JourneyPhase == EncounterJourneyPhase.Investigation;
            Board.Root.gameObject.SetActive(board);
            if (menu) MainMenu.Render(journey.JourneyStatus);
            else MainMenu.Hide();
            if (board) Board.Render(journey, encounter, false, true);
            if (!board) Collection.Hide();
            bool arrived = board && !showingBoard;
            showingBoard = board;
            if (arrived) BoardRewards.Arrive(journey, () => ItemDetailSources.Of(encounter));
            else if (!board) BoardRewards.Hide();
        }

        public void ShowCollection()
        {
            var progression = encounter as ISpiritProgressionControls;
            Collection.Show(encounter.Catalog, encounter.ModifierPresentation, progression?.ProfileUnlockedModifierIds ?? Array.Empty<ModifierDefinition.Id>(),
                journey.JourneyCatalog, progression?.ProfileDiscoveredFamilyIds ?? Array.Empty<ModifierFamilyDefinition.Id>());
        }

        private void OnDestroy() => Unbind();
        private void Unbind()
        {
            if (encounter != null) encounter.StateChanged -= Refresh;
            if (journey != null) journey.JourneyChanged -= Refresh;
        }
    }
}
