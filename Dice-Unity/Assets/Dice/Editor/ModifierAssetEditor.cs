using System.Collections.Generic;
using System.Linq;
using Dice.UnityContent;
using UnityEditor;
using UnityEngine;

namespace Dice.Editor
{
    /**
     * Shows a charm's fields, among them Requires Unlock as a checkbox with its tooltip, and, under Offered by, the
     * encounters whose own charm additions hold it and the rules whose charm list holds it. Those lists and the reward
     * pools are the only statement of where a charm is offered, so the section is read-only.
     */
    [CustomEditor(typeof(ModifierAsset))]
    public sealed class ModifierAssetEditor : UnityEditor.Editor
    {
        // Found once per inspector; whether each lists the charm is read on every draw, so list edits show at once.
        private readonly List<EncounterAsset> encounters = new List<EncounterAsset>();
        private readonly List<EncounterRuleAsset> rules = new List<EncounterRuleAsset>();

        private void OnEnable()
        {
            encounters.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(EncounterAsset)))
            {
                var encounter = AssetDatabase.LoadAssetAtPath<EncounterAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (encounter != null) encounters.Add(encounter);
            }
            rules.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(EncounterRuleAsset)))
            {
                var rule = AssetDatabase.LoadAssetAtPath<EncounterRuleAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (rule != null) rules.Add(rule);
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var charm = (ModifierAsset)target;
            var offering = encounters.Where(encounter => encounter != null && encounter.EncounterModifiers.Contains(charm))
                .Cast<Object>()
                .Concat(rules.Where(rule => rule != null && rule.AddedModifiers.Contains(charm)))
                .ToList();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Offered by", EditorStyles.boldLabel);
            foreach (var source in offering) DrawSource(source);
            if (offering.Count == 0)
                EditorGUILayout.LabelField("No encounter's charm additions or rule's charm list holds this charm.", EditorStyles.miniLabel);
        }

        /** The encounter or rule as an object field that cannot be assigned: a click pings it and a double click selects it. */
        private static void DrawSource(Object source)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            EditorGUI.LabelField(rect, EditorGUIUtility.ObjectContent(source, source.GetType()), EditorStyles.objectField);
            Event current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0 || !rect.Contains(current.mousePosition)) return;
            if (current.clickCount == 2) Selection.activeObject = source;
            else EditorGUIUtility.PingObject(source);
            current.Use();
        }
    }
}
