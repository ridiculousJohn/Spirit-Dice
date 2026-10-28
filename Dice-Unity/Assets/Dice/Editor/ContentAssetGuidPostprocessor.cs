using Dice.UnityContent;
using UnityEditor;

namespace Dice.Editor
{
    /**
     * Keeps the GUID each content asset stores equal to the GUID in its .meta file. A created, imported, duplicated
     * or moved asset is imported, and a duplicate receives its own GUID from Unity, so the copied value is replaced.
     */
    internal sealed class ContentAssetGuidPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets) Synchronize(path);
            foreach (string path in movedAssets) Synchronize(path);
        }

        private static void Synchronize(string path)
        {
            if (!typeof(ContentAsset).IsAssignableFrom(AssetDatabase.GetMainAssetTypeAtPath(path))) return;
            var asset = (ContentAsset)AssetDatabase.LoadMainAssetAtPath(path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            var serialized = new SerializedObject(asset);
            SerializedProperty stored = serialized.FindProperty(ContentAsset.AssetGuidField);
            if (stored.stringValue == guid) return;
            stored.stringValue = guid;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}
