using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    /**
     * The rot rule's presenter: each die's mould by its rot count, the wither warning while the player chooses, rot steps,
     * withering, restoration and snapshots. Mechanism tests use a test-created rot presenter asset over a test per-die shell
     * visual and test clips.
     */
    public sealed class RotPresenterTests
    {
        private static readonly EncounterRuleDefinition.Id RotId = TestRules.IdOf("test.rot.presenter");
        private const ulong Seed = 11;
        // The test asset's levels, distinct so each count's mould and the warning are told apart; counts above 2 show the last.
        private static readonly float[] MouldLevels = { 0f, .2f, .5f };
        private const float WarningLevel = .3f;
        private readonly List<Object> created = new List<Object>();
        private readonly List<TestRulePresenterContext> contexts = new List<TestRulePresenterContext>();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                if (value != null) Object.DestroyImmediate(value);
            created.Clear();
            foreach (var context in contexts) context.Dispose();
            contexts.Clear();
        }

        /** Checks each die's mould shows its rot count at its authored level, capped at the last, whatever the selection and while playback presents, and nothing without a snapshot / fails if a count shows the wrong level, a selection or the start of playback changes it, or it outlives the snapshot. */
        [Test]
        public void EachDiesMouldShowsItsRotCountThroughoutPlay()
        {
            var fixture = Fixture(RotEndpoint.Clamp);
            var session = TestContent.WithDice(Catalog(RotEndpoint.Clamp), Seed, 2, 6, 6, 6, 6);
            TestContent.Execute(session, new RerollDiceAction(0b00001));
            TestContent.Execute(session, new RerollDiceAction(0b00001));
            var rotted = TestContent.Execute(session, new RerollDiceAction(0b00010));
            var counts = new[] { 1, 0, 3, 3, 3 };
            Assert.That(rotted.Snapshot.EncounterRules.OfType<RotRuleSnapshot>().Single().Counts.ToArray(), Is.EqualTo(counts));
            fixture.Presenter.Sync(rotted.Snapshot, false);
            AssertLevels(fixture, counts, 0, 0, "Synced");

            fixture.Presenter.SelectionChanged(0b00101);
            fixture.Settle();
            AssertLevels(fixture, counts, 0, 0, "Two dice selected");

            var reroll = TestContent.Execute(session, new RerollDiceAction(0b00101));
            Assert.That(fixture.Presenter.Present(reroll.Events.OfType<DiceRolledEvent>().Single()), Is.Null);
            fixture.Settle();
            AssertLevels(fixture, counts, 0, 0, "Playback presents the next event");

            fixture.Presenter.Sync(null, false);
            AssertLevels(fixture, new int[DiceValues.Count], 0, 0, "No snapshot");
        }

        /** Checks a kept 1 shows the warning under the Wither endpoint while the player chooses, not under another endpoint, not when selected and not once playback presents / fails if the warning shows on a die that will not wither or outlasts the choice. */
        [TestCase(RotEndpoint.Wither)]
        [TestCase(RotEndpoint.Clamp)]
        public void AKeptOneShowsTheWarningOnlyWhenItWillWither(RotEndpoint endpoint)
        {
            var fixture = Fixture(endpoint);
            var session = TestContent.WithDice(Catalog(endpoint), Seed, 1, 3, 4, 5, 6);
            var none = new int[DiceValues.Count];
            fixture.Presenter.Sync(session.Snapshot, false);
            bool withers = endpoint == RotEndpoint.Wither;
            AssertLevels(fixture, none, withers ? 0b00001 : 0, 0, endpoint + ": a kept 1");
            fixture.Presenter.SelectionChanged(0b00001);
            fixture.Settle();
            AssertLevels(fixture, none, 0, 0, endpoint + ": a selected 1");
            fixture.Presenter.SelectionChanged(0);
            fixture.Settle();
            var reroll = TestContent.Execute(session, new RerollDiceAction(0b00010));
            Assert.That(fixture.Presenter.Present(reroll.Events.OfType<DiceRolledEvent>().Single()), Is.Null);
            fixture.Settle();
            AssertLevels(fixture, none, 0, 0, endpoint + ": playback presents the next event");
        }

        /** Checks a rot step turns only the kept dice whose face changed, rerolls those the rule rolled again, grows the rotted dice's mould as they turn, plays its sound once and reacts with the host's voice only when a die withers, closing the mould over it / fails if an unchanged die turns, the mould waits for the turn, or an ordinary step reacts or plays the voice. */
        [Test]
        public void ARotStepTurnsOnlyTheChangedDiceAndReactsOnlyWhenADieWithers()
        {
            var fixture = Fixture(RotEndpoint.Wither);
            var session = TestContent.WithDice(Catalog(RotEndpoint.Wither), Seed, 2, 3, 4, 5, 6);
            var rotted = TestContent.Execute(session, new RerollDiceAction(0b00001)).Events.OfType<DiceRottedEvent>().Single();
            Assert.That(fixture.Presenter.PresentsDuringRoll(rotted), Is.True);
            var routine = fixture.Presenter.Present(rotted);
            for (int bay = 0; bay < DiceValues.Count; ++bay)
                Assert.That(fixture.Presenter[bay].MarkTarget, Is.EqualTo(bay == 0 ? 0f : MouldLevels[1]), "The mould grows as the dice turn: " + bay);
            TestRulePresenterContext.Run(routine);
            Assert.That(fixture.Context.Log, Is.EqualTo(new[] { "cue Rot", "flip 1 to 2, 2 to 3, 3 to 4, 4 to 5", "landed" }),
                "An ordinary rot step makes no reaction and plays no voice.");
            fixture.Settle();
            AssertLevels(fixture, new[] { 0, 1, 1, 1, 1 }, 0, 0, "The rotted dice's mould");

            fixture = Fixture(RotEndpoint.Wither);
            session = TestContent.WithDice(Catalog(RotEndpoint.Wither), Seed, 1, 3, 4, 5, 6);
            rotted = TestContent.Execute(session, new RerollDiceAction(0b10000)).Events.OfType<DiceRottedEvent>().Single();
            Assert.That(rotted.WitheredMask, Is.EqualTo(0b00001));
            TestRulePresenterContext.Run(fixture.Presenter.Present(rotted));
            Assert.That(fixture.Context.Log, Is.EqualTo(new[]
            {
                "cue Rot", "cue Wither", "cue Voice", "react", "jolt", "flip 1 to 2, 2 to 3, 3 to 4", "landed"
            }), "The host's voice plays with her reaction; the withering die keeps its face; only the changed dice turn.");
            fixture.Settle();
            AssertLevels(fixture, new[] { 1, 1, 1, 1, 0 }, 0, 0b00001, "The mould closes over the withered die");

            fixture = Fixture(RotEndpoint.Reroll);
            session = TestContent.WithDice(Catalog(RotEndpoint.Reroll), Seed, 1, 3, 4, 5, 6);
            rotted = TestContent.Execute(session, new RerollDiceAction(0b10000)).Events.OfType<DiceRottedEvent>().Single();
            Assert.That(rotted.RerolledMask, Is.EqualTo(0b00001));
            TestRulePresenterContext.Run(fixture.Presenter.Present(rotted));
            Assert.That(fixture.Context.Log, Is.EqualTo(new[]
            {
                "cue Rot", "flip 1 to 2, 2 to 3, 3 to 4", "reroll 0 to " + rotted.After[0], "landed", "landed"
            }), "A die the rule rolls again rerolls while the others turn.");
            fixture.Settle();
            AssertLevels(fixture, new[] { 0, 1, 1, 1, 0 }, 0, 0, "A die the rule rolls again shows no mould");
        }

        /** Checks Sync shows the withered dice closed in mould at once, and a roll, a reroll or the next opening roll, crumbles the thrown dice's mould away with its sound, claimed during the roll / fails if the mould stays or a roll of dice showing no mould plays. */
        [Test]
        public void SyncShowsWitheredDiceAndARollCrumblesTheThrownDicesMould()
        {
            var fixture = Fixture(RotEndpoint.Wither);
            var session = TestContent.WithDice(Catalog(RotEndpoint.Wither), Seed, 1, 3, 4, 5, 6);
            var withered = TestContent.Execute(session, new RerollDiceAction(0b10000));
            fixture.Presenter.Sync(withered.Snapshot, false);
            for (int bay = 0; bay < DiceValues.Count; ++bay) Assert.That(fixture.Presenter[bay].IsAnimating, Is.False);
            AssertLevels(fixture, new[] { 1, 1, 1, 1, 0 }, 0, 0b00001, "A resumed withered die");

            var restored = TestContent.Execute(session, new RerollDiceAction(0b00001)).Events.OfType<DiceRestoredEvent>().Single();
            Assert.That(restored.Roll, Is.EqualTo(RollKind.PlayerReroll));
            Assert.That(fixture.Presenter.PresentsDuringRoll(restored), Is.True);
            var routine = fixture.Presenter.Present(restored);
            Assert.That(routine, Is.Not.Null);
            Assert.That(fixture.Presenter[0].GrowthTarget, Is.Zero);
            Assert.That(fixture.Presenter[0].MarkTarget, Is.Zero);
            TestRulePresenterContext.Run(routine);
            fixture.Settle();
            AssertLevels(fixture, new[] { 0, 1, 1, 1, 0 }, 0, 0, "The thrown withered die lands clean");
            Assert.That(fixture.Context.Log, Is.EqualTo(new[] { "cue Restore" }));
            Assert.That(fixture.Presenter.Present(restored), Is.Null, "Dice showing no mould have nothing to crumble.");

            var cleared = TestContent.Execute(session, new RerollDiceAction(0b00010)).Events.OfType<DiceRestoredEvent>().Single();
            Assert.That(cleared.RestoredMask, Is.Zero);
            Assert.That(cleared.ClearedMask, Is.EqualTo(0b00010));
            TestRulePresenterContext.Run(fixture.Presenter.Present(cleared));
            fixture.Settle();
            Assert.That(fixture.Presenter[1].Mark, Is.Zero, "A thrown mouldy die lands clean.");
            Assert.That(fixture.Context.Log, Is.EqualTo(new[] { "cue Restore", "cue Restore" }));

            var again = TestContent.Execute(session, new RerollDiceAction(0b10000));
            Assert.That(again.Snapshot.EncounterRules.OfType<RotRuleSnapshot>().Single().WitheredMask, Is.Not.Zero);
            fixture.Presenter.Sync(again.Snapshot, false);
            var opening = TestContent.Execute(session, PlayHandAction.Instance).Events.OfType<DiceRestoredEvent>().Single();
            Assert.That(opening.Roll, Is.EqualTo(RollKind.Initial));
            Assert.That(fixture.Presenter.PresentsDuringRoll(opening), Is.True);
            TestRulePresenterContext.Run(fixture.Presenter.Present(opening));
            fixture.Settle();
            AssertLevels(fixture, new int[DiceValues.Count], 0, 0, "The next opening roll throws every die clean");
        }

        /** Checks the presenter claims only its own rule's rot steps and restorations, and its disposal removes its per-die visuals / fails if another rule's event or a roll is claimed, or a visual is left behind. */
        [Test]
        public void ThePresenterClaimsOnlyItsOwnRuleAndDisposalRemovesItsVisuals()
        {
            var fixture = Fixture(RotEndpoint.Wither);
            var otherId = TestRules.IdOf("test.rot.other");
            var other = TestContent.WithDice(Catalog(RotEndpoint.Wither, 20, otherId), Seed, 1, 3, 4, 5, 6);
            var rerolled = TestContent.Execute(other, new RerollDiceAction(0b10000));
            foreach (var ruleEvent in rerolled.Events)
            {
                Assert.That(fixture.Presenter.PresentsDuringRoll(ruleEvent), Is.False, ruleEvent.GetType().Name);
                Assert.That(fixture.Presenter.Present(ruleEvent), Is.Null, ruleEvent.GetType().Name);
            }
            Assert.That(fixture.Context.Log, Is.Empty);
            foreach (var die in fixture.Context.Dice)
                Assert.That(die.GetComponentsInChildren<DieBindingVisual>().Length, Is.EqualTo(1));
            fixture.Presenter.Dispose();
            foreach (var die in fixture.Context.Dice) Assert.That(die.GetComponentsInChildren<DieBindingVisual>(), Is.Empty);
        }

        /** Each die's mark is the test level of its rot count, capped at the last, and its growth the withered shell or the warning. */
        private static void AssertLevels(PresenterFixture fixture, int[] counts, int warnedMask, int witheredMask, string message)
        {
            for (int bay = 0; bay < DiceValues.Count; ++bay)
            {
                int bit = 1 << bay;
                float mark = MouldLevels[System.Math.Min(counts[bay], MouldLevels.Length - 1)];
                Assert.That(fixture.Presenter[bay].Mark, Is.EqualTo(mark), message + ": mark " + bay);
                float mould = (witheredMask & bit) != 0 ? 1f : (warnedMask & bit) != 0 ? WarningLevel : 0f;
                Assert.That(fixture.Presenter[bay].Growth, Is.EqualTo(mould), message + ": mould " + bay);
            }
        }

        private static MechanicalCatalog Catalog(RotEndpoint endpoint, int rerolls = 20, EncounterRuleDefinition.Id? ruleId = null)
        {
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1000000, 20, rerolls) },
                rules: new EncounterRuleDefinition[] { new RotRuleDefinition(ruleId ?? RotId, endpoint) });
            return new MechanicalCatalog("test.rot.presenter", TestContent.Hands(), new[] { encounter });
        }

        private PresenterFixture Fixture(RotEndpoint endpoint)
        {
            var context = TestRulePresenterContext.Create();
            contexts.Add(context);
            var asset = Asset();
            var rule = Catalog(endpoint).GetEncounter(TestContent.EncounterId).Rules.Single();
            return new PresenterFixture(context, (RotPresenter)asset.Create(Card(asset), rule, context));
        }

        /** A test rot presenter asset over a test shell visual, with the test levels and clips named after their moments. */
        private RotPresenterAsset Asset()
        {
            var asset = ScriptableObject.CreateInstance<RotPresenterAsset>();
            asset.name = "Test Rot Presenter";
            created.Add(asset);
            using var serialized = new SerializedObject(asset);
            serialized.FindProperty("diePrefab").objectReferenceValue = ShellPrefab();
            var levels = serialized.FindProperty("mouldLevels");
            levels.arraySize = MouldLevels.Length;
            for (int i = 0; i < MouldLevels.Length; ++i) levels.GetArrayElementAtIndex(i).floatValue = MouldLevels[i];
            serialized.FindProperty("warningLevel").floatValue = WarningLevel;
            serialized.FindProperty("rotClip").objectReferenceValue = Clip("Rot");
            serialized.FindProperty("witherClip").objectReferenceValue = Clip("Wither");
            serialized.FindProperty("witherVoiceClip").objectReferenceValue = Clip("Voice");
            serialized.FindProperty("restoreClip").objectReferenceValue = Clip("Restore");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private AudioClip Clip(string name)
        {
            var clip = AudioClip.Create(name, 1, 1, 44100, false);
            created.Add(clip);
            return clip;
        }

        /** A test shell visual with mark and growth shells, standing in for an authored per-die prefab. */
        private DieBindingShellVisual ShellPrefab()
        {
            var root = new GameObject("Test Rot Shell");
            created.Add(root);
            var shell = root.AddComponent<DieBindingShellVisual>();
            var mark = new GameObject("Mark Shell");
            mark.transform.SetParent(root.transform, false);
            var mould = new GameObject("Mould Shell");
            mould.transform.SetParent(root.transform, false);
            using var serialized = new SerializedObject(shell);
            serialized.FindProperty("markShell").objectReferenceValue = mark.AddComponent<MeshRenderer>();
            serialized.FindProperty("rootShell").objectReferenceValue = mould.AddComponent<MeshRenderer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return shell;
        }

        private EncounterRulePresentationAsset Card(RulePresenterAsset presenter)
        {
            var rule = TestRules.Asset(RotId);
            created.Add(rule);
            var card = ScriptableObject.CreateInstance<EncounterRulePresentationAsset>();
            created.Add(card);
            using var serialized = new SerializedObject(card);
            serialized.FindProperty("rule").objectReferenceValue = rule;
            serialized.FindProperty("presenter").objectReferenceValue = presenter;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        /** Five stand-in dice with a rot presenter created under them. */
        private sealed class PresenterFixture
        {
            public readonly TestRulePresenterContext Context;
            public readonly RotPresenter Presenter;

            public PresenterFixture(TestRulePresenterContext context, RotPresenter presenter)
            {
                Context = context;
                Presenter = presenter;
            }

            /** Lets every running animation finish. */
            public void Settle()
            {
                for (int bay = 0; bay < DiceValues.Count; ++bay) Presenter[bay].Advance(10f);
            }
        }
    }
}
