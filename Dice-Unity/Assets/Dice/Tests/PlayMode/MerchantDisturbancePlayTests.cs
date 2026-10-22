using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Resolution = Dice.Engine.Resolution;

namespace Dice.Tests
{
    /**
     * Playback of the poltergeist in the merchant encounter: a committed hand pauses briefly before interference that
     * precedes its scoring while a reroll's interference follows the roll at once, the score panel names the scored
     * rank as soon as the disturbed die lands, and the candle's repel flare and sparks finish below the HUD's resource
     * plaques. Each case searches session seeds against the shipped catalog for an opening whose rule draws produce it.
     */
    public sealed class MerchantDisturbancePlayTests
    {
        private const string EncounterId = "encounter.merchant.unquiet_cart";
        private const ulong SeedSearchLimit = 4000;
        // Rerolls take the two leftmost dice, so the poltergeist targets one of the kept three.
        private const int RerollMask = 0b00011;
        // The committed hand's pause before its interference, and the most a reroll's interference may wait after its roll.
        private const float PlayHandBeat = .25f, PromptLimit = .15f;
        // The topmost visible flame row of ProtectiveCandle.png, counted from the top of its 1377-row frame.
        private const int FlameTopRow = 15, CandleRows = 1377;

        [UnityTest]
        public IEnumerator PlayHandDisturbancePausesThenNamesTheScoredRankWhenTheDieLands()
        {
            DieDisturbedEvent planned = null;
            ScoreBreakdown scored = null;
            SpiritEncounterController controller = null;
            yield return Begin(catalog =>
            {
                for (ulong candidate = 1; candidate <= SeedSearchLimit; candidate++)
                {
                    var session = EncounterSession.CreateEncounter(catalog, EncounterId, candidate);
                    var opened = session.Snapshot.CurrentDice;
                    var played = session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
                    var disturbed = played.Events.OfType<DieDisturbedEvent>().SingleOrDefault();
                    if (disturbed == null || disturbed.Before == disturbed.After) continue;
                    var score = played.Events.OfType<HandScoredEvent>().Single().Score;
                    if (score.Category == HandEvaluator.Evaluate(opened)) continue;
                    planned = disturbed;
                    scored = score;
                    return candidate;
                }
                Assert.Fail("No searched seed disturbs a played hand into another rank.");
                return 0;
            }, value => controller = value);
            var view = ContentReady.EncounterView(controller);
            HandCategory opening = HandEvaluator.Evaluate(controller.Snapshot.CurrentDice);
            Assert.That(view.GameplayHud.CategoryLabel.text, Is.EqualTo(Rank(opening)), "The panel previews the undisturbed rank.");
            var resolutions = new List<Resolution>();
            controller.Resolved += resolutions.Add;
            var watch = new Watch(view, planned.Bay);
            controller.PlayHand();
            yield return watch.UntilDisturbance(() => watch.TargetMoved);
            Assert.That(watch.Started, Is.True, "The disturbed die moves.");
            Assert.That(watch.Idle, Is.GreaterThanOrEqualTo(PlayHandBeat - .01f),
                "A committed hand pauses before the poltergeist disturbs it.");
            Assert.That(view.GameplayHud.CategoryLabel.text, Is.EqualTo(Rank(opening)), "The rank waits for the die to land.");

            float deadline = Time.realtimeSinceStartup + 10;
            while (view.DiceTray.IsRolling && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(view.DiceTray.IsRolling, Is.False, "The disturbed die lands.");
            float landed = Time.realtimeSinceStartup;
            // The scored rank replaces the stale one within the disturbance's hold, before scoring begins.
            while (view.GameplayHud.CategoryLabel.text == Rank(opening) && Time.realtimeSinceStartup < landed + .2f) yield return null;
            Assert.That(view.GameplayHud.CategoryLabel.text, Is.EqualTo(Rank(scored.Category)),
                "The panel names the scored rank once the disturbed die lands.");
            Assert.That(Time.realtimeSinceStartup - landed, Is.LessThan(.2f));

            deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            controller.Resolved -= resolutions.Add;
            Assert.That(controller.IsBusy, Is.False, "Playback completes.");
            Assert.That(resolutions, Has.Count.EqualTo(1));
            Assert.That(resolutions[0].Events.OfType<HandScoredEvent>().Single().Score.Category, Is.EqualTo(scored.Category),
                "The rank shown on landing is the rank that was scored.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RerollDisturbanceFollowsTheRollWithoutAPause()
        {
            DieDisturbedEvent planned = null;
            SpiritEncounterController controller = null;
            yield return Begin(catalog =>
            {
                for (ulong candidate = 1; candidate <= SeedSearchLimit; candidate++)
                {
                    var session = EncounterSession.CreateEncounter(catalog, EncounterId, candidate);
                    planned = session.Execute(new RerollDiceAction(RerollMask), session.Snapshot.Revision)
                        .Events.OfType<DieDisturbedEvent>().SingleOrDefault();
                    if (planned != null) return candidate;
                }
                Assert.Fail("No searched seed disturbs a reroll.");
                return 0;
            }, value => controller = value);
            var view = ContentReady.EncounterView(controller);
            ToggleRerollDice(controller);
            var watch = new Watch(view, planned.Bay);
            controller.Reroll();
            yield return watch.UntilDisturbance(() => watch.TargetMoved);
            Assert.That(watch.Started, Is.True, "The disturbed die moves.");
            Assert.That(watch.Idle, Is.LessThan(PromptLimit), "A reroll's interference follows its roll without the hand's pause.");
            yield return Complete(controller);
        }

        [UnityTest]
        public IEnumerator BlockedPlayHandPausesBeforeTheCandleFlaresAndABlockedRerollDoesNot()
        {
            foreach (bool playHand in new[] { true, false })
            {
                int bay = -1;
                SpiritEncounterController controller = null;
                yield return Begin(catalog => FindBlockedSeed(catalog, playHand, out bay), value => controller = value);
                var view = ContentReady.EncounterView(controller);
                var piece = view.TablePieces.Single();
                yield return Move(controller, piece, PiecePosition.Bay(bay));
                if (!playHand) ToggleRerollDice(controller);
                var watch = new Watch(view, bay);
                if (playHand) controller.PlayHand();
                else controller.Reroll();
                yield return watch.UntilDisturbance(() => piece.LightIntensity > 1.0001f);
                Assert.That(watch.Started, Is.True, "The candle flares.");
                if (playHand)
                    Assert.That(watch.Idle, Is.GreaterThanOrEqualTo(PlayHandBeat - .01f), "A committed hand pauses before the candle repels.");
                else Assert.That(watch.Idle, Is.LessThan(PromptLimit), "A reroll's repelled attempt follows its roll at once.");
                yield return Complete(controller);
            }
        }

        [UnityTest]
        public IEnumerator CandleFlareAndSparksRunAndEndBelowTheResourcePlaques()
        {
            SpiritEncounterController controller = null;
            yield return Begin(catalog => 1, value => controller = value);
            var view = ContentReady.EncounterView(controller);
            var hud = view.GameplayHud;
            var piece = view.TablePieces.Single();
            var flame = piece.FlareTarget;
            Assert.That(flame, Is.Not.Null, "The candle's flame flares.");
            Assert.That(piece.FlareEffect, Is.Not.Null, "The candle authors a flare effect.");
            var sparks = piece.FlareEffect.GetComponent<SpiritFlareSparks>();
            Assert.That(sparks, Is.Not.Null, "The flare effect throws sparks.");
            Assert.That(piece.FlareEffect.activeSelf, Is.False, "Sparks rest hidden.");
            Assert.That(sparks.Duration, Is.LessThan(piece.FlareDuration), "Sparks finish within the flare.");
            Assert.That(piece.FlarePeakScale.y, Is.LessThan(piece.FlarePeakScale.x), "The flare widens rather than reaching upward.");
            Assert.That(piece.FlarePeakScale.y, Is.LessThanOrEqualTo(1f), "The flame tip keeps its height while flaring.");
            Vector3 lossy = sparks.transform.lossyScale;
            Assert.That(lossy.x, Is.EqualTo(lossy.y).Within(lossy.y * .001f), "Sparks are drawn at a uniform scale.");

            // The candle only repels attempts on the bay it wards, so it flares only on bays.
            foreach (var position in new[] { PiecePosition.Bay(0), PiecePosition.Bay(2), PiecePosition.Bay(4) })
                foreach (int width in new[] { 540, 443 })
                {
                    var portrait = ContentReady.Portrait();
                    portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                    portrait.sizeDelta = new Vector2(width, 960);
                    portrait.anchoredPosition = Vector2.zero;
                    yield return Move(controller, piece, position);
                    Canvas.ForceUpdateCanvases();
                    string context = "bay " + (position.Index + 1) + " at " + width;
                    float plaque = Mathf.Min(Bottom(hud.RerollsPanel), Bottom(hud.HandsPanel));
                    Vector3 restScale = flame.localScale;
                    Rect rect = flame.rect;
                    var tip = new Vector3(0f, rect.yMin + rect.height * (CandleRows - FlameTopRow) / CandleRows, 0f);
                    Vector2 peak = piece.FlarePeakScale;
                    float peakTip = flame.parent.TransformPoint(flame.localPosition + new Vector3(tip.x * restScale.x * peak.x,
                        tip.y * restScale.y * peak.y, 0f)).y;
                    float sparkTop = sparks.transform.TransformPoint(new Vector3(0f, sparks.Top, 0f)).y;
                    Assert.That(peakTip, Is.LessThan(plaque), "The flame tip at its peak stays below the resource plaques, " + context);
                    Assert.That(sparkTop, Is.LessThan(plaque), "Sparks stay below the resource plaques, " + context);

                    float highest = float.NegativeInfinity, brightest = 1f;
                    int sparksSeen = 0;
                    piece.Flare();
                    float deadline = Time.realtimeSinceStartup + 5;
                    while (piece.IsFlaring && Time.realtimeSinceStartup < deadline)
                    {
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        brightest = Mathf.Max(brightest, piece.LightIntensity);
                        highest = Mathf.Max(highest, flame.TransformPoint(tip).y);
                        int live = 0;
                        foreach (var part in sparks.Sparks.Append(sparks.Glow))
                        {
                            if (part == null || !part.gameObject.activeInHierarchy) continue;
                            if (part != sparks.Glow) ++live;
                            Assert.That(part.raycastTarget, Is.False, "Flare parts never take input.");
                            highest = Mathf.Max(highest, Top(part.rectTransform));
                        }
                        sparksSeen = Mathf.Max(sparksSeen, live);
                    }
                    Assert.That(piece.IsFlaring, Is.False, "The flare ends, " + context);
                    Assert.That(highest, Is.LessThan(plaque), "Every flare part stays below the resource plaques, " + context);
                    Assert.That(sparksSeen, Is.GreaterThan(0), "Sparks fly, " + context);
                    Assert.That(brightest, Is.GreaterThan(2f), "The flare brightens the candle light, " + context);
                    Assert.That(piece.LightIntensity, Is.EqualTo(1f), "The light returns to the candle at rest, " + context);
                    Assert.That(Vector3.Distance(flame.localScale, restScale), Is.LessThan(1e-5f), "The flame returns to rest, " + context);
                    Assert.That(piece.FlareEffect.activeSelf, Is.False, "The effect hides after the flare, " + context);
                    Assert.That(sparks.IsPlaying, Is.False);
                    Assert.That(sparks.Sparks.Append(sparks.Glow).Where(part => part != null).All(part => !part.gameObject.activeSelf),
                        Is.True, "Every spark rests inactive, " + context);
                }
            LogAssert.NoUnexpectedReceived();
        }

        /** A seed whose first interference attempt, on Play Hand or after a reroll, is repelled by the candle on its bay. */
        private static ulong FindBlockedSeed(MechanicalCatalog catalog, bool playHand, out int bay)
        {
            var ward = catalog.GetEncounter(EncounterId).Rules.OfType<BayWardRuleDefinition>().Single();
            for (ulong candidate = 1; candidate <= SeedSearchLimit; candidate++)
            {
                // Moving the candle draws nothing, so the attempt targets the same bay with the candle on it.
                var probe = EncounterSession.CreateEncounter(catalog, EncounterId, candidate);
                var attempt = Commit(probe, playHand).Events;
                var target = attempt.OfType<DieDisturbedEvent>().Select(value => (int?)value.Bay).SingleOrDefault()
                    ?? attempt.OfType<DisturbanceBlockedEvent>().Select(value => (int?)value.Bay).SingleOrDefault();
                if (target == null) continue;
                var warded = EncounterSession.CreateEncounter(catalog, EncounterId, candidate);
                if (!warded.Execute(new InteractWithPieceAction(ward.PieceId, ward.InteractionId,
                    new MovePieceInput(PiecePosition.Bay(target.Value))), warded.Snapshot.Revision).Accepted) continue;
                var blocked = Commit(warded, playHand).Events.OfType<DisturbanceBlockedEvent>().SingleOrDefault();
                if (blocked == null || blocked.Bay != target.Value) continue;
                bay = target.Value;
                return candidate;
            }
            Assert.Fail("No searched seed is blocked by the candle on " + (playHand ? "Play Hand" : "a reroll"));
            bay = -1;
            return 0;
        }

        private static Resolution Commit(EncounterSession session, bool playHand) => playHand
            ? session.Execute(PlayHandAction.Instance, session.Snapshot.Revision)
            : session.Execute(new RerollDiceAction(RerollMask), session.Snapshot.Revision);

        private static string Rank(HandCategory category) => DiagnosticText.Humanize(category.ToString()).ToUpperInvariant();

        private static float Bottom(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[0].y;
        }

        private static float Top(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[1].y;
        }

        private static void ToggleRerollDice(SpiritEncounterController controller)
        {
            for (int die = 0; die < DiceValues.Count; die++)
                if ((RerollMask & (1 << die)) != 0) controller.ToggleDie(die);
            Assert.That(controller.SelectedMask, Is.EqualTo(RerollMask));
        }

        /** Opens the merchant encounter with a fixed session seed and waits for its opening roll. */
        private static IEnumerator Begin(System.Func<MechanicalCatalog, ulong> chooseSeed, System.Action<SpiritEncounterController> ready)
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = controller.JourneyCatalog.Entries.Single(value => !value.IsStoryOnly && value.EncounterId == EncounterId);
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            ContentReady.SeedJourneyPrerequisites(controller, journey.Id);
            controller.SelectEncounter(journey.EncounterId);
            yield return ContentReady.Routed(controller);
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();
            ulong seed = chooseSeed(controller.Catalog);
            controller.ConfigureSeedPolicy(true, seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            yield return null;
            ready(controller);
        }

        private static IEnumerator Complete(SpiritEncounterController controller)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False, "Playback completes.");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator Move(SpiritEncounterController controller, SpiritTablePieceView piece, PiecePosition position)
        {
            if (piece.Position != position)
            {
                controller.InteractWithPiece(piece.PieceId, piece.InteractionId, new MovePieceInput(position));
                float deadline = Time.realtimeSinceStartup + 10;
                while ((controller.IsBusy || piece.Position != position) && Time.realtimeSinceStartup < deadline) yield return null;
            }
            Assert.That(piece.Position, Is.EqualTo(position), piece.PieceId);
            Vector3 previous = piece.transform.position;
            int stable = 0;
            float settle = Time.realtimeSinceStartup + 5;
            while (stable < 3 && Time.realtimeSinceStartup < settle)
            {
                yield return null;
                stable = (piece.transform.position - previous).sqrMagnitude < 1e-6f ? stable + 1 : 0;
                previous = piece.transform.position;
            }
            Assert.That(stable, Is.EqualTo(3), piece.PieceId + " must settle.");
        }

        /**
         * Measures the quiet time between a commit and the poltergeist's first visible act: real time during which no
         * die is rolling, until the condition first holds.
         */
        private sealed class Watch
        {
            private readonly SpiritEncounterView view;
            private readonly Transform target;
            private readonly Quaternion rest;
            private readonly float height;

            internal Watch(SpiritEncounterView view, int bay)
            {
                this.view = view;
                target = ContentReady.Die(view.DiceTray, bay);
                rest = target.localRotation;
                height = target.localPosition.y;
            }

            internal float Idle { get; private set; }
            internal bool Started { get; private set; }
            internal bool TargetMoved => Quaternion.Angle(target.localRotation, rest) > .5f || Mathf.Abs(target.localPosition.y - height) > .001f;

            internal IEnumerator UntilDisturbance(System.Func<bool> acted)
            {
                float deadline = Time.realtimeSinceStartup + 15;
                while (!Started && Time.realtimeSinceStartup < deadline)
                {
                    float before = Time.realtimeSinceStartup;
                    yield return null;
                    if (acted()) Started = true;
                    else if (!view.DiceTray.IsRolling) Idle += Time.realtimeSinceStartup - before;
                }
            }
        }
    }
}
