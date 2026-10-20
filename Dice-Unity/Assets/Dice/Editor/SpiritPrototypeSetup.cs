using UnityEditor;
using UnityEditor.SceneManagement;

namespace Dice.Editor
{
    public static class SpiritPrototypeSetup
    {
        public const string ScenePath = "Assets/Scenes/Application.unity";

        [MenuItem("Dice/Open Application")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
