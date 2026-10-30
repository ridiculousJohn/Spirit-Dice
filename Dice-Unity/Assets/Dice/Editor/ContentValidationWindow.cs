using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dice.Editor
{
    /** Runs the content checks on request and lists what each found, with a button that selects the asset or object. */
    public sealed class ContentValidationWindow : EditorWindow
    {
        private ContentValidationReport report;
        private Vector2 scroll;
        private bool[] expanded;

        [MenuItem("Dice/Validate Content")]
        public static void Open() => GetWindow<ContentValidationWindow>("Validate Content").Show();

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Checks read saved scenes and prefabs in preview scenes and modify nothing. Checks 2 to 4 list " +
                "magnitudes for judgement, not failures.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Run")) Run();
            if (report == null) return;
            EditorGUILayout.LabelField("Finished " + report.Finished.ToString("yyyy-MM-dd HH:mm:ss"));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < report.Checks.Count; ++i)
            {
                var check = report.Checks[i];
                expanded[i] = EditorGUILayout.Foldout(expanded[i], check.Title + " (" + check.Entries.Count + ")", true);
                if (!expanded[i]) continue;
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.LabelField(check.Description, EditorStyles.wordWrappedMiniLabel);
                    if (check.Coverage.Length > 0)
                        EditorGUILayout.LabelField("Examined: " + check.Coverage, EditorStyles.wordWrappedMiniLabel);
                    if (check.Entries.Count == 0) EditorGUILayout.LabelField("None.");
                    foreach (var entry in check.Entries) DrawEntry(entry);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void Run()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Validate Content", "Running content checks", 0f);
                report = ContentValidation.Run();
            }
            finally { EditorUtility.ClearProgressBar(); }
            expanded = report.Checks.Select(check => check.Entries.Count > 0).ToArray();
        }

        private static void DrawEntry(ContentValidationEntry entry)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(entry.AssetPath.Length == 0))
                    if (GUILayout.Button("Select", GUILayout.Width(60))) Select(entry);
                EditorGUILayout.LabelField(entry.Location, EditorStyles.boldLabel);
            }
            EditorGUILayout.LabelField(entry.Message, EditorStyles.wordWrappedLabel);
        }

        /**
         * Selects and pings the entry's object: in a scene that is open, the object at its path; in a prefab, that object
         * inside the prefab asset; otherwise the asset itself.
         */
        private static void Select(ContentValidationEntry entry)
        {
            Object target = AssetDatabase.LoadMainAssetAtPath(entry.AssetPath);
            if (entry.ObjectPath != null)
            {
                if (target is SceneAsset)
                {
                    Scene scene = SceneManager.GetSceneByPath(entry.AssetPath);
                    if (scene.IsValid() && scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene))
                        target = Find(scene.GetRootGameObjects(), entry.ObjectPath) ?? target;
                }
                else if (target is GameObject prefab) target = Find(new[] { prefab }, entry.ObjectPath) ?? target;
            }
            if (target == null) return;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private static GameObject Find(GameObject[] roots, string path)
            => roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(transform => ContentValidation.HierarchyPath(transform) == path)?.gameObject;
    }
}
