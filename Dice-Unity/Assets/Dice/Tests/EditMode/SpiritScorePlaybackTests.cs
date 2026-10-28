using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Resolution = Dice.Engine.Resolution;

namespace Dice.Tests
{
    /**
     * Score playback of a test encounter's resolutions on a fabricated host, a synthetic HUD and a test tray rig. The
     * playback is stepped by hand: nested routines are entered, realtime waits pass at once, and a routine that waits a
     * frame is cut short there as if its time had passed.
     */
    public sealed class SpiritScorePlaybackTests
    {
        private const string EncounterId = "encounter.test.playback";
        private Scene preview;
        private TestTrayRig rig;
        private SyntheticUi ui;
        private SyntheticCharms charms;
        private SpiritGameplayHud hud;
        private SpiritEncounterAudio audio;

        [SetUp]
        public void SetUp()
        {
            preview = EditorSceneManager.NewPreviewScene();
            rig = new TestTrayRig(preview);
            ui = new SyntheticUi();
            charms = new SyntheticCharms();
            hud = Hud();
            audio = Audio();
        }

        [TearDown]
        public void TearDown()
        {
            rig.Dispose();
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            ui.Dispose();
            charms.Dispose();
        }

        /**
         * Checks a reroll shows no hand rank at any point of its playback, and a played hand shows its rank only during
         * its scoring, after the dice are counted and before the final score, never after the next hand's roll / fails if
         * a rank shows after a roll or outside the played hand's scoring.
         */
        [Test]
        public void PlaybackShowsTheRankOnlyWhileAPlayedHandIsScored()
        {
            var encounter = new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 3, 3) });
            var catalog = new MechanicalCatalog("test.playback", TestContent.Hands(), new[] { encounter });
            var session = EncounterSession.CreateEncounter(catalog, EncounterId, 7);
            var host = new PlaybackHost(new Controls(catalog, charms.For()));
            var playback = new SpiritScorePlayback(host, hud, rig.StartDice(), rig.CreateHistoryTray(), audio, null, null);
            var ranks = Enum.GetValues(typeof(HandCategory)).Cast<HandCategory>().Select(Rank).ToArray();

            var reroll = session.Execute(new RerollDiceAction(0b00011), session.Snapshot.Revision);
            Assert.That(reroll.Accepted, Is.True, reroll.Rejection.ToString());
            Assert.That(reroll.Events.OfType<DiceRolledEvent>(), Is.Not.Empty);
            var rerolled = Run(playback.Play(reroll, 0));
            Assert.That(rerolled.Intersect(ranks), Is.Empty, "A reroll shows no hand rank.");

            var played = session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
            Assert.That(played.Accepted, Is.True, played.Rejection.ToString());
            Assert.That(played.Events.OfType<DiceRolledEvent>(), Is.Not.Empty, "The played hand is followed by the next hand's roll.");
            string rank = Rank(played.Events.OfType<HandScoredEvent>().Single().Score.Category);
            var scored = Run(playback.Play(played, 0));
            int first = scored.IndexOf(rank);
            int last = scored.LastIndexOf(rank);
            Assert.That(first, Is.GreaterThan(0), "The played hand's scoring shows its rank.");
            Assert.That(scored.Take(first), Has.Member("COUNTING DICE"), "The dice are counted before the rank shows.");
            Assert.That(scored.Take(first).Intersect(ranks), Is.Empty, "No rank shows before the played hand is scored.");
            Assert.That(scored.Skip(first).Take(last - first + 1), Is.All.EqualTo(rank));
            Assert.That(scored.Skip(last + 1).First(), Is.EqualTo("FINAL SCORE"));
            Assert.That(scored.Skip(last + 1).Intersect(ranks), Is.Empty, "No rank shows after the hand is scored.");
            Assert.That(scored.Last(), Is.Empty, "The next hand's roll shows no rank.");
            Assert.That(host.Finished, Is.EqualTo(new[] { reroll, played }));
        }

        private static string Rank(HandCategory category) => DiagnosticText.Humanize(category.ToString()).ToUpperInvariant();

        /** Runs the playback to its end and returns the HUD's rank label as it stood at every yield. */
        private List<string> Run(IEnumerator routine)
        {
            var labels = new List<string>();
            var running = new Stack<IEnumerator>();
            running.Push(routine);
            for (int steps = 0; running.Count > 0; ++steps)
            {
                if (steps > 10000) throw new InvalidOperationException("The playback did not end.");
                var current = running.Peek();
                if (!current.MoveNext())
                {
                    running.Pop();
                    continue;
                }
                labels.Add(hud.CategoryLabel.text);
                if (current.Current is CustomYieldInstruction) continue;
                if (current.Current is IEnumerator nested) running.Push(nested);
                else if (running.Count > 1)
                {
                    running.Pop();
                    (current as IDisposable)?.Dispose();
                }
            }
            return labels;
        }

        /** A HUD holding only what score playback writes: its labels, panels, counters, rule badges and action buttons. */
        private SpiritGameplayHud Hud()
        {
            var created = ui.Add<SpiritGameplayHud>(ui.Root, "Gameplay HUD");
            Transform parent = created.transform;
            SyntheticUi.Set(created, "<PlayingPanel>k__BackingField", ui.Rect(parent, "Playing"));
            foreach (string label in new[] { "SelectionLabel", "CategoryLabel", "ScoreDetail", "BaseScoreLabel", "MultiplierLabel",
                "HandScoreLabel", "TargetLabel", "TargetCaption", "HandsLabel", "RerollsLabel", "ResourcesLabel" })
                SyntheticUi.Set(created, "<" + label + ">k__BackingField", ui.Add<Text>(parent, label));
            SyntheticUi.Set(created, "<ModifierEmblem>k__BackingField", ui.Add<Image>(parent, "Emblem"));
            SyntheticUi.Set(created, "<ModifierActivationIcon>k__BackingField", ui.Add<Image>(parent, "Activation Icon"));
            foreach (string highlight in new[] { "BaseScoreHighlight", "MultiplierHighlight", "RerollsHighlight", "HandsHighlight" })
            {
                string path = "<" + highlight + ">k__BackingField.";
                SyntheticUi.Set(created, path + "Label", ui.Add<Text>(parent, highlight + " Label"));
                SyntheticUi.Set(created, path + "Caption", ui.Add<Text>(parent, highlight + " Caption"));
                SyntheticUi.Set(created, path + "Icon", ui.Add<Image>(parent, highlight + " Icon"));
            }
            foreach (string action in new[] { "reroll", "play" })
            {
                var badge = ui.Add<CanvasGroup>(parent, action + " rule badge");
                SyntheticUi.Set(created, action + "EncounterRuleGroup", badge);
                SyntheticUi.Set(created, action + "EncounterRuleLabel", ui.Add<Text>(badge.transform, "Label"));
                SyntheticUi.Set(created, action + "EncounterRuleIcon", ui.Add<Image>(badge.transform, "Icon"));
                var button = ui.Add<SpiritUiButtonVisual>(parent, action);
                button.targetGraphic = button.gameObject.AddComponent<Image>();
                SyntheticUi.Set(created, action == "reroll" ? "<RerollButton>k__BackingField" : "<PlayButton>k__BackingField", button);
            }
            return created;
        }

        /** Encounter audio with its authored voices; without a theme it plays nothing and times rolls by its defaults. */
        private SpiritEncounterAudio Audio()
        {
            var created = ui.Add<SpiritEncounterAudio>(ui.Root, "Encounter Audio");
            AudioSource Voice(string name) => ui.Add<AudioSource>(created.transform, name);
            using var serialized = new SerializedObject(created);
            serialized.FindProperty("ambienceSource").objectReferenceValue = Voice("Ambience");
            serialized.FindProperty("creakSource").objectReferenceValue = Voice("Creak");
            serialized.FindProperty("rollSource").objectReferenceValue = Voice("Roll");
            foreach (string voices in new[] { "cueSources", "propSources" })
            {
                var list = serialized.FindProperty(voices);
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = Voice(voices);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return created;
        }

        /** A playback host with the encounter's controls and no host portrait, journey, rule cards or ambient activity. */
        private sealed class PlaybackHost : ISpiritScorePlaybackHost
        {
            public readonly List<Resolution> Finished = new List<Resolution>();

            public PlaybackHost(ISpiritEncounterControls controls) => Controls = controls;

            public ISpiritEncounterControls Controls { get; }
            public IEncounterJourneyControls Journey => null;
            public SpiritHostPortrait Host => null;
            public IReadOnlyList<EncounterRulePresentationAsset> RuleCards => Array.Empty<EncounterRulePresentationAsset>();
            public EncounterRulePresentationAsset HudRuleCard => null;
            public EncounterAmbientActivity Ambient => null;
            public EncounterRulePresenters RulePresenters => null;
            public void SetDiceSelection(int mask) { }
            public void HideRewardsAndEnding() { }
            public void SetOpeningOpacity(float opacity) { }
            public void ShowEnding() { }
            public void RecordScore(ScoreBreakdown score) { }
            public void FinishPresentation(Resolution resolution) => Finished.Add(resolution);
        }

        /** Controls that supply the encounter's catalog and charm presentation; playback calls no action. */
        private sealed class Controls : ISpiritEncounterControls
        {
            public Controls(MechanicalCatalog catalog, ModifierPresentationCatalog charms)
            {
                Catalog = catalog;
                ModifierPresentation = charms;
            }

            public EncounterSnapshot Snapshot => null;
            public MechanicalCatalog Catalog { get; }
            public ModifierPresentationCatalog ModifierPresentation { get; }
            public HandPreview Preview => null;
            public IReadOnlyList<ModifierInspection> ModifierInspections => Array.Empty<ModifierInspection>();
            public int SelectedMask => 0;
            public bool IsBusy => true;
            public string StatusMessage => "";
            public event Action StateChanged { add { } remove { } }
            public event Action<Resolution> Resolved { add { } remove { } }
            public void ToggleDie(int index) => throw new NotSupportedException();
            public void Reroll() => throw new NotSupportedException();
            public void PlayHand() => throw new NotSupportedException();
            public void InteractWithPiece(string pieceId, string interactionId, PieceInteractionInput input) => throw new NotSupportedException();
            public void RefreshRewards() => throw new NotSupportedException();
            public void ChooseReward(long offerId, long optionId) => throw new NotSupportedException();
            public void StartNew() => throw new NotSupportedException();
            public void Save() => throw new NotSupportedException();
            public void Load() => throw new NotSupportedException();
            public void Abandon() => throw new NotSupportedException();
            public void CompletePresentation() => throw new NotSupportedException();
        }
    }
}
