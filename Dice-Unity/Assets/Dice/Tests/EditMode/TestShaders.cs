using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    /** The project's shared shaders that presentation tests render with, by asset path. */
    internal static class TestShaders
    {
        public const string SceneLighting = "Assets/Dice/Presentation/Shared/SceneLighting.shader";
        public const string SceneLitImage = "Assets/Dice/Presentation/Shared/SceneLitImage.shader";
        public const string SceneLitImagePreview = "Assets/Dice/Presentation/Shared/SceneLitImagePreview.shader";
        public const string SourceMaskedImage = "Assets/Dice/Presentation/Shared/SourceMaskedImage.shader";
        public const string SpiritSmoke = "Assets/Dice/Presentation/Shared/SpiritSmoke.shader";

        public static Shader Load(string path)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(shader, Is.Not.Null, path);
            return shader;
        }
    }
}
