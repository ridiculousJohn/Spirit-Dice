using System;
using System.Collections.Generic;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    /**
     * Minimal test-created UI hierarchies that stand in for authored prefabs: a canvas root with a test theme, and the
     * screens and rows the presentation tests bind. Disposing destroys everything the fixture created.
     */
    internal sealed class SyntheticUi : IDisposable
    {
        private readonly List<Object> created = new List<Object>();

        public RectTransform Root { get; }

        public SyntheticUi()
        {
            var theme = Own(ScriptableObject.CreateInstance<SpiritUiTheme>());
            var root = Own(new GameObject("Synthetic UI", typeof(RectTransform), typeof(Canvas), typeof(SpiritUiContext)));
            root.GetComponent<SpiritUiContext>().Configure(theme);
            Root = (RectTransform)root.transform;
        }

        /** Records an object the fixture destroys on disposal. */
        public T Own<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        public RectTransform Rect(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        public T Add<T>(Transform parent, string name) where T : Component => Rect(parent, name).gameObject.AddComponent<T>();

        public Button NewButton(Transform parent, string name)
        {
            var image = Add<Image>(parent, name);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        /** A category badge with its accent, glyph and border. */
        public SpiritModifierCategoryBadge CategoryBadge(Transform parent)
        {
            var badge = Add<SpiritModifierCategoryBadge>(parent, "Category");
            var accent = Add<Image>(badge.transform, "Accent");
            Set(badge, "<Accent>k__BackingField", accent);
            Set(badge, "<Glyph>k__BackingField", Add<Image>(badge.transform, "Glyph"));
            Set(badge, "border", accent.gameObject.AddComponent<Outline>());
            return badge;
        }

        /** A family row template, inactive outside any list, with the given number of variant slots. */
        public SpiritModifierFamilyRow FamilyRow(int variantCount)
        {
            var row = Add<SpiritModifierFamilyRow>(Root, "Family Row Template");
            var artwork = Rect(row.transform, "Artwork");
            var variantRoot = Rect(row.transform, "Variants");
            Set(row, "artwork", artwork.gameObject);
            Set(row, "variantRoot", variantRoot.gameObject);
            Set(row, "icon", Add<Image>(artwork, "Icon"));
            Set(row, "sigil", Add<SpiritModifierSigilGraphic>(artwork, "Sigil"));
            Set(row, "familyName", Add<Text>(row.transform, "Family Name"));
            Set(row, "condition", Add<Text>(row.transform, "Condition"));
            using (var serialized = new SerializedObject(row))
            {
                var variants = serialized.FindProperty("variants");
                Assert.That(variants, Is.Not.Null, "variants");
                variants.arraySize = variantCount;
                for (int index = 0; index < variantCount; ++index)
                {
                    var button = NewButton(variantRoot, "Variant " + index);
                    var element = variants.GetArrayElementAtIndex(index);
                    element.FindPropertyRelative("Button").objectReferenceValue = button;
                    element.FindPropertyRelative("Category").objectReferenceValue = Add<Image>(button.transform, "Category");
                    element.FindPropertyRelative("Effect").objectReferenceValue = Add<Text>(button.transform, "Effect");
                    element.FindPropertyRelative("State").objectReferenceValue = Add<Text>(button.transform, "State");
                    element.FindPropertyRelative("Selection").objectReferenceValue = Add<Image>(button.transform, "Selection");
                    element.FindPropertyRelative("Visibility").objectReferenceValue = button.gameObject.AddComponent<CanvasGroup>();
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            row.gameObject.SetActive(false);
            return row;
        }

        /** An encounter-charms screen with a scrolling family list of two-variant rows, narration, Begin and Back. */
        public SpiritEncounterCharmsScreen CharmsScreen(Transform parent)
        {
            var screen = Add<SpiritEncounterCharmsScreen>(parent, "Encounter Charms");
            var scroll = Add<ScrollRect>(screen.transform, "Families");
            var content = Rect(scroll.transform, "Content");
            scroll.content = content;
            Set(screen, "rowPrefab", FamilyRow(2));
            Set(screen, "<Scroll>k__BackingField", scroll);
            Set(screen, "<Content>k__BackingField", content);
            Set(screen, "<Narration>k__BackingField", Add<Text>(screen.transform, "Narration"));
            Set(screen, "<BeginButton>k__BackingField", NewButton(screen.transform, "Begin"));
            Set(screen, "<BackButton>k__BackingField", NewButton(screen.transform, "Back"));
            return screen;
        }

        public static void Set(Object target, string property, Object value)
        {
            using var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(property);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + property);
            field.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(field.objectReferenceValue, Is.SameAs(value), target.GetType().Name + "." + property);
        }

        public static void Set(Object target, string property, Color value)
        {
            using var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(property);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + property);
            field.colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Object target, string property, string value)
        {
            using var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(property);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + property);
            field.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public void Dispose()
        {
            for (int index = created.Count - 1; index >= 0; --index)
                if (created[index] != null) Object.DestroyImmediate(created[index]);
            created.Clear();
        }
    }
}
