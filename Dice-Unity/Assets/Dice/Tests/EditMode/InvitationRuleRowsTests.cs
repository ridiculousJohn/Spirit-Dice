using System.Collections.Generic;
using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dice.Tests
{
    /** The encounter rule rows the journey view shows on an invitation, driven by a synthetic journey. */
    public sealed class InvitationRuleRowsTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private readonly SyntheticCharmAssets content = new SyntheticCharmAssets();
        private Scene preview;

        [SetUp]
        public void SetUp() => preview = EditorSceneManager.NewPreviewScene();

        [TearDown]
        public void TearDown()
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            foreach (var item in created)
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            created.Clear();
            content.Dispose();
        }

        /**
         * Checks that the invitation beat shows one row per rule presentation of the journey's table, in table order,
         * with each rule's name and description, and that a beat other than the invitation shows no rule row. Fails if a
         * rule is missing, out of order or shows another rule's text, or if rule rows remain on a non-invitation beat.
         */
        [Test]
        public void InvitationShowsOneRowPerTableRuleAndOtherBeatsShowNone()
        {
            var table = Create<EncounterTablePresentationAsset>();
            SyntheticUi.Set(table, "rulePresentations", new[] { Rule("first", "First rule", "First rule text"),
                Rule("second", "Second rule", "Second rule text") });
            var journey = Create<EncounterJourneyAsset>();
            TestJourneys.SetEncounterBeats(journey, table, content.Encounter("encounter.rules"), new DialogueBeat());
            var controls = new TestJourneyControls
            {
                SelectedJourney = journey, JourneyPhase = EncounterJourneyPhase.Sequence, BeatIndex = 1
            };
            var prefab = AssetDatabase.LoadAssetAtPath<EncounterJourneyView>("Assets/Dice/Content/Presentation/UI/EncounterJourneyView.prefab");
            Assert.That(prefab, Is.Not.Null);
            var view = UnityEngine.Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(view.gameObject, preview);
            view.Build(null);
            view.Bind(controls, null);

            view.Refresh();
            Assert.That(ShownRows(view), Is.EqualTo(new[] { "First rule / First rule text", "Second rule / Second rule text" }));

            controls.BeatIndex = 0;
            view.Refresh();
            Assert.That(ShownRows(view), Is.Empty, "Only the invitation shows the encounter rules.");
        }

        private static string[] ShownRows(EncounterJourneyView view)
            => view.GetComponentsInChildren<SpiritInvitationRuleView>(true).Where(row => row.gameObject.activeSelf)
                .Select(row => row.Title.text + " / " + row.Description.text).ToArray();

        private EncounterRulePresentationAsset Rule(string id, string name, string description)
        {
            var rule = Create<EncounterRulePresentationAsset>();
            SyntheticUi.Set(rule, "rule", content.Own(TestRules.Asset(TestRules.IdOf(id))));
            SyntheticUi.Set(rule, "displayName", name);
            SyntheticUi.Set(rule, "description", description);
            return rule;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }
    }
}
