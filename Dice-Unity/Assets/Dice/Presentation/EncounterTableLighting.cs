using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /**
     * Scene lighting of one configured encounter table. Each update samples the table's lights onto the room and table
     * images and the tray, modifier history and host receivers; lights bound to table pieces follow their props. Every
     * light's multiplier of its authored intensity combines the table's resting level, the ambient activity's multiplier,
     * any rule flare and any play-mode testing override. The encounter view creates one when it configures a table, updates
     * it every frame and releases it with the table.
     */
    public sealed class EncounterTableLighting : IAmbientLightingTarget, IRuleSceneLights
    {
        private readonly EncounterTablePresentationAsset table;
        private readonly IReadOnlyList<SpiritTablePieceView> pieces;
        // The prop carrying each of the table's piece lights, in the table's order.
        private readonly SpiritTablePieceView[] pieceLightProps;
        private readonly RawImage roomImage;
        private readonly RawImage tableImage;
        private readonly SpiritTrayScene trayScene;
        private readonly SpiritDiceTray diceTray;
        private readonly SpiritModifierHistoryRenderer historyRenderer;
        private readonly SpiritHostPortrait host;
        private readonly SceneLightingState state = new SceneLightingState();
        private SceneLightingMaterial sceneLightingMaterial;
        private float[] pieceLightIntensities = Array.Empty<float>();
        private Vector2[] pieceLightUvOffsets = Array.Empty<Vector2>();
        private Vector3[] pieceLightReceivers = Array.Empty<Vector3>();
        private readonly float[] ambientLightIntensities;
        // Play-mode testing multipliers set from the encounter screen inspector; NaN leaves a light to the ambient activity.
        private readonly float[] testLightOverrides;
        private readonly float[] sampledLightIntensities;
        // Resting multipliers authored on the table; the ambient activity's multipliers scale them.
        private readonly float[] restingLightLevels;
        // Timed raises of lights that rule presenters ask for.
        private readonly SceneLightFlares lightFlares = new SceneLightFlares();

        /**
         * Lighting for the table's authored lights, shown on the split room and table images, the table's prop pieces and
         * the receivers; a missing receiver is skipped. Fails when a piece light lacks its piece or scene light, or a resting
         * level lacks its scene light or is negative.
         */
        public EncounterTableLighting(EncounterTablePresentationAsset table, IReadOnlyList<SpiritTablePieceView> pieces,
            RawImage roomImage, RawImage tableImage, SpiritTrayScene trayScene, SpiritDiceTray diceTray,
            SpiritModifierHistoryRenderer historyRenderer, SpiritHostPortrait host)
        {
            this.table = table != null ? table : throw new ArgumentNullException(nameof(table));
            this.pieces = pieces ?? throw new ArgumentNullException(nameof(pieces));
            this.roomImage = roomImage != null ? roomImage : throw new ArgumentNullException(nameof(roomImage));
            this.tableImage = tableImage != null ? tableImage : throw new ArgumentNullException(nameof(tableImage));
            this.trayScene = trayScene;
            this.diceTray = diceTray;
            this.historyRenderer = historyRenderer;
            this.host = host;
            // Piece lights move the table's scene lights, which are checked once here rather than as they are sampled.
            if (table.PieceLights.Count > 0) table.SceneLighting?.Validate();
            pieceLightProps = new SpiritTablePieceView[table.PieceLights.Count];
            for (int i = 0; i < pieceLightProps.Length; ++i)
            {
                var pieceLight = table.PieceLights[i];
                if (pieceLight == null || pieceLight.Piece == null)
                    throw new InvalidOperationException("Each table piece light of " + table.name + " requires its piece.");
                pieceLightProps[i] = SpiritTablePieceView.Find(pieces, pieceLight.Piece.Identity);
                if (pieceLightProps[i] == null || table.SceneLighting != null &&
                    (pieceLight.LightIndex < 0 || pieceLight.LightIndex >= table.SceneLighting.Lights.Length))
                    throw new InvalidOperationException("The piece light of " + pieceLight.Piece.name + " on " + table.name +
                        " requires an authored prop and scene light.");
            }
            int count = table.SceneLighting != null && table.SceneLighting.Lights != null ? table.SceneLighting.Lights.Length : 0;
            var resting = table.RestingLightLevels;
            if (resting.Count > count)
                throw new InvalidOperationException("Each resting light level requires a scene light.");
            ambientLightIntensities = new float[count];
            restingLightLevels = new float[count];
            testLightOverrides = new float[count];
            sampledLightIntensities = new float[count];
            for (int i = 0; i < count; ++i)
            {
                ambientLightIntensities[i] = 1f;
                restingLightLevels[i] = i < resting.Count ? resting[i] : 1f;
                if (!FiniteValue.Is(restingLightLevels[i]) || restingLightLevels[i] < 0f)
                    throw new InvalidOperationException("Resting light levels must be finite and nonnegative.");
            }
            lightFlares.Resize(count);
            ReleaseTestLightOverrides();
        }

        /** The table's lights as last sampled; empty before the first update and after a release. */
        public SceneLightingState State => state;

        /** Lights of the table that ambient actors, rule flares and the testing controls address. */
        public int LightCount => ambientLightIntensities.Length;

        /** The lights rule presenters may flare, or null when the table authors no scene lighting. */
        public IRuleSceneLights RuleLights => table.SceneLighting != null ? this : null;

        /** Samples the table's lights now and shows them on the images, pieces and receivers. */
        public void Update()
        {
            var asset = table.SceneLighting;
            if (asset != null && pieceLightProps.Length > 0) SamplePieceLights(asset, table.PieceLights);
            else state.Sample(asset, Time.unscaledTimeAsDouble, AmbientLightIntensities(asset));
            if (asset != null)
            {
                if (sceneLightingMaterial == null || sceneLightingMaterial.Material.shader != asset.Shader)
                {
                    sceneLightingMaterial?.Dispose();
                    sceneLightingMaterial = new SceneLightingMaterial(asset.Shader);
                }
                sceneLightingMaterial.Apply(state);
            }
            else
            {
                sceneLightingMaterial?.Dispose();
                sceneLightingMaterial = null;
            }
            roomImage.material = tableImage.material = sceneLightingMaterial?.Material;
            var shown = asset != null ? state : null;
            trayScene?.SetSceneLighting(shown);
            diceTray?.SetSceneLighting(shown);
            historyRenderer?.SetSceneLighting(shown);
            host?.SetSceneLighting(shown, table.HostLightingPosition);
        }

        /**
         * Takes the lighting off the images, pieces and receivers and frees its material; the multipliers are kept, so a
         * later update shows the lighting again.
         */
        public void Release()
        {
            foreach (var piece in pieces)
                if (piece != null) piece.RestoreEmission();
            if (roomImage != null) roomImage.material = null;
            if (tableImage != null) tableImage.material = null;
            sceneLightingMaterial?.Dispose();
            sceneLightingMaterial = null;
            state.Sample(null, Time.unscaledTimeAsDouble);
            trayScene?.SetSceneLighting(null);
            diceTray?.SetSceneLighting(null);
            historyRenderer?.SetSceneLighting(null);
            host?.SetSceneLighting(null, Vector3.zero);
        }

        /** A presentation reset ends every rule flare and returns every light to the ambient activity. */
        public void ResetPresentation()
        {
            lightFlares.Clear();
            ReleaseTestLightOverrides();
        }

        /** Whether a testing override holds the light; the multiplier is the held one, or the ambient activity's when none holds it. */
        public bool GetTestLightOverride(int index, out float multiplier)
        {
            if (index < 0 || index >= testLightOverrides.Length) throw new ArgumentOutOfRangeException(nameof(index));
            multiplier = testLightOverrides[index];
            if (!float.IsNaN(multiplier)) return true;
            multiplier = AmbientLightIntensity(index);
            return false;
        }

        /** Holds the light at this multiplier of its authored intensity, in place of the ambient activity's, until released. */
        public void SetTestLightOverride(int index, float multiplier)
        {
            if (index < 0 || index >= testLightOverrides.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (!FiniteValue.Is(multiplier) || multiplier < 0f) throw new ArgumentOutOfRangeException(nameof(multiplier));
            testLightOverrides[index] = multiplier;
        }

        /** Returns every light to the ambient activity's multiplier. */
        public void ReleaseTestLightOverrides()
        {
            for (int i = 0; i < testLightOverrides.Length; ++i) testLightOverrides[i] = float.NaN;
        }

        float IAmbientLightingTarget.GetLightIntensity(int index)
        {
            if (index < 0 || index >= ambientLightIntensities.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return ambientLightIntensities[index];
        }

        /** Multiplies the authored light intensity for the ambient activity; piece lights multiply it with their piece's level. */
        void IAmbientLightingTarget.SetLightIntensity(int index, float multiplier)
        {
            if (index < 0 || index >= ambientLightIntensities.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (!FiniteValue.Is(multiplier) || multiplier < 0f) throw new ArgumentOutOfRangeException(nameof(multiplier));
            ambientLightIntensities[index] = multiplier;
        }

        /** Raises the light above its resting and ambient level for a rule presenter; presentation resets end the flare. */
        void IRuleSceneLights.Flare(int index, float peak, float rise, float hold, float fall)
            => lightFlares.Start(index, peak, rise, hold, fall, Time.unscaledTimeAsDouble);

        /** Lights bound to table pieces follow their props; every other light keeps its authored position. */
        private void SamplePieceLights(SceneLightingAsset asset, IReadOnlyList<EncounterTablePieceLight> pieceLights)
        {
            int count = asset.Lights.Length;
            if (pieceLightIntensities.Length != count)
            {
                pieceLightIntensities = new float[count];
                pieceLightUvOffsets = new Vector2[count];
                pieceLightReceivers = new Vector3[count];
            }
            for (int i = 0; i < count; ++i)
            {
                pieceLightIntensities[i] = AmbientLightIntensity(i);
                pieceLightUvOffsets[i] = asset.Lights[i].UvOffset;
                pieceLightReceivers[i] = asset.Lights[i].ReceiverPosition;
            }
            for (int i = 0; i < pieceLightProps.Length; ++i)
            {
                var pieceLight = pieceLights[i];
                var piece = pieceLightProps[i];
                int index = pieceLight.LightIndex;
                if (!piece.gameObject.activeInHierarchy) { pieceLightIntensities[index] = 0f; continue; }
                Vector2 delta = RoomTextureCoordinate(piece.transform.position) - pieceLight.ReferencePosition;
                pieceLightUvOffsets[index] += delta;
                pieceLightReceivers[index] += pieceLight.ReceiverOffsetX * delta.x + pieceLight.ReceiverOffsetY * delta.y;
                pieceLightIntensities[index] = piece.LightIntensity * AmbientLightIntensity(index);
            }
            state.Sample(asset, Time.unscaledTimeAsDouble, pieceLightIntensities, pieceLightUvOffsets, pieceLightReceivers);
            for (int i = 0; i < pieceLightProps.Length; ++i)
            {
                int index = pieceLights[i].LightIndex;
                SceneLightLayer light = asset.Lights[index];
                float relativeIntensity = light.Intensity > 0f ? state.GetIntensity(index) / light.Intensity : 0f;
                pieceLightProps[i].SetEmission(Mathf.Clamp01(Mathf.Pow(relativeIntensity, light.SourceResponse)));
            }
        }

        /**
         * A held testing override replaces the light's multiplier. Otherwise the ambient activity's multiplier scales the
         * table's resting level, and a rule's flare raises the light to at least its own level while it runs.
         */
        private float AmbientLightIntensity(int index)
        {
            if (index < testLightOverrides.Length && !float.IsNaN(testLightOverrides[index])) return testLightOverrides[index];
            if (index >= ambientLightIntensities.Length) return 1f;
            float level = (index < restingLightLevels.Length ? restingLightLevels[index] : 1f) * ambientLightIntensities[index];
            return index < lightFlares.Count ? Mathf.Max(level, lightFlares.Level(index, Time.unscaledTimeAsDouble)) : level;
        }

        /** Per-light multipliers for a table without piece lights; null when the asset's lights differ from the table's. */
        private float[] AmbientLightIntensities(SceneLightingAsset asset)
        {
            if (asset == null || asset.Lights == null || asset.Lights.Length != ambientLightIntensities.Length) return null;
            for (int i = 0; i < sampledLightIntensities.Length; ++i) sampledLightIntensities[i] = AmbientLightIntensity(i);
            return sampledLightIntensities;
        }

        /** The room and table images show different slices of one room texture; lights use that texture's coordinates. */
        private Vector2 RoomTextureCoordinate(Vector3 world)
        {
            RectTransform front = tableImage.rectTransform;
            RectTransform back = roomImage.rectTransform;
            return SpiritTablePieceLayout.TextureCoordinate(front.InverseTransformPoint(world), front.rect, tableImage.uvRect,
                back.InverseTransformPoint(world), back.rect, roomImage.uvRect);
        }
    }
}
