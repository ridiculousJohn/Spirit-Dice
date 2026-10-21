using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Authored prop for a table piece's position interaction; the engine owns the position and this prop only proposes moves. */
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class SpiritTablePieceView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("Table piece ID from the encounter's mechanical content.")]
        [SerializeField] private string pieceId;
        [Tooltip("ID of the piece's position interaction.")]
        [SerializeField] private string interactionId;
        [Tooltip("Authored neutral spot; the prop's pivot rests at this transform's position. Required when the interaction allows neutral.")]
        [SerializeField] private RectTransform neutralAnchor;
        [Tooltip("Pivot rest point on a bay, normalized within that die's tray slot rect (0,0 is bottom-left); values outside 0-1 rest beside the die.")]
        [SerializeField] private Vector2 bayPlacement = new Vector2(.5f, 0f);
        [Tooltip("A release snaps to the nearest allowed position within this distance, in the parent's local units; farther releases return the prop.")]
        [SerializeField, Min(0)] private float snapRadius = 90;
        [Tooltip("Seconds the prop eases into its position after a release or an engine change.")]
        [SerializeField, Min(0)] private float settleDuration = .12f;
        [Tooltip("Flame or glow graphics whose alpha follows the bound scene light's sampled intensity.")]
        [SerializeField] private Graphic[] emissiveImages = Array.Empty<Graphic>();
        [Tooltip("Optional part scaled while the piece repels interference, such as the flame.")]
        [SerializeField] private RectTransform flareTarget;
        [Tooltip("Optional authored effect, such as sparks, shown only while the piece flares.")]
        [SerializeField] private GameObject flareEffect;
        [SerializeField, Min(.05f)] private float flareDuration = .7f;
        [Tooltip("Peak scale of the flare target.")]
        [SerializeField, Min(1)] private float flareScale = 1.35f;
        [Tooltip("Peak multiplier applied to the bound scene light while flaring.")]
        [SerializeField, Min(1)] private float flareLightIntensity = 2.2f;

        private readonly Vector3[] bayPoints = new Vector3[DiceValues.Count];
        private Func<SpiritTablePieceView, bool> canMove;
        private Func<SpiritTablePieceView, PiecePosition, bool> moveRequested;
        private PositionInteractionDefinition definition;
        private PiecePosition position;
        private bool bound;
        private bool hasBayPoints;
        private bool dragging;
        private int dragPointer;
        private Vector3 grabOffset;
        private Vector3 settleFrom;
        private float settleStart = float.NegativeInfinity;
        private float flareStart = float.NegativeInfinity;
        private Vector3 flareBaseScale;
        private Color[] emissiveColors;

        public string PieceId => pieceId;
        public string InteractionId => interactionId;
        public Vector2 BayPlacement => bayPlacement;
        public bool IsBound => bound;
        public bool IsDragging => dragging;
        public PiecePosition Position => position;
        /** Multiplies the bound scene light; above one only while flaring. */
        public float LightIntensity => 1f + (flareLightIntensity - 1f) * FlarePulse();

        public void Initialize(Func<SpiritTablePieceView, bool> canMovePiece,
            Func<SpiritTablePieceView, PiecePosition, bool> requestMove)
        {
            if (string.IsNullOrWhiteSpace(pieceId) || string.IsNullOrWhiteSpace(interactionId))
                throw new InvalidOperationException(name + " requires authored piece and interaction IDs.");
            canMove = canMovePiece ?? throw new ArgumentNullException(nameof(canMovePiece));
            moveRequested = requestMove ?? throw new ArgumentNullException(nameof(requestMove));
        }

        /** Binds the engine position; a changed position eases in unless the prop is still in the player's hand. */
        public void Bind(PiecePosition current, PositionInteractionDefinition interaction)
        {
            if (interaction == null) throw new ArgumentNullException(nameof(interaction));
            if (interaction.AllowsNeutral && neutralAnchor == null)
                throw new InvalidOperationException(name + " requires an authored neutral anchor.");
            if (bound && current != position) BeginSettle();
            definition = interaction;
            position = current;
            bound = true;
        }

        /** The current snapshot has no matching interaction; the prop keeps its place and cannot be moved. */
        public void Unbind()
        {
            CancelDrag();
            definition = null;
            bound = false;
        }

        /** Places the prop at its bound position using the tray's current bay points in world space. */
        public void Layout(IReadOnlyList<Vector3> bayWorldPoints)
        {
            if (bayWorldPoints == null || bayWorldPoints.Count != DiceValues.Count)
                throw new ArgumentException("Five bay points are required.", nameof(bayWorldPoints));
            for (int i = 0; i < DiceValues.Count; ++i) bayPoints[i] = bayWorldPoints[i];
            hasBayPoints = true;
            if (!bound || dragging || !TryGetPoint(position, out Vector3 target)) return;
            float progress = settleDuration > 0f ? (Time.unscaledTime - settleStart) / settleDuration : 1f;
            transform.position = progress >= 1f ? target : Vector3.Lerp(settleFrom, target, Mathf.SmoothStep(0f, 1f, progress));
        }

        /** Starts the repel reaction and returns its duration. */
        public float Flare()
        {
            if (flareTarget != null && float.IsNegativeInfinity(flareStart)) flareBaseScale = flareTarget.localScale;
            flareStart = Time.unscaledTime;
            if (flareEffect != null) flareEffect.SetActive(true);
            return flareDuration;
        }

        /** Scales emissive alpha from the authored colors; one restores them. */
        public void SetEmission(float emission)
        {
            if (emissiveImages == null || emissiveImages.Length == 0) return;
            if (emissiveColors == null)
            {
                emissiveColors = new Color[emissiveImages.Length];
                for (int i = 0; i < emissiveImages.Length; ++i)
                    if (emissiveImages[i] != null) emissiveColors[i] = emissiveImages[i].color;
            }
            for (int i = 0; i < emissiveImages.Length; ++i)
            {
                if (emissiveImages[i] == null) continue;
                Color color = emissiveColors[i];
                color.a *= Mathf.Clamp01(emission);
                emissiveImages[i].color = color;
            }
        }

        public void RestoreEmission()
        {
            if (emissiveColors == null) return;
            SetEmission(1f);
            emissiveColors = null;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (dragging || !bound || canMove == null || !canMove(this) || !TryGetPointer(eventData, out Vector3 pointer)) return;
            dragging = true;
            dragPointer = eventData.pointerId;
            grabOffset = transform.position - pointer;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || eventData.pointerId != dragPointer) return;
            if (TryGetPointer(eventData, out Vector3 pointer)) transform.position = pointer + grabOffset;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging || eventData.pointerId != dragPointer) return;
            dragging = false;
            Drop();
        }

        private void Drop()
        {
            BeginSettle();
            if (!bound || !hasBayPoints) return;
            var positions = new List<PiecePosition>(DiceValues.Count + 1);
            var points = new List<Vector2>(DiceValues.Count + 1);
            Transform frame = transform.parent != null ? transform.parent : transform;
            if (definition.AllowsNeutral) AddCandidate(PiecePosition.Neutral, frame, positions, points);
            for (int bay = 0; bay < DiceValues.Count; ++bay)
                if (definition.Allows(PiecePosition.Bay(bay))) AddCandidate(PiecePosition.Bay(bay), frame, positions, points);
            int nearest = SpiritTablePieceLayout.FindNearest(frame.InverseTransformPoint(transform.position), points, snapRadius);
            // A release away from every position, or onto the current one, returns the prop without an action.
            if (nearest < 0 || positions[nearest] == position || !canMove(this)) return;
            PiecePosition previous = position;
            position = positions[nearest];
            // A submitted move is rebound from the resulting snapshot; an unsubmitted one returns to the previous position.
            if (!moveRequested(this, position)) position = previous;
        }

        private void AddCandidate(PiecePosition candidate, Transform frame, List<PiecePosition> positions, List<Vector2> points)
        {
            if (!TryGetPoint(candidate, out Vector3 point)) return;
            positions.Add(candidate);
            points.Add(frame.InverseTransformPoint(point));
        }

        private bool TryGetPoint(PiecePosition candidate, out Vector3 point)
        {
            if (candidate.Kind == PiecePositionKind.Neutral)
            {
                point = neutralAnchor != null ? neutralAnchor.position : Vector3.zero;
                return neutralAnchor != null;
            }
            point = bayPoints[candidate.Index];
            return hasBayPoints;
        }

        private bool TryGetPointer(PointerEventData eventData, out Vector3 pointer)
        {
            var frame = transform.parent as RectTransform ?? (RectTransform)transform;
            return RectTransformUtility.ScreenPointToWorldPointInRectangle(frame, eventData.position,
                eventData.pressEventCamera, out pointer);
        }

        private void BeginSettle()
        {
            settleFrom = transform.position;
            settleStart = Time.unscaledTime;
        }

        private void CancelDrag()
        {
            if (!dragging) return;
            dragging = false;
            BeginSettle();
        }

        private float FlarePulse()
        {
            if (float.IsNegativeInfinity(flareStart)) return 0f;
            float progress = (Time.unscaledTime - flareStart) / flareDuration;
            if (progress < 0f || progress >= 1f) return 0f;
            // A quick flare-up followed by a slower settle back to the authored flame.
            return progress < .15f ? progress / .15f : 1f - Mathf.SmoothStep(0f, 1f, (progress - .15f) / .85f);
        }

        private void Update()
        {
            if (float.IsNegativeInfinity(flareStart)) return;
            if (Time.unscaledTime - flareStart >= flareDuration) { EndFlare(); return; }
            if (flareTarget != null) flareTarget.localScale = flareBaseScale * (1f + (flareScale - 1f) * FlarePulse());
        }

        private void EndFlare()
        {
            if (float.IsNegativeInfinity(flareStart)) return;
            if (flareTarget != null) flareTarget.localScale = flareBaseScale;
            if (flareEffect != null) flareEffect.SetActive(false);
            flareStart = float.NegativeInfinity;
        }

        private void OnDisable()
        {
            CancelDrag();
            EndFlare();
            RestoreEmission();
        }
    }
}
