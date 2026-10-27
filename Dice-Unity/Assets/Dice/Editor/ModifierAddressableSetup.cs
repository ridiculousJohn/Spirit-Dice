using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Dice.Editor
{
    /**
     * Configures Addressables to build with the player. The application configuration (address dice.application) is
     * the content entry: its mechanical catalog, the charms the catalog references and the modifier skin load as its
     * dependencies, so charms and their presentation have no Addressables entries or labels of their own.
     */
    public static class ModifierAddressableSetup
    {
        [MenuItem("Dice/Configure Addressables")]
        public static void Configure()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
