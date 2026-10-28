using System;
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
        [SerializeField] private ulong initialSeed = EncounterApplication.DefaultSeed;

        public MechanicalCatalogAsset CatalogAsset => catalogAsset;
        public AssetReference FrontendScene => frontendScene;
        public EncounterJourneyCatalogAsset JourneyCatalog => journeyCatalog;
        public ModifierSkinAsset ModifierSkin => modifierSkin;
        public string EncounterId => encounterId;
        public bool UseFixedSeed => useFixedSeed;
        public ulong InitialSeed => initialSeed;

        public void Validate()
        {
            if (catalogAsset == null) throw new InvalidOperationException("A mechanical catalog is required.");
            if (modifierSkin == null) throw new InvalidOperationException("A modifier skin is required.");
            if (journeyCatalog == null && string.IsNullOrWhiteSpace(encounterId))
                throw new InvalidOperationException("A direct encounter ID is required when no journey catalog is configured.");
        }
    }
}
