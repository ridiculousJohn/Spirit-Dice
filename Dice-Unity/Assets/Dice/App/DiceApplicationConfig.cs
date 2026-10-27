using System;
using System.Globalization;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Dice.App
{
    [CreateAssetMenu(menuName = "Dice/Application Configuration")]
    public sealed class DiceApplicationConfig : ScriptableObject
    {
        [SerializeField] private MechanicalCatalogAsset catalogAsset;
        [SerializeField] private AssetReference frontendScene;
        [SerializeField] private EncounterJourneyCatalogAsset journeyCatalog;
        [SerializeField] private ModifierSkinAsset modifierSkin;
        [SerializeField] private string encounterId;
        [SerializeField, Tooltip("Use Initial Seed for repeatable debug runs. Otherwise every new run uses a fresh seed.")]
        private bool useFixedSeed;
        [SerializeField] private string initialSeed = "60";

        public MechanicalCatalogAsset CatalogAsset => catalogAsset;
        public AssetReference FrontendScene => frontendScene;
        public EncounterJourneyCatalogAsset JourneyCatalog => journeyCatalog;
        public ModifierSkinAsset ModifierSkin => modifierSkin;
        public string EncounterId => encounterId;
        public bool UseFixedSeed => useFixedSeed;
        public string InitialSeed => initialSeed;

        public void Validate()
        {
            if (catalogAsset == null) throw new InvalidOperationException("A mechanical catalog is required.");
            if (modifierSkin == null) throw new InvalidOperationException("A modifier skin is required.");
            if (journeyCatalog == null && string.IsNullOrWhiteSpace(encounterId))
                throw new InvalidOperationException("A direct encounter ID is required when no journey catalog is configured.");
            if (useFixedSeed && !ulong.TryParse(initialSeed, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                throw new InvalidOperationException("Initial Seed must be an unsigned 64-bit integer.");
        }
    }
}
