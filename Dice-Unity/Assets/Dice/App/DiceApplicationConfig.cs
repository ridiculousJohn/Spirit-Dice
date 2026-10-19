using System;
using System.Globalization;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEngine;

namespace Dice.App
{
    [CreateAssetMenu(menuName = "Dice/Application Configuration")]
    public sealed class DiceApplicationConfig : ScriptableObject
    {
        [SerializeField] private MechanicalCatalogAsset catalogAsset;
        [SerializeField] private EncounterJourneyCatalogAsset journeyCatalog;
        [SerializeField] private string encounterId;
        [SerializeField] private string modifierLabel = ModifierAssetLoader.Label;
        [SerializeField] private string modifierPresentationLabel = ModifierPresentationLoader.Label;
        [SerializeField, Tooltip("Use Initial Seed for repeatable debug runs. Otherwise every new run uses a fresh seed.")]
        private bool useFixedSeed;
        [SerializeField] private string initialSeed = "60";

        public MechanicalCatalogAsset CatalogAsset => catalogAsset;
        public EncounterJourneyCatalogAsset JourneyCatalog => journeyCatalog;
        public string EncounterId => encounterId;
        public string ModifierLabel => modifierLabel;
        public string ModifierPresentationLabel => modifierPresentationLabel;
        public bool UseFixedSeed => useFixedSeed;
        public string InitialSeed => initialSeed;

        public void Validate()
        {
            if (catalogAsset == null) throw new InvalidOperationException("A mechanical catalog is required.");
            if (string.IsNullOrWhiteSpace(modifierLabel)) throw new InvalidOperationException("A modifier label is required.");
            if (string.IsNullOrWhiteSpace(modifierPresentationLabel)) throw new InvalidOperationException("A theme label is required.");
            if (journeyCatalog == null && string.IsNullOrWhiteSpace(encounterId))
                throw new InvalidOperationException("A direct encounter ID is required when no journey catalog is configured.");
            if (useFixedSeed && !ulong.TryParse(initialSeed, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                throw new InvalidOperationException("Initial Seed must be an unsigned 64-bit integer.");
        }
    }
}
