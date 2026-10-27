using UnityEditor;
using UnityEngine;

namespace Dice.Editor
{
    /**
     * An asset a preview window remembers between sessions: its GUID is kept in EditorPrefs under the window's key. A
     * remembered asset that no longer exists clears the selection.
     */
    internal static class PreviewAssetSelection
    {
        /** The remembered asset, or null when none is remembered or it no longer exists. */
        internal static T Load<T>(string key) where T : Object
        {
            string guid = EditorPrefs.GetString(key, "");
            if (guid.Length == 0) return null;
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null) EditorPrefs.DeleteKey(key);
            return asset;
        }

        /** Remembers the asset; null clears the selection. */
        internal static void Save(string key, Object asset)
        {
            if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long _))
                EditorPrefs.SetString(key, guid);
            else EditorPrefs.DeleteKey(key);
        }

        /** An object field for an asset that is remembered whenever the user changes it. */
        internal static T Field<T>(string label, T current, string key) where T : Object
        {
            var selected = (T)EditorGUILayout.ObjectField(label, current, typeof(T), false);
            if (selected != current) Save(key, selected);
            return selected;
        }
    }
}
