using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dice.Tests
{
    /** The gameplay HUD's encounter rule badge and the rule wording it shows, driven by synthetic rule encounters. */
    public sealed class EncounterRuleBadgeTests
    {
        private const string EncounterId = "encounter.rule-badge";
        private readonly List<Object> created = new List<Object>();
        private Scene preview;
        private SpiritGameplayHud hud;

        [SetUp]
        public void SetUp()
        {
            preview = EditorSceneManager.NewPreviewScene();
            var prefab = AssetDatabase.LoadAssetAtPath<SpiritGameplayHud>("Assets/Dice/Content/Presentation/UI/SpiritGameplayHud.prefab");
            Assert.That(prefab, Is.Not.Null);
            hud = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(hud.gameObject, preview);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
                if (item != null) Object.DestroyImmediate(item);
            created.Clear();
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }

        /**
         * Checks that the badge shows a rule's available wording at full opacity while the rule is available and its used
         * wording dimmed once the rule is spent, and that it hides a rule the encounter does not have. Fails if the badge
         * ignores the rule's state or shows a rule absent from the encounter.
         */
        [Test]
        public void BadgeShowsTheRuleStateAndHidesARuleTheEncounterLacks()
        {
            var session = Session(new FirstRerollFreeRuleDefinition());
            var rule = Presentation(FirstRerollFreeRuleDefinition.StableId, ModifierPhase.PlayerReroll, "favor ready", "favor spent");
            hud.RenderEncounterRule(session.Snapshot, rule);
            Assert.That(hud.EncounterRuleGroup.gameObject.activeSelf, Is.True);
            Assert.That(hud.EncounterRuleLabel.text, Is.EqualTo("favor ready"));
            Assert.That(hud.EncounterRuleGroup.alpha, Is.EqualTo(1f));

            var spent = session.Execute(new RerollDiceAction(1), session.Snapshot.Revision);
            Assert.That(spent.Accepted, Is.True);
            Assert.That(spent.Snapshot.EncounterRules.Single().IsAvailable, Is.False);
            hud.RenderEncounterRule(spent.Snapshot, rule);
            Assert.That(hud.EncounterRuleGroup.gameObject.activeSelf, Is.True);
            Assert.That(hud.EncounterRuleLabel.text, Is.EqualTo("favor spent"));
            Assert.That(hud.EncounterRuleGroup.alpha, Is.LessThan(1f), "A spent rule is dimmed.");

            hud.RenderEncounterRule(session.Snapshot, Presentation("rule.absent", ModifierPhase.PlayerReroll, "absent", "absent"));
            Assert.That(hud.EncounterRuleGroup.gameObject.activeSelf, Is.False, "A rule the encounter lacks is not shown.");
        }

        /**
         * Checks that a rule acting on Play Hand is shown in a different badge from a rule acting on a reroll, with only
         * the shown badge active. Fails if both phases share one badge or the other phase's badge stays visible.
         */
        [Test]
        public void BadgeFollowsTheActionOfTheRulesActivationPhase()
        {
            var session = Session(new FirstRerollFreeRuleDefinition());
            hud.RenderEncounterRule(session.Snapshot,
                Presentation(FirstRerollFreeRuleDefinition.StableId, ModifierPhase.PlayerReroll, "reroll", "reroll"));
            var rerollBadge = hud.EncounterRuleGroup;
            hud.RenderEncounterRule(session.Snapshot,
                Presentation(FirstRerollFreeRuleDefinition.StableId, ModifierPhase.PlayHand, "play", "play"));
            var playBadge = hud.EncounterRuleGroup;
            Assert.That(playBadge, Is.Not.SameAs(rerollBadge));
            Assert.That(playBadge.gameObject.activeSelf, Is.True);
            Assert.That(rerollBadge.gameObject.activeSelf, Is.False);
            Assert.That(hud.EncounterRuleLabel.text, Is.EqualTo("play"));
        }

        /**
         * Checks that the badge shows a rule's authored icon and disables the icon for a rule without one. Fails if an
         * authored icon is missing or an empty icon is shown.
         */
        [Test]
        public void BadgeShowsTheRuleIconOnlyWhenOneIsAuthored()
        {
            var session = Session(new FirstRerollFreeRuleDefinition());
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f));
            created.Add(sprite);
            hud.RenderEncounterRule(session.Snapshot,
                Presentation(FirstRerollFreeRuleDefinition.StableId, ModifierPhase.PlayerReroll, "icon", "icon", sprite));
            Assert.That(hud.EncounterRuleIcon.sprite, Is.SameAs(sprite));
            Assert.That(hud.EncounterRuleIcon.enabled, Is.True);

            hud.RenderEncounterRule(session.Snapshot,
                Presentation(FirstRerollFreeRuleDefinition.StableId, ModifierPhase.PlayerReroll, "none", "none"));
            Assert.That(hud.EncounterRuleIcon.sprite == null, Is.True);
            Assert.That(hud.EncounterRuleIcon.enabled, Is.False);
        }

        /**
         * Checks that a rule's wording names the encounter's current favored face and that wording of a rule without a
         * favored face is kept as authored. Fails if the face is not named or is substituted for a rule that has none.
         */
        [Test]
        public void RuleWordingNamesTheCurrentFavoredFace()
        {
            var favored = Session(new CyclingFavoredFaceMultiplierRuleDefinition()).Snapshot.EncounterRules.Single();
            Assert.That(favored.FavoredFace, Is.GreaterThan(0));
            Assert.That(Presentation(favored.Id, ModifierPhase.PlayHand, "Favored {face}", "Favored {face}").TextFor(favored),
                Is.EqualTo("Favored " + favored.FavoredFace.ToString(CultureInfo.InvariantCulture)));

            var plain = Session(new FirstRerollFreeRuleDefinition()).Snapshot.EncounterRules.Single();
            Assert.That(plain.FavoredFace, Is.Zero);
            Assert.That(Presentation(plain.Id, ModifierPhase.PlayerReroll, "Favored {face}", "Favored {face}").TextFor(plain),
                Is.EqualTo("Favored {face}"));
        }

        /** A one-stage test encounter with the given rule. */
        private static EncounterSession Session(EncounterRuleDefinition rule)
        {
            var encounter = new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 3, 3) },
                rules: new[] { rule });
            return EncounterSession.CreateEncounter(
                new MechanicalCatalog("rule-badge.v1", TestContent.Hands(), new[] { encounter }), EncounterId, 1);
        }

        private EncounterRulePresentationAsset Presentation(string ruleId, ModifierPhase phase, string available, string used,
            Sprite icon = null)
        {
            var asset = ScriptableObject.CreateInstance<EncounterRulePresentationAsset>();
            created.Add(asset);
            Set(asset, "ruleId", ruleId);
            Set(asset, "displayName", ruleId);
            Set(asset, "availableText", available);
            Set(asset, "usedText", used);
            Set(asset, "icon", icon);
            Set(asset, "activationPhase", phase);
            return asset;
        }

        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
