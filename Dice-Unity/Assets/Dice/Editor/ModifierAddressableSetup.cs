using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Dice.Editor
{
    /** Explicit import of the frozen catalog; ordinary configuration never changes authored assets. */
    public static class ModifierAddressableSetup
    {
        private const string MechanicalPath = "Assets/Dice/Content/Mechanical/Modifiers";
        private const string PresentationPath = "Assets/Dice/Content/Presentation/SpiritHouse/Modifiers";
        private const string CatalogPath = "Assets/Dice/Content/Mechanical/Standard.asset";
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

        [MenuItem("Dice/Import Modifier Assets From Snapshot")]
        public static void Import()
        {
            string migrationRoot = Environment.GetEnvironmentVariable("DICE_MODIFIER_MIGRATION_ROOT");
            if (string.IsNullOrWhiteSpace(migrationRoot) || !Path.IsPathFullyQualified(migrationRoot))
                throw new InvalidOperationException("DICE_MODIFIER_MIGRATION_ROOT must name the absolute frozen source directory.");

            string source = File.ReadAllText(Path.Combine(migrationRoot, "Standard.asset"));
            string presentationSource = File.ReadAllText(Path.Combine(migrationRoot, "SpiritModifierPresentation.cs"));
            List<ModifierSource> modifiers = ReadModifiers(source);
            Dictionary<string, string> familyNames = ReadFamilyNames(presentationSource);
            Dictionary<string, IdentitySource> identities = ReadIdentities(presentationSource);
            string currentCatalog = File.ReadAllText(CatalogPath);
            ValidateCatalogSource(source, currentCatalog);
            foreach (ModifierSource modifier in modifiers)
                if (!familyNames.ContainsKey(modifier.Fields["familyId"]))
                    throw new InvalidOperationException("Frozen presentation has no family name for " + modifier.Fields["familyId"]);

            EnsureFolder(MechanicalPath);
            EnsureFolder(PresentationPath);
            var imported = new List<ModifierAsset>(modifiers.Count);
            foreach (ModifierSource modifier in modifiers)
            {
                string id = modifier.Fields["id"];
                string path = MechanicalPath + "/" + id + ".asset";
                ModifierAsset asset = LoadOrCreate<ModifierAsset>(path, out bool created);
                if (created)
                {
                    var serialized = new SerializedObject(asset);
                    WriteModifier(serialized, modifier);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                ValidateModifier(asset, modifier);
                imported.Add(asset);

                string presentationPath = PresentationPath + "/" + id + ".asset";
                ModifierPresentationAsset presentation = LoadOrCreate<ModifierPresentationAsset>(presentationPath, out created);
                if (created)
                {
                    identities.TryGetValue(id, out IdentitySource identity);
                    var serialized = new SerializedObject(presentation);
                    serialized.FindProperty("modifier").objectReferenceValue = asset;
                    serialized.FindProperty("displayName").stringValue = identity == null
                        ? familyNames[modifier.Fields["familyId"]] : identity.Name;
                    serialized.FindProperty("conditionTemplate").stringValue = identity == null ? "" : identity.Template;
                    serialized.FindProperty("conditionTypeName").stringValue = identity == null ? "" : identity.ConditionType;
                    serialized.FindProperty("artwork").intValue = identity == null ? 0 : (int)identity.Artwork;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                var validation = new SerializedObject(presentation);
                if (validation.FindProperty("modifier").objectReferenceValue != asset
                    || string.IsNullOrWhiteSpace(validation.FindProperty("displayName").stringValue))
                    throw new InvalidOperationException("Existing presentation is incomplete or references a different modifier: " + presentationPath);
            }

            MechanicalCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<MechanicalCatalogAsset>(CatalogPath)
                ?? throw new InvalidOperationException("Standard mechanical catalog is missing.");
            catalog.Build(imported);
            AssetDatabase.SaveAssets();
            Configure();
            // Remove the embedded list only after every replacement has been built and verified.
            currentCatalog = File.ReadAllText(CatalogPath);
            ValidateCatalogSource(source, currentCatalog);
            Match embedded = ModifierSection(currentCatalog);
            if (embedded.Success)
            {
                string updated = currentCatalog.Remove(embedded.Index, embedded.Length);
                File.WriteAllText(CatalogPath, updated, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("Modifier import complete: modifiers=" + modifiers.Count + " presentations=" + modifiers.Count);
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

        private static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            created = existing == null;
            if (!created) return existing;
            if (File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Import target already exists with another type: " + path);
            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = folder.Substring(0, folder.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static Match ModifierSection(string source)
        {
            return Regex.Match(source, @"^  modifiers:\r?\n(?:(?!^  [A-Za-z])[\s\S])*", RegexOptions.Multiline);
        }

        private static void ValidateCatalogSource(string source, string current)
        {
            string version = Regex.Match(source, @"^  catalogVersion: (.+)$", RegexOptions.Multiline).Groups[1].Value.TrimEnd('\r');
            string currentVersion = Regex.Match(current, @"^  catalogVersion: (.+)$", RegexOptions.Multiline).Groups[1].Value.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(version) || version != currentVersion)
                throw new InvalidOperationException("Frozen and current catalog versions differ.");
            Match embedded = ModifierSection(current);
            if (embedded.Success && embedded.Value.Replace("\r\n", "\n") != ModifierSection(source).Value.Replace("\r\n", "\n"))
                throw new InvalidOperationException("Embedded modifiers changed after the snapshot; preserve the edits and update the migration input explicitly.");
        }

        private static List<ModifierSource> ReadModifiers(string source)
        {
            Match section = ModifierSection(source);
            if (!section.Success) throw new InvalidOperationException("Frozen catalog has no embedded modifier list.");
            var result = new List<ModifierSource>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match record in Regex.Matches(section.Value, @"^  - id: [^\r\n]+\r?\n(?:(?!^  - id:)[\s\S])*", RegexOptions.Multiline))
            {
                var modifier = new ModifierSource();
                foreach (Match field in Regex.Matches(record.Value, @"^(?:  - |    )([A-Za-z]+): ([^\r\n]+)\r?$", RegexOptions.Multiline))
                {
                    if (field.Groups[1].Value != "effects")
                        modifier.Fields.Add(field.Groups[1].Value, field.Groups[2].Value.TrimEnd('\r'));
                }
                foreach (Match effect in Regex.Matches(record.Value, @"^    - kind: (-?\d+)\r?\n      amount: (-?\d+)", RegexOptions.Multiline))
                    modifier.Effects.Add(new EffectSource(ParseLong(effect.Groups[1].Value), ParseLong(effect.Groups[2].Value)));
                foreach (string field in new[] { "id", "familyId", "phase", "conditionKind", "parameter", "secondaryParameter", "activationLimit", "rewardEligible" })
                    if (!modifier.Fields.ContainsKey(field)) throw new InvalidOperationException("Frozen modifier is missing " + field);
                if (modifier.Fields.Count != 8 || !ids.Add(modifier.Fields["id"]))
                    throw new InvalidOperationException("Frozen modifier contains unknown fields or a duplicate id.");
                int effectCount = Regex.Matches(record.Value, @"^    - kind:", RegexOptions.Multiline).Count;
                if (effectCount != modifier.Effects.Count) throw new InvalidOperationException("An effect could not be parsed.");
                result.Add(modifier);
            }
            if (result.Count == 0) throw new InvalidOperationException("Frozen modifier list is empty.");
            return result;
        }

        private static void WriteModifier(SerializedObject serialized, ModifierSource source)
        {
            foreach (KeyValuePair<string, string> field in source.Fields)
            {
                SerializedProperty property = serialized.FindProperty(field.Key)
                    ?? throw new InvalidOperationException("ModifierAsset has no serialized " + field.Key);
                if (property.propertyType == SerializedPropertyType.String) property.stringValue = field.Value;
                else if (property.propertyType == SerializedPropertyType.Boolean) property.boolValue = field.Value == "1";
                else if (property.propertyType == SerializedPropertyType.Enum) property.intValue = (int)ParseLong(field.Value);
                else property.longValue = ParseLong(field.Value);
            }
            SerializedProperty effects = serialized.FindProperty("effects");
            effects.arraySize = source.Effects.Count;
            for (int i = 0; i < source.Effects.Count; ++i)
            {
                SerializedProperty effect = effects.GetArrayElementAtIndex(i);
                effect.FindPropertyRelative("kind").intValue = (int)source.Effects[i].Kind;
                effect.FindPropertyRelative("amount").longValue = source.Effects[i].Amount;
            }
        }

        private static void ValidateModifier(ModifierAsset asset, ModifierSource source)
        {
            var serialized = new SerializedObject(asset);
            foreach (KeyValuePair<string, string> field in source.Fields)
            {
                SerializedProperty property = serialized.FindProperty(field.Key);
                string actual = property.propertyType == SerializedPropertyType.String ? property.stringValue
                    : property.propertyType == SerializedPropertyType.Boolean ? (property.boolValue ? "1" : "0")
                    : property.propertyType == SerializedPropertyType.Enum ? property.intValue.ToString(CultureInfo.InvariantCulture)
                    : property.longValue.ToString(CultureInfo.InvariantCulture);
                if (actual != field.Value) throw new InvalidOperationException("Existing modifier differs from frozen source: " + asset.name + "." + field.Key);
            }
            SerializedProperty effects = serialized.FindProperty("effects");
            if (effects.arraySize != source.Effects.Count) throw new InvalidOperationException("Modifier effect count differs: " + asset.name);
            for (int i = 0; i < effects.arraySize; ++i)
            {
                SerializedProperty effect = effects.GetArrayElementAtIndex(i);
                if (effect.FindPropertyRelative("kind").intValue != source.Effects[i].Kind
                    || effect.FindPropertyRelative("amount").longValue != source.Effects[i].Amount)
                    throw new InvalidOperationException("Modifier effect differs: " + asset.name);
            }
            asset.Build();
        }

        private static Dictionary<string, string> ReadFamilyNames(string source)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            Match block = Regex.Match(source, @"familyNames\s*=[\s\S]*?\{([\s\S]*?)\n        \};");
            foreach (Match entry in Regex.Matches(block.Groups[1].Value, @"\{ ""([^""]+)"", ""([^""]+)"" \}"))
                result.Add(entry.Groups[1].Value, entry.Groups[2].Value);
            if (result.Count == 0) throw new InvalidOperationException("Frozen family names could not be parsed.");
            return result;
        }

        private static Dictionary<string, IdentitySource> ReadIdentities(string source)
        {
            var result = new Dictionary<string, IdentitySource>(StringComparer.Ordinal);
            const string pattern = @"\{ ""([^""]+)"", new Identity\(""([^""]+)"", SpiritModifierArtwork\.Kind\.(\w+),\s*condition => condition is (\w+)(?: value)?\s*\? ([\s\S]*?) : null\) \}";
            foreach (Match match in Regex.Matches(source, pattern))
            {
                var template = new StringBuilder();
                string expression = match.Groups[5].Value;
                foreach (Match token in Regex.Matches(expression, @"""([^""]*)""|value\.(Parameter|SecondaryParameter)"))
                    template.Append(token.Groups[2].Success
                        ? (token.Groups[2].Value == "Parameter" ? "{parameter}" : "{secondaryParameter}")
                        : token.Groups[1].Value);
                result.Add(match.Groups[1].Value, new IdentitySource(match.Groups[2].Value,
                    (ModifierArtworkKind)Enum.Parse(typeof(ModifierArtworkKind), match.Groups[3].Value),
                    match.Groups[4].Value, template.ToString()));
            }
            int sourceCount = Regex.Matches(source, @"new Identity\(").Count;
            if (result.Count != sourceCount || result.Count == 0)
                throw new InvalidOperationException("Frozen identities could not all be parsed.");
            return result;
        }

        private static long ParseLong(string value) => long.Parse(value, CultureInfo.InvariantCulture);

        private sealed class ModifierSource
        {
            internal readonly Dictionary<string, string> Fields = new Dictionary<string, string>(StringComparer.Ordinal);
            internal readonly List<EffectSource> Effects = new List<EffectSource>();
        }

        private readonly struct EffectSource
        {
            internal readonly long Kind;
            internal readonly long Amount;
            internal EffectSource(long kind, long amount) { Kind = kind; Amount = amount; }
        }

        private sealed class IdentitySource
        {
            internal readonly string Name;
            internal readonly ModifierArtworkKind Artwork;
            internal readonly string ConditionType;
            internal readonly string Template;
            internal IdentitySource(string name, ModifierArtworkKind artwork, string conditionType, string template)
            { Name = name; Artwork = artwork; ConditionType = conditionType; Template = template; }
        }
    }
}
