using System;
using System.Collections.Generic;
using System.Linq;
using Dice.UnityContent;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /**
     * On the investigation board: the rewards of journeys the player has completed that the player still lacks, as when the
     * app closed on a won ending before its Continue, or a reward was added to a journey already finished. When the player
     * arrives at the board it asks, for every completed journey, what its rewards (every beat's and its completion reward)
     * would add; lacking items show here as item details with one Continue button that gives those rewards, and rewards
     * that lack only charms or families are given at once without the panel. It acts on arriving and on its button, never
     * polls.
     */
    public sealed class SpiritBoardRewardsPanel : MonoBehaviour
    {
        [SerializeField] private SpiritItemDetailList itemDetails;
        [SerializeField] private Button continueButton;
        private IEncounterJourneyControls controls;
        private readonly List<ProgressionRewardAsset> pending = new List<ProgressionRewardAsset>();

        public SpiritItemDetailList ItemDetails => itemDetails;
        public Button ContinueButton => continueButton;
        public bool IsOpen => gameObject.activeSelf;

        public void Build()
        {
            if (itemDetails == null || continueButton == null)
                throw new InvalidOperationException("The board rewards panel " + name + " requires its item details and Continue button.");
            continueButton.onClick.RemoveListener(Give);
            continueButton.onClick.AddListener(Give);
            SpiritUi.SetButtonLabel(continueButton, SpiritUi.GetWording(transform).journey.continueButton);
            Hide();
        }

        /**
         * The player arrived at the board: gives at once what the completed journeys' rewards lack when that holds no item,
         * and otherwise shows the lacking items until Continue gives them.
         */
        public void Arrive(IEncounterJourneyControls journeyControls, Func<ItemDetailSources> sources)
        {
            controls = journeyControls ?? throw new ArgumentNullException(nameof(journeyControls));
            pending.Clear();
            var catalog = controls.JourneyCatalog;
            if (catalog != null)
                foreach (var journey in catalog.Entries)
                    if (journey != null && controls.CompletedJourneyIds.Contains(journey.Identity)) pending.AddRange(journey.AllRewards);
            var lack = pending.Count > 0 ? controls.Lacks(pending) : RewardLack.None;
            if (!lack.Any)
            {
                Hide();
                return;
            }
            if (lack.Items.Count == 0)
            {
                // Rewards give charms and families without showing them, as continuing from a beat does.
                Give();
                return;
            }
            gameObject.SetActive(true);
            itemDetails.Show(lack.Items, sources);
        }

        public void Hide()
        {
            pending.Clear();
            gameObject.SetActive(false);
        }

        private void Give()
        {
            var rewards = pending.ToArray();
            Hide();
            if (rewards.Length > 0) controls?.GiveRewards(rewards);
        }
    }
}
