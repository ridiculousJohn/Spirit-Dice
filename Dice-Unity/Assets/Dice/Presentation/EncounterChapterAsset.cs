using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dice.Presentation
{
    [CreateAssetMenu(menuName = "Dice/Encounter Chapter")]
    public sealed class EncounterChapterAsset : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Texture2D artwork;
        [SerializeField] private EncounterJourneyAsset[] entries = Array.Empty<EncounterJourneyAsset>();

        public string Id => id;
        public string Title => title;
        public string Description => description;
        public Texture2D Artwork => artwork;
        public IReadOnlyList<EncounterJourneyAsset> Entries => entries;
    }
}
