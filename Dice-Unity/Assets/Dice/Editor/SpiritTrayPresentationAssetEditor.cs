using Dice.Presentation;
using UnityEditor;
using UnityEngine;

namespace Dice.Editor
{
    [CustomEditor(typeof(SpiritTrayPresentationAsset))]
    public sealed class SpiritTrayPresentationAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("RigPrefab"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("TextureSize"));
            serializedObject.ApplyModifiedProperties();
            var asset = (SpiritTrayPresentationAsset)target;
            if (GUILayout.Button("Open encounter rig") && asset.RigPrefab != null)
                AssetDatabase.OpenAsset(asset.RigPrefab.gameObject);
            EditorGUILayout.HelpBox("Author camera, lighting, geometry, and surface materials on the rig. " +
                "Author tray display placement on the encounter scene's DiceTrayFrame.", MessageType.Info);
        }
    }
}
