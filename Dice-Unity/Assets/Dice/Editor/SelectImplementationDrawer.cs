using System;
using System.Linq;
using Dice.UnityContent;
using UnityEditor;
using UnityEngine;

namespace Dice.Editor
{
    /** Draws a [SelectImplementation] reference with its fields and a menu of None and the concrete classes its field type allows. */
    [CustomPropertyDrawer(typeof(SelectImplementationAttribute))]
    public sealed class SelectImplementationDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUI.GetPropertyHeight(property, label, true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var menuRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y,
                position.width - EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
            Type current = property.managedReferenceValue?.GetType();
            if (EditorGUI.DropdownButton(menuRect, new GUIContent(current != null ? current.Name : "None"), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                var target = property.serializedObject;
                string path = property.propertyPath;
                void Set(object value)
                {
                    target.Update();
                    target.FindProperty(path).managedReferenceValue = value;
                    target.ApplyModifiedProperties();
                }
                // None clears an optional reference; the field type itself is a choice when it is concrete.
                menu.AddItem(new GUIContent("None"), current == null, () => Set(null));
                Type fieldType = FieldType(property);
                foreach (Type type in TypeCache.GetTypesDerivedFrom(fieldType).Prepend(fieldType)
                    .Where(type => !type.IsAbstract && !type.IsGenericType && type.GetConstructor(Type.EmptyTypes) != null)
                    .OrderBy(type => type.Name, StringComparer.Ordinal))
                    menu.AddItem(new GUIContent(type.Name), type == current, () => Set(Activator.CreateInstance(type)));
                menu.DropDown(menuRect);
            }
            EditorGUI.PropertyField(position, property, label, true);
            EditorGUI.EndProperty();
        }

        /** Unity names a managed reference's field type as "assembly namespace.class". */
        private static Type FieldType(SerializedProperty property)
        {
            string name = property.managedReferenceFieldTypename;
            int split = name.IndexOf(' ');
            return Type.GetType(name.Substring(split + 1) + ", " + name.Substring(0, split), true);
        }
    }
}
