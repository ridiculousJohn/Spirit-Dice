using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR
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
        private readonly Dictionary<ModifierFamilyDefinition.Id, ModifierFamilyAsset> families =
            new Dictionary<ModifierFamilyDefinition.Id, ModifierFamilyAsset>();

        public T Own<T>(T item) where T : UnityEngine.Object
        {
            created.Add(item);
            return item;
        }

        /** The family asset of the test family named by the text (see TestFamilies); one asset per family. */
        public ModifierFamilyAsset Family(string name) => Family(TestFamilies.IdOf(name), name);

        /** The family asset of a family definition, named as TestFamilies names it; one asset per family. */
        public ModifierFamilyAsset Family(ModifierFamilyDefinition family) => Family(family.Identity, TestFamilies.NameOf(family));

        private ModifierFamilyAsset Family(ModifierFamilyDefinition.Id id, string name)
        {
#if UNITY_EDITOR
            if (families.TryGetValue(id, out ModifierFamilyAsset family)) return family;
            family = Own(ScriptableObject.CreateInstance<ModifierFamilyAsset>());
            family.name = name;
            var serialized = new SerializedObject(family);
            serialized.FindProperty(ContentAsset.AssetGuidField).stringValue = id.Value.ToString("N");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            families.Add(id, family);
            return family;
#else
            throw EditorRequired();
#endif
        }

        /**
         * An encounter asset whose id is the test encounter id of the name (see TestEncounters), with one stage of the
         * given threshold and the given charm lists. With reward layouts, each the pools of its slots, it has a reward
         * policy offering them with one refresh cost and one hand-upgrade option.
         */
        public EncounterAsset Encounter(string name, long threshold = 100, ModifierAsset[] startingModifiers = null,
            ModifierAsset[] encounterModifiers = null, ModifierAsset[] unlockRewardModifiers = null,
            RewardPoolAsset[][] rewardLayouts = null)
        {
#if UNITY_EDITOR
            var encounter = Own(ScriptableObject.CreateInstance<EncounterAsset>());
            encounter.name = name;
            var serialized = new SerializedObject(encounter);
            serialized.FindProperty(ContentAsset.AssetGuidField).stringValue = TestEncounters.IdOf(name).Value.ToString("N");
            SerializedProperty stages = serialized.FindProperty("stages");
            stages.arraySize = 1;
            stages.GetArrayElementAtIndex(0).FindPropertyRelative("threshold").longValue = threshold;
            stages.GetArrayElementAtIndex(0).FindPropertyRelative("startingHands").intValue = 3;
            SetList(serialized.FindProperty("startingModifiers"), startingModifiers ?? Array.Empty<ModifierAsset>());
            SetList(serialized.FindProperty("encounterModifiers"), encounterModifiers ?? Array.Empty<ModifierAsset>());
            SetList(serialized.FindProperty("unlockRewardModifiers"), unlockRewardModifiers ?? Array.Empty<ModifierAsset>());
            if (rewardLayouts != null)
            {
                SerializedProperty policy = serialized.FindProperty("rewardPolicy");
                policy.FindPropertyRelative("id").stringValue = "policy." + name;
                policy.FindPropertyRelative("refreshCost").intValue = 1;
                policy.FindPropertyRelative("handUpgradeOptionCount").intValue = 1;
                SerializedProperty history = policy.FindPropertyRelative("upgradeHistory");
                history.arraySize = 1;
                history.GetArrayElementAtIndex(0).FindPropertyRelative("maxIncomingStageNumber").intValue = int.MaxValue;
                SerializedProperty layouts = policy.FindPropertyRelative("modifierLayouts");
                layouts.arraySize = rewardLayouts.Length;
                for (int i = 0; i < rewardLayouts.Length; ++i)
                    SetList(layouts.GetArrayElementAtIndex(i).FindPropertyRelative("pools"), rewardLayouts[i]);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return encounter;
#else
            throw EditorRequired();
#endif
        }

        /** A table piece asset whose id is the test piece id of the name (see TestPieces). */
        public TablePieceAsset Piece(string name)
        {
#if UNITY_EDITOR
            var piece = Own(ScriptableObject.CreateInstance<TablePieceAsset>());
            piece.name = name;
            var serialized = new SerializedObject(piece);
            serialized.FindProperty(ContentAsset.AssetGuidField).stringValue = TestPieces.IdOf(name).Value.ToString("N");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return piece;
#else
            throw EditorRequired();
#endif
        }

        /** A reward pool asset of the name, drawing from the charms, with the fallback charm if one is given. */
        public RewardPoolAsset RewardPool(string name, ModifierAsset[] modifiers, ModifierAsset fallback = null)
        {
#if UNITY_EDITOR
            var pool = Own(ScriptableObject.CreateInstance<RewardPoolAsset>());
            pool.name = name;
            var serialized = new SerializedObject(pool);
            SetList(serialized.FindProperty("modifiers"), modifiers);
            serialized.FindProperty("fallbackModifier").objectReferenceValue = fallback;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return pool;
#else
            throw EditorRequired();
#endif
        }

        /**
         * A charm asset in the test family named by family, or by its id without one; without a condition it always
         * activates, and without effects it adds 10 base score.
         */
        public ModifierAsset Charm(string id, string family = null, ModifierPhase phase = ModifierPhase.PlayHand,
            ModifierConditionConfig condition = null, ModifierActivationLimit limit = ModifierActivationLimit.OncePerHand,
            params ModifierEffectConfig[] effects)
            => CharmIn(Family(family ?? id), id, phase, condition, limit, effects);

        private ModifierAsset CharmIn(ModifierFamilyAsset family, string id, ModifierPhase phase = ModifierPhase.PlayHand,
            ModifierConditionConfig condition = null, ModifierActivationLimit limit = ModifierActivationLimit.OncePerHand,
            params ModifierEffectConfig[] effects)
        {
#if UNITY_EDITOR
            var asset = Own(ScriptableObject.CreateInstance<ModifierAsset>());
            asset.name = id;
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("family").objectReferenceValue = family;
            serialized.FindProperty("phase").intValue = (int)phase;
            serialized.FindProperty("condition").managedReferenceValue = condition ?? new AlwaysConditionConfig();
            serialized.FindProperty("activationLimit").intValue = (int)limit;
            if (effects.Length == 0) effects = new ModifierEffectConfig[] { new AddBaseScoreEffectConfig(10) };
            SerializedProperty list = serialized.FindProperty("effects");
            list.arraySize = effects.Length;
            for (int i = 0; i < effects.Length; ++i) list.GetArrayElementAtIndex(i).managedReferenceValue = effects[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
#else
            throw EditorRequired();
#endif
        }

        /** A 2 x 2 sprite destroyed on disposal. */
        public Sprite Icon()
        {
            var texture = Own(new Texture2D(2, 2));
            return Own(Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f)));
        }

        /** A category with the given mark; without one it shows no mark. */
        public ModifierCategoryPresentationAsset Category(Sprite symbol = null, Color? accent = null, Color? ink = null)
        {
#if UNITY_EDITOR
            var category = Own(ScriptableObject.CreateInstance<ModifierCategoryPresentationAsset>());
            category.name = symbol != null ? "Category " + symbol.name : "Category without symbol";
            var serialized = new SerializedObject(category);
            serialized.FindProperty("symbol").objectReferenceValue = symbol;
            if (accent.HasValue) serialized.FindProperty("accentColor").colorValue = accent.Value;
            if (ink.HasValue) serialized.FindProperty("inkColor").colorValue = ink.Value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return category;
#else
            throw EditorRequired();
#endif
        }

        /** A charm presentation; without a given icon it shows a fabricated one. */
        public ModifierPresentationAsset Presentation(ModifierAsset modifier, ModifierCategoryPresentationAsset category,
            string displayName = null, string template = "Test condition.", Sprite icon = null)
        {
#if UNITY_EDITOR
            var presentation = Own(ScriptableObject.CreateInstance<ModifierPresentationAsset>());
            presentation.name = "Presentation " + (modifier != null ? modifier.Id : "without charm");
            var serialized = new SerializedObject(presentation);
            serialized.FindProperty("modifier").objectReferenceValue = modifier;
            serialized.FindProperty("displayName").stringValue = displayName ?? "Name of " + modifier?.Id;
            serialized.FindProperty("conditionTemplate").stringValue = template;
            serialized.FindProperty("category").objectReferenceValue = category;
            serialized.FindProperty("icon").objectReferenceValue = icon != null ? icon : Icon();
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

        /** A distinct test text format: amounts and labels in brackets, each separator its own mark. */
        public static ModifierTextFormat TextFormat() => new ModifierTextFormat("[{amount} {label}]", " & ", "[{amount}]",
            "[{amount} {label}]", " # ", "{phase} @ {limit}", "[{amount} {label}]", " ; ");

        /** Test family names: "Family of <family asset name>" for every family of the given charm presentations. */
        public static ModifierFamilyWording[] FamilyWording(IEnumerable<ModifierPresentationAsset> modifiers) => modifiers
            .Where(presentation => presentation != null && presentation.Modifier != null)
            .Select(presentation => presentation.Modifier.Family).Distinct()
            .Select(family => new ModifierFamilyWording(family, "Family of " + family.name)).ToArray();

        /** A skin of the given presentations and categories; without family names it names every family as FamilyWording does. */
        public ModifierSkinAsset Skin(IEnumerable<ModifierPresentationAsset> modifiers,
            IEnumerable<ModifierCategoryPresentationAsset> categories, IEnumerable<ModifierEffectWording> wording = null,
            string noEffectsText = "No effects", IEnumerable<ModifierPhaseWording> phases = null,
            IEnumerable<ModifierActivationLimitWording> limits = null, ModifierContributionWording contribution = null,
            IEnumerable<ModifierFamilyWording> families = null, ModifierTextFormat format = null)
        {
#if UNITY_EDITOR
            var skin = Own(ScriptableObject.CreateInstance<ModifierSkinAsset>());
            skin.name = "Test Skin";
            var serialized = new SerializedObject(skin);
            ModifierPresentationAsset[] presentations = modifiers.ToArray();
            SetList(serialized.FindProperty("modifiers"), presentations);
            SetList(serialized.FindProperty("categories"), categories.ToArray());
            ModifierFamilyWording[] familyRows = (families ?? FamilyWording(presentations)).ToArray();
            SerializedProperty familyList = serialized.FindProperty("familyWording");
            familyList.arraySize = familyRows.Length;
            for (int i = 0; i < familyRows.Length; ++i)
            {
                SerializedProperty element = familyList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("family").objectReferenceValue = familyRows[i].Family;
                element.FindPropertyRelative("name").stringValue = familyRows[i].Name;
            }
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
            format = format ?? TextFormat();
            SerializedProperty textFormat = serialized.FindProperty("textFormat");
            textFormat.FindPropertyRelative("effect").stringValue = format.Effect;
            textFormat.FindPropertyRelative("effectSeparator").stringValue = format.EffectSeparator;
            textFormat.FindPropertyRelative("compactEffect").stringValue = format.CompactEffect;
            textFormat.FindPropertyRelative("labeledCompactEffect").stringValue = format.LabeledCompactEffect;
            textFormat.FindPropertyRelative("compactEffectSeparator").stringValue = format.CompactEffectSeparator;
            textFormat.FindPropertyRelative("timing").stringValue = format.Timing;
            textFormat.FindPropertyRelative("contribution").stringValue = format.Contribution;
            textFormat.FindPropertyRelative("contributionSeparator").stringValue = format.ContributionSeparator;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return skin;
#else
            throw EditorRequired();
#endif
        }

        /** Builds the presentation of the content from the skin. */
        public ModifierPresentationCatalog Load(ModifierSkinAsset skin, MechanicalContent content)
            => new ModifierPresentationCatalog(skin, content);

        /**
         * A presentation of every modifier of an engine catalog: each is paired with a charm asset of its id and presented
         * as "Name of <id>" with the condition text "Condition of <id>", the given category (or one with a fabricated
         * symbol) and a fabricated icon.
         */
        public ModifierPresentationCatalog For(MechanicalCatalog catalog, ModifierCategoryPresentationAsset category = null)
        {
            category = category != null ? category : Category(Icon());
            var pairs = new List<KeyValuePair<ModifierAsset, ModifierDefinition>>();
            var presentations = new List<ModifierPresentationAsset>();
            foreach (ModifierDefinition definition in catalog.Modifiers)
            {
                ModifierAsset charm = CharmIn(Family(definition.Family), definition.Id);
                pairs.Add(new KeyValuePair<ModifierAsset, ModifierDefinition>(charm, definition));
                presentations.Add(Presentation(charm, category, template: "Condition of " + definition.Id));
            }
            return Load(Skin(presentations, new[] { category }), new MechanicalContent(catalog, pairs));
        }

        public void Dispose()
        {
            foreach (UnityEngine.Object item in created)
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            created.Clear();
            families.Clear();
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
