using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Dice.Editor
{
    public static class SpiritAndroidBuild
    {
        private const string BuildPathEnvironmentVariable = "DICE_ANDROID_BUILD_PATH";

        [MenuItem("Dice/Build Android Prototype")]
        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Android Build Support must be installed for this Unity editor.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Select Android as the active build target, or launch Unity with -buildTarget Android.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SpiritPrototypeSetup.ScenePath) == null)
                throw new BuildFailedException("Prototype scene is missing: " + SpiritPrototypeSetup.ScenePath);

            string outputPath = GetOutputPath();
            if (File.Exists(outputPath) || Directory.Exists(outputPath))
                throw new BuildFailedException("Build output already exists: " + outputPath);

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "net.ridiculousgaming.diceprototype");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.useCustomKeystore = false;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            ModifierAddressableSetup.Configure();
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SpiritPrototypeSetup.ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });

            if (report == null)
                throw new BuildFailedException("Android build did not return a build report.");

            BuildSummary summary = report.summary;
            Debug.Log("Android prototype build result=" + (int)summary.result
                + " path=" + outputPath
                + " bytes=" + summary.totalSize
                + " warnings=" + summary.totalWarnings
                + " errors=" + summary.totalErrors);
            if (summary.result != BuildResult.Succeeded || !File.Exists(outputPath))
                throw new BuildFailedException("Android prototype build failed: " + summary.result);
        }

        private static string GetOutputPath()
        {
            string outputPath = Environment.GetEnvironmentVariable(BuildPathEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                if (Application.isBatchMode)
                    throw new BuildFailedException(BuildPathEnvironmentVariable + " must specify an absolute .apk path in batch mode.");

                outputPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", "Android",
                    "DicePrototype-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".apk");
            }

            if (!Path.IsPathFullyQualified(outputPath)
                || !string.Equals(Path.GetExtension(outputPath), ".apk", StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException(BuildPathEnvironmentVariable + " must specify an absolute .apk path.");

            return Path.GetFullPath(outputPath);
        }
    }
}
