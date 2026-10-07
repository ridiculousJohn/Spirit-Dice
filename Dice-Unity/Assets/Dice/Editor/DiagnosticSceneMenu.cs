using UnityEditor;
using UnityEditor.SceneManagement;

namespace Dice.Editor
{
    public static class DiagnosticSceneMenu
    {
        [MenuItem("Dice/Open Diagnostic Scene")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/Scenes/DiagnosticEncounter.unity");
        }
    }
}
