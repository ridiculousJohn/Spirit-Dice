using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Resolution = Dice.Engine.Resolution;
using Case = Dice.Tests.MerchantDisturbancePlans.Case;

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
        private const int Lantern = 0;
        private const float PreRoll = .5f, Tail = 1.5f, TailLimit = 4f;

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
            yield return ContentReady.JourneyAtBoard();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = ContentReady.JourneyWithRule<BayWardRuleDefinition>(controller);
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            ContentReady.SeedJourneyPrerequisites(controller, journey);
            controller.SelectEncounter(journey);
            yield return ContentReady.Routed(controller);
            while (!(controller.SelectedJourney.Beats[controller.BeatIndex] is InvitationBeat))
                controller.AdvanceJourney();
            controller.ConfigureSeedPolicy(true, 1);
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            var view = ContentReady.EncounterView(controller);
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            yield return null;
            PortraitCapture.FitPortrait();

            var activity = (EncounterAmbientActivity)typeof(SpiritEncounterView)
                .GetField("ambientActivity", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            activity?.Stop();
            var lighting = (IAmbientLightingTarget)view.TableLighting;
            var piece = view.TablePieces.Single();
            var asset = journey.TableSceneAt(0).SceneLighting;
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
                yield return MerchantDisturbancePlans.Move(controller, piece, PiecePosition.Neutral);
                foreach (float lantern in new[] { 1f, 0f })
                    foreach (bool before in new[] { true, false })
                    {
                        candle.Contribution = before ? sourceFootprint : installedFootprint;
                        candle.ResponseMap = before ? sourceResponse : installedResponse;
                        candle.ReceiverRange = before ? 12f : installedRange;
                        lighting.SetLightIntensity(Lantern, lantern);
                        for (int frame = 0; frame < 3; frame++) yield return null;
                        string file = "rest-lantern-" + (lantern > 0f ? "on" : "off") + "-" + (before ? "before" : "after") + ".png";
                        yield return PortraitCapture.Still(Path.Combine(root, file), 1);
                        report.Append(file).Append(": receiver range ").Append(candle.ReceiverRange).Append('\n');
                    }
                candle.Contribution = installedFootprint;
                candle.ResponseMap = installedResponse;
                candle.ReceiverRange = installedRange;
                lighting.SetLightIntensity(Lantern, 1f);
                yield return MerchantDisturbancePlans.Move(controller, piece, PiecePosition.Bay(2));
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
                    yield return PortraitCapture.Still(Path.Combine(root, file), 1);
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

        private static IEnumerator CaptureCase(Case kind, string name)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Review capture only; set DICE_SPIRIT_CAPTURE_ROOT to write it.");
            root = Path.Combine(root, "merchant-disturbance", name);
            Assert.That(Directory.Exists(root), Is.False, "Capture output must be fresh.");
            Directory.CreateDirectory(root);

            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.JourneyAtBoard();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = ContentReady.JourneyWithRule<BayWardRuleDefinition>(controller);
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            ContentReady.SeedJourneyPrerequisites(controller, journey);
            controller.SelectEncounter(journey);
            yield return ContentReady.Routed(controller);
            while (!(controller.SelectedJourney.Beats[controller.BeatIndex] is InvitationBeat))
                controller.AdvanceJourney();

            var encounterId = journey.Encounters.First().Identity;
            var ward = controller.Catalog.GetEncounter(encounterId).Rules.OfType<BayWardRuleDefinition>().Single();
            var plan = MerchantDisturbancePlans.FindPlan(controller.Catalog, encounterId, kind, ward);
            TestContext.WriteLine("Merchant disturbance capture " + name + ": " + plan.Summary);
            controller.ConfigureSeedPolicy(true, plan.Seed);
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            var view = ContentReady.EncounterView(controller);
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.Snapshot.CurrentDice, Is.EqualTo(plan.Opening), "The encounter must roll the searched opening.");
            yield return null;
            PortraitCapture.FitPortrait();
            // A hidden editor reports no focus, which suspends the ambient activity as it does the ambient audio.
            if (!Application.isFocused) view.SendMessage("OnApplicationFocus", true);

            var piece = view.TablePieces.Single();
            Assert.That(piece.Piece.Identity, Is.EqualTo(ward.PieceId));
            if (kind == Case.BlockedByCandle) yield return MerchantDisturbancePlans.Move(controller, piece, PiecePosition.Bay(plan.Bay));
            if (kind != Case.ForcedRerollOnPlayHand)
                for (int die = 0; die < DiceValues.Count; die++)
                    if ((ContentReady.RerollMask & (1 << die)) != 0) controller.ToggleDie(die);
            Assert.That(controller.SelectedMask, Is.EqualTo(kind == Case.ForcedRerollOnPlayHand ? 0 : ContentReady.RerollMask));
            for (int frame = 0; frame < 3; frame++) yield return null;

            var lighting = (IAmbientLightingTarget)view.TableLighting;
            var target = view.DiceTray.Dice[plan.Bay];
            Quaternion targetRest = target.localRotation;
            float targetHeight = target.localPosition.y;
            var resolutions = new List<Resolution>();
            Action<Resolution> record = resolutions.Add;
            controller.Resolved += record;
            var recorder = new PortraitCapture.Recorder(root, 1, "busy,rolling,lantern,die_angle,die_lift,candle_light", () => string.Join(",",
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
                Assert.That(blocked.ProtectingRuleId, Is.EqualTo(ward.Identity));
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

        private static string Format(float value) => value.ToString("F3", CultureInfo.InvariantCulture);

        private static string Describe(RuleEvent ruleEvent)
        {
            switch (ruleEvent)
            {
                case DiceRolledEvent rolled: return "rolled " + rolled.Kind + " " + MerchantDisturbancePlans.Faces(rolled.After);
                case DieDisturbedEvent disturbed:
                    return disturbed.Kind + " bay " + (disturbed.Bay + 1) + " " + disturbed.Before + " -> " + disturbed.After;
                case DisturbanceBlockedEvent blocked: return "blocked " + blocked.Kind + " bay " + (blocked.Bay + 1);
                case HandScoredEvent scored:
                    return "scored " + MerchantDisturbancePlans.Faces(scored.Dice) + " " + scored.Score.Category + " " + scored.Score.HandScore;
                default: return ruleEvent.GetType().Name;
            }
        }
    }
}
