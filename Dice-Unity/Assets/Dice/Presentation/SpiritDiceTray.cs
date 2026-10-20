using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    public sealed class SpiritDiceTray : MonoBehaviour
    {
        public const int RenderLayer = 31;
        private const float SlotSpacing = 1.45f;
        private const float FeltHeight = 0.04f;
        private const float RestHeight = FeltHeight + 0.5f;
        private static readonly Vector3[] FaceNormals =
        {
            Vector3.forward, Vector3.up, Vector3.right,
            Vector3.left, Vector3.down, Vector3.back
        };

        [SerializeField] private SpiritTrayPresentationAsset trayPresentation;
        [SerializeField] private Transform[] dice = new Transform[DiceValues.Count];
        [SerializeField] private GameObject[] selectionOutlines = new GameObject[DiceValues.Count];
        [SerializeField] private GameObject[] selectionFrames = new GameObject[DiceValues.Count];
        [SerializeField] private MeshRenderer[] contactShadows = new MeshRenderer[DiceValues.Count];
        [SerializeField] private GameObject renderRoot;
        private bool initialized;
        private Camera renderCamera;
        private RenderTexture texture;
        private Vector3[] supportPoints;
        private readonly Vector3[] shadowScales = new Vector3[DiceValues.Count];
        private int selectionMask;
        private int scoringHighlightMask;
        private int rollingMask;
        private int presentationVersion;
        private SpiritTrayScene sharedScene;
        [SerializeField] private Transform[] slotRoots = new Transform[DiceValues.Count];
        private float DieScale => sharedScene != null ? sharedScene.Geometry.DiceScale : 1;

        public RenderTexture Texture => texture;
        public Camera RenderCamera => renderCamera;
        public Transform RenderRoot => sharedScene != null ? sharedScene.RenderRoot : renderRoot != null ? renderRoot.transform : null;
        public bool IsRolling { get; private set; }
        public int ScoringHighlightMask => scoringHighlightMask;

        public void SetSceneLighting(SceneLightingState state)
        {
            sharedScene?.SetSceneLighting(state);
        }

        public void Configure(SpiritTrayPresentationAsset presentation)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            presentation.Validate();
            if (trayPresentation != presentation || sharedScene != null) ReleasePresentation();
            trayPresentation = presentation;
        }

        public void Configure(SpiritTrayPresentationAsset presentation, SpiritTrayScene scene)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            presentation.ValidateScene();
            if (scene.Presentation != presentation)
                throw new InvalidOperationException("The dice tray must use the shared scene's presentation.");
            if (trayPresentation != presentation || sharedScene != scene) ReleasePresentation();
            trayPresentation = presentation;
            sharedScene = scene;
        }

        public void Initialize()
        {
            if (initialized) return;
            if (trayPresentation == null || sharedScene == null || renderRoot == null)
                throw new InvalidOperationException("The dice tray requires an authored shared tray scene.");
            sharedScene.Initialize();
            texture = sharedScene.Texture;
            renderCamera = sharedScene.RenderCamera;
            for (int index = 0; index < DiceValues.Count; ++index)
            {
                if (dice[index] == null || selectionOutlines[index] == null || selectionFrames[index] == null || contactShadows[index] == null)
                    throw new InvalidOperationException("The dice tray requires five authored dice, shadows, and selection highlights.");
                shadowScales[index] = contactShadows[index].transform.localScale;
            }
            supportPoints = CreateSupportPoints(dice[0].GetComponent<MeshFilter>().sharedMesh);
            initialized = true;
            SetContentActive(isActiveAndEnabled);
        }

        public void SetValues(DiceValues values)
        {
            if (!values.IsValid) throw new ArgumentException("Every die must have a face from 1 to 6.", nameof(values));
            Initialize();
            if (IsRolling) throw new InvalidOperationException("Cannot replace dice values during a roll.");
            for (int index = 0; index < DiceValues.Count; index++)
            {
                dice[index].localRotation = RotationForFace(values[index], index);
                dice[index].localPosition = SlotPosition(index);
                SetShadowHeight(index, 0f);
            }
        }

        public void SetSelection(int mask)
        {
            Initialize();
            selectionMask = mask & DiceValues.AllMask;
            RefreshHighlights();
        }

        public void SetScoringHighlight(int mask)
        {
            scoringHighlightMask = mask & DiceValues.AllMask;
            // Clearing feedback during scene shutdown must not recreate the shared render scene.
            if (!initialized) return;
            RefreshHighlights();
        }

        private void RefreshHighlights()
        {
            for (int index = 0; index < DiceValues.Count; index++)
            {
                if (dice[index] == null || selectionOutlines[index] == null || selectionFrames[index] == null) continue;
                bool selected = ((selectionMask | scoringHighlightMask) & (1 << index)) != 0;
                selectionOutlines[index].SetActive(selected);
                selectionFrames[index].SetActive(selected);
                if ((rollingMask & (1 << index)) == 0) dice[index].localPosition = SlotPosition(index);
            }
        }

        public IEnumerator Roll(DiceValues after, int mask, float duration = 0.55f)
            => Roll(after, mask, SpiritRollTiming.ForDuration(duration));

        public IEnumerator Roll(DiceValues after, int mask, SpiritRollTiming timing)
        {
            if (!after.IsValid) throw new ArgumentException("Every die must have a face from 1 to 6.", nameof(after));
            if (timing == null) throw new ArgumentNullException(nameof(timing));
            Initialize();
            if (IsRolling) throw new InvalidOperationException("A dice roll is already running.");
            rollingMask = mask & DiceValues.AllMask;
            if (rollingMask == 0) yield break;

            Quaternion[] startRotations = new Quaternion[DiceValues.Count];
            Quaternion[] endRotations = new Quaternion[DiceValues.Count];
            int version = presentationVersion;
            for (int index = 0; index < DiceValues.Count; index++)
            {
                startRotations[index] = dice[index].localRotation;
                endRotations[index] = RotationForFace(after[index], index);
            }

            IsRolling = true;
            try
            {
                int diceCount = 0;
                for (int index = 0; index < DiceValues.Count; index++)
                    if ((rollingMask & (1 << index)) != 0) diceCount++;
                while (true)
                {
                    if (version != presentationVersion) yield break;
                    float elapsed = timing.Elapsed;
                    int ordinal = 0;
                    for (int index = 0; index < DiceValues.Count; index++)
                    {
                        if ((rollingMask & (1 << index)) == 0) continue;
                        timing.SamplePose(ordinal++, diceCount, elapsed, startRotations[index], endRotations[index],
                            out Quaternion rotation, out float lift, out float drift);
                        dice[index].localRotation = rotation;
                        Vector3 position = SlotPosition(index);
                        position.y = (sharedScene != null ? 0 : FeltHeight) + (SupportHeight(rotation) + lift) * DieScale;
                        position.x += drift * DieScale;
                        dice[index].localPosition = position;
                        SetShadowHeight(index, lift / .34f);
                    }
                    if (elapsed >= timing.Duration) break;
                    yield return null;
                }
            }
            finally
            {
                for (int index = 0; version == presentationVersion && index < DiceValues.Count; index++)
                {
                    if ((rollingMask & (1 << index)) == 0) continue;
                    if (dice[index] == null) continue;
                    dice[index].localRotation = endRotations[index];
                    dice[index].localPosition = SlotPosition(index);
                    SetShadowHeight(index, 0f);
                }
                if (version == presentationVersion)
                {
                    rollingMask = 0;
                    IsRolling = false;
                }
            }
        }

        public int GetVisibleFace(int index)
        {
            if (index < 0 || index >= DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(index));
            Initialize();
            Vector3 towardCamera = renderCamera.orthographic ? -renderCamera.transform.forward
                : (renderCamera.transform.position - dice[index].position).normalized;
            float bestAlignment = float.NegativeInfinity;
            int visibleFace = 1;
            for (int face = 1; face <= 6; face++)
            {
                float alignment = Vector3.Dot(dice[index].TransformDirection(FaceNormals[face - 1]), towardCamera);
                if (alignment <= bestAlignment) continue;
                bestAlignment = alignment;
                visibleFace = face;
            }
            return visibleFace;
        }

        public Vector2 GetSlotViewportPosition(int index)
        {
            if (index < 0 || index >= DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(index));
            Initialize();
            Vector3 point = dice[index].parent.TransformPoint(SlotPosition(index) + Vector3.up * (.18f * DieScale));
            return renderCamera.WorldToViewportPoint(point);
        }

        public Vector2 GetSlotLabelViewportPosition(int index)
        {
            if (index < 0 || index >= DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(index));
            Initialize();
            Vector3 point = sharedScene != null ? sharedScene.Geometry.DiceLabelAnchors[index].position
                : renderRoot.transform.TransformPoint(new Vector3((index - 2) * SlotSpacing, 0.35f, -1.13f));
            return renderCamera.WorldToViewportPoint(point);
        }

        public Rect GetSlotViewportRect(int index)
        {
            if (index < 0 || index >= DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(index));
            Initialize();
            Vector2 min = Vector2.one * float.MaxValue;
            Vector2 max = Vector2.one * float.MinValue;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 point = renderCamera.WorldToViewportPoint(dice[index].TransformPoint(new Vector3(x, y, z) * .5f));
                        min = Vector2.Min(min, new Vector2(point.x, point.y));
                        max = Vector2.Max(max, new Vector2(point.x, point.y));
                    }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void SetShadowHeight(int index, float lift)
        {
            if (contactShadows[index] == null) return;
            Vector3 scale = shadowScales[index];
            contactShadows[index].transform.localScale = new Vector3(scale.x * (1 + lift * (.24f / 1.24f)), scale.y,
                scale.z * (1 + lift * (.18f / 1.16f)));
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetFloat("_ShadowLift", lift);
            contactShadows[index].SetPropertyBlock(properties);
        }

        private Vector3 SlotPosition(int index)
        {
            return sharedScene != null ? Vector3.up * (SupportHeight(dice[index].localRotation) * DieScale)
                : new Vector3((index - 2) * SlotSpacing, RestHeight, 0f);
        }

        private float SupportHeight(Quaternion rotation)
        {
            Vector3 downward = Quaternion.Inverse(rotation) * Vector3.down;
            float height = 0f;
            foreach (Vector3 point in supportPoints) height = Mathf.Max(height, Vector3.Dot(point, downward));
            return height;
        }

        private static Vector3[] CreateSupportPoints(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            List<Vector3> points = new List<Vector3>();
            // Support lies on the bevel or flat-face perimeter, never inside a pip depression.
            foreach (Vector3 point in vertices)
            {
                int outerAxes = (Mathf.Abs(point.x) >= 0.3f ? 1 : 0)
                    + (Mathf.Abs(point.y) >= 0.3f ? 1 : 0) + (Mathf.Abs(point.z) >= 0.3f ? 1 : 0);
                if (outerAxes >= 2) points.Add(point);
            }
            return points.ToArray();
        }

        private static Quaternion RotationForFace(int face, int index)
        {
            // Results face upward, so a resting die always has a flat base on the felt.
            Quaternion result = Quaternion.FromToRotation(FaceNormals[face - 1], Vector3.up);
            return Quaternion.AngleAxis(index % 4 * 90f + (index - 2) * 3f, Vector3.up) * result;
        }



        public void ReleasePresentation()
        {
            ++presentationVersion;
            if (initialized)
                for (int index = 0; index < contactShadows.Length; ++index)
                    if (contactShadows[index] != null) contactShadows[index].transform.localScale = shadowScales[index];
            texture = null;
            renderCamera = null;
            initialized = false;
            selectionMask = 0;
            scoringHighlightMask = 0;
            rollingMask = 0;
            IsRolling = false;
            RefreshHighlights();
        }

        private void SetContentActive(bool active)
        {
            if (renderRoot != null && renderRoot != gameObject) renderRoot.SetActive(active);
            foreach (Transform slot in slotRoots)
                if (slot != null) slot.gameObject.SetActive(active);
        }

        private void OnEnable() => SetContentActive(true);
        private void OnDisable() => SetContentActive(false);
        private void OnDestroy() => ReleasePresentation();

    }
}
