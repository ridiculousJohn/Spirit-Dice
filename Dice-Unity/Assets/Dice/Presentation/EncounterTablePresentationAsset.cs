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
        [SerializeField] private Vector3 hostLightingPosition = new Vector3(0, 1, 2);
        [SerializeField] private SpiritHostPresentationAsset hostPresentation;
        [SerializeField] private SpiritAudioTheme audioTheme;
        [SerializeField] private SpiritTrayPresentationAsset trayPresentation;
        [SerializeField] private EncounterRulePresentationAsset[] rulePresentations = Array.Empty<EncounterRulePresentationAsset>();
        [SerializeField, Range(0, 1)] private float roomTableEdge = .5323f;
        [SerializeField, Range(0, 1)] private float roomSplit = .625f;
        [SerializeField, Range(0, 2)] private float openingFadeDuration;

        public string DisplayTitle => displayTitle;
        public AssetReference Scene => scene;
        public Texture2D RoomTexture => roomTexture;
        public SceneLightingAsset SceneLighting => sceneLighting;
        public IReadOnlyList<EncounterTablePieceLight> PieceLights => pieceLights ?? Array.Empty<EncounterTablePieceLight>();
        public Vector3 HostLightingPosition => hostLightingPosition;
        public SpiritHostPresentationAsset HostPresentation => hostPresentation;
        public SpiritAudioTheme AudioTheme => audioTheme;
        public SpiritTrayPresentationAsset TrayPresentation => trayPresentation;
        public IReadOnlyList<EncounterRulePresentationAsset> RulePresentations => rulePresentations ?? Array.Empty<EncounterRulePresentationAsset>();
        public float RoomTableEdge => roomTableEdge;
        public float RoomSplit => roomSplit;
        public float OpeningFadeDuration => openingFadeDuration;
    }
}
