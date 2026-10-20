using System;
using System.Collections;
using System.IO;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dice.Tests
{
    public sealed class DiagnosticScenePlayTests
    {
        [UnityTest]
        public IEnumerator SceneRunsDiceRewardsInspectionAndSaveResume()
        {
            yield return SceneManager.LoadSceneAsync("DiagnosticEncounter");
            yield return ContentReady.Diagnostic();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<DiagnosticEncounterController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<DiagnosticEncounterView>().enabled, Is.True);
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo("encounter.standard"));
            Assert.That(controller.Snapshot.StageCount, Is.EqualTo(25));
            var artifactRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "diagnostic-file-tests", Guid.NewGuid().ToString("N")));
            string savePath = Path.Combine(artifactRoot, "playmode-encounter.json");
            controller.Initialize(controller.Catalog, "encounter.standard", 60, savePath);
            yield return null;
            yield return CaptureIfRequested("initial");

            controller.ToggleDie(0);
            controller.ToggleDie(3);
            Assert.That(controller.SelectedMask, Is.EqualTo(9));
            controller.Reroll();
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(controller.Preview.Score.HandScore, Is.EqualTo(152));
            yield return null;
            controller.PlayHand();
            Assert.That(controller.Snapshot.RemainingThreshold, Is.EqualTo(8));
            Assert.That(controller.Preview.Score.HandScore, Is.EqualTo(81));
            yield return null;
            controller.PlayHand();
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier));
            Assert.That(controller.Preview, Is.Null);
            yield return null;
            yield return CaptureIfRequested("modifier-reward");

            controller.ChooseReward(1, 2);
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingHandUpgrade));
            yield return null;
            yield return CaptureIfRequested("hand-upgrade");
            controller.ChooseReward(2, 7);
            Assert.That(controller.Snapshot.StageIndex, Is.EqualTo(1));
            Assert.That(controller.Snapshot.Modifiers.Count, Is.EqualTo(1));
            Assert.That(controller.ModifierInspections.Count, Is.EqualTo(1));
            yield return null;
            yield return CaptureIfRequested("modifier-inspection");

            controller.Save();
            Assert.That(File.Exists(savePath), Is.True, controller.StatusMessage);
            var saved = EncounterSaveFileStore.Load(savePath);
            controller.SelectAllDice();
            controller.Reroll();
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(saved.Revision + 1));
            yield return null;
            controller.Load();
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(saved.Revision));
            Assert.That(controller.Snapshot.CurrentDice.ToArray(), Is.EqualTo(saved.CurrentDice));
            Assert.That(controller.SelectedMask, Is.Zero);
            yield return null;
            controller.Abandon();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Abandoned));
            Assert.That(controller.Preview, Is.Null);
            yield return null;
            yield return CaptureIfRequested("completed");
            controller.StartNew("0");
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.Playing));
            Assert.That(controller.Snapshot.StageIndex, Is.Zero);
            Assert.That(controller.Snapshot.Revision, Is.Zero);
            yield return null;
            LogAssert.NoUnexpectedReceived();
            TestContext.WriteLine("Retained diagnostic save: " + savePath);
        }

        private static IEnumerator CaptureIfRequested(string name)
        {
            string root = Environment.GetEnvironmentVariable("DICE_DIAGNOSTIC_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) yield break;
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, name + ".png");
            Assert.That(File.Exists(path), Is.False, "Each validation run must use a fresh capture directory.");
            for (int i = 0; i < 3; ++i) yield return null;
            ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 240 && !File.Exists(path); ++i) yield return null;
            Assert.That(File.Exists(path), Is.True, "Screenshot was not produced: " + path);
            TestContext.WriteLine("Diagnostic screenshot: " + path);
        }
    }
}
