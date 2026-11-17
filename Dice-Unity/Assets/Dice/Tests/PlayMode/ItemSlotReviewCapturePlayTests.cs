using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Dice.Tests
{
    /**
     * Review capture and input check of the item slot and the bay indicator. At every table a journey uses the player carries
     * the carry-over item, as a run carries owned items: each table is captured at 540 and 443 wide with the item on the
     * player's slot and with its piece on a middle bay (the slot dimmed, the indicator on the bay). At the first table, at
     * 540 and 443 wide, it drives the real input path through the event system's raycasts and pointer events: a drag from
     * the slot onto a bay, which must move the engine's piece there; with the item on the bay, the die's own tap target
     * must be hit first at the die's centre and its lower edge, and a drag from the emblem must reach the slot's handle; a
     * drag from the emblem to empty table must put the item back on its slot; and one frame is captured mid-drag over the
     * tray. Then, in a fresh application whose encounters start with every starter Play Hand charm (the board-state
     * capture's way of filling the charm row), with the item on a bay above the row, the charm row's tokens must be the
     * topmost hit at their centres and a drag from a token must reach the row's scroll. It writes PNGs under
     * DICE_SPIRIT_CAPTURE_ROOT/item-slots and is ignored without that variable; it is a capture, not a regression test.
     * The fixture is explicit: it runs only when a run selects it by name.
     */
    [Explicit("Review capture tool; select it by name with DICE_SPIRIT_CAPTURE_ROOT set.")]
    [Category("Capture")]
    public sealed class ItemSlotReviewCapturePlayTests
    {
        // Real seconds the table rests after its opening roll or a piece move before it is captured, so props settle.
        private const float SettleSeconds = 1f;
        private const int MiddleBay = 2;
        // Drag steps between the press and the release, so the drag passes through the points between them.
        private const int DragSteps = 6;

        [UnityTest]
        public IEnumerator ItemSlotOnEveryTableCapture()
        {
            string root = NarrowCropReviewCapturePlayTests.Fresh("item-slots");
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.JourneyAtBoard();
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            var carryOver = ItemWithRule<DieCarryOverRuleDefinition>(controller.Catalog);
            Own(controller, carryOver);

            var tables = new HashSet<EncounterTablePresentationAsset>();
            var journeys = controller.JourneyCatalog.Entries.Where(journey => journey != null && journey.Encounters.Any())
                .Where(journey => tables.Add(Table(journey))).ToArray();
            TestContext.WriteLine("Tables: " + string.Join(", ", journeys.Select(journey => Table(journey).name)));

            bool first = true;
            foreach (var journey in journeys)
            {
                yield return Enter(controller, journey);
                string name = Table(journey).name;
                var slot = ContentReady.EncounterView(controller).GameplayHud.PlayerItemSlot;
                Assert.That(slot.gameObject.activeInHierarchy, Is.True, name + ": the player's slot shows the carried item.");
                Assert.That(slot.IsDimmed, Is.False, name);
                if (first)
                {
                    first = false;
                    foreach (int width in Widths) yield return DriveInput(controller, root, name, carryOver, width);
                }
                yield return CaptureBothWidths(root, name + "-on-slot");
                yield return Move(controller, carryOver, PiecePosition.Bay(MiddleBay));
                Assert.That(slot.IsDimmed, Is.True, name + ": the slot dims while its item is on a bay.");
                yield return CaptureBothWidths(root, name + "-on-bay-" + MiddleBay);
            }

            yield return CheckCharmRow(root, journeys[0], carryOver);
            LogAssert.NoUnexpectedReceived();
        }

        private static readonly int[] Widths = { NarrowCropReviewCapturePlayTests.Width, NarrowCropReviewCapturePlayTests.NarrowWidth };

        /**
         * In a fresh application whose encounters start with every starter Play Hand charm, with the item on a bay above the
         * charm row, checks at both widths that each of the row's tokens is the topmost hit at its centre and that a drag
         * from a token reaches the row's scroll, not the item's handle or a die's target.
         */
        private static IEnumerator CheckCharmRow(string root, EncounterJourneyAsset journey, ItemDefinition item)
        {
            yield return ContentReady.LoadApplication();
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            var authored = controller.Catalog;
            var starters = authored.Modifiers.Where(modifier => !modifier.Availability.RequiresUnlock &&
                modifier.Phase == ModifierPhase.PlayHand).ToArray();
            // Each encounter starts with the starters that can activate with its own rules, as the catalog requires.
            var equipped = new MechanicalCatalog(authored.Version + ".item-slot-review", authored.Hands,
                authored.Encounters.Select(encounter => WithStartingCharms(encounter, starters
                    .Where(modifier => modifier.Condition.CanEverActivateWith(encounter.Rules)).Select(modifier => modifier.Identity).ToArray())),
                authored.Modifiers, authored.Items);
            Assert.That(controller.InitializeJourney(equipped, controller.JourneyCatalog, ContentReady.SpiritSavePath()), Is.True,
                controller.JourneyStatus.ToString());
            Own(controller, item);
            yield return Enter(controller, journey);
            yield return Move(controller, item, PiecePosition.Bay(MiddleBay));
            string name = Table(journey).name;
            foreach (int width in Widths)
            {
                PortraitCapture.FitPortrait(width);
                yield return null;
                var view = ContentReady.EncounterView(controller);
                var history = view.ModifierHistory;
                // Tokens scrolled out of the row's viewport or past the screen's edge take no input; the shown ones are checked.
                Canvas canvas = view.Canvas.rootCanvas;
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var tokens = history.Content.GetComponentsInChildren<Button>(false)
                    .Where(token => RectTransformUtility.RectangleContainsScreenPoint(history.Scroll.viewport, ScreenPoint(view, Center(token)), camera) &&
                        new Rect(0, 0, Screen.width, Screen.height).Contains(ScreenPoint(view, Center(token))))
                    .ToArray();
                TestContext.WriteLine(name + " at " + width + ": " + history.Count + " ready charms in the charm row, " + tokens.Length + " shown.");
                Assert.That(tokens, Is.Not.Empty, name + ": the charm row shows no ready charm to check.");
                foreach (var token in tokens)
                {
                    Vector2 point = ScreenPoint(view, Center(token));
                    GameObject hit = TopHit(point);
                    Assert.That(hit.GetComponentInParent<Button>(), Is.SameAs(token), name + " at " + width + ": the charm token is topmost; hit " + hit.name);
                    Assert.That(DragTarget(point), Is.SameAs(history.Scroll.gameObject), name + " at " + width + ": a drag from a charm token reaches the row's scroll.");
                }
                yield return NarrowCropReviewCapturePlayTests.Capture(root, name + "-charm-row-on-bay-" + MiddleBay + "-" + width + "x" +
                    NarrowCropReviewCapturePlayTests.Height + ".png", width);
            }
        }

        /** The encounter as authored, starting with the given charms. */
        private static EncounterDefinition WithStartingCharms(EncounterDefinition definition, ModifierDefinition.Id[] charms)
            => new EncounterDefinition(definition.Identity, definition.Stages, definition.StartingUpgrades, charms,
                definition.RewardPolicy, definition.OwnRules, definition.UnlockMilestones, definition.OwnEncounterModifierIds,
                definition.UnlockRewardModifierIds, definition.OwnPieces, definition.EncounterModifiersFirstClearedStageNumber,
                definition.Items);

        /** Drives the slot, the handle and the dice's taps through the event system at the width. */
        private static IEnumerator DriveInput(SpiritEncounterController controller, string root, string name, ItemDefinition item,
            int width)
        {
            PortraitCapture.FitPortrait(width);
            yield return null;
            var view = ContentReady.EncounterView(controller);
            var icon = view.GameplayHud.PlayerItemSlot.Icon;
            var dieButton = ContentReady.Find<Button>(controller, "DieButton" + (MiddleBay + 1));

            // From the slot onto the middle bay's die.
            Vector2 from = ScreenPoint(view, icon.transform.position);
            Vector2 onBay = ScreenPoint(view, Center(dieButton));
            Assert.That(DragTarget(from), Is.SameAs(icon.gameObject), name + ": a drag from the slot takes its icon.");
            yield return Drag(from, onBay, null);
            yield return ContentReady.Idle(controller);
            Assert.That(PositionOf(controller, item), Is.EqualTo(PiecePosition.Bay(MiddleBay)), name + ": a drop on the bay moves the piece there.");
            Assert.That(controller.SelectedMask, Is.Zero, name + ": dragging changes no dice selection.");
            yield return new WaitForSecondsRealtime(SettleSeconds);

            // The die's own tap target first at its centre and its lower edge; the handle at the emblem.
            var rect = (RectTransform)dieButton.transform;
            Vector2 lowerEdge = ScreenPoint(view, rect.TransformPoint(new Vector2(rect.rect.center.x, rect.rect.yMin + 2f)));
            Assert.That(TopHit(ScreenPoint(view, Center(dieButton))), Is.SameAs(dieButton.gameObject), name + ": the die's centre taps the die.");
            Assert.That(TopHit(lowerEdge), Is.SameAs(dieButton.gameObject), name + ": the die's lower edge taps the die.");
            Vector2 emblem = ScreenPoint(view, icon.transform.position);
            TestContext.WriteLine(name + " at " + width + ": emblem at " + emblem + ", topmost hit there " + TopHit(emblem).name +
                ", die's lower edge at " + lowerEdge);
            Assert.That(DragTarget(emblem), Is.SameAs(icon.gameObject), name + ": a drag from the emblem takes the item's handle.");
            Assert.That(DragTarget(ScreenPoint(view, Center(dieButton))), Is.Null,
                name + ": a drag from the die's centre takes nothing, so a press there only taps the die.");

            // From the emblem to empty table, capturing a frame over the tray mid-drag.
            Vector2 empty = emblem + new Vector2(-Screen.width * .15f, Screen.height * .3f);
            yield return Drag(emblem, empty, () => NarrowCropReviewCapturePlayTests.Capture(root,
                name + "-mid-drag-" + width + "x" + NarrowCropReviewCapturePlayTests.Height + ".png", width));
            yield return ContentReady.Idle(controller);
            Assert.That(PositionOf(controller, item), Is.EqualTo(PiecePosition.Neutral), name + ": a drop on empty table puts the item back on its slot.");
            Assert.That(controller.SelectedMask, Is.Zero, name + ": dragging changes no dice selection.");
            yield return new WaitForSecondsRealtime(SettleSeconds);
        }

        /**
         * The object a press at the point gives its drag to, as the input module does: the drag handler of the topmost hit,
         * after it is offered the potential drag (which may hand it on); null when the press starts no drag.
         */
        private static GameObject DragTarget(Vector2 screenPoint) => Press(screenPoint).pointerDrag;

        private static PointerEventData Press(Vector2 screenPoint)
        {
            var hits = Raycast(screenPoint);
            Assert.That(hits, Is.Not.Empty, "Nothing takes the press at " + screenPoint);
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = screenPoint, pressPosition = screenPoint, button = PointerEventData.InputButton.Left, pointerId = -1,
                pointerPressRaycast = hits[0], pointerCurrentRaycast = hits[0]
            };
            pointer.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject);
            if (pointer.pointerDrag != null) ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.initializePotentialDrag);
            return pointer;
        }

        /**
         * Presses at the point, drags in steps to the target and releases there, as a pointer does through the input
         * module; midway, over the tray, it runs the given capture.
         */
        private static IEnumerator Drag(Vector2 from, Vector2 to, System.Func<IEnumerator> midway)
        {
            var pointer = Press(from);
            var handler = pointer.pointerDrag;
            Assert.That(handler, Is.Not.Null, "Nothing takes a drag at " + from);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.beginDragHandler);
            for (int step = 1; step <= DragSteps; ++step)
            {
                pointer.position = Vector2.Lerp(from, to, (float)step / DragSteps);
                ExecuteEvents.Execute(handler, pointer, ExecuteEvents.dragHandler);
                yield return null;
                // A third of the way from the emblem the held icon is still over the tray.
                if (step == DragSteps / 3 && midway != null) yield return midway();
            }
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.endDragHandler);
        }

        private static List<RaycastResult> Raycast(Vector2 screenPoint)
        {
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) { position = screenPoint, pointerId = -1 };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            return hits;
        }

        private static GameObject TopHit(Vector2 screenPoint)
        {
            var hits = Raycast(screenPoint);
            Assert.That(hits, Is.Not.Empty, "Nothing is hit at " + screenPoint);
            return hits[0].gameObject;
        }

        private static Vector3 Center(Component target)
        {
            var rect = (RectTransform)target.transform;
            return rect.TransformPoint(rect.rect.center);
        }

        private static Vector2 ScreenPoint(SpiritEncounterView view, Vector3 world)
        {
            Canvas canvas = view.Canvas.rootCanvas;
            return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, world);
        }

        private static PiecePosition PositionOf(SpiritEncounterController controller, ItemDefinition item)
            => ((PiecePositionState)controller.Snapshot.PieceInteractions.Single(interaction => interaction.PieceId == item.Piece.Identity).State).Position;

        private static ItemDefinition ItemWithRule<TRule>(MechanicalCatalog catalog) where TRule : EncounterRuleDefinition
        {
            var item = catalog.Items.FirstOrDefault(candidate => candidate.Piece != null && candidate.Rules.Any(rule => rule is TRule));
            Assert.That(item, Is.Not.Null, "The catalog has no item with a piece and a " + typeof(TRule).Name + ".");
            return item;
        }

        /** Records the item as owned on the test profile, so every run started afterwards carries it. */
        private static void Own(SpiritEncounterController controller, ItemDefinition item)
        {
            var profile = PlayerProfileFileStore.LoadOrCreate(controller.ProfilePath);
            profile.OwnItem(item.Identity);
            PlayerProfileFileStore.Save(controller.ProfilePath, profile);
            Assert.That(controller.InitializeJourney(controller.Catalog, controller.JourneyCatalog, controller.SavePath), Is.True,
                controller.JourneyStatus.ToString());
        }

        private static EncounterTablePresentationAsset Table(EncounterJourneyAsset journey)
            => journey.TableSceneAt(journey.IndexOfEncounter(journey.Encounters.First().Identity));

        private static IEnumerator Enter(SpiritEncounterController controller, EncounterJourneyAsset journey)
        {
            yield return ContentReady.EnterEncounter(controller, journey, ContentReady.Seed);
            yield return new WaitForSecondsRealtime(SettleSeconds);
        }

        /** Moves the item's piece through the screen's controls, as the player's handling of it does. */
        private static IEnumerator Move(SpiritEncounterController controller, ItemDefinition item, PiecePosition position)
        {
            var piece = item.Piece;
            controller.InteractWithPiece(piece.Identity, piece.IndexOf<PositionInteractionDefinition>(), new MovePieceInput(position));
            yield return ContentReady.Idle(controller);
            Assert.That(PositionOf(controller, item), Is.EqualTo(position), "The piece did not move.");
            yield return new WaitForSecondsRealtime(SettleSeconds);
        }

        private static IEnumerator CaptureBothWidths(string root, string name)
        {
            foreach (int width in new[] { NarrowCropReviewCapturePlayTests.Width, NarrowCropReviewCapturePlayTests.NarrowWidth })
                yield return NarrowCropReviewCapturePlayTests.Capture(root, name + "-" + width + "x" + NarrowCropReviewCapturePlayTests.Height + ".png", width);
        }
    }
}
