using System;
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
        private ISpiritEncounterControls encounter;
        private IEncounterJourneyControls journey;

        public void Bind(ISpiritEncounterControls encounterControls, IEncounterJourneyControls journeyControls,
            ISpiritMainMenuControls menuControls)
        {
            Unbind();
            encounter = encounterControls;
            journey = journeyControls;
            MainMenu.Build(Portrait);
            MainMenu.Bind(menuControls);
            Board.Build(Portrait, journey.ResumeEncounter, encounter.Load, ShowCollection);
            Board.BindMainMenu(menuControls);
            Collection.Build(Portrait, Collection.Hide);
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
        }

        public void ShowCollection()
        {
            var progression = encounter as ISpiritProgressionControls;
            Collection.Show(encounter.Catalog, progression?.ProfileUnlockedModifierIds ?? Array.Empty<string>(),
                journey.JourneyCatalog, progression?.ProfileDiscoveredFamilyIds ?? Array.Empty<string>());
        }

        private void OnDestroy() => Unbind();
        private void Unbind()
        {
            if (encounter != null) encounter.StateChanged -= Refresh;
            if (journey != null) journey.JourneyChanged -= Refresh;
        }
    }
}
