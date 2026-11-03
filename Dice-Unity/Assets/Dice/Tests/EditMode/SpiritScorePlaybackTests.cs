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
        private static readonly EncounterDefinition.Id EncounterId = TestEncounters.IdOf("encounter.test.playback");
        private Scene preview;
        private TestTrayRig rig;
        private SyntheticUi ui;
        private SyntheticCharms charms;
        private SpiritGameplayHud hud;
        // The synthetic HUD's labels by the HUD property each fills.
        private readonly Dictionary<string, Text> labels = new Dictionary<string, Text>();
        private SpiritEncounterAudio audio;
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private TestRulePresenterContext context;

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
            foreach (var value in created)
                if (value != null) UnityEngine.Object.DestroyImmediate(value);
            created.Clear();
            context?.Dispose();
            context = null;
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
            Assert.That(scored.Take(first), Has.Member("<scoring.diceCategory>"), "The dice are counted before the rank shows.");
            Assert.That(scored.Take(first).Intersect(ranks), Is.Empty, "No rank shows before the played hand is scored.");
            Assert.That(scored.Skip(first).Take(last - first + 1), Is.All.EqualTo(rank));
            Assert.That(scored.Skip(last + 1).First(), Is.EqualTo("<scoring.totalCategory>"));
            Assert.That(scored.Skip(last + 1).Intersect(ranks), Is.Empty, "No rank shows after the hand is scored.");
            Assert.That(scored.Last(), Is.Empty, "The next hand's roll shows no rank.");
            Assert.That(host.Finished, Is.EqualTo(new[] { reroll, played }));
        }

        /** Checks a resource change a charm activation granted is left to the charm's own step while any other change is shown at once / fails if playback searches the events for the grant's charm or shows a grant on its own. */
        [TestCase(false)]
        [TestCase(true)]
        public void GrantedResourceChangeIsLeftToItsCharmsStep(bool granted)
        {
            var encounter = new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 3, 3) });
            var catalog = new MechanicalCatalog("test.playback", TestContent.Hands(), new[] { encounter });
            var session = EncounterSession.CreateEncounter(catalog, EncounterId, 7);
            var host = new PlaybackHost(new Controls(catalog, charms.For()));
            var playback = new SpiritScorePlayback(host, hud, rig.StartDice(), rig.CreateHistoryTray(), audio, null, null);
            var charm = new ModifierDefinition(TestModifiers.IdOf("test.playback.grant"), TestFamilies.Of("test.playback.grant"), ModifierPhase.PlayerReroll,
                new AlwaysCondition(), new ModifierEffect[] { new AddRerollsEffect(1) });
            var state = new EncounterState();
            var activated = granted ? new ModifierActivatedEvent(state, new ModifierContribution(1, charm, 0, 0, 1)) : null;
            var change = new ResourceChangedEvent(state, ResourceKind.Rerolls,
                granted ? ResourceChangeCause.ModifierEffect : ResourceChangeCause.RerollDice, 1, 2, activated);
            hud.ShowRuleSelection("before");
            Run(playback.Play(new Resolution(session.Snapshot.Revision, session.Snapshot, new RuleEvent[] { change }), 0));
            Assert.That(labels["SelectionLabel"].text, Is.EqualTo(granted ? "before" : "<hud.rerollsRemaining.many>2"));
        }

        /**
         * Checks an event a rule presenter claims during the roll before it starts in the roll's first frame, without
         * waiting for the roll, and playback moves on only once the roll and that presentation have both ended, whichever
         * ends last; an unclaimed event's presentation still waits for the roll / fails if a claimed event waits for the
         * roll, playback moves on while either still runs, or an unclaimed event starts during the roll.
         */
        [TestCase(true, 3)]
        [TestCase(true, 20)]
        [TestCase(false, 3)]
        public void EventsClaimedDuringARollRunWithItAndPlaybackWaitsForBoth(bool claims, int steps)
        {
            const int RollingFrames = 11;
            var ruleId = TestRules.IdOf("test.playback.claimed");
            var encounter = new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 3, 3) },
                rules: new EncounterRuleDefinition[] { new RotRuleDefinition(ruleId, RotEndpoint.Wither) });
            var catalog = new MechanicalCatalog("test.playback", TestContent.Hands(), new[] { encounter });
            var session = EncounterSession.CreateEncounter(catalog, EncounterId, 7);
            var tray = rig.StartDice();
            var log = new List<string>();
            var presenter = ScriptableObject.CreateInstance<ClaimingPresenterAsset>();
            created.Add(presenter);
            presenter.Tray = tray;
            presenter.Log = log;
            presenter.Claims = claims;
            presenter.Steps = steps;
            var rule = TestRules.Asset(ruleId);
            created.Add(rule);
            var card = ScriptableObject.CreateInstance<EncounterRulePresentationAsset>();
            created.Add(card);
            using (var serialized = new SerializedObject(card))
            {
                serialized.FindProperty("rule").objectReferenceValue = rule;
                serialized.FindProperty("presenter").objectReferenceValue = presenter;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            context = TestRulePresenterContext.Create();
            var presenters = EncounterRulePresenters.Create(new[] { card }, encounter, () => context);
            var host = new PlaybackHost(new Controls(catalog, charms.For()), presenters, log);
            var playback = new SpiritScorePlayback(host, hud, tray, rig.CreateHistoryTray(), audio, null, null);

            var reroll = session.Execute(new RerollDiceAction(0b00011), session.Snapshot.Revision);
            Assert.That(reroll.Accepted, Is.True, reroll.Rejection.ToString());
            Assert.That(reroll.Events.SkipWhile(value => !(value is DiceRolledEvent)).Skip(1).First(),
                Is.InstanceOf<DiceRottedEvent>(), "The rot step directly follows the reroll's roll.");
            var running = new Stack<IEnumerator>();
            running.Push(playback.Play(reroll, 0));
            for (int frame = 0; frame < 10 && !tray.IsRolling; ++frame) Assert.That(Frame(running), Is.True);
            Assert.That(tray.IsRolling, Is.True, "The roll is in the air.");
            Assert.That(log, Is.EqualTo(claims ? new[] { "step 0 rolling" } : Array.Empty<string>()),
                claims ? "The claimed event starts in the roll's first frame." : "An unclaimed event waits for the roll.");
            for (int frame = 1; frame < RollingFrames; ++frame) Assert.That(Frame(running), Is.True);
            int stepsDuringRoll = claims ? Math.Min(steps, RollingFrames) : 0;
            Assert.That(log, Is.EqualTo(Enumerable.Range(0, stepsDuringRoll).Select(step => "step " + step + " rolling")));
            Assert.That(host.Finished, Is.Empty, "Playback waits for the roll.");

            // Ends the roll as its landing would.
            tray.ReleasePresentation();
            for (int frame = 0; Frame(running); ++frame)
                if (frame > 100) throw new InvalidOperationException("The playback did not end.");
            var expected = Enumerable.Range(0, steps)
                .Select(step => "step " + step + (step < stepsDuringRoll ? " rolling" : " resting")).ToList();
            expected.Add("finished");
            Assert.That(log, Is.EqualTo(expected), "Playback finishes only once the claimed presentation has ended.");
            Assert.That(host.Finished, Is.EqualTo(new[] { reroll }));
        }

        /**
         * Advances the playback by one frame: until a routine waits for the next frame, entering nested routines and passing
         * realtime waits at once; false when it has ended.
         */
        private static bool Frame(Stack<IEnumerator> running)
        {
            while (running.Count > 0)
            {
                var current = running.Peek();
                if (!current.MoveNext())
                {
                    running.Pop();
                    continue;
                }
                if (current.Current is CustomYieldInstruction) continue;
                if (current.Current is IEnumerator nested)
                {
                    running.Push(nested);
                    continue;
                }
                return true;
            }
            return false;
        }

        private string Rank(HandCategory category) => ui.Wording.handNames.Name(category).ToUpperInvariant();

        /** Runs the playback to its end and returns the HUD's rank label as it stood at every yield. */
        private List<string> Run(IEnumerator routine)
        {
            var shown = new List<string>();
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
                shown.Add(labels["CategoryLabel"].text);
                if (current.Current is CustomYieldInstruction) continue;
                if (current.Current is IEnumerator nested) running.Push(nested);
                else if (running.Count > 1)
                {
                    running.Pop();
                    (current as IDisposable)?.Dispose();
                }
            }
            return shown;
        }

        /** A HUD holding only what score playback writes: its labels, panels, counters, rule badges and action buttons. */
        private SpiritGameplayHud Hud()
        {
            var created = ui.Add<SpiritGameplayHud>(ui.Root, "Gameplay HUD");
            Transform parent = created.transform;
            SyntheticUi.Set(created, "<PlayingPanel>k__BackingField", ui.Rect(parent, "Playing"));
            foreach (string label in new[] { "SelectionLabel", "CategoryLabel", "ScoreDetail", "BaseScoreLabel", "MultiplierLabel",
                "HandScoreLabel", "TargetLabel", "TargetCaption", "HandsLabel", "RerollsLabel", "ResourcesLabel" })
            {
                labels[label] = ui.Add<Text>(parent, label);
                SyntheticUi.Set(created, "<" + label + ">k__BackingField", labels[label]);
            }
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

        /**
         * A playback host with the encounter's controls and optionally rule presenters, and no host portrait, journey, rule
         * cards, ambient activity or figures; a given log records "finished" when a presentation finishes.
         */
        private sealed class PlaybackHost : ISpiritScorePlaybackHost
        {
            public readonly List<Resolution> Finished = new List<Resolution>();
            private readonly List<string> log;

            public PlaybackHost(ISpiritEncounterControls controls, EncounterRulePresenters presenters = null,
                List<string> log = null)
            {
                Controls = controls;
                RulePresenters = presenters;
                this.log = log;
            }

            public ISpiritEncounterControls Controls { get; }
            public IEncounterJourneyControls Journey => null;
            public SpiritHostPortrait Host => null;
            public IReadOnlyList<EncounterRulePresentationAsset> RuleCards => Array.Empty<EncounterRulePresentationAsset>();
            public EncounterRulePresentationAsset HudRuleCard => null;
            public EncounterAmbientActivity Ambient => null;
            public EncounterRulePresenters RulePresenters { get; }
            public void SetDiceSelection(int mask) { }
            public void HideRewardsAndEnding() { }
            public void SetOpeningOpacity(float opacity) { }
            public void ShowEnding() { }
            public void RecordScore(ScoreBreakdown score) { }
            public void PlayOutcome(EncounterOutcome outcome) { }
            public void FinishPresentation(Resolution resolution)
            {
                Finished.Add(resolution);
                log?.Add("finished");
            }
        }

        /**
         * A presenter of rot steps that claims them during the roll before them or not, and whose presentation logs, at
         * each of its steps, whether the tray is rolling.
         */
        private sealed class ClaimingPresenterAsset : RulePresenterAsset
        {
            [NonSerialized] public SpiritDiceTray Tray;
            [NonSerialized] public List<string> Log;
            [NonSerialized] public bool Claims;
            [NonSerialized] public int Steps;

            public override RulePresenter Create(EncounterRulePresentationAsset card, EncounterRuleDefinition rule,
                RulePresenterContext context)
                => new ClaimingPresenter(this);
        }

        private sealed class ClaimingPresenter : RulePresenter
        {
            private readonly ClaimingPresenterAsset asset;

            public ClaimingPresenter(ClaimingPresenterAsset asset) => this.asset = asset;

            public override bool PresentsDuringRoll(RuleEvent ruleEvent) => asset.Claims && ruleEvent is DiceRottedEvent;

            public override IEnumerator Present(RuleEvent ruleEvent) => ruleEvent is DiceRottedEvent ? Play() : null;

            private IEnumerator Play()
            {
                for (int step = 0; step < asset.Steps; ++step)
                {
                    if (step > 0) yield return null;
                    asset.Log.Add("step " + step + (asset.Tray.IsRolling ? " rolling" : " resting"));
                }
            }
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
            public SpiritStatus Status => SpiritStatus.None;
            public event Action StateChanged { add { } remove { } }
            public event Action<Resolution> Resolved { add { } remove { } }
            public void ToggleDie(int index) => throw new NotSupportedException();
            public void Reroll() => throw new NotSupportedException();
            public void PlayHand() => throw new NotSupportedException();
            public void InteractWithPiece(TablePieceDefinition.Id pieceId, int interactionIndex, PieceInteractionInput input)
                => throw new NotSupportedException();
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
