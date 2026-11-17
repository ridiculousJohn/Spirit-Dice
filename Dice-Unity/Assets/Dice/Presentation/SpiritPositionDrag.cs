using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Dice.Presentation
{
    /**
     * The drag of a table piece's position, shared by everything that shows one on the HUD (a scene prop, an item slot's
     * icon): the RectTransform rests at the engine's position, on its neutral spot or at a bay point it is given, and a drag
     * released near an allowed position proposes a move there, then settles. The engine owns the position; this component
     * only proposes moves. Each kind adds what only it needs.
     */
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public abstract class SpiritPositionDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("Authored neutral spot; the pivot rests at this transform's position. Required when the interaction allows neutral.")]
        [SerializeField] private RectTransform neutralAnchor;
        [Tooltip("A release snaps to the nearest allowed position within this distance, in the parent's local units; farther releases return it.")]
        [SerializeField, Min(0)] private float snapRadius = 90;
        [Tooltip("Seconds it eases into its position after a release or an engine change.")]
        [SerializeField, Min(0)] private float settleDuration = .12f;

        // The bay points in the parent space (world space without a parent), so they move with the parent when the screen
        // moves it without a new layout.
        private readonly Vector3[] bayPoints = new Vector3[DiceValues.Count];
        private Func<bool> canMove;
        private Func<PiecePosition, bool> moveRequested;
        private PositionInteractionDefinition definition;
        private PiecePosition position;
        private bool bound;
        private bool hasBayPoints;
        private bool dragging;
        // Whether it is easing into its position; only then does it move itself each frame.
        private bool settling;
        private int dragPointer;
        private Vector3 grabOffset;
        // Where the settle started, in the same space as the bay points.
        private Vector3 settleFrom;
        private float settleStart = float.NegativeInfinity;

        public bool IsBound => bound;
        public bool IsDragging => dragging;
        /** Whether it is easing into its position after a release or an engine change. */
        public bool IsSettling => settling;
        public PiecePosition Position => position;

        /** Sets whether the piece may move now and how a move is proposed; a proposal returns whether it was submitted. */
        public void Initialize(Func<bool> canMovePiece, Func<PiecePosition, bool> requestMove)
        {
            canMove = canMovePiece ?? throw new ArgumentNullException(nameof(canMovePiece));
            moveRequested = requestMove ?? throw new ArgumentNullException(nameof(requestMove));
        }

        /** Binds the engine position; a changed position eases in unless the piece is still in the player's hand. */
        public void Bind(PiecePosition current, PositionInteractionDefinition interaction)
        {
            if (interaction == null) throw new ArgumentNullException(nameof(interaction));
            if (interaction.AllowsNeutral && neutralAnchor == null)
                throw new InvalidOperationException(name + " requires an authored neutral anchor.");
            if (bound && current != position) BeginSettle();
            definition = interaction;
            position = current;
            bound = true;
            Place();
            StateChanged();
        }

        /** The current snapshot has no matching interaction; it keeps its place and cannot be moved. */
        public void Unbind()
        {
            CancelDrag();
            definition = null;
            bound = false;
            StateChanged();
        }

        /**
         * Takes the tray's bay points in world space, keeps them in the parent space and places itself at its bound position.
         * The parent's current transform converts them, so the points follow the parent wherever it moves later.
         */
        public void Layout(IReadOnlyList<Vector3> bayWorldPoints)
        {
            if (bayWorldPoints == null || bayWorldPoints.Count != DiceValues.Count)
                throw new ArgumentException("Five bay points are required.", nameof(bayWorldPoints));
            for (int i = 0; i < DiceValues.Count; ++i) bayPoints[i] = ToParentSpace(bayWorldPoints[i]);
            hasBayPoints = true;
            Place();
        }

        /** Called whenever it is bound or unbound, taken up or released, or ends a settle; a kind shows its state then. */
        protected virtual void StateChanged() { }

        /**
         * Whether a release away from every position means the neutral position, where the interaction allows it, rather
         * than a return to the current position.
         */
        protected virtual bool ReleaseAwayIsNeutral => false;

        protected void Update()
        {
            if (settling) Place();
        }

        /** Moves to the bound position, easing in while it settles; a held or unbound piece stays where it is. */
        private void Place()
        {
            bool wasSettling = settling;
            if (!bound || dragging || !TryGetPoint(position, out Vector3 target))
            {
                settling = false;
                if (wasSettling) StateChanged();
                return;
            }
            float progress = settleDuration > 0f ? (Time.unscaledTime - settleStart) / settleDuration : 1f;
            settling = progress < 1f;
            transform.position = settling
                ? Vector3.Lerp(FromParentSpace(settleFrom), target, Mathf.SmoothStep(0f, 1f, progress)) : target;
            if (wasSettling && !settling) StateChanged();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (dragging || !bound || canMove == null || !canMove() || !TryGetPointer(eventData, out Vector3 pointer)) return;
            dragging = true;
            dragPointer = eventData.pointerId;
            grabOffset = transform.position - pointer;
            StateChanged();
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
            StateChanged();
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
            PiecePosition target = nearest >= 0 ? positions[nearest]
                : ReleaseAwayIsNeutral && definition.AllowsNeutral ? PiecePosition.Neutral : position;
            // A release onto the current position, or away from every position where that is not neutral, returns it
            // without an action.
            if (target == position || !canMove()) return;
            PiecePosition previous = position;
            position = target;
            // A submitted move is rebound from the resulting snapshot; an unsubmitted one returns to the previous position.
            if (!moveRequested(position)) position = previous;
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
            point = FromParentSpace(bayPoints[candidate.Index]);
            return hasBayPoints;
        }

        // Without a parent the points are kept in world space, which is the parent space.
        private Vector3 ToParentSpace(Vector3 world) => transform.parent != null ? transform.parent.InverseTransformPoint(world) : world;

        private Vector3 FromParentSpace(Vector3 local) => transform.parent != null ? transform.parent.TransformPoint(local) : local;

        private bool TryGetPointer(PointerEventData eventData, out Vector3 pointer)
        {
            var frame = transform.parent as RectTransform ?? (RectTransform)transform;
            return RectTransformUtility.ScreenPointToWorldPointInRectangle(frame, eventData.position,
                eventData.pressEventCamera, out pointer);
        }

        private void BeginSettle()
        {
            settleFrom = ToParentSpace(transform.position);
            settleStart = Time.unscaledTime;
            settling = true;
        }

        private void CancelDrag()
        {
            if (!dragging) return;
            dragging = false;
            BeginSettle();
        }

        protected virtual void OnDisable() => CancelDrag();
    }
}
