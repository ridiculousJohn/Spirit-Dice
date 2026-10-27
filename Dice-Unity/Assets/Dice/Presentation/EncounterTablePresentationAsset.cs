using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Dice.Presentation
{
    /** Moves one scene light with a positioned table piece's prop, like SceneLightingSourceBinding but keyed by engine IDs. */
    [Serializable]
    public sealed class EncounterTablePieceLight
    {
        [Tooltip("Index into the scene lighting asset's lights.")]
        [Min(0)] public int LightIndex;
        [Tooltip("Table piece ID whose authored prop carries the light.")]
        public string PieceId;
        [Tooltip("Interaction ID identifying the prop together with the piece ID.")]
        public string InteractionId;
        [Tooltip("Prop pivot in normalized bottom-left room-texture coordinates at the light's authored position.")]
        public Vector2 ReferencePosition;
        [Tooltip("Receiver-space displacement produced by one horizontal normalized-texture unit.")]
        public Vector3 ReceiverOffsetX;
        [Tooltip("Receiver-space displacement produced by one vertical normalized-texture unit.")]
        public Vector3 ReceiverOffsetY;
    }

    /** Room, host, rules and sound belong to the selected encounter's authored table. */
    [CreateAssetMenu(menuName = "Dice/Presentation/Encounter Table")]
    public sealed class EncounterTablePresentationAsset : ScriptableObject
    {
        [SerializeField] private string displayTitle;
        [SerializeField] private AssetReference scene;
        [SerializeField] private Texture2D roomTexture;
        [SerializeField] private SceneLightingAsset sceneLighting;
        [Tooltip("Scene lights that follow table piece props; lights without an entry keep their authored positions.")]
        [SerializeField] private EncounterTablePieceLight[] pieceLights = Array.Empty<EncounterTablePieceLight>();
        [Tooltip("Resting multiplier of each scene light's authored intensity while the encounter is played, such as 0 for a flare " +
            "light that only rule reactions raise; a light without an entry rests at 1.")]
        [SerializeField] private float[] restingLightLevels = Array.Empty<float>();
        [SerializeField] private Vector3 hostLightingPosition = new Vector3(0, 1, 2);
        [SerializeField] private SpiritHostPresentationAsset hostPresentation;
        [SerializeField] private SpiritAudioTheme audioTheme;
        [SerializeField] private SpiritTrayPresentationAsset trayPresentation;
        [SerializeField] private EncounterRulePresentationAsset[] rulePresentations = Array.Empty<EncounterRulePresentationAsset>();
        [Tooltip("Presentation-only light outages and prop jolts while the encounter is played; empty for a still table.")]
        [SerializeField] private EncounterAmbientActivityAsset ambientActivity;
        [SerializeField, Range(0, 1)] private float roomTableEdge = .5323f;
        [SerializeField, Range(0, 1)] private float roomSplit = .625f;
        [SerializeField, Range(0, 2)] private float openingFadeDuration;

        public string DisplayTitle => displayTitle;
        public AssetReference Scene => scene;
        public Texture2D RoomTexture => roomTexture;
        public SceneLightingAsset SceneLighting => sceneLighting;
        public IReadOnlyList<EncounterTablePieceLight> PieceLights => pieceLights ?? Array.Empty<EncounterTablePieceLight>();
        public IReadOnlyList<float> RestingLightLevels => restingLightLevels ?? Array.Empty<float>();
        public Vector3 HostLightingPosition => hostLightingPosition;
        public SpiritHostPresentationAsset HostPresentation => hostPresentation;
        public SpiritAudioTheme AudioTheme => audioTheme;
        public SpiritTrayPresentationAsset TrayPresentation => trayPresentation;
        public IReadOnlyList<EncounterRulePresentationAsset> RulePresentations => rulePresentations ?? Array.Empty<EncounterRulePresentationAsset>();
        public EncounterAmbientActivityAsset AmbientActivity => ambientActivity;
        public float RoomTableEdge => roomTableEdge;
        public float RoomSplit => roomSplit;
        public float OpeningFadeDuration => openingFadeDuration;

        /** The rule card flagged as shown on the HUD, or null when no card is; more than one flagged card fails. */
        public EncounterRulePresentationAsset HudRuleCard
        {
            get
            {
                EncounterRulePresentationAsset shown = null;
                foreach (var card in RulePresentations)
                {
                    if (card == null || !card.ShownOnHud) continue;
                    if (shown != null)
                        throw new InvalidOperationException("Encounter table '" + name +
                            "' flags more than one rule card as shown on the HUD: '" + shown.name + "' and '" + card.name + "'.");
                    shown = card;
                }
                return shown;
            }
        }
    }
}
