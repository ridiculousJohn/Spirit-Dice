using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Dice.Tests
{
    /**
     * Test-only charm assets, charm presentations, categories and skins, created in memory and destroyed on disposal,
     * with the charm presentations loaded from them. Authoring the assets' serialized fields requires the Unity editor.
     */
    public class SyntheticCharmAssets : IDisposable
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private readonly List<IDisposable> loaded = new List<IDisposable>();

        public T Own<T>(T item) where T : UnityEngine.Object
        {
            created.Add(item);
            return item;
        }

        /** A charm asset; without effects it adds 10 base score. */
        public ModifierAsset Charm(string id, string familyId = null, ModifierPhase phase = ModifierPhase.PlayHand,
            ModifierConditionKind condition = ModifierConditionKind.Always, int parameter = 0, int secondaryParameter = 0,
            ModifierActivationLimit limit = ModifierActivationLimit.OncePerHand, params (ModifierEffectKind kind, long amount)[] effects)
        {
#if UNITY_EDITOR
            var asset = Own(ScriptableObject.CreateInstance<ModifierAsset>());
            asset.name = id;
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("familyId").stringValue = familyId ?? id;
            serialized.FindProperty("phase").intValue = (int)phase;
            serialized.FindProperty("conditionKind").intValue = (int)condition;
            serialized.FindProperty("parameter").intValue = parameter;
            serialized.FindProperty("secondaryParameter").intValue = secondaryParameter;
            serialized.FindProperty("activationLimit").intValue = (int)limit;
            if (effects.Length == 0) effects = new[] { (ModifierEffectKind.AddBaseScore, 10L) };
            SerializedProperty list = serialized.FindProperty("effects");
            list.arraySize = effects.Length;
            for (int i = 0; i < effects.Length; ++i)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("kind").intValue = (int)effects[i].kind;
                list.GetArrayElementAtIndex(i).FindPropertyRelative("amount").longValue = effects[i].amount;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
#else
            throw EditorRequired();
#endif
        }

        public ModifierCategoryPresentationAsset Category(ModifierCategorySymbol symbol = ModifierCategorySymbol.Base,
            Color? accent = null, Color? ink = null)
        {
#if UNITY_EDITOR
            var category = Own(ScriptableObject.CreateInstance<ModifierCategoryPresentationAsset>());
            category.name = "Category " + symbol;
            var serialized = new SerializedObject(category);
            serialized.FindProperty("symbol").intValue = (int)symbol;
            if (accent.HasValue) serialized.FindProperty("accentColor").colorValue = accent.Value;
            if (ink.HasValue) serialized.FindProperty("inkColor").colorValue = ink.Value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return category;
#else
            throw EditorRequired();
#endif
        }

        public ModifierPresentationAsset Presentation(ModifierAsset modifier, ModifierCategoryPresentationAsset category,
            string displayName = null, string template = "Test condition.", ModifierArtworkKind artwork = ModifierArtworkKind.None,
            Sprite icon = null)
        {
#if UNITY_EDITOR
            var presentation = Own(ScriptableObject.CreateInstance<ModifierPresentationAsset>());
            presentation.name = "Presentation " + (modifier != null ? modifier.Id : "without charm");
            var serialized = new SerializedObject(presentation);
            serialized.FindProperty("modifier").objectReferenceValue = modifier;
            serialized.FindProperty("displayName").stringValue = displayName ?? "Name of " + modifier?.Id;
            serialized.FindProperty("conditionTemplate").stringValue = template;
            serialized.FindProperty("artwork").intValue = (int)artwork;
            serialized.FindProperty("category").objectReferenceValue = category;
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return presentation;
#else
            throw EditorRequired();
#endif
        }

        /** Distinct test wording for every effect kind. */
        public static ModifierEffectWording[] Wording() => new[]
        {
            new ModifierEffectWording(ModifierEffectKind.AddBaseScore, "base score", "base score", "B"),
            new ModifierEffectWording(ModifierEffectKind.AddMultiplier, "multiplier", "multiplier", "M"),
            new ModifierEffectWording(ModifierEffectKind.AddRerolls, "reroll", "rerolls", "R")
        };

        /** Distinct test wording for every phase; no phase implies a limit. */
        public static ModifierPhaseWording[] PhaseWording() => Enum.GetValues(typeof(ModifierPhase)).Cast<ModifierPhase>()
            .Select(phase => new ModifierPhaseWording(phase, "timing of " + phase, "lead of " + phase)).ToArray();

        /** Distinct test wording for every activation limit. */
        public static ModifierActivationLimitWording[] LimitWording() => Enum.GetValues(typeof(ModifierActivationLimit))
            .Cast<ModifierActivationLimit>()
            .Select(limit => new ModifierActivationLimitWording(limit, "timing of " + limit, "note of " + limit)).ToArray();

        /** Distinct test contribution wording. */
        public static ModifierContributionWording ContributionWording() =>
            new ModifierContributionWording("base label", "multiplier label", "reroll label", "rerolls label");

        public ModifierSkinAsset Skin(IEnumerable<ModifierPresentationAsset> modifiers,
            IEnumerable<ModifierCategoryPresentationAsset> categories, IEnumerable<ModifierEffectWording> wording = null,
            string noEffectsText = "No effects", IEnumerable<ModifierPhaseWording> phases = null,
            IEnumerable<ModifierActivationLimitWording> limits = null, ModifierContributionWording contribution = null)
        {
#if UNITY_EDITOR
            var skin = Own(ScriptableObject.CreateInstance<ModifierSkinAsset>());
            skin.name = "Test Skin";
            var serialized = new SerializedObject(skin);
            SetList(serialized.FindProperty("modifiers"), modifiers.ToArray());
            SetList(serialized.FindProperty("categories"), categories.ToArray());
            ModifierEffectWording[] effects = (wording ?? Wording()).ToArray();
            SerializedProperty list = serialized.FindProperty("effectWording");
            list.arraySize = effects.Length;
            for (int i = 0; i < effects.Length; ++i)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("kind").intValue = (int)effects[i].Kind;
                element.FindPropertyRelative("singularLabel").stringValue = effects[i].SingularLabel;
                element.FindPropertyRelative("pluralLabel").stringValue = effects[i].PluralLabel;
                element.FindPropertyRelative("compactLabel").stringValue = effects[i].CompactLabel;
            }
            serialized.FindProperty("noEffectsText").stringValue = noEffectsText;
            ModifierPhaseWording[] phaseRows = (phases ?? PhaseWording()).ToArray();
            SerializedProperty phaseList = serialized.FindProperty("phaseWording");
            phaseList.arraySize = phaseRows.Length;
            for (int i = 0; i < phaseRows.Length; ++i)
            {
                SerializedProperty element = phaseList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("phase").intValue = (int)phaseRows[i].Phase;
                element.FindPropertyRelative("timingLabel").stringValue = phaseRows[i].TimingLabel;
                element.FindPropertyRelative("descriptionLead").stringValue = phaseRows[i].DescriptionLead;
                SerializedProperty implied = element.FindPropertyRelative("impliedLimits");
                implied.arraySize = phaseRows[i].ImpliedLimits.Count;
                for (int j = 0; j < phaseRows[i].ImpliedLimits.Count; ++j)
                    implied.GetArrayElementAtIndex(j).intValue = (int)phaseRows[i].ImpliedLimits[j];
            }
            ModifierActivationLimitWording[] limitRows = (limits ?? LimitWording()).ToArray();
            SerializedProperty limitList = serialized.FindProperty("limitWording");
            limitList.arraySize = limitRows.Length;
            for (int i = 0; i < limitRows.Length; ++i)
            {
                SerializedProperty element = limitList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("limit").intValue = (int)limitRows[i].Limit;
                element.FindPropertyRelative("timingLabel").stringValue = limitRows[i].TimingLabel;
                element.FindPropertyRelative("descriptionNote").stringValue = limitRows[i].DescriptionNote;
            }
            contribution = contribution ?? ContributionWording();
            SerializedProperty contributionWording = serialized.FindProperty("contributionWording");
            contributionWording.FindPropertyRelative("baseScoreLabel").stringValue = contribution.BaseScoreLabel;
            contributionWording.FindPropertyRelative("multiplierLabel").stringValue = contribution.MultiplierLabel;
            contributionWording.FindPropertyRelative("rerollLabel").stringValue = contribution.RerollLabel;
            contributionWording.FindPropertyRelative("rerollsLabel").stringValue = contribution.RerollsLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return skin;
#else
            throw EditorRequired();
#endif
        }

        /** Builds a presentation that is disposed with these charms. */
        public ModifierPresentationCatalog Load(ModifierSkinAsset skin, MechanicalContent content)
        {
            var presentation = new ModifierPresentationCatalog(skin, content);
            loaded.Add(presentation);
            return presentation;
        }

        /**
         * A presentation of every modifier of an engine catalog: each is paired with a charm asset of its id and presented
         * as "Name of <id>" with the condition text "Condition of <id>", the given category (a base category without one)
         * and the given icon.
         */
        public ModifierPresentationCatalog For(MechanicalCatalog catalog, ModifierCategoryPresentationAsset category = null,
            Sprite icon = null)
        {
            category = category != null ? category : Category();
            var pairs = new List<KeyValuePair<ModifierAsset, ModifierDefinition>>();
            var presentations = new List<ModifierPresentationAsset>();
            foreach (ModifierDefinition definition in catalog.Modifiers)
            {
                ModifierAsset charm = Charm(definition.Id, definition.FamilyId);
                pairs.Add(new KeyValuePair<ModifierAsset, ModifierDefinition>(charm, definition));
                presentations.Add(Presentation(charm, category, template: "Condition of " + definition.Id, icon: icon));
            }
            return Load(Skin(presentations, new[] { category }), new MechanicalContent(catalog, pairs));
        }

        /**
         * A presentation of every modifier of an engine catalog in a test skin with the wording of the given skin: a
         * modifier with the id of a charm the given skin presents shows that charm's presentation, paired with that
         * charm asset, and every other modifier is presented as For(catalog) presents it. The given skin's assets are
         * referenced, never owned.
         */
        public ModifierPresentationCatalog For(MechanicalCatalog catalog, ModifierSkinAsset presented)
        {
            if (presented == null) throw new ArgumentNullException(nameof(presented));
            var presentedById = new Dictionary<string, ModifierPresentationAsset>(StringComparer.Ordinal);
            foreach (ModifierPresentationAsset presentation in presented.Modifiers)
                presentedById.Add(presentation.Modifier.Id, presentation);
            ModifierCategoryPresentationAsset testCategory = null;
            var categories = new List<ModifierCategoryPresentationAsset>();
            var pairs = new List<KeyValuePair<ModifierAsset, ModifierDefinition>>();
            var presentations = new List<ModifierPresentationAsset>();
            foreach (ModifierDefinition definition in catalog.Modifiers)
            {
                if (presentedById.TryGetValue(definition.Id, out ModifierPresentationAsset presentation))
                {
                    pairs.Add(new KeyValuePair<ModifierAsset, ModifierDefinition>(presentation.Modifier, definition));
                    presentations.Add(presentation);
                    if (!categories.Contains(presentation.Category)) categories.Add(presentation.Category);
                    continue;
                }
                if (testCategory == null)
                {
                    testCategory = Category();
                    categories.Add(testCategory);
                }
                ModifierAsset charm = Charm(definition.Id, definition.FamilyId);
                pairs.Add(new KeyValuePair<ModifierAsset, ModifierDefinition>(charm, definition));
                presentations.Add(Presentation(charm, testCategory, template: "Condition of " + definition.Id));
            }
            return Load(Skin(presentations, categories, presented.EffectWording, presented.NoEffectsText, presented.PhaseWording,
                presented.LimitWording, presented.ContributionWording), new MechanicalContent(catalog, pairs));
        }

        public void Dispose()
        {
            foreach (IDisposable presentation in loaded) presentation.Dispose();
            loaded.Clear();
            foreach (UnityEngine.Object item in created)
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            created.Clear();
        }

#if UNITY_EDITOR
        private static void SetList(SerializedProperty list, UnityEngine.Object[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; ++i) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
#else
        private static NotSupportedException EditorRequired() =>
            new NotSupportedException("Synthetic charm assets are authored through the Unity editor's serialized objects.");
#endif
    }
}
