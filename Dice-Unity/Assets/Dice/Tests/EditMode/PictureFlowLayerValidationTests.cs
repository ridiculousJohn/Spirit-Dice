using System;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class PictureFlowLayerValidationTests
    {
        private PictureSceneAsset picture;
        private SceneLightingAsset lighting;
        private Texture2D window, flow;

        [SetUp]
        public void SetUp()
        {
            picture = ScriptableObject.CreateInstance<PictureSceneAsset>();
            lighting = ScriptableObject.CreateInstance<SceneLightingAsset>();
            window = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            flow = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(picture);
            UnityEngine.Object.DestroyImmediate(lighting);
            UnityEngine.Object.DestroyImmediate(window);
            UnityEngine.Object.DestroyImmediate(flow);
        }

        /** Checks a picture scene accepts a drawable flow layer and rejects one it cannot draw / fails if a missing texture, a
         * rectangle outside the picture, a speed, period or noise out of range, or a lit picture is accepted. */
        [TestCase(null)]
        [TestCase("window")]
        [TestCase("flow")]
        [TestCase("rect")]
        [TestCase("speed")]
        [TestCase("phasePeriod")]
        [TestCase("noiseStrength")]
        [TestCase("lighting")]
        public void FlowLayerRequiresItsTexturesARectangleInsideThePictureAndPositiveValues(string invalid)
        {
            using (var serialized = new SerializedObject(picture))
            {
                serialized.FindProperty("background").objectReferenceValue = Texture2D.whiteTexture;
                if (invalid == "lighting") serialized.FindProperty("sceneLighting").objectReferenceValue = lighting;
                var layers = serialized.FindProperty("flowLayers");
                layers.arraySize = 1;
                var layer = layers.GetArrayElementAtIndex(0);
                layer.FindPropertyRelative("window").objectReferenceValue = invalid == "window" ? null : window;
                layer.FindPropertyRelative("flow").objectReferenceValue = invalid == "flow" ? null : flow;
                layer.FindPropertyRelative("rect").rectValue =
                    invalid == "rect" ? new Rect(.8f, .4f, .3f, .2f) : new Rect(.2f, .7f, .3f, .2f);
                layer.FindPropertyRelative("speed").floatValue = invalid == "speed" ? 0 : 12;
                layer.FindPropertyRelative("phasePeriod").floatValue = invalid == "phasePeriod" ? float.NaN : 3;
                layer.FindPropertyRelative("noiseStrength").floatValue = invalid == "noiseStrength" ? 1.5f : .5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            if (invalid == null) Assert.DoesNotThrow(() => picture.Validate("beat"));
            else Assert.That(Assert.Throws<FormatException>(() => picture.Validate("beat")).Message, Does.StartWith("Flow layer 0"));
        }
    }
}
