using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Resolution = Dice.Engine.Resolution;

namespace Dice.Tests
{
    /**
     * Video capture of the poltergeist's disturbances in the merchant encounter: an opposite-face flip after a player
     * reroll, a forced reroll on Play Hand followed by its scoring, and an attempt repelled by the candle. Each case
     * searches session seeds against the shipped catalog for an opening whose rule draws produce it, then plays that
     * seed through the real controller, engine and playback. Frames of the portrait canvas are written at 30 per second
     * of real time, from half a second before the action until playback settles, under DICE_SPIRIT_CAPTURE_ROOT; it is
     * ignored without it. It is a capture, not a regression test; the behaviour it shows is tested by
     * DieInterferenceRulePlayTests and BayWardRulePlayTests.
     */
    [Explicit("Review capture; run it explicitly with DICE_SPIRIT_CAPTURE_ROOT set.")]
    [Category("Capture")]
    public sealed class MerchantDisturbanceCapturePlayTests
    {
        private const string EncounterId = "encounter.merchant.unquiet_cart";
        private const int Width = 540, Height = 960, FramesPerSecond = 30;
        private const int Lantern = 0;
        private const ulong SeedSearchLimit = 4000;
        private const float PreRoll = .5f, Tail = 1.5f, TailLimit = 4f;
        // Captured player rerolls take the two leftmost dice, so the poltergeist targets one of the kept three.
        private const int RerollMask = 0b00011;

        private enum Case { FlipAfterReroll, ForcedRerollOnPlayHand, BlockedByCandle }

        /** A searched seed, the opening it rolls, and the bay its first interference attempt targets. */
        private sealed class Plan
        {
            public ulong Seed;
            public DiceValues Opening;
            public int Bay;
            public bool Preferred;
            public string Summary;
        }

        [UnityTest]
        public IEnumerator FlipAfterPlayerRerollCapture() => CaptureCase(Case.FlipAfterReroll, "a-flip-after-reroll");

        [UnityTest]
        public IEnumerator ForcedRerollOnPlayHandCapture() => CaptureCase(Case.ForcedRerollOnPlayHand, "b-forced-reroll-play-hand");

        [UnityTest]
        public IEnumerator BlockedByCandleCapture() => CaptureCase(Case.BlockedByCandle, "c-blocked-by-candle");

        /**
         * Stills comparing the candle with its preserved source light (the wider footprint and response map, receiver
         * range 12) and its installed light: at rest at its neutral spot with the lantern on and off, and at the peak of
         * a repel flare on bay 3 with the earlier flare (uniform 1.35 scale, 2.2x light, no sparks) and the installed one.
         * Flicker is held still for both, so only the compared settings differ.
         */
        [UnityTest]
        public IEnumerator CandleComparisonCapture()
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Review capture only; set DICE_SPIRIT_CAPTURE_ROOT to write it.");
            root = Path.Combine(root, "merchant-disturbance", "d-candle-comparison");
            Assert.That(Directory.Exists(root), Is.False, "Capture output must be fresh.");
            Directory.CreateDirectory(root);

            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = controller.JourneyCatalog.Entries.Single(value => !value.IsStoryOnly && value.EncounterId == EncounterId);
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            ContentReady.SeedJourneyPrerequisites(controller, journey.Id);
            controller.SelectEncounter(journey.EncounterId);
            yield return ContentReady.Routed(controller);
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();
            controller.ConfigureSeedPolicy(true, "1");
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            var view = ContentReady.EncounterView(controller);
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            yield return null;
            var portrait = ContentReady.Portrait();
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(Width, Height);
            portrait.anchoredPosition = Vector2.zero;

            var activity = (EncounterAmbientActivity)typeof(SpiritEncounterView)
                .GetField("ambientActivity", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            activity?.Stop();
            var lighting = (IAmbientLightingTarget)view.TableLighting;
            var piece = view.TablePieces.Single();
            var asset = journey.TablePresentation.SceneLighting;
            SceneLightLayer candle = asset.Lights[1];
            Texture2D installedFootprint = candle.Contribution, installedResponse = candle.ResponseMap;
            float installedRange = candle.ReceiverRange;
            float[] flicker = asset.Lights.Select(light => light.FlickerAmount).ToArray();
            string sources = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/Reference/Art/"));
            Texture2D sourceFootprint = Load(sources + "HauntedMerchantCandleFootprintSource.png", false);
            Texture2D sourceResponse = Load(sources + "HauntedMerchantCandleResponseSource.png", true);
            const BindingFlags Field = BindingFlags.Instance | BindingFlags.NonPublic;
            string[] flareFields = { "flareScale", "flareHeightScale", "flareLightIntensity", "flareEffect" };
            var flare = flareFields.ToDictionary(name => name, name => typeof(SpiritTablePieceView).GetField(name, Field).GetValue(piece));
            var report = new StringBuilder("case d-candle-comparison\n");
            try
            {
                foreach (var light in asset.Lights) light.FlickerAmount = 0f;
                yield return Move(controller, piece, PiecePosition.Neutral);
                foreach (float lantern in new[] { 1f, 0f })
                    foreach (bool before in new[] { true, false })
                    {
                        candle.Contribution = before ? sourceFootprint : installedFootprint;
                        candle.ResponseMap = before ? sourceResponse : installedResponse;
                        candle.ReceiverRange = before ? 12f : installedRange;
                        lighting.SetLightIntensity(Lantern, lantern);
                        for (int frame = 0; frame < 3; frame++) yield return null;
                        string file = "rest-lantern-" + (lantern > 0f ? "on" : "off") + "-" + (before ? "before" : "after") + ".png";
                        yield return Still(Path.Combine(root, file));
                        report.Append(file).Append(": receiver range ").Append(candle.ReceiverRange).Append('\n');
                    }
                candle.Contribution = installedFootprint;
                candle.ResponseMap = installedResponse;
                candle.ReceiverRange = installedRange;
                lighting.SetLightIntensity(Lantern, 1f);
                yield return Move(controller, piece, PiecePosition.Bay(2));
                foreach (bool before in new[] { true, false })
                {
                    if (before)
                    {
                        // The earlier flare: a uniform 1.35 scale of the flame and 2.2x light, with no effect.
                        typeof(SpiritTablePieceView).GetField("flareScale", Field).SetValue(piece, 1.35f);
                        typeof(SpiritTablePieceView).GetField("flareHeightScale", Field).SetValue(piece, 1.35f);
                        typeof(SpiritTablePieceView).GetField("flareLightIntensity", Field).SetValue(piece, 2.2f);
                        typeof(SpiritTablePieceView).GetField("flareEffect", Field).SetValue(piece, null);
                    }
                    else foreach (var entry in flare) typeof(SpiritTablePieceView).GetField(entry.Key, Field).SetValue(piece, entry.Value);
                    for (int frame = 0; frame < 3; frame++) yield return null;
                    float peak = (float)typeof(SpiritTablePieceView).GetField("flareLightIntensity", Field).GetValue(piece);
                    piece.Flare();
                    deadline = Time.realtimeSinceStartup + 2;
                    while (piece.LightIntensity < 1f + (peak - 1f) * .97f && piece.IsFlaring && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    string file = "flare-peak-" + (before ? "before" : "after") + ".png";
                    report.Append(file).Append(": candle light ").Append(Format(piece.LightIntensity)).Append(" of peak ")
                        .Append(Format(peak)).Append(", flame scale ").Append(piece.FlareTarget.localScale.ToString("F3")).Append('\n');
                    yield return Still(Path.Combine(root, file));
                    deadline = Time.realtimeSinceStartup + 3;
                    while (piece.IsFlaring && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(piece.IsFlaring, Is.False);
                }
            }
            finally
            {
                candle.Contribution = installedFootprint;
                candle.ResponseMap = installedResponse;
                candle.ReceiverRange = installedRange;
                for (int i = 0; i < flicker.Length; i++) asset.Lights[i].FlickerAmount = flicker[i];
                foreach (var entry in flare) typeof(SpiritTablePieceView).GetField(entry.Key, Field).SetValue(piece, entry.Value);
                UnityEngine.Object.Destroy(sourceFootprint);
                UnityEngine.Object.Destroy(sourceResponse);
            }
            File.WriteAllText(Path.Combine(root, "case.txt"), report.ToString());
            TestContext.WriteLine(report + "in " + root);
            LogAssert.NoUnexpectedReceived();
        }

        /** Decodes a preserved PNG with the installed candle maps' import settings: bilinear, clamped, no mipmaps. */
        private static Texture2D Load(string path, bool linear)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
            Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        /** Renders the portrait canvas once through a capture camera and writes it as a 540 x 960 PNG. */
        private static IEnumerator Still(string path)
        {
            var canvas = ContentReady.PresentationRoot().GetComponentInChildren<Canvas>(true);
            var scaler = canvas.GetComponent<CanvasScaler>();
            bool scalerEnabled = scaler != null && scaler.enabled;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousPlane = canvas.planeDistance, previousScale = canvas.scaleFactor;
            var cameraRoot = new GameObject("Merchant Candle Capture Camera");
            var camera = cameraRoot.AddComponent<Camera>();
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D readback = null;
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                target.Create();
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.orthographic = true;
                camera.orthographicSize = Height / 2f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 200;
                camera.cullingMask = ~(1 << SpiritDiceTray.RenderLayer);
                camera.backgroundColor = new Color(.04f, .055f, .05f, 1);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.allowHDR = camera.allowMSAA = false;
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                if (scaler != null) scaler.enabled = false;
                canvas.scaleFactor = 1;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                readback = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                readback.Apply();
                File.WriteAllBytes(path, readback.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = previousPlane;
                if (scaler != null) scaler.enabled = scalerEnabled;
                if (scaler == null || !scalerEnabled) canvas.scaleFactor = previousScale;
                RenderTexture.active = previousTarget;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(cameraRoot);
                if (readback != null) UnityEngine.Object.Destroy(readback);
            }
        }

        private static IEnumerator CaptureCase(Case kind, string name)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Review capture only; set DICE_SPIRIT_CAPTURE_ROOT to write it.");
            root = Path.Combine(root, "merchant-disturbance", name);
            Assert.That(Directory.Exists(root), Is.False, "Capture output must be fresh.");
            Directory.CreateDirectory(Path.Combine(root, "frames"));

            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = controller.JourneyCatalog.Entries.Single(value => !value.IsStoryOnly && value.EncounterId == EncounterId);
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            ContentReady.SeedJourneyPrerequisites(controller, journey.Id);
            controller.SelectEncounter(journey.EncounterId);
            yield return ContentReady.Routed(controller);
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();

            var ward = controller.Catalog.GetEncounter(EncounterId).Rules.OfType<BayWardRuleDefinition>().Single();
            var plan = FindPlan(controller.Catalog, kind, ward);
            TestContext.WriteLine("Merchant disturbance capture " + name + ": " + plan.Summary);
            controller.ConfigureSeedPolicy(true, plan.Seed.ToString(CultureInfo.InvariantCulture));
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            var view = ContentReady.EncounterView(controller);
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.Snapshot.CurrentDice, Is.EqualTo(plan.Opening), "The encounter must roll the searched opening.");
            yield return null;
            var portrait = ContentReady.Portrait();
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(Width, Height);
            portrait.anchoredPosition = Vector2.zero;
            // A hidden editor reports no focus, which suspends the ambient activity as it does the ambient audio.
            if (!Application.isFocused) view.SendMessage("OnApplicationFocus", true);

            var piece = view.TablePieces.Single();
            Assert.That(piece.PieceId, Is.EqualTo(ward.PieceId));
            if (kind == Case.BlockedByCandle) yield return Move(controller, piece, PiecePosition.Bay(plan.Bay));
            if (kind != Case.ForcedRerollOnPlayHand)
                for (int die = 0; die < DiceValues.Count; die++)
                    if ((RerollMask & (1 << die)) != 0) controller.ToggleDie(die);
            Assert.That(controller.SelectedMask, Is.EqualTo(kind == Case.ForcedRerollOnPlayHand ? 0 : RerollMask));
            for (int frame = 0; frame < 3; frame++) yield return null;

            var lighting = (IAmbientLightingTarget)view.TableLighting;
            var target = view.DiceTray.Dice[plan.Bay];
            Quaternion targetRest = target.localRotation;
            float targetHeight = target.localPosition.y;
            var resolutions = new List<Resolution>();
            Action<Resolution> record = resolutions.Add;
            controller.Resolved += record;
            var recorder = new FrameRecorder(root, () => string.Join(",",
                controller.IsBusy ? 1 : 0, view.DiceTray.IsRolling ? 1 : 0,
                Format(lighting.GetLightIntensity(Lantern)), Format(Quaternion.Angle(target.localRotation, targetRest)),
                Format(target.localPosition.y - targetHeight), Format(piece.LightIntensity)));
            try
            {
                yield return recorder.Begin();
                while (recorder.Elapsed < PreRoll) yield return null;
                recorder.Mark("commit");
                if (kind == Case.ForcedRerollOnPlayHand) controller.PlayHand();
                else controller.Reroll();
                Assert.That(controller.IsBusy, Is.True, controller.Status.ToString());
                deadline = Time.realtimeSinceStartup + 30;
                while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(controller.IsBusy, Is.False, "Disturbance playback must complete.");
                recorder.Mark("settled");
                float settled = recorder.Elapsed;
                while (recorder.Elapsed < settled + Tail ||
                    (lighting.GetLightIntensity(Lantern) < .999f && recorder.Elapsed < settled + TailLimit))
                    yield return null;
            }
            finally
            {
                controller.Resolved -= record;
                recorder.End();
            }

            Assert.That(resolutions, Has.Count.EqualTo(1), "One committed action is captured.");
            var events = resolutions[0].Events;
            if (kind == Case.BlockedByCandle)
            {
                var blocked = events.OfType<DisturbanceBlockedEvent>().Single();
                Assert.That(blocked.Bay, Is.EqualTo(plan.Bay));
                Assert.That(blocked.ProtectingRuleId, Is.EqualTo(ward.Id));
                Assert.That(events.OfType<DieDisturbedEvent>(), Is.Empty);
            }
            else
            {
                var disturbed = events.OfType<DieDisturbedEvent>().Single();
                Assert.That(disturbed.Bay, Is.EqualTo(plan.Bay));
                Assert.That(disturbed.Kind, Is.EqualTo(kind == Case.FlipAfterReroll ? DieInterferenceKind.Flip : DieInterferenceKind.ForcedReroll));
                // A played hand scores the disturbed face; the tray then shows the next hand's opening roll.
                if (kind == Case.ForcedRerollOnPlayHand)
                    Assert.That(events.OfType<HandScoredEvent>().Single().Dice[plan.Bay], Is.EqualTo(disturbed.After));
                else Assert.That(view.DiceTray.GetVisibleFace(plan.Bay), Is.EqualTo(disturbed.After), "The disturbed die shows its new face.");
            }
            File.WriteAllText(Path.Combine(root, "case.txt"), "case " + name + "\n" + plan.Summary + "\nevents " +
                string.Join(", ", events.Select(Describe)) + "\n" + recorder.Report + "\n");
            TestContext.WriteLine("Merchant disturbance capture " + name + ": " + recorder.Report + " in " + root);
            LogAssert.NoUnexpectedReceived();
        }

        /** The first seed whose interference produces the case, preferring one whose result changes the hand rank. */
        private static Plan FindPlan(MechanicalCatalog catalog, Case kind, BayWardRuleDefinition ward)
        {
            Plan fallback = null;
            for (ulong seed = 1; seed <= SeedSearchLimit; seed++)
            {
                var plan = Trial(catalog, kind, ward, seed);
                if (plan == null) continue;
                if (plan.Preferred) return plan;
                if (fallback == null) fallback = plan;
            }
            Assert.That(fallback, Is.Not.Null, "No searched seed produces " + kind);
            return fallback;
        }

        private static Plan Trial(MechanicalCatalog catalog, Case kind, BayWardRuleDefinition ward, ulong seed)
        {
            var session = EncounterSession.CreateEncounter(catalog, EncounterId, seed);
            var opening = session.Snapshot.CurrentDice;
            var plan = new Plan { Seed = seed, Opening = opening };
            if (kind == Case.ForcedRerollOnPlayHand)
            {
                var played = session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
                var disturbed = played.Events.OfType<DieDisturbedEvent>().SingleOrDefault();
                if (disturbed == null || disturbed.Kind != DieInterferenceKind.ForcedReroll || disturbed.After == disturbed.Before)
                    return null;
                var scored = played.Events.OfType<HandScoredEvent>().Single().Dice;
                plan.Bay = disturbed.Bay;
                plan.Preferred = HandEvaluator.Evaluate(opening) != HandEvaluator.Evaluate(scored);
                plan.Summary = "seed " + seed + ", opening " + Faces(opening) + ", Play Hand; bay " + (plan.Bay + 1) +
                    " forced reroll " + disturbed.Before + " -> " + disturbed.After + ", scored " + Faces(scored) + " as " +
                    HandEvaluator.Evaluate(scored) + " (undisturbed " + HandEvaluator.Evaluate(opening) + ")";
                return plan;
            }

            var rerolled = session.Execute(new RerollDiceAction(RerollMask), session.Snapshot.Revision);
            var rolled = rerolled.Events.OfType<DiceRolledEvent>().Single();
            var disturbance = rerolled.Events.OfType<DieDisturbedEvent>().SingleOrDefault();
            if (disturbance == null) return null;
            plan.Bay = disturbance.Bay;
            if (kind == Case.FlipAfterReroll)
            {
                if (disturbance.Kind != DieInterferenceKind.Flip) return null;
                var flipped = With(rolled.After, disturbance.Bay, disturbance.After);
                plan.Preferred = HandEvaluator.Evaluate(rolled.After) != HandEvaluator.Evaluate(flipped);
                plan.Summary = "seed " + seed + ", opening " + Faces(opening) + ", reroll dice 1-2 -> " + Faces(rolled.After) +
                    "; bay " + (plan.Bay + 1) + " flipped " + disturbance.Before + " -> " + disturbance.After + ", hand " +
                    Faces(flipped) + " as " + HandEvaluator.Evaluate(flipped) + " (undisturbed " + HandEvaluator.Evaluate(rolled.After) + ")";
                return plan;
            }

            // Moving the candle draws nothing, so the same rule draws target the bay it now wards.
            var warded = EncounterSession.CreateEncounter(catalog, EncounterId, seed);
            var moved = warded.Execute(new InteractWithPieceAction(ward.PieceId, ward.InteractionId,
                new MovePieceInput(PiecePosition.Bay(plan.Bay))), warded.Snapshot.Revision);
            if (!moved.Accepted) return null;
            var attempted = warded.Execute(new RerollDiceAction(RerollMask), warded.Snapshot.Revision);
            var blocked = attempted.Events.OfType<DisturbanceBlockedEvent>().SingleOrDefault();
            if (blocked == null || blocked.Bay != plan.Bay) return null;
            plan.Preferred = true;
            plan.Summary = "seed " + seed + ", opening " + Faces(opening) + ", candle on bay " + (plan.Bay + 1) +
                ", reroll dice 1-2 -> " + Faces(attempted.Events.OfType<DiceRolledEvent>().Single().After) + "; " + blocked.Kind +
                " on bay " + (plan.Bay + 1) + " blocked by " + blocked.ProtectingRuleId;
            return plan;
        }

        private static DiceValues With(DiceValues dice, int bay, int face)
        {
            var faces = new int[DiceValues.Count];
            for (int die = 0; die < faces.Length; die++) faces[die] = die == bay ? face : dice[die];
            return new DiceValues(faces[0], faces[1], faces[2], faces[3], faces[4]);
        }

        private static string Faces(DiceValues dice)
            => string.Join(" ", Enumerable.Range(0, DiceValues.Count).Select(die => dice[die].ToString(CultureInfo.InvariantCulture)));

        private static string Format(float value) => value.ToString("F3", CultureInfo.InvariantCulture);

        private static string Describe(RuleEvent ruleEvent)
        {
            switch (ruleEvent)
            {
                case DiceRolledEvent rolled: return "rolled " + rolled.Kind + " " + Faces(rolled.After);
                case DieDisturbedEvent disturbed:
                    return disturbed.Kind + " bay " + (disturbed.Bay + 1) + " " + disturbed.Before + " -> " + disturbed.After;
                case DisturbanceBlockedEvent blocked: return "blocked " + blocked.Kind + " bay " + (blocked.Bay + 1);
                case HandScoredEvent scored:
                    return "scored " + Faces(scored.Dice) + " " + scored.Score.Category + " " + scored.Score.HandScore;
                default: return ruleEvent.GetType().Name;
            }
        }

        private static IEnumerator Move(SpiritEncounterController controller, SpiritTablePieceView piece, PiecePosition position)
        {
            var placement = piece.GetComponent<SpiritTablePiecePosition>();
            if (placement.Position != position)
            {
                controller.InteractWithPiece(piece.PieceId, piece.InteractionId, new MovePieceInput(position));
                float deadline = Time.realtimeSinceStartup + 10;
                while ((controller.IsBusy || placement.Position != position) && Time.realtimeSinceStartup < deadline) yield return null;
            }
            Assert.That(placement.Position, Is.EqualTo(position), piece.PieceId);
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
         * Renders the portrait canvas through a capture camera into a 540 x 960 target every frame and reads back the
         * first rendered frame of each 1/30 s tick of real time. Readbacks are asynchronous and PNG encoding runs off
         * the main thread, so recording does not slow the playback it records. A tick with no rendered frame is
         * counted as skipped; frames.csv records each written tick with the caller's metrics.
         */
        private sealed class FrameRecorder
        {
            private readonly string root;
            private readonly string frames;
            private readonly Func<string> metrics;
            private readonly StringBuilder csv = new StringBuilder("tick,time,busy,rolling,lantern,die_angle,die_lift,candle_light,mark\n");
            private readonly List<Task> writes = new List<Task>();
            private Canvas canvas;
            private CanvasScaler scaler;
            private bool scalerEnabled;
            private RenderMode previousMode;
            private Camera previousCamera;
            private float previousPlane, previousScale;
            private GameObject cameraRoot;
            private Camera camera;
            private RenderTexture target;
            private double start;
            private int lastTick = -1, written, skipped, failed;
            private string mark = string.Empty;
            private bool recording;

            internal FrameRecorder(string root, Func<string> metrics)
            {
                this.root = root;
                frames = Path.Combine(root, "frames");
                this.metrics = metrics;
            }

            internal float Elapsed => (float)(Time.realtimeSinceStartupAsDouble - start);

            internal string Report => written + " frames at " + FramesPerSecond + " fps over " + (lastTick + 1) + " ticks, " +
                skipped + " ticks without a rendered frame, " + failed + " failed readbacks";

            internal void Mark(string value) => mark = mark.Length == 0 ? value : mark + " " + value;

            internal IEnumerator Begin()
            {
                canvas = ContentReady.PresentationRoot().GetComponentInChildren<Canvas>(true);
                scaler = canvas.GetComponent<CanvasScaler>();
                scalerEnabled = scaler != null && scaler.enabled;
                previousMode = canvas.renderMode;
                previousCamera = canvas.worldCamera;
                previousPlane = canvas.planeDistance;
                previousScale = canvas.scaleFactor;
                cameraRoot = new GameObject("Merchant Disturbance Capture Camera");
                camera = cameraRoot.AddComponent<Camera>();
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.transform.position = new Vector3(0, 0, -100);
                camera.orthographic = true;
                camera.orthographicSize = Height / 2f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 200;
                camera.cullingMask = ~(1 << SpiritDiceTray.RenderLayer);
                camera.backgroundColor = new Color(.04f, .055f, .05f, 1);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.allowHDR = camera.allowMSAA = false;
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                if (scaler != null) scaler.enabled = false;
                canvas.scaleFactor = 1;
                Canvas.ForceUpdateCanvases();
                yield return null;
                yield return null;
                start = Time.realtimeSinceStartupAsDouble;
                recording = true;
                RenderPipelineManager.endCameraRendering += Rendered;
            }

            private void Rendered(ScriptableRenderContext context, Camera rendered)
            {
                if (!recording || rendered != camera) return;
                int tick = (int)Math.Floor((Time.realtimeSinceStartupAsDouble - start) * FramesPerSecond);
                if (tick <= lastTick) return;
                skipped += tick - lastTick - 1;
                lastTick = tick;
                csv.Append(tick).Append(',').Append((tick / (double)FramesPerSecond).ToString("F3", CultureInfo.InvariantCulture))
                    .Append(',').Append(metrics()).Append(',').Append(mark).Append('\n');
                mark = string.Empty;
                string path = Path.Combine(frames, "frame_" + tick.ToString("D5", CultureInfo.InvariantCulture) + ".png");
                AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, request =>
                {
                    if (request.hasError) { ++failed; return; }
                    byte[] pixels = request.GetData<byte>().ToArray();
                    ++written;
                    writes.Add(Task.Run(() => File.WriteAllBytes(path,
                        ImageConversion.EncodeArrayToPNG(pixels, GraphicsFormat.R8G8B8A8_SRGB, Width, Height))));
                });
            }

            internal void End()
            {
                if (recording)
                {
                    recording = false;
                    RenderPipelineManager.endCameraRendering -= Rendered;
                    AsyncGPUReadback.WaitAllRequests();
                    Task.WaitAll(writes.ToArray());
                    File.WriteAllText(Path.Combine(root, "frames.csv"), csv.ToString());
                }
                if (canvas != null)
                {
                    canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = previousPlane;
                    if (scaler != null) scaler.enabled = scalerEnabled;
                    if (scaler == null || !scalerEnabled) canvas.scaleFactor = previousScale;
                }
                if (camera != null) camera.targetTexture = null;
                if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
                if (cameraRoot != null) UnityEngine.Object.Destroy(cameraRoot);
            }
        }
    }
}
