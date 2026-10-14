using Dice.Presentation;
using UnityEditor;
using UnityEngine;

namespace Dice.Editor
{
    [CustomEditor(typeof(SpiritTrayPresentationAsset))]
    public sealed class SpiritTrayPresentationAssetEditor : UnityEditor.Editor
    {
        private bool showRendering;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPlacement(serializedObject);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("CameraEulerAngles"), new GUIContent("Viewing angle"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("GeometryPrefab"));
            showRendering = EditorGUILayout.Foldout(showRendering, "Rendering and materials", true);
            if (showRendering) DrawRendering(serializedObject);
            serializedObject.ApplyModifiedProperties();
        }

        public static void DrawPlacement(SerializedObject serialized)
        {
            Rect placement = serialized.FindProperty("CompositionRect").rectValue;
            EditorGUI.BeginChangeCheck();
            Vector2 center = EditorGUILayout.Vector2Field(new GUIContent("Position",
                "Center on the portrait. (0, 0) is bottom left; (1, 1) is top right."), placement.center);
            float width = Mathf.Max(.01f, EditorGUILayout.FloatField(
                new GUIContent("Size", "Scale relative to the portrait width."), placement.width));
            if (EditorGUI.EndChangeCheck()) SetPlacement(serialized, center, width);
        }

        public static void SetPlacement(SerializedObject serialized, Vector2 center, float width)
        {
            Vector2Int texture = serialized.FindProperty("TextureSize").vector2IntValue;
            float height = width * (540f / 960f) * texture.y / texture.x;
            var size = new Vector2(width, height);
            serialized.FindProperty("CompositionRect").rectValue = new Rect(center - size * .5f, size);
        }

        public static void DrawRendering(SerializedObject serialized)
        {
            foreach (string name in new[] { "Orthographic", "FieldOfView", "TextureSize", "DiceShader", "BoneTexture",
                "WoodTexture", "FallbackLightEulerAngles", "FallbackLightColor", "FallbackLightIntensity" })
                EditorGUILayout.PropertyField(serialized.FindProperty(name));
        }
    }
}
