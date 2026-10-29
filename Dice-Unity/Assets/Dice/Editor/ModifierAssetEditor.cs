using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.UnityContent;
using UnityEditor;
using UnityEngine;

namespace Dice.Editor
{
    /**
     * Shows a charm's fields and, for an encounter-exclusive charm, the encounters whose own charm list offers it. That
     * list is the only statement of the encounter, so the section is read-only and warns unless exactly one encounter
     * lists the charm.
     */
    [CustomEditor(typeof(ModifierAsset))]
    public sealed class ModifierAssetEditor : UnityEditor.Editor
    {
        // Found once per inspector; whether each lists the charm is read on every draw, so list edits show at once.
        private readonly List<EncounterAsset> encounters = new List<EncounterAsset>();
        private SerializedProperty access;

        private void OnEnable()
        {
            access = serializedObject.FindProperty("access");
            encounters.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(EncounterAsset)))
            {
                var encounter = AssetDatabase.LoadAssetAtPath<EncounterAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (encounter != null) encounters.Add(encounter);
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (access.intValue != (int)ModifierAccess.EncounterExclusive) return;
            var charm = (ModifierAsset)target;
            var offering = encounters.Where(encounter => encounter != null && encounter.EncounterModifiers.Contains(charm)).ToList();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Offered by", EditorStyles.boldLabel);
            foreach (var encounter in offering) DrawEncounter(encounter);
            if (offering.Count > 1)
                EditorGUILayout.HelpBox("More than one encounter lists this encounter-exclusive charm in its Encounter Modifiers.",
                    MessageType.Warning);
            else if (offering.Count == 0)
                EditorGUILayout.HelpBox("No encounter lists this encounter-exclusive charm in its Encounter Modifiers, so no encounter offers it.",
                    MessageType.Warning);
        }

        /** The encounter as an object field that cannot be assigned: a click pings it and a double click selects it. */
        private static void DrawEncounter(EncounterAsset encounter)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            EditorGUI.LabelField(rect, EditorGUIUtility.ObjectContent(encounter, typeof(EncounterAsset)), EditorStyles.objectField);
            Event current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0 || !rect.Contains(current.mousePosition)) return;
            if (current.clickCount == 2) Selection.activeObject = encounter;
            else EditorGUIUtility.PingObject(encounter);
            current.Use();
        }
    }
}
