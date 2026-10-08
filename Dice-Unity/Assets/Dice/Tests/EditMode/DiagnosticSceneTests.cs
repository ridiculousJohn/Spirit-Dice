using System.Linq;
using Dice.App;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class DiagnosticSceneTests
    {
        [Test]
        public void DiagnosticSceneHasItsCatalogViewAndCamera()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/DiagnosticEncounter.unity");
            try
            {
                DiagnosticEncounterController controller = null;
                Camera camera = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    controller = controller != null ? controller : root.GetComponentInChildren<DiagnosticEncounterController>();
                    camera = camera != null ? camera : root.GetComponentInChildren<Camera>();
                }
                Assert.That(controller, Is.Not.Null);
                Assert.That(camera, Is.Not.Null);
                Assert.That(controller.GetComponent<DiagnosticEncounterView>(), Is.Not.Null);
                using (var serialized = new SerializedObject(controller))
                {
                    Assert.That(serialized.FindProperty("catalogAsset").objectReferenceValue,
                        Is.SameAs(AssetDatabase.LoadAssetAtPath<MechanicalCatalogAsset>("Assets/Dice/Content/Mechanical/Standard.asset")));
                    Assert.That(serialized.FindProperty("encounterId").stringValue, Is.EqualTo("encounter.standard"));
                    Assert.That(serialized.FindProperty("initialSeed").stringValue, Is.EqualTo("60"));
                }
                Assert.That(EditorBuildSettings.scenes.Any(entry => entry.path == scene.path && entry.enabled), Is.True);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
