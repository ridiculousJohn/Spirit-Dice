using System;
using Dice.Presentation;
using UnityEditor;
using UnityEngine;

namespace Dice.Editor
{
    /** Adds play-mode testing controls for the configured table's scene lights; the overrides are never saved. */
    [CustomEditor(typeof(SpiritEncounterView))]
    public sealed class SpiritEncounterViewEditor : UnityEditor.Editor
    {
        private const float MaximumTestIntensity = 2f;
        // The multiplier a light returns to when switched back on, per light index.
        private float[] onIntensities = Array.Empty<float>();

        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!EditorApplication.isPlaying) return;
            var view = (SpiritEncounterView)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing: scene lights", EditorStyles.boldLabel);
            var tableLighting = view.TableLighting;
            int count = tableLighting != null ? tableLighting.LightCount : 0;
            if (count == 0)
            {
                EditorGUILayout.HelpBox("The encounter screen has no configured table with scene lights.", MessageType.None);
                return;
            }
            if (onIntensities.Length != count)
            {
                onIntensities = new float[count];
                for (int i = 0; i < count; ++i) onIntensities[i] = 1f;
            }
            var lighting = view.TablePresentation != null ? view.TablePresentation.SceneLighting : null;
            var lights = lighting != null ? lighting.Lights : null;
            for (int i = 0; i < count; ++i)
            {
                bool held = tableLighting.GetTestLightOverride(i, out float multiplier);
                if (held && multiplier > 0f) onIntensities[i] = multiplier;
                Texture2D contribution = lights != null && i < lights.Length && lights[i] != null ? lights[i].Contribution : null;
                EditorGUILayout.LabelField("Light " + i + " (" + (contribution != null ? contribution.name : "no contribution") + ")",
                    held ? "Held" : "Ambient activity");
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUI.BeginChangeCheck();
                    bool on = EditorGUILayout.Toggle("On", multiplier > 0f);
                    if (EditorGUI.EndChangeCheck()) tableLighting.SetTestLightOverride(i, on ? onIntensities[i] : 0f);
                    EditorGUI.BeginChangeCheck();
                    float intensity = EditorGUILayout.Slider("Intensity", Mathf.Min(multiplier, MaximumTestIntensity), 0f, MaximumTestIntensity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        tableLighting.SetTestLightOverride(i, intensity);
                        if (intensity > 0f) onIntensities[i] = intensity;
                    }
                }
            }
            if (GUILayout.Button("Release overrides")) tableLighting.ReleaseTestLightOverrides();
        }
    }
}
