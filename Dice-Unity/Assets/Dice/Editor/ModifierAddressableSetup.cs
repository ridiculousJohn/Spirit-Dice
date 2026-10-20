using System.IO;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Dice.Editor
{
    /** Configures modifier Addressable groups, labels, and player build settings. */
    public static class ModifierAddressableSetup
    {
        private const string MechanicalPath = "Assets/Dice/Content/Mechanical/Modifiers";
        private const string PresentationPath = "Assets/Dice/Content/Presentation/SpiritHouse/Modifiers";
        private const string MechanicalLabel = "dice.modifiers";
        private const string PresentationLabel = "dice.theme.spirit-house.modifiers";

        [MenuItem("Dice/Configure Modifier Addressables")]
        public static void Configure()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            ConfigureGroup<ModifierAsset>(settings, "Dice Modifiers", MechanicalPath, MechanicalLabel, "");
            ConfigureGroup<ModifierPresentationAsset>(settings, "Spirit House Modifier Presentation", PresentationPath,
                PresentationLabel, "theme.spirit-house.");
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureGroup<T>(AddressableAssetSettings settings, string groupName,
            string folder, string label, string addressPrefix) where T : ScriptableObject
        {
            settings.AddLabel(label);
            AddressableAssetGroup group = settings.FindGroup(groupName)
                ?? settings.CreateGroup(groupName, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>()
                ?? group.AddSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            schema.IncludeInBuild = true;
            schema.IncludeAddressInCatalog = true;
            schema.IncludeGUIDInCatalog = true;
            schema.IncludeLabelsInCatalog = true;
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            if (!AssetDatabase.IsValidFolder(folder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
                entry.address = addressPrefix + Path.GetFileNameWithoutExtension(path);
                entry.SetLabel(label, true, true);
            }
            EditorUtility.SetDirty(schema);
            EditorUtility.SetDirty(group);
        }
    }
}
