using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

namespace Ceres.Gameplay.Level.Editor
{
    [CustomPropertyDrawer(typeof(LevelNameAttribute))]
    public class LevelNameDrawer : PropertyDrawer
    {
        private static readonly GUIContent IsNotStringLabel = new("The property type is not string.");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            if (property.propertyType == SerializedPropertyType.String)
            {
                Rect popupPosition = new(position)
                {
                    height = EditorGUIUtility.singleLineHeight
                };
                var names = LevelSceneDataTableManager.Get().GetLevelReferences().Select(x => x.Name).Distinct()
                    .ToArray();
                int currentIdx = Array.IndexOf(names, property.stringValue);
                if (currentIdx == -1) currentIdx = 0;
                int index = EditorGUI.Popup(position: popupPosition, label.text, selectedIndex: currentIdx,
                    displayedOptions: names);
                if (index >= 0)
                {
                    property.stringValue = names[index];
                }
            }
            else
            {
                EditorGUI.LabelField(position, label, IsNotStringLabel);
            }

            EditorGUI.EndProperty();
        }
    }
}