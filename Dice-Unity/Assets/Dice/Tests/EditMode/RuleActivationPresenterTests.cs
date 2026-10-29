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
     * The rule activation presenter, fed the activations of a test encounter with a reroll-cost rule and a scored-hand
     * bonus rule, and a test-created card and presenter asset.
     */
    public sealed class RuleActivationPresenterTests
    {
        private static readonly EncounterDefinition.Id EncounterId = TestEncounters.IdOf("encounter.test.rule_activation");
        private const float Sweep = .4f;
        private readonly List<Object> created = new List<Object>();
        private TestRulePresenterContext context;

        [SetUp]
        public void SetUp() => context = TestRulePresenterContext.Create();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                if (value != null) Object.DestroyImmediate(value);
            created.Clear();
            context.Dispose();
        }

        /** Checks its rule's activation refreshes the badge, cues the card's action with its icon, names the rule, reacts and waits for the authored sweep / fails if a step is missing, out of order or uses another card's values or timing. */
        [Test]
        public void AnActivationShowsTheCardsRuleAndWaitsForTheSweep()
        {
            var icon = Icon("favor-icon");
            var presenter = Presenter(Card(FirstRerollFreeRuleDefinition.StableId, "Favor", ModifierPhase.PlayerReroll, icon),
                FirstRerollFreeRuleDefinition.StableId);
            var activated = Reroll().OfType<EncounterRuleActivatedEvent>().Single();
            Assert.That(presenter.LeadIn(activated), Is.Zero);
            var waits = TestRulePresenterContext.Waits(presenter.Present(activated));
            Assert.That(context.Log, Is.EqualTo(new[]
            {
                "badge", "action PlayerReroll favor-icon", "selection Favor", "react", "sweep favor-icon"
            }));
            Assert.That(waits, Is.EqualTo(new[] { Sweep }));
        }

        /** Checks the presenter ignores another rule's activation, a scored-hand bonus and every other event / fails if it presents anything but its own rule's activation without a bonus. */
        [Test]
        public void ThePresenterPresentsOnlyItsOwnRulesActivationWithoutABonus()
        {
            var favor = Presenter(Card(FirstRerollFreeRuleDefinition.StableId, "Favor", ModifierPhase.PlayerReroll, null),
                FirstRerollFreeRuleDefinition.StableId);
            var distinct = Presenter(Card(DistinctFacesMultiplierRuleDefinition.StableId, "Distinct", ModifierPhase.PlayHand, null),
                DistinctFacesMultiplierRuleDefinition.StableId);
            foreach (var ruleEvent in Reroll().Where(value => !(value is EncounterRuleActivatedEvent)))
                Assert.That(favor.Present(ruleEvent), Is.Null, ruleEvent.GetType().Name);
            foreach (var ruleEvent in Reroll()) Assert.That(distinct.Present(ruleEvent), Is.Null, ruleEvent.GetType().Name);
            var session = Session();
            var played = session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
            Assert.That(played.Accepted, Is.True);
            var bonus = played.Events.OfType<EncounterRuleActivatedEvent>().Single(value => value.MultiplierBonus != 0);
            Assert.That(distinct.Present(bonus), Is.Null, "A scored-hand bonus is presented with the hand's scoring.");
            Assert.That(context.Log, Is.Empty);
        }

        private RulePresenter Presenter(EncounterRulePresentationAsset card, string ruleId)
        {
            var asset = ScriptableObject.CreateInstance<RuleActivationPresenterAsset>();
            created.Add(asset);
            using var serialized = new SerializedObject(asset);
            serialized.FindProperty("sweepDuration").floatValue = Sweep;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var rule = Encounter().Rules.Single(value => value.Id == ruleId);
            return asset.Create(card, rule, context);
        }

        private EncounterRulePresentationAsset Card(string ruleId, string displayName, ModifierPhase phase, Sprite icon)
        {
            var card = ScriptableObject.CreateInstance<EncounterRulePresentationAsset>();
            created.Add(card);
            using var serialized = new SerializedObject(card);
            serialized.FindProperty("ruleId").stringValue = ruleId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("activationPhase").intValue = (int)phase;
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        private Sprite Icon(string name)
        {
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f));
            sprite.name = name;
            created.Add(sprite);
            return sprite;
        }

        /** The events of a first reroll, which the reroll-cost rule makes free. */
        private static IReadOnlyList<RuleEvent> Reroll()
        {
            var session = Session();
            var result = session.Execute(new RerollDiceAction(1), session.Snapshot.Revision);
            Assert.That(result.Accepted, Is.True, result.Rejection.ToString());
            return result.Events;
        }

        private static EncounterSession Session()
            => EncounterSession.CreateEncounter(new MechanicalCatalog("test.rule_activation", TestContent.Hands(), new[] { Encounter() }),
                EncounterId, 5);

        private static EncounterDefinition Encounter()
            => new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 3, 3) },
                rules: new EncounterRuleDefinition[] { new FirstRerollFreeRuleDefinition(), new DistinctFacesMultiplierRuleDefinition() });
    }
}
