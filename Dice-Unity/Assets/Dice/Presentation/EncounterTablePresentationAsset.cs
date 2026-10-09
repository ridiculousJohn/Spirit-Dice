using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dice.Presentation
{
    /** Room, host, rules and sound belong to the selected encounter's authored table. */
    [CreateAssetMenu(menuName = "Dice/Presentation/Encounter Table")]
    public sealed class EncounterTablePresentationAsset : ScriptableObject
    {
        [SerializeField] private string displayTitle;
        [SerializeField] private Texture2D roomTexture;
        [SerializeField] private SpiritHostPresentationAsset hostPresentation;
        [SerializeField] private SpiritAudioTheme audioTheme;
        [SerializeField] private EncounterRulePresentationAsset[] rulePresentations = Array.Empty<EncounterRulePresentationAsset>();
        [SerializeField, Range(0, 1)] private float roomTableEdge = .5323f;
        [SerializeField, Range(0, 1)] private float roomSplit = .625f;

        public string DisplayTitle => displayTitle;
        public Texture2D RoomTexture => roomTexture;
        public SpiritHostPresentationAsset HostPresentation => hostPresentation;
        public SpiritAudioTheme AudioTheme => audioTheme;
        public IReadOnlyList<EncounterRulePresentationAsset> RulePresentations => rulePresentations ?? Array.Empty<EncounterRulePresentationAsset>();
        public float RoomTableEdge => roomTableEdge;
        public float RoomSplit => roomSplit;
    }
}
