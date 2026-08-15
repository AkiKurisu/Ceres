using System;
using System.Collections.Generic;
using System.Reflection;
using Ceres.Graph;
using UnityEditor;
using UnityEngine;

namespace Ceres.Editor.Graph
{
    /// <summary>
    /// Utility class for drawing inspector properties with special handling for SharedVariable fields
    /// </summary>
    public static class InspectorPropertyDrawer
    {
        /// <summary>
        /// Draw a property field with special handling for SharedVariable types
        /// </summary>
        /// <param name="prop">Serialized property to draw</param>
        /// <param name="fieldInfoMap">Map of field names to FieldInfo for type checking</param>
        /// <param name="allowedFieldNames">Set of allowed field names</param>
        /// <returns>True if the property was drawn, false if it should be skipped</returns>
        public static bool DrawPropertyField(SerializedProperty prop, Dictionary<string, FieldInfo> fieldInfoMap, HashSet<string> allowedFieldNames)
        {
            var propertyPath = prop.propertyPath;
            var fieldName = propertyPath.Replace("m_Value.", "");

            if (!allowedFieldNames.Contains(fieldName)) return false;

            if (fieldInfoMap.TryGetValue(fieldName, out var fieldInfo))
            {
                var fieldType = fieldInfo.FieldType;

                if (typeof(SharedVariable).IsAssignableFrom(fieldType))
                {
                    var valueProp = prop.FindPropertyRelative("value");
                    if (valueProp == null) return false;

                    if (typeof(SharedString).IsAssignableFrom(fieldType))
                    {
                        var multiline = fieldInfo.GetCustomAttribute<MultilineAttribute>() != null;
                        if (multiline)
                        {
                            EditorGUILayout.LabelField(prop.displayName);
                            EditorGUI.indentLevel++;
                            var textAreaStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
                            valueProp.stringValue = EditorGUILayout.TextArea(valueProp.stringValue, textAreaStyle, GUILayout.MinHeight(60));
                            EditorGUI.indentLevel--;
                            EditorGUILayout.Space(3);
                            return true;
                        }
                    }

                    EditorGUILayout.PropertyField(valueProp, new GUIContent(prop.displayName), true);
                    EditorGUILayout.Space(3);
                    return true;
                }
            }

            EditorGUILayout.PropertyField(prop, true);
            EditorGUILayout.Space(3);
            return true;
        }
    }
}

