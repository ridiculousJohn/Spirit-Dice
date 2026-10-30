using System.Collections.Generic;
using System.Linq;
using Dice.App;
using UnityEditor;

namespace Dice.Editor
{
    /**
     * Finds the project's application configuration assets. The editor tools that need the configuration require exactly
     * one and each reports a different count in its own way.
     */
    internal static class ApplicationConfigurationAssets
    {
        /** Every DiceApplicationConfig asset in the project, with its asset path. */
        internal static IReadOnlyList<(string Path, DiceApplicationConfig Configuration)> FindAll() =>
            AssetDatabase.FindAssets("t:" + nameof(DiceApplicationConfig))
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => (Path: path, Configuration: AssetDatabase.LoadAssetAtPath<DiceApplicationConfig>(path)))
                .ToList();
    }
}
