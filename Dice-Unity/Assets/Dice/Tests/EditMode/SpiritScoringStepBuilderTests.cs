using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    /** The scoring steps built from fabricated scored hands, synthetic charms and a synthetic theme. */
    public sealed class SpiritScoringStepBuilderTests
    {
        private static readonly DiceValues HighDieDice = new DiceValues(1, 2, 3, 4, 6);
        private static readonly DiceValues PairDice = new DiceValues(2, 5, 2, 3, 6);
        // A fabricated forming mask no category of the fixture dice would derive.
        private const int FormingMask = (1 << 1) | (1 << 4);
        private SyntheticCharms synthetic;
        private SpiritUiTheme theme;
        private ModifierDefinition bank;
        private ModifierDefinition first;
        private ModifierDefinition second;
        private SpiritScoringStepBuilder builder;
        private ModifierPresentationCatalog charms;
        private readonly List<EncounterRulePresentationAsset> cards = new List<EncounterRulePresentationAsset>();

        [SetUp]
        public void SetUp()
        {
            synthetic = new SyntheticCharms();
            theme = ScriptableObject.CreateInstance<SpiritUiTheme>();
            theme.muted = new Color(.1f, .2f, .3f);
            theme.amber = new Color(.6f, .5f, .4f);
            bank = Definition("test.steps.bank", ModifierPhase.InitialRoll);
            first = Definition("test.steps.first", ModifierPhase.PlayHand);
            second = Definition("test.steps.second", ModifierPhase.PlayHand);
            var catalog = SyntheticCharms.Catalog(bank, first, second);
            charms = synthetic.For(bank, first, second);
            builder = new SpiritScoringStepBuilder(catalog, charms, theme);
        }

        [TearDown]
        public void TearDown()
        {
            synthetic.Dispose();
            UnityEngine.Object.DestroyImmediate(theme);
            foreach (var card in cards) UnityEngine.Object.DestroyImmediate(card);
            cards.Clear();
        }

        /** Checks the dice step adds the dice sum to the banked base, keeps the banked multiplier and highlights every die / fails if it ignores the banked counters, the dice sum or the full highlight. */
        [TestCase(0L, 0L)]
        [TestCase(7L, 3L)]
        public void DiceStepAddsTheDiceSumToTheBankedCountersAndHighlightsEveryDie(long bankedBase, long bankedMultiplier)
        {
            var score = Score(HighDieDice, HandCategory.HighDie, 0);
            var dice = Build(score, bankedBase, bankedMultiplier)[0];
            Assert.That(dice.BaseScore, Is.EqualTo(bankedBase + score.DiceSum));
            Assert.That(dice.Multiplier, Is.EqualTo(bankedMultiplier));
            Assert.That(dice.StepLabel, Is.EqualTo("STEP 1 OF 4 - DICE"));
            Assert.That(dice.SelectionText, Is.EqualTo("DICE\n" + charms.GetContributionText(score.DiceSum, 0, 0)));
            Assert.That(dice.HighlightMask, Is.EqualTo(DiceValues.AllMask));
            Assert.That(dice.ResolvingIndex, Is.EqualTo(-1));
            Assert.That(dice.Charm, Is.Null);
        }

        /** Checks the hand rank step adds the category's and its upgrades' base and multiplier and highlights the dice the score states form the category / fails if upgrades are dropped or counted when absent, or the dice highlight is derived instead of read. */
        [TestCase(0)]
        [TestCase(3)]
        public void HandRankStepAddsCategoryAndUpgradeValuesWithTheCategoryHighlight(int upgrades)
        {
            var score = Score(PairDice, HandCategory.Pair, upgrades);
            var steps = Build(score, 0, 0);
            var rank = steps[1];
            Assert.That(rank.BaseScore, Is.EqualTo(steps[0].BaseScore + score.CategoryBaseScore + score.UpgradeBaseScore));
            Assert.That(rank.Multiplier, Is.EqualTo(steps[0].Multiplier + score.CategoryMultiplier + score.UpgradeMultiplier));
            Assert.That(score.UpgradeBaseScore != 0 || score.UpgradeMultiplier != 0, Is.EqualTo(upgrades > 0), "Fixture upgrades");
            Assert.That(rank.StepLabel, Is.EqualTo("STEP 2 OF 4 - HAND RANK"));
            Assert.That(rank.SelectionText, Does.Contain(" / LEVEL " + upgrades + "\n"));
            Assert.That(rank.HighlightMask, Is.EqualTo(FormingMask), "Only the dice the score states are highlighted.");
            Assert.That(rank.ResolvingIndex, Is.EqualTo(-1));
        }

        /** Checks each charm activation of the scored hand gets one charm step in activation order, adding its base and multiplier and resolving its own token / fails if charms are reordered, merged or skipped, or the counters or resolving token drift. */
        [Test]
        public void EachPlayHandActivationGetsOneCharmStepInActivationOrder()
        {
            var contributions = new[]
            {
                Contribution(second, 4, multiplier: 2),
                Contribution(first, 2, baseScore: 10),
                Contribution(second, 5, baseScore: 3, multiplier: 1)
            };
            var steps = Build(Score(HighDieDice, HandCategory.HighDie, 0, contributions), 0, 0, contributions);
            var charmSteps = steps.Where(step => step.Charm != null).ToArray();
            Assert.That(charmSteps.Select(step => step.Contribution.Value.InstanceId), Is.EqualTo(new long[] { 4, 2, 5 }));
            Assert.That(charmSteps.Select(step => step.ResolvingIndex), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(steps.Skip(2).Take(3), Is.EqualTo(charmSteps), "Charm steps follow the hand rank.");
            for (int index = 0; index < charmSteps.Length; ++index)
            {
                var previous = steps[index + 1];
                Assert.That(charmSteps[index].BaseScore, Is.EqualTo(previous.BaseScore + contributions[index].BaseScore));
                Assert.That(charmSteps[index].Multiplier, Is.EqualTo(previous.Multiplier + contributions[index].Multiplier));
                Assert.That(charmSteps[index].StepLabel, Is.EqualTo("STEP 3 OF 4 - CHARMS"));
                Assert.That(charmSteps[index].HighlightMask, Is.Zero);
            }
        }

        /** Checks a charm step shows the charm's presented name, accent and contribution wording and highlights only the counters it changes / fails if the wording, colour or counter highlights stop following the presentation and contribution. */
        [TestCase(10L, 0L)]
        [TestCase(0L, 2L)]
        public void CharmStepShowsThePresentedCharmAndHighlightsTheCountersItChanges(long baseScore, long multiplier)
        {
            var contribution = Contribution(first, 1, baseScore, multiplier);
            var step = Build(Score(HighDieDice, HandCategory.HighDie, 0, contribution), 0, 0, contribution)[2];
            Assert.That(step.Charm, Is.SameAs(first));
            Assert.That(step.Emblem, Is.SameAs(first));
            Assert.That(step.CategoryText, Is.EqualTo(charms.GetName(first)));
            Assert.That(step.SelectionText, Is.EqualTo(charms.GetName(first) + "\n" +
                charms.GetContributionText(baseScore, multiplier, 0) + " (on play)"));
            Assert.That(step.SelectionColor, Is.EqualTo(charms.GetEffectColor(first)));
            Assert.That(step.HighlightsBaseScore, Is.EqualTo(baseScore != 0));
            Assert.That(step.HighlightsMultiplier, Is.EqualTo(multiplier != 0));
        }

        /** Checks the action cue marks reroll for a contribution that grants rerolls and play for one that scores, and both for one that does both / fails if the cue no longer follows the contribution. */
        [TestCase(0L, 0L, 1, true, false)]
        [TestCase(10L, 0L, 0, false, true)]
        [TestCase(0L, 2L, 0, false, true)]
        [TestCase(4L, 0L, 1, true, true)]
        public void CueMarksRerollForGrantedRerollsAndPlayForScoring(long baseScore, long multiplier, int rerolls,
            bool rerollCue, bool playCue)
        {
            var contribution = Contribution(first, 1, baseScore, multiplier, rerolls);
            var step = Build(Score(HighDieDice, HandCategory.HighDie, 0, contribution), 0, 0, contribution)[2];
            Assert.That(step.RerollCue, Is.EqualTo(rerollCue));
            Assert.That(step.PlayCue, Is.EqualTo(playCue));
        }

        /** Checks a charm step shows and highlights the resource changes its activation states it granted, and none for an activation that granted none / fails if grants are searched for elsewhere, dropped, or invented. */
        [TestCase(false)]
        [TestCase(true)]
        public void CharmStepShowsTheResourceChangesItsActivationGranted(bool granted)
        {
            var contribution = Contribution(first, 1, rerolls: granted ? 1 : 0);
            var activated = new ModifierActivatedEvent(new EncounterState(), contribution);
            var grant = granted ? new ResourceChangedEvent(new EncounterState(), ResourceKind.Rerolls,
                ResourceChangeCause.ModifierEffect, 1, 2, activated) : null;
            var scored = Scored(Score(HighDieDice, HandCategory.HighDie, 0, contribution), new[] { activated });
            var step = builder.BuildHand(scored, 0, 0, Array.Empty<EncounterRulePresentationAsset>())[2];
            Assert.That(step.Grants, Is.EqualTo(granted ? new[] { grant } : Array.Empty<ResourceChangedEvent>()));
            Assert.That(step.HighlightsRerolls, Is.EqualTo(granted));
            Assert.That(step.HighlightsHands, Is.False);
        }

        /** Checks contributions banked outside the PlayHand phase produce no charm step: only the scored hand's activations do / fails if a roll bonus is replayed during scoring. */
        [TestCase(false)]
        [TestCase(true)]
        public void ContributionsOutsideThePlayHandPhaseProduceNoCharmStep(bool withPlayHandCharm)
        {
            var contributions = new List<ModifierContribution> { Contribution(bank, 1, baseScore: 7) };
            var played = new List<ModifierContribution>();
            if (withPlayHandCharm) played.Add(Contribution(first, 2, baseScore: 10));
            contributions.AddRange(played);
            var steps = Build(Score(HighDieDice, HandCategory.HighDie, 0, contributions.ToArray()), 7, 0, played.ToArray());
            Assert.That(steps.Where(step => step.Charm != null).Select(step => step.Charm),
                Is.EqualTo(withPlayHandCharm ? new[] { first } : Array.Empty<ModifierDefinition>()));
            Assert.That(steps.Count, Is.EqualTo(withPlayHandCharm ? 4 : 3));
        }

        /** Checks the total stays zero until the last step, which reveals the hand score with its base and multiplier / fails if any earlier step shows a total or the last step does not reveal it. */
        [TestCase(0L)]
        [TestCase(2L)]
        public void TotalStaysZeroUntilTheLastStep(long ruleMultiplier)
        {
            var contributions = new[] { Contribution(first, 1, baseScore: 10), Contribution(second, 2, multiplier: 3) };
            var score = Score(PairDice, HandCategory.Pair, 1, contributions, ruleMultiplier);
            var steps = Build(score, 0, 0, contributions);
            foreach (var step in steps.Take(steps.Count - 1))
            {
                Assert.That(step.Total, Is.Zero, step.StepLabel);
                Assert.That(step.RevealsTotal, Is.False, step.StepLabel);
            }
            var total = steps[steps.Count - 1];
            Assert.That(total.RevealsTotal, Is.True);
            Assert.That(total.Total, Is.EqualTo(score.HandScore));
            Assert.That(total.StepLabel, Is.EqualTo("STEP 4 OF 4 - " + DiagnosticText.Number(score.BaseScore) + " x " +
                DiagnosticText.Number(score.Multiplier)));
            Assert.That(total.Emblem, Is.Null);
            Assert.That(total.ResolvingIndex, Is.EqualTo(-1));
        }

        /** Checks a scored hand's rule bonus adds one step that raises the multiplier only, named by the card of the bonus's rule, and a bonus whose rule has no card fails naming the rule / fails if the rule step is missing, names another rule's card, changes the base or is built without a card. */
        [Test]
        public void EncounterRuleBonusAddsOneMultiplierStepNamedByItsRulesCard()
        {
            const string encounterId = "encounter.test.rule_bonus";
            var encounter = new EncounterDefinition(encounterId, new[] { new StageDefinition(1000000, 3, 3) },
                rules: new EncounterRuleDefinition[] { new FirstRerollFreeRuleDefinition(), new DistinctFacesMultiplierRuleDefinition() });
            var session = EncounterSession.CreateEncounter(
                new MechanicalCatalog("test.steps.rules", TestContent.Hands(), new[] { encounter }), encounterId, 5);
            var played = session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
            Assert.That(played.Accepted, Is.True);
            var scored = played.Events.OfType<HandScoredEvent>().Single();
            long bonus = played.Events.OfType<EncounterRuleActivatedEvent>().Single(value => value.MultiplierBonus != 0).MultiplierBonus;
            var favor = Card(FirstRerollFreeRuleDefinition.StableId, "Favor");
            var distinct = Card(DistinctFacesMultiplierRuleDefinition.StableId, "Distinct");

            var steps = builder.BuildHand(scored, 0, 0, new[] { favor, distinct });
            var rule = steps.Single(step => step.RuleCue);
            var rank = steps[1];
            Assert.That(steps[2], Is.SameAs(rule));
            Assert.That(rule.Rule, Is.SameAs(distinct));
            Assert.That(rule.SelectionText, Does.StartWith("Distinct\n+" + bonus));
            Assert.That(rule.BaseScore, Is.EqualTo(rank.BaseScore));
            Assert.That(rule.Multiplier, Is.EqualTo(rank.Multiplier + bonus));
            Assert.That(rule.StepLabel, Is.EqualTo("STEP 3 OF 4 - ENCOUNTER RULE"));
            Assert.That(rule.SelectionColor, Is.EqualTo(theme.amber));

            var unnamed = Assert.Throws<InvalidOperationException>(() => builder.BuildHand(scored, 0, 0, new[] { favor }));
            Assert.That(unnamed.Message, Does.Contain(DistinctFacesMultiplierRuleDefinition.StableId));
        }

        /** Checks a banked roll bonus adds its contribution to the current counters as a roll bonus without a resolving token / fails if it is worded as a scored charm or claims a history token. */
        [Test]
        public void BankedContributionIsARollBonusWithoutAResolvingToken()
        {
            var contribution = Contribution(bank, 1, baseScore: 7, multiplier: 2);
            var step = builder.BuildBankedContribution(new ModifierActivatedEvent(new EncounterState(), contribution), 5, 1);
            Assert.That(step.BaseScore, Is.EqualTo(5 + contribution.BaseScore));
            Assert.That(step.Multiplier, Is.EqualTo(1 + contribution.Multiplier));
            Assert.That(step.StepLabel, Is.EqualTo("ROLL BONUS"));
            Assert.That(step.SelectionText, Does.EndWith(" (banked)"));
            Assert.That(step.ResolvingIndex, Is.EqualTo(-1));
            Assert.That(step.RerollCue, Is.True, "A roll-phase charm that scores cues the reroll button.");
            Assert.That(step.PlayCue, Is.False);
        }

        /** The steps of a scored hand whose charm activations are the given PlayHand contributions, without rule bonuses. */
        private IReadOnlyList<SpiritScoringStep> Build(ScoreBreakdown score, long bankedBase, long bankedMultiplier,
            params ModifierContribution[] activated)
            => builder.BuildHand(Scored(score, activated.Select(value => new ModifierActivatedEvent(new EncounterState(), value))),
                bankedBase, bankedMultiplier, Array.Empty<EncounterRulePresentationAsset>());

        private static HandScoredEvent Scored(ScoreBreakdown score, IEnumerable<ModifierActivatedEvent> activations)
            => new HandScoredEvent(new EncounterState(), score, activations, Array.Empty<EncounterRuleActivatedEvent>());

        /** A rule card naming the rule with the given display name. */
        private EncounterRulePresentationAsset Card(string ruleId, string displayName)
        {
            var card = ScriptableObject.CreateInstance<EncounterRulePresentationAsset>();
            cards.Add(card);
            using var serialized = new UnityEditor.SerializedObject(card);
            serialized.FindProperty("ruleId").stringValue = ruleId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        private static ModifierDefinition Definition(string id, ModifierPhase phase)
            => new ModifierDefinition(id, id, phase, new AlwaysCondition(),
                new ModifierEffect[] { new AddBaseScoreEffect(1) });

        private static ModifierContribution Contribution(ModifierDefinition definition, long instanceId, long baseScore = 0,
            long multiplier = 0, int rerolls = 0)
            => new ModifierContribution(instanceId, definition, baseScore, multiplier, rerolls);

        private static ScoreBreakdown Score(DiceValues dice, HandCategory category, int upgrades,
            params ModifierContribution[] contributions)
            => Score(dice, category, upgrades, contributions, 0);

        /** A scored hand of the given dice under a synthetic category definition, formed by the dice of FormingMask. */
        private static ScoreBreakdown Score(DiceValues dice, HandCategory category, int upgrades,
            IEnumerable<ModifierContribution> contributions, long ruleMultiplier)
            => new ScoreBreakdown(dice, FormingMask, new HandDefinition("test.steps.hand", category, 6, 2, 3, 1, 6), upgrades,
                contributions, ruleMultiplier);
    }
}
