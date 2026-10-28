using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            Set(table, "rulePresentations", new[] { Rule("rule.first", "First rule", "First rule text"),
                Rule("rule.second", "Second rule", "Second rule text") });
            var journey = Create<EncounterJourneyAsset>();
            Set(journey, "beats", new[] { Beat("dialogue", EncounterJourneyBeatKind.Dialogue),
                Beat("invitation", EncounterJourneyBeatKind.Invitation) });
            Set(journey, "tablePresentation", table);
            var controls = new JourneyControls
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
            Set(rule, "ruleId", id);
            Set(rule, "displayName", name);
            Set(rule, "description", description);
            return rule;
        }

        private static EncounterJourneyBeat Beat(string id, EncounterJourneyBeatKind kind)
        {
            var beat = new EncounterJourneyBeat();
            Set(beat, "id", id);
            Set(beat, "kind", kind);
            return beat;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }

        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private sealed class JourneyControls : IEncounterJourneyControls
        {
            public EncounterJourneyPhase JourneyPhase { get; set; }
            public EncounterJourneyAsset SelectedJourney { get; set; }
            public int BeatIndex { get; set; }
            public EncounterJourneyCatalogAsset JourneyCatalog => null;
            public EncounterChapterAsset SelectedChapter => null;
            public bool CanSkipIntro => false;
            public bool HasActiveEncounter => false;
            public bool CanResume => false;
            public SpiritStatus JourneyStatus => SpiritStatus.None;
            public IReadOnlyList<string> VisitedEncounterIds => Array.Empty<string>();
            public IReadOnlyList<string> HighlightedEncounterIds => Array.Empty<string>();
            public IReadOnlyList<string> CompletedStoryIds => Array.Empty<string>();
            public bool IsJourneyAvailable(string id) => true;
            public bool IsChapterAvailable(string id) => false;
            public bool IsChapterCompleted(string id) => false;
            public event Action JourneyChanged { add { } remove { } }
            public void SelectChapter(string id) { }
            public void SelectEncounter(string encounterId) { }
            public void AdvanceJourney() { }
            public void SkipIntro() { }
            public void AcceptEncounter() { }
            public void ReturnToInvestigation() { }
            public void ResumeEncounter() { }
            public void RetryEncounter() { }
        }
    }
}
