using Dice.UnityContent;
using UnityEditor;

namespace Dice.Editor
{
    /** Shows a charm's fields and rejects an availability scope combined with the wrong scope target. */
    [CustomEditor(typeof(ModifierAsset))]
    public sealed class ModifierAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            string problem = ((ModifierAsset)target).AvailabilityProblem;
            if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Error);
        }
    }
}
