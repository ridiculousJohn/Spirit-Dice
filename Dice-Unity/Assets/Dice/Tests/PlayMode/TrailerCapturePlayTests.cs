using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Case = Dice.Tests.MerchantDisturbancePlans.Case;
using Resolution = Dice.Engine.Resolution;

namespace Dice.Tests
{
    /**
     * Captures for the internal portrait trailer, rendered from the editor at 1080 x 1920 (twice the 540 x 960 authoring
     * size). TrailerScaleCheck shows whether the game holds up at that size: it writes the Sunken Larder's arrival picture
     * (the journey's first beat) at 1080 x 1920, then enters each encounter table with the fixtures' seed and writes its
     * table after the opening roll at 1080 x 1920 and 540 x 960, named by table. scale-check.txt records each table's tray
     * render texture and every shown texture the 1080 x 1920 output magnifies. It writes under
     * DICE_SPIRIT_CAPTURE_ROOT/scale-check and is ignored without it; it is a capture, not a regression test.
     *
     * The shots record the trailer's raw footage from real gameplay through the real controller, engine and playback: one
     * folder per shot under DICE_SPIRIT_CAPTURE_ROOT, each a PortraitCapture.Recorder recording at 1080 x 1920 and 30 frames
     * per second of real time with the game's mixed audio (frames/, frames.csv, audio.wav, audio-sync.txt), and shot.txt
     * naming what it shows, its seed, the marks it made and the recording's report. Marks in frames.csv's mark column name
     * the key moments (roll start and land, each selection, commits, disturbances, binds, withering, scoring steps and the
     * total) on the first frame that shows them. Each shot runs a little longer than the trailer uses. Journeys are chosen by
     * the rule their encounter has, beats by their kind and scene.
     */
    [Explicit("Review capture; run it explicitly with DICE_SPIRIT_CAPTURE_ROOT set.")]
    [Category("Capture")]
    public sealed class TrailerCapturePlayTests
    {
        private const int TrailerScale = 2, AuthoringScale = 1;
        // Real seconds the table rests after its opening roll before it is captured, so the host has settled into idle.
        private const float TableSettleSeconds = 1f;
        // Shown textures magnified by more than this at the trailer scale are listed in the report.
        private const float ReportedMagnification = 1.01f;
        // Real seconds of a picture beat's drift and of the main menu, and the menu's rest first while its buttons ease to their states.
        private const float PictureSeconds = 9f, MenuSeconds = 7f, MenuSettle = .5f;
        // Real seconds a recorded shot rests before its first action, between dice selected one at a time, after the last
        // selection before the commit, and after its last moment.
        private const float LeadIn = 1.2f, SelectInterval = .5f, BeforeCommit = .9f, Tail = 4f;
        // Real seconds after a recorded beat's motion and narration have settled, and after a montage table's opening roll lands.
        private const float BeatTail = 3f, MontageAfterLanding = 4f;
        // Real seconds the fox shot runs after its total is revealed, through the total's hold and what the hand leads to.
        private const float FoxTail = 7f;
        // Degrees a kept die must turn in one frame to count as turning, which marks the poltergeist's flip.
        private const float TurningDegreesPerFrame = .05f;
        // The fox shot plays a hand of at least this rank, preferring one of the second.
        private const HandCategory FoxMinimumRank = HandCategory.ThreeOfAKind, FoxPreferredRank = HandCategory.FullHouse;
        private const string ShotMetrics = "busy,rolling,selected,beat,visible_beat,host_state";

        [UnityTest]
        public IEnumerator MerchantRoadShot() => PictureShot("merchant-road", controller =>
            ContentReady.JourneyWithRule<BayWardRuleDefinition>(controller));

        [UnityTest]
        public IEnumerator LarderArrivalShot() => PictureShot("larder-arrival", controller =>
            ContentReady.JourneyWithRule<RotRuleDefinition>(controller));

        /** The merchant's greeting, "Looking for someone? ...": his journey's first Arrival beat at his table, where he arrives. */
        [UnityTest]
        public IEnumerator MerchantGreetingShot() => ArrivalShot("merchant-greeting", controller =>
            ContentReady.JourneyWithRule<BayWardRuleDefinition>(controller), true);

        /** "The bark splits": the Bound Tree journey's Arrival beat, where the spirit's face splits out of the bark. */
        [UnityTest]
        public IEnumerator BoundTreeArrivalShot() => ArrivalShot("bound-tree-arrival", controller =>
            ContentReady.JourneyWithRule<DieBindingRuleDefinition>(controller), false);

        /**
         * The Last Lantern's core loop: the opening roll lands on a pair, the three other dice are selected one at a time and
         * rerolled (the first reroll of a hand is free), and Play Hand scores the hand through every step to its total.
         */
        [UnityTest]
        public IEnumerator FoxCoreLoopShot()
        {
            string root = Fresh("fox-core-loop");
            yield return ContentReady.LoadApplication();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = ContentReady.JourneyWithRule<FirstRerollFreeRuleDefinition>(controller);
            var plan = FindFoxPlan(controller.Catalog, journey.Encounters.First().Identity);
            yield return ContentReady.ShowInvitation(controller, journey);
            var shot = new Shot(root, controller, journey.name + ", " + plan.Summary);
            var view = shot.Prepare();
            var scoring = new ScoreCues(shot, view);
            yield return shot.Begin();
            try
            {
                shot.WatchTable(view);
                shot.Mark("accept");
                yield return ContentReady.AcceptEncounter(controller, plan.Seed);
                yield return shot.Until(() => controller.Snapshot != null && !controller.IsBusy, "The opening roll did not settle.");
                Assert.That(controller.Snapshot.CurrentDice, Is.EqualTo(plan.Opening), "The encounter must roll the searched opening.");
                yield return shot.For(LeadIn);
                yield return SelectOneByOne(shot, controller, plan.RerollMask);
                yield return shot.For(BeforeCommit);
                int rerolls = controller.Snapshot.RerollsRemaining;
                shot.Mark("reroll");
                controller.Reroll();
                yield return shot.Until(() => !controller.IsBusy, "The reroll did not settle.");
                Assert.That(controller.Snapshot.CurrentDice, Is.EqualTo(plan.Rerolled), "The reroll must land the searched dice.");
                shot.Note("Rerolls remaining before the reroll " + rerolls + ", after it " + controller.Snapshot.RerollsRemaining + ".");
                yield return shot.For(LeadIn);
                var played = Commit(controller, controller.PlayHand);
                shot.Mark("play-hand");
                var scored = played.Events.OfType<HandScoredEvent>().Single();
                Assert.That(scored.Score.Category, Is.EqualTo(plan.Played), "The hand must score the searched rank.");
                scoring.Expect(new SpiritScoringStepBuilder(controller.Catalog).BuildHand(scored, 0, 0, view.TablePresentation.RulePresentations));
                yield return shot.Until(() => scoring.TotalShown, "The scoring did not reach its total.");
                yield return shot.For(FoxTail);
            }
            finally
            {
                shot.End();
            }
            shot.Note("Scoring steps cued " + scoring.Cued + " of " + scoring.Expected + ".");
            shot.Write();
            yield return ContentReady.Idle(controller, ContentReady.SequenceTimeout);
            LogAssert.NoUnexpectedReceived();
        }

        /** A player reroll of the two leftmost dice, after which the poltergeist flips a kept die; the merchant reacts and it giggles. */
        [UnityTest]
        public IEnumerator MerchantFlipShot() => MerchantDisturbanceShot("merchant-flip", Case.FlipAfterReroll);

        /** The candle set on a bay and an attempt on that bay repelled with the flare, the sparks and the grumble. */
        [UnityTest]
        public IEnumerator MerchantBlockShot() => MerchantDisturbanceShot("merchant-block", Case.BlockedByCandle);

        /** The opening roll marks a die; a reroll of two other dice binds it, the roots closing with the flare and the groan. */
        [UnityTest]
        public IEnumerator BoundTreeBindShot()
        {
            string root = Fresh("bound-tree-bind");
            yield return ContentReady.LoadApplication();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = ContentReady.JourneyWithRule<DieBindingRuleDefinition>(controller);
            yield return ContentReady.ShowInvitation(controller, journey);
            var shot = new Shot(root, controller, journey.name + ", seed " + ContentReady.Seed);
            var view = shot.Prepare();
            yield return shot.Begin();
            try
            {
                shot.WatchTable(view);
                shot.Mark("accept");
                yield return ContentReady.AcceptEncounter(controller, ContentReady.Seed);
                for (int bay = 0; bay < DiceValues.Count; ++bay) WatchBinding(shot, view, bay);
                yield return shot.Until(() => controller.Snapshot != null && !controller.IsBusy, "The opening roll did not settle.");
                int marked = controller.Snapshot.Binding().MarkedMask;
                Assert.That(DiceValues.CountBays(marked), Is.EqualTo(1), "The opening roll marks one die.");
                int rerolled = 0;
                for (int bay = 0; bay < DiceValues.Count && DiceValues.CountBays(rerolled) < 2; ++bay)
                    if ((marked & (1 << bay)) == 0) rerolled |= 1 << bay;
                shot.Note("Opening " + MerchantDisturbancePlans.Faces(controller.Snapshot.CurrentDice) + ", marked bay " +
                    (Bay(marked) + 1) + ", rerolling bays " + Bays(rerolled) + ".");
                yield return shot.For(LeadIn);
                yield return SelectOneByOne(shot, controller, rerolled);
                yield return shot.For(BeforeCommit);
                shot.Mark("reroll");
                controller.Reroll();
                yield return shot.Until(() => !controller.IsBusy, "The reroll did not settle.");
                Assert.That(controller.Snapshot.Binding().BoundMask & marked, Is.EqualTo(marked), "The marked die binds.");
                shot.Note("After the reroll " + MerchantDisturbancePlans.Faces(controller.Snapshot.CurrentDice) + ", bound bays " +
                    Bays(controller.Snapshot.Binding().BoundMask) + ", next mark bays " + Bays(controller.Snapshot.Binding().MarkedMask) + ".");
                yield return shot.For(Tail);
            }
            finally
            {
                shot.End();
            }
            shot.Write();
            LogAssert.NoUnexpectedReceived();
        }

        /** A kept 1 shows the wither warning; a reroll of two other dice rots it, and it withers with her smile, chuckle and the fire's flicker. */
        [UnityTest]
        public IEnumerator LarderWitherShot()
        {
            string root = Fresh("larder-wither");
            yield return ContentReady.LoadApplication();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = ContentReady.JourneyWithRule<RotRuleDefinition>(controller);
            var plan = FindWitherPlan(controller.Catalog, journey.Encounters.First().Identity);
            yield return ContentReady.ShowInvitation(controller, journey);
            var shot = new Shot(root, controller, journey.name + ", " + plan.Summary);
            var view = shot.Prepare();
            yield return ContentReady.AcceptEncounter(controller, plan.Seed);
            yield return ContentReady.Idle(controller);
            Assert.That(controller.Snapshot.CurrentDice, Is.EqualTo(plan.Opening), "The encounter must roll the searched opening.");
            int one = Bay(plan.WitherMask);
            var mould = ContentReady.DieVisual(view, one);
            Assert.That(mould.GrowthTarget, Is.GreaterThan(0f), "The kept 1 shows the wither warning.");
            yield return shot.Begin();
            try
            {
                shot.WatchTable(view);
                shot.Mark("wither-warning-bay-" + (one + 1));
                shot.Watch(() => mould.GrowthTarget >= 1f, "wither-bay-" + (one + 1));
                yield return shot.For(LeadIn);
                yield return SelectOneByOne(shot, controller, plan.RerollMask);
                yield return shot.For(BeforeCommit);
                shot.Mark("reroll");
                controller.Reroll();
                yield return shot.Until(() => !controller.IsBusy, "The reroll did not settle.");
                Assert.That(controller.Snapshot.Rot().WitheredMask, Is.EqualTo(plan.WitherMask), "The kept 1 withers.");
                shot.Note("After the reroll " + MerchantDisturbancePlans.Faces(controller.Snapshot.CurrentDice) + ", withered bays " +
                    Bays(controller.Snapshot.Rot().WitheredMask) + ".");
                yield return shot.For(Tail);
            }
            finally
            {
                shot.End();
            }
            shot.Write();
            LogAssert.NoUnexpectedReceived();
        }

        /** The fox, owl and moth tables from just before the opening roll lands, one sub-shot folder each under the shot. */
        [UnityTest]
        public IEnumerator ChapterOneMontageShot()
        {
            string montage = Fresh("chapter-one-montage");
            yield return ContentReady.LoadApplication();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var tables = new[]
            {
                ("fox", ContentReady.JourneyWithRule<FirstRerollFreeRuleDefinition>(controller)),
                ("owl", ContentReady.JourneyWithRule<DistinctFacesMultiplierRuleDefinition>(controller)),
                ("moth", ContentReady.JourneyWithRule<CyclingFavoredFaceMultiplierRuleDefinition>(controller))
            };
            foreach (var (name, journey) in tables)
            {
                yield return ContentReady.ShowInvitation(controller, journey);
                var shot = new Shot(Path.Combine(montage, name), controller, journey.name + ", seed " + ContentReady.Seed);
                var view = shot.Prepare();
                yield return ContentReady.AcceptEncounter(controller, ContentReady.Seed);
                // The table shows the invitation until play begins, so recording starts with the table's opening.
                yield return shot.Begin();
                try
                {
                    shot.WatchTable(view);
                    yield return shot.Until(() => controller.Snapshot != null && !controller.IsBusy && !view.DiceTray.IsRolling,
                        "The opening roll did not settle.");
                    Assert.That(shot.HasMark("roll-land"), Is.True, "The recording starts before the opening roll lands.");
                    yield return shot.For(MontageAfterLanding);
                }
                finally
                {
                    shot.End();
                }
                shot.Write();
            }
            LogAssert.NoUnexpectedReceived();
        }

        /** The main menu as the game opens on it, as a possible end-card backdrop. */
        [UnityTest]
        public IEnumerator MainMenuShot()
        {
            string root = Fresh("main-menu");
            yield return SceneManager.LoadSceneAsync("Application");
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var campaign = controller.GetComponent<SpiritCampaignController>();
            Assert.That(campaign, Is.Not.Null, "The application runs the campaign beside the encounter controller.");
            yield return ContentReady.WaitUntil(() => campaign.IsInitialized, "Startup did not initialize the main menu.");
            yield return ContentReady.Routed(controller);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
            Assert.That(ContentReady.FrontendView(controller).MainMenu.Root.gameObject.activeInHierarchy, Is.True);
            yield return new WaitForSecondsRealtime(MenuSettle);
            var shot = new Shot(root, controller, "main menu at startup");
            shot.Prepare();
            yield return shot.Begin();
            try
            {
                yield return shot.For(MenuSeconds);
            }
            finally
            {
                shot.End();
            }
            shot.Write();
            LogAssert.NoUnexpectedReceived();
        }

        /** A journey's first beat, its picture scene, recorded for PictureSeconds once its transition has finished. */
        private static IEnumerator PictureShot(string folder, Func<SpiritEncounterController, EncounterJourneyAsset> select)
        {
            string root = Fresh(folder);
            yield return ContentReady.LoadApplication();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = select(controller);
            Assert.That(journey.SceneAt(0), Is.InstanceOf<PictureSceneAsset>(), journey.name + " opens on a picture scene.");
            yield return ContentReady.OpenJourney(controller, journey);
            yield return ShowBeat(controller, 0);
            var beat = journey.Beats[0];
            var shot = new Shot(root, controller, journey.name + " beat 0 (" + beat.GetType().Name + " \"" + beat.Title + "\", " +
                journey.SceneAt(0).name + ")");
            shot.Prepare();
            yield return shot.Begin();
            try
            {
                yield return shot.For(PictureSeconds);
            }
            finally
            {
                shot.End();
            }
            shot.Write();
            LogAssert.NoUnexpectedReceived();
        }

        /**
         * The journey's first Arrival beat that plays the host's Arrival, at a table scene when atTable is set: the beat before
         * it is shown and settled, then recording starts, the journey advances and the beat plays its motion and narration.
         */
        private static IEnumerator ArrivalShot(string folder, Func<SpiritEncounterController, EncounterJourneyAsset> select, bool atTable)
        {
            string root = Fresh(folder);
            yield return ContentReady.LoadApplication();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = select(controller);
            int index = Enumerable.Range(0, journey.Beats.Count).First(beat => journey.Beats[beat] is ArrivalBeat arrival &&
                arrival.HostState == SpiritPresentationState.Arrival && (!atTable || journey.SceneAt(beat) is EncounterTablePresentationAsset));
            Assert.That(index, Is.GreaterThan(0), "The arrival beat follows another beat.");
            yield return ContentReady.OpenJourney(controller, journey);
            yield return ShowBeat(controller, index - 1);
            var beat = journey.Beats[index];
            var shot = new Shot(root, controller, journey.name + " beat " + index + " (" + beat.GetType().Name + " \"" + beat.Title +
                "\", " + journey.SceneAt(index).name + "): " + beat.Text);
            var view = shot.Prepare();
            yield return shot.Begin();
            try
            {
                shot.Watch(() => view.HostPortrait.State == SpiritPresentationState.Arrival, "host-arrival", "host-arrival-end");
                yield return shot.For(LeadIn);
                shot.Mark("advance");
                controller.AdvanceJourney();
                yield return shot.Until(() => view.JourneyView.VisibleBeatIndex == index, "The arrival beat was not shown.");
                shot.Mark("beat-shown");
                yield return shot.Until(() => !view.JourneyView.IsAnimating && !view.JourneyView.IsEasingNarrative,
                    "The arrival beat's motion did not settle.");
                shot.Mark("beat-settled");
                Assert.That(view.HostPortrait.State, Is.EqualTo(SpiritPresentationState.Arrival), "The host plays its Arrival.");
                yield return shot.For(BeatTail);
            }
            finally
            {
                shot.End();
            }
            shot.Write();
            LogAssert.NoUnexpectedReceived();
        }

        /** The merchant table from the searched seed's opening: the case's player reroll and its disturbance, from rest to rest. */
        private static IEnumerator MerchantDisturbanceShot(string folder, Case kind)
        {
            string root = Fresh(folder);
            yield return ContentReady.LoadApplication();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = ContentReady.JourneyWithRule<BayWardRuleDefinition>(controller);
            var encounterId = journey.Encounters.First().Identity;
            var ward = controller.Catalog.GetEncounter(encounterId).Rules.OfType<BayWardRuleDefinition>().Single();
            var plan = MerchantDisturbancePlans.FindPlan(controller.Catalog, encounterId, kind, ward);
            yield return ContentReady.ShowInvitation(controller, journey);
            var shot = new Shot(root, controller, journey.name + ", " + plan.Summary);
            var view = shot.Prepare();
            yield return ContentReady.AcceptEncounter(controller, plan.Seed);
            yield return ContentReady.Idle(controller);
            Assert.That(controller.Snapshot.CurrentDice, Is.EqualTo(plan.Opening), "The encounter must roll the searched opening.");
            var piece = view.TablePieces.Single();
            Assert.That(piece.Piece.Identity, Is.EqualTo(ward.PieceId));
            Resolution resolution;
            yield return shot.Begin();
            try
            {
                shot.WatchTable(view);
                WatchTurning(shot, view.DiceTray.Dice[plan.Bay], "flip-bay-" + (plan.Bay + 1));
                shot.Watch(() => piece.IsFlaring, "candle-flare", "candle-flare-end");
                yield return shot.For(LeadIn);
                if (kind == Case.BlockedByCandle)
                {
                    shot.Mark("candle-move");
                    yield return MerchantDisturbancePlans.Move(controller, piece, PiecePosition.Bay(plan.Bay));
                    shot.Mark("candle-on-bay-" + (plan.Bay + 1));
                    yield return shot.For(LeadIn);
                }
                yield return SelectOneByOne(shot, controller, ContentReady.RerollMask);
                yield return shot.For(BeforeCommit);
                resolution = Commit(controller, controller.Reroll);
                shot.Mark("reroll");
                yield return shot.Until(() => !controller.IsBusy, "Disturbance playback did not complete.");
                yield return shot.For(Tail);
            }
            finally
            {
                shot.End();
            }
            var events = resolution.Events;
            if (kind == Case.BlockedByCandle)
                Assert.That(events.OfType<DisturbanceBlockedEvent>().Single().Bay, Is.EqualTo(plan.Bay));
            else
            {
                var disturbed = events.OfType<DieDisturbedEvent>().Single();
                Assert.That(disturbed.Bay, Is.EqualTo(plan.Bay));
                Assert.That(disturbed.Kind, Is.EqualTo(DieInterferenceKind.Flip));
            }
            shot.Write();
            LogAssert.NoUnexpectedReceived();
        }

        /** Commits an action through the controller and returns the resolution it was accepted with. */
        private static Resolution Commit(SpiritEncounterController controller, Action commit)
        {
            Resolution resolution = null;
            Action<Resolution> record = value => resolution = value;
            controller.Resolved += record;
            try { commit(); }
            finally { controller.Resolved -= record; }
            Assert.That(resolution, Is.Not.Null, controller.Status.ToString());
            return resolution;
        }

        /** Selects the masked dice one at a time from the left, as a player taps them, marking each. */
        private static IEnumerator SelectOneByOne(Shot shot, SpiritEncounterController controller, int mask)
        {
            Assert.That(controller.SelectedMask, Is.Zero);
            for (int bay = 0; bay < DiceValues.Count; ++bay)
            {
                if ((mask & (1 << bay)) == 0) continue;
                controller.ToggleDie(bay);
                shot.Mark("select-bay-" + (bay + 1));
                yield return shot.For(SelectInterval);
            }
            Assert.That(controller.SelectedMask, Is.EqualTo(mask));
        }

        /** Marks the die's binding: its mark appearing and its roots closing. */
        private static void WatchBinding(Shot shot, SpiritEncounterView view, int bay)
        {
            DieBindingVisual roots = null;
            Func<DieBindingVisual> visual = () => roots != null ? roots : roots = ContentReady.DieVisual(view, bay);
            shot.Watch(() => visual() != null && visual().MarkTarget > 0f && visual().GrowthTarget <= 0f, "mark-bay-" + (bay + 1));
            shot.Watch(() => visual() != null && visual().GrowthTarget > 0f, "bind-bay-" + (bay + 1), "release-bay-" + (bay + 1));
        }

        /** Marks the start and end of the die turning, as a kept die does only when it is flipped. */
        private static void WatchTurning(Shot shot, Transform die, string name)
        {
            Quaternion previous = die.localRotation;
            shot.Watch(() =>
            {
                bool turning = Quaternion.Angle(previous, die.localRotation) > TurningDegreesPerFrame;
                previous = die.localRotation;
                return turning;
            }, name, name + "-end");
        }

        private static int Bay(int mask)
        {
            for (int bay = 0; bay < DiceValues.Count; ++bay)
                if ((mask & (1 << bay)) != 0) return bay;
            throw new ArgumentException("No die in the mask.", nameof(mask));
        }

        private static string Bays(int mask)
            => string.Join(" ", Enumerable.Range(0, DiceValues.Count).Where(bay => (mask & (1 << bay)) != 0).Select(bay => (bay + 1).ToString(CultureInfo.InvariantCulture)));

        /** A fox encounter seed whose opening is one pair, the three other dice it rerolls, and the hand they play. */
        private sealed class FoxPlan
        {
            public ulong Seed;
            public DiceValues Opening, Rerolled;
            public int RerollMask;
            public HandCategory Played;
            public string Summary;
        }

        /**
         * The first seed whose opening is one pair and whose reroll of the other three dice plays a hand of at least
         * FoxMinimumRank, preferring one of at least FoxPreferredRank, so the scoring reads strong.
         */
        private static FoxPlan FindFoxPlan(MechanicalCatalog catalog, EncounterDefinition.Id encounterId)
        {
            FoxPlan fallback = null;
            for (ulong seed = 1; seed <= ContentReady.RuleSeedSearchLimit; seed++)
            {
                var session = EncounterSession.CreateEncounter(catalog, encounterId, seed);
                var opening = session.Snapshot.CurrentDice;
                if (HandEvaluator.Evaluate(HandDice.Full(opening)) != HandCategory.Pair) continue;
                int singles = 0;
                for (int die = 0; die < DiceValues.Count; die++)
                    if (Enumerable.Range(0, DiceValues.Count).Count(other => opening[other] == opening[die]) == 1) singles |= 1 << die;
                session.Execute(new RerollDiceAction(singles), session.Snapshot.Revision);
                var rerolled = session.Snapshot.CurrentDice;
                var scored = session.Execute(PlayHandAction.Instance, session.Snapshot.Revision).Events.OfType<HandScoredEvent>().Single();
                if (scored.Score.Category < FoxMinimumRank) continue;
                var plan = new FoxPlan
                {
                    Seed = seed, Opening = opening, Rerolled = rerolled, RerollMask = singles, Played = scored.Score.Category,
                    Summary = "seed " + seed + ", opening " + MerchantDisturbancePlans.Faces(opening) + ", reroll bays " + Bays(singles) +
                        " -> " + MerchantDisturbancePlans.Faces(rerolled) + ", Play Hand scores " + scored.Score.Category + " for " +
                        scored.Score.HandScore
                };
                if (scored.Score.Category >= FoxPreferredRank) return plan;
                if (fallback == null) fallback = plan;
            }
            Assert.That(fallback, Is.Not.Null, "No searched seed plays a hand of at least " + FoxMinimumRank);
            return fallback;
        }

        /** A Sunken Larder seed whose opening has one 1, the two other dice rerolled, and the die that withers. */
        private sealed class WitherPlan
        {
            public ulong Seed;
            public DiceValues Opening;
            public int RerollMask, WitherMask;
            public string Summary;
        }

        /** The first seed whose opening has exactly one 1, which withers when the two leftmost other dice are rerolled. */
        private static WitherPlan FindWitherPlan(MechanicalCatalog catalog, EncounterDefinition.Id encounterId)
        {
            for (ulong seed = 1; seed <= ContentReady.RuleSeedSearchLimit; seed++)
            {
                var session = EncounterSession.CreateEncounter(catalog, encounterId, seed);
                var opening = session.Snapshot.CurrentDice;
                int ones = 0;
                for (int die = 0; die < DiceValues.Count; die++)
                    if (opening[die] == DiceValues.MinimumFace) ones |= 1 << die;
                if (DiceValues.CountBays(ones) != 1) continue;
                int rerolled = 0;
                for (int die = 0; die < DiceValues.Count && DiceValues.CountBays(rerolled) < 2; die++)
                    if ((ones & (1 << die)) == 0) rerolled |= 1 << die;
                session.Execute(new RerollDiceAction(rerolled), session.Snapshot.Revision);
                if (session.Snapshot.Rot().WitheredMask != ones) continue;
                return new WitherPlan
                {
                    Seed = seed, Opening = opening, RerollMask = rerolled, WitherMask = ones,
                    Summary = "seed " + seed + ", opening " + MerchantDisturbancePlans.Faces(opening) + ", reroll bays " + Bays(rerolled) +
                        " -> " + MerchantDisturbancePlans.Faces(session.Snapshot.CurrentDice) + ", bay " + (Bay(ones) + 1) + " withers"
                };
            }
            Assert.Fail("No searched seed opens with exactly one 1 that withers.");
            return null;
        }

        /**
         * One recorded shot: the recorder at the trailer scale with the shot metrics, the moments it watches for and marks
         * while the test waits through it, the marks it made with their times, and notes written to shot.txt.
         */
        private sealed class Shot
        {
            private readonly SpiritEncounterController controller;
            private readonly PortraitCapture.Recorder recorder;
            private readonly List<Action> watches = new List<Action>();
            private readonly List<string> marks = new List<string>();
            private readonly StringBuilder notes = new StringBuilder();
            private readonly string root, title;

            internal Shot(string root, SpiritEncounterController controller, string title)
            {
                this.root = root;
                this.controller = controller;
                this.title = title;
                recorder = new PortraitCapture.Recorder(root, TrailerScale, ShotMetrics, Metrics);
            }

            /**
             * Lays the shown screen out at the authoring size and gives a shown encounter screen the focus the test run's
             * editor lacks; returns the encounter screen, or null at the frontend.
             */
            internal SpiritEncounterView Prepare()
            {
                PortraitCapture.FitPortrait();
                var view = ContentReady.Router(controller).EncounterView;
                bool told = view != null && PortraitCapture.PresentAsFocused(view);
                Note("Application.isFocused " + Application.isFocused + ", Application.runInBackground " + Application.runInBackground +
                    (view == null ? "; no encounter screen shown." : told ? "; the encounter screen and its audio were told they have focus."
                    : "; the application has focus."));
                return view;
            }

            internal IEnumerator Begin() => recorder.Begin();

            internal void End() => recorder.End();

            internal void Note(string line) => notes.AppendLine(line);

            internal void Mark(string name)
            {
                recorder.Mark(name);
                marks.Add(name);
                notes.AppendLine("mark " + name + " at " + recorder.Elapsed.ToString("F3", CultureInfo.InvariantCulture) + " s");
            }

            internal bool HasMark(string name) => marks.Contains(name);

            /** Marks on when the state turns true and off, when given, when it turns false, checked each frame the test waits. */
            internal void Watch(Func<bool> state, string on, string off = null)
            {
                bool last = state();
                watches.Add(() =>
                {
                    bool now = state();
                    if (now == last) return;
                    last = now;
                    if (now) Mark(on);
                    else if (off != null) Mark(off);
                });
            }

            /** Watches a check that marks moments itself, each frame the test waits. */
            internal void Watch(Action check) => watches.Add(check);

            /** Marks the table's dice rolls and the host's reactions. */
            internal void WatchTable(SpiritEncounterView view)
            {
                Watch(() => view.DiceTray.IsRolling, "roll-start", "roll-land");
                Watch(() => view.HostPortrait.IsReacting, "host-reaction", "host-reaction-end");
            }

            internal IEnumerator For(float seconds)
            {
                float end = recorder.Elapsed + seconds;
                while (recorder.Elapsed < end)
                {
                    Poll();
                    yield return null;
                }
                Poll();
            }

            internal IEnumerator Until(Func<bool> condition, string description, float timeout = ContentReady.SequenceTimeout)
            {
                float deadline = Time.realtimeSinceStartup + timeout;
                while (!condition() && Time.realtimeSinceStartup < deadline)
                {
                    Poll();
                    yield return null;
                }
                Poll();
                Assert.That(condition(), Is.True, description);
            }

            private void Poll()
            {
                foreach (var watch in watches) watch();
            }

            internal void Write()
            {
                string text = title + "\n" + notes + recorder.Report + "\n";
                File.WriteAllText(Path.Combine(root, "shot.txt"), text);
                TestContext.WriteLine("Trailer shot " + root + ": " + text);
            }

            private string Metrics()
            {
                var view = ContentReady.Router(controller).EncounterView;
                return string.Join(",", controller.IsBusy ? 1 : 0, view != null && view.DiceTray.IsRolling ? 1 : 0, controller.SelectedMask,
                    controller.BeatIndex, view != null ? view.JourneyView.VisibleBeatIndex : -1,
                    view != null ? view.HostPortrait.State.ToString() : "");
            }
        }

        /**
         * Marks each scoring step of a played hand as the game plays its score cue, which score playback plays once per step,
         * naming the step that reveals the total. The cue voices are read from the start, so a step cued in the frame of the
         * commit is still seen; the steps are named once the played hand is known.
         */
        private sealed class ScoreCues
        {
            private readonly AudioSource[] voices;
            private readonly bool[] playing;
            private readonly float[] times;
            private readonly AudioClip cue;
            private IReadOnlyList<SpiritScoringStep> steps;

            internal int Cued { get; private set; }
            internal int Expected => steps?.Count ?? 0;
            internal bool TotalShown { get; private set; }

            internal ScoreCues(Shot shot, SpiritEncounterView view)
            {
                var audio = view.GetComponentsInChildren<SpiritEncounterAudio>(true).Single();
                cue = audio.Theme != null ? audio.Theme.score : null;
                Assert.That(cue, Is.Not.Null, "The table's audio theme has a score cue.");
                voices = view.GetComponentsInChildren<AudioSource>(true);
                playing = new bool[voices.Length];
                times = new float[voices.Length];
                Read(null);
                shot.Watch(() => Read(shot));
            }

            internal void Expect(IReadOnlyList<SpiritScoringStep> built) => steps = built;

            private void Read(Shot shot)
            {
                for (int i = 0; i < voices.Length; ++i)
                {
                    var voice = voices[i];
                    // Only a voice playing the cue is timed: a voice without a clip reports no time.
                    bool now = voice.isPlaying && voice.clip == cue;
                    float time = now ? voice.time : 0f;
                    bool started = now && (!playing[i] || time < times[i]);
                    playing[i] = now;
                    times[i] = time;
                    if (!started || shot == null) continue;
                    ++Cued;
                    var step = steps != null && Cued <= steps.Count ? steps[Cued - 1] : null;
                    string name = "score-" + Cued + (step == null ? "" : step.RevealsTotal ? "-total" : step.Charm != null ? "-charm" :
                        step.RuleCue ? "-rule" : "");
                    shot.Mark(name);
                    if (step != null && step.RevealsTotal) TotalShown = true;
                }
            }
        }

        [UnityTest]
        public IEnumerator TrailerScaleCheck()
        {
            string root = Fresh("scale-check");
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.JourneyAtBoard();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var report = new StringBuilder();

            var larder = ContentReady.JourneyWithRule<RotRuleDefinition>(controller);
            Assert.That(larder.SceneAt(0), Is.InstanceOf<PictureSceneAsset>(), "The Sunken Larder journey opens on its arrival picture.");
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            ContentReady.SeedJourneyPrerequisites(controller, larder);
            controller.SelectEncounter(larder);
            yield return ContentReady.Routed(controller);
            yield return ShowBeat(controller, 0);
            PortraitCapture.FitPortrait();
            string picture = larder.SceneAt(0).name + "-beat0";
            yield return Still(root, picture, TrailerScale);
            report.AppendLine(picture);
            AppendMagnified(report, TrailerScale);

            var journeys = new[]
            {
                ContentReady.JourneyWithRule<FirstRerollFreeRuleDefinition>(controller),
                ContentReady.JourneyWithRule<DistinctFacesMultiplierRuleDefinition>(controller),
                ContentReady.JourneyWithRule<CyclingFavoredFaceMultiplierRuleDefinition>(controller),
                ContentReady.JourneyWithRule<BayWardRuleDefinition>(controller),
                ContentReady.JourneyWithRule<DieBindingRuleDefinition>(controller),
                larder
            };
            foreach (var journey in journeys)
            {
                yield return ContentReady.EnterEncounter(controller, journey, ContentReady.Seed);
                var view = ContentReady.EncounterView(controller);
                yield return ContentReady.WaitUntil(() => !view.DiceTray.IsRolling, "The opening roll did not settle.");
                yield return new WaitForSecondsRealtime(TableSettleSeconds);
                PortraitCapture.FitPortrait();
                string table = journey.TableSceneAt(journey.IndexOfEncounter(journey.Encounters.First().Identity)).name;
                yield return Still(root, table, TrailerScale);
                yield return Still(root, table, AuthoringScale);
                var tray = view.DiceTray.Texture;
                report.AppendLine(table).Append("  tray render texture ").Append(tray.width).Append(" x ").Append(tray.height)
                    .Append(", MSAA ").Append(tray.antiAliasing).Append(", ").Append(tray.filterMode).AppendLine(" filtering");
                AppendMagnified(report, TrailerScale);
            }
            File.WriteAllText(Path.Combine(root, "scale-check.txt"), report.ToString());
            TestContext.WriteLine(report + "in " + root);
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator Still(string root, string name, int scale)
        {
            string path = Path.Combine(root, name + "-" + PortraitCapture.Width * scale + "x" + PortraitCapture.Height * scale + ".png");
            yield return PortraitCapture.Still(path, scale);
            TestContext.WriteLine("Trailer capture: " + path);
        }

        /**
         * Lists every shown graphic under the presentation root whose texture covers fewer texels than the output pixels it
         * fills at the scale: its source texels, the pixels it is shown across and the magnification. Sliced and tiled
         * images are left out, as their borders are drawn at a fixed size.
         */
        private static void AppendMagnified(StringBuilder report, int scale)
        {
            var root = ContentReady.PresentationRoot();
            foreach (var graphic in root.GetComponentsInChildren<Graphic>())
            {
                if (!graphic.isActiveAndEnabled || graphic.canvasRenderer.GetInheritedAlpha() <= 0) continue;
                Vector2 texels;
                if (graphic is RawImage raw && raw.texture != null)
                    texels = new Vector2(raw.texture.width * Mathf.Abs(raw.uvRect.width), raw.texture.height * Mathf.Abs(raw.uvRect.height));
                else if (graphic is Image image && image.sprite != null &&
                    (image.type == Image.Type.Simple || image.type == Image.Type.Filled))
                    texels = image.sprite.rect.size;
                else continue;
                var canvas = graphic.canvas.rootCanvas.transform;
                Vector2 relative = new Vector2(graphic.transform.lossyScale.x / canvas.lossyScale.x,
                    graphic.transform.lossyScale.y / canvas.lossyScale.y);
                Vector2 pixels = Vector2.Scale(graphic.rectTransform.rect.size, relative) * scale;
                float magnification = Mathf.Max(Mathf.Abs(pixels.x) / texels.x, Mathf.Abs(pixels.y) / texels.y);
                if (!(magnification > ReportedMagnification)) continue;
                report.Append("  ").Append(PathOf(graphic.transform, root.transform)).Append(": ")
                    .Append(Format(texels.x)).Append(" x ").Append(Format(texels.y)).Append(" texels shown across ")
                    .Append(Format(Mathf.Abs(pixels.x))).Append(" x ").Append(Format(Mathf.Abs(pixels.y))).Append(" px, magnified ")
                    .Append(magnification.ToString("F2", CultureInfo.InvariantCulture)).AppendLine("x");
            }
        }

        private static string PathOf(Transform target, Transform root)
        {
            string path = target.name;
            for (var parent = target.parent; parent != null && parent != root; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }

        private static string Format(float value) => value.ToString("F0", CultureInfo.InvariantCulture);

        private static string Fresh(string folder)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Review capture only; set DICE_SPIRIT_CAPTURE_ROOT to write it.");
            root = Path.Combine(root, folder);
            Assert.That(Directory.Exists(root), Is.False, "Capture output must be fresh.");
            Directory.CreateDirectory(root);
            return root;
        }

        /** Advances the journey to the beat at the index and waits until the view shows it with its motion complete. */
        private static IEnumerator ShowBeat(SpiritEncounterController controller, int index)
        {
            Assert.That(index, Is.InRange(0, controller.SelectedJourney.Beats.Count - 1));
            string id = controller.SelectedJourney.name + " beat " + index;
            float deadline = Time.realtimeSinceStartup + 15;
            while (controller.BeatIndex != index && Time.realtimeSinceStartup < deadline)
            {
                controller.AdvanceJourney();
                yield return null;
            }
            Assert.That(controller.BeatIndex, Is.EqualTo(index), id);
            yield return ContentReady.Routed(controller);
            var view = ContentReady.EncounterView(controller);
            deadline = Time.realtimeSinceStartup + 15;
            while ((view.JourneyView.VisibleBeatIndex != controller.BeatIndex || view.JourneyView.IsAnimating ||
                view.JourneyView.IsEasingNarrative) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(view.JourneyView.VisibleBeatIndex, Is.EqualTo(controller.BeatIndex), id);
            Assert.That(view.JourneyView.IsAnimating, Is.False, id);
            Assert.That(view.JourneyView.IsEasingNarrative, Is.False, id);
            yield return null;
        }
    }
}
