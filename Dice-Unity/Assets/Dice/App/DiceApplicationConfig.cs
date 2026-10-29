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
        [SerializeField, Tooltip("The encounter a configuration without a journey catalog plays directly.")]
        private EncounterAsset encounter;
        [SerializeField, Tooltip("Use Initial Seed for repeatable debug runs. Otherwise every new run uses a fresh seed.")]
        private bool useFixedSeed;
        [SerializeField] private ulong initialSeed = EncounterApplication.DefaultSeed;

        public MechanicalCatalogAsset CatalogAsset => catalogAsset;
        public AssetReference FrontendScene => frontendScene;
        public EncounterJourneyCatalogAsset JourneyCatalog => journeyCatalog;
        public ModifierSkinAsset ModifierSkin => modifierSkin;
        public EncounterAsset Encounter => encounter;
        public bool UseFixedSeed => useFixedSeed;
        public ulong InitialSeed => initialSeed;

        public void Validate()
        {
            if (catalogAsset == null) throw new InvalidOperationException("A mechanical catalog is required.");
            if (modifierSkin == null) throw new InvalidOperationException("A modifier skin is required.");
            if (journeyCatalog == null && encounter == null)
                throw new InvalidOperationException("A direct encounter is required when no journey catalog is configured.");
        }
    }
}
