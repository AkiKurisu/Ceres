using System;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Ceres.Serialization.Editor
{
    [CustomPropertyDrawer(typeof(SerializedObjectBase), true)]
    public sealed class SerializedObjectDrawer : PropertyDrawer
    {
        private const string NullType = "Null";

        private Type GetFieldType()
        {
            var fieldType = fieldInfo.FieldType;
            if (fieldType.IsArray)
            {
                fieldType = fieldType.GetElementType();
            }

            return fieldType;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var fieldType = GetFieldType();
            if (fieldType == typeof(SerializedObjectBase))
            {
                return GetPropertyHeightBase(property);
            }
            
            return GetPropertyHeightGeneric(property, label);
        }

        private static float GetPropertyHeightGeneric(SerializedProperty property, GUIContent label)
        {
            var reference = property.FindPropertyRelative("serializedTypeString");
                var objectHandleProp = property.FindPropertyRelative("objectHandle");
                var handle = new SoftObjectHandle(objectHandleProp.ulongValue);
                var type = SerializedType.FromString(reference.stringValue);
                var wrapper = SerializedObjectWrapperManager.CreateWrapper(type, ref handle);
                if (objectHandleProp.ulongValue != handle.Handle)
                {
                    objectHandleProp.ulongValue = handle.Handle;
                    property.serializedObject.ApplyModifiedProperties();
                }

                return EditorGUIUtility.singleLineHeight
                        + SerializedObjectWrapperDrawer.CalculatePropertyHeight(wrapper)
                        + EditorGUIUtility.standardVerticalSpacing;
        }
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var fieldType = GetFieldType();
            if (fieldType == typeof(SerializedObjectBase))
            {
                DrawGUIBase(position, property, label);
                return;
            }
            DrawGUI(position, property, label,fieldType);
        }
        
        private void DrawGUI(Rect position, SerializedProperty property, GUIContent label, Type fieldType)
        {
            var reference = property.FindPropertyRelative("serializedTypeString");
            var json = property.FindPropertyRelative("jsonData");
            var objectHandleProp = property.FindPropertyRelative("objectHandle");
            var handle = new SoftObjectHandle(objectHandleProp.ulongValue);
            var type = SerializedType.FromString(reference.stringValue);
            SerializedObjectWrapper wrapper = SerializedObjectWrapperManager.GetWrapper(type, handle);
            string id = type != null ? type.Name : NullType;
            if (type != null && !wrapper)
            {
                wrapper = SerializedObjectWrapperManager.CreateWrapper(type, ref handle);
                objectHandleProp.ulongValue = handle.Handle;
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.BeginProperty(position, label, property);
            var totalHeight = position.height;
            position.height = EditorGUIUtility.singleLineHeight;

            float width = position.width;
            float x = position.x;
            position.width = GUI.skin.label.CalcSize(label).x;
            GUI.Label(position, label);
            position.x += position.width + 10;
            position.width = width - position.width - 10;
            if (EditorGUI.DropdownButton(position, new GUIContent(id), FocusType.Keyboard))
            {
                var provider = ScriptableObject.CreateInstance<TypeSearchWindow>();
                provider.Initialize(fieldType!.GetGenericArguments()[0], (selectType) =>
                {
                    reference.stringValue = selectType != null ? SerializedType.ToString(selectType) : string.Empty;
                    if (selectType != null)
                    {
                        wrapper = SerializedObjectWrapperManager.CreateWrapper(type, ref handle);
                    }
                    else
                    {
                        wrapper = null;
                        SerializedObjectWrapperManager.DestroyWrapper(handle);
                    }
                    objectHandleProp.ulongValue = handle.Handle;
                    property.serializedObject.ApplyModifiedProperties();
                });
                SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), provider);
            }
            position.x = x;
            if (wrapper && !string.IsNullOrEmpty(json.stringValue))
            {
                JsonUtility.FromJsonOverwrite(json.stringValue, wrapper.Value);
            }
            if (wrapper)
            {
                position.x = x;
                position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
                position.height = totalHeight - position.height - EditorGUIUtility.standardVerticalSpacing;
                position.width = width;
                GUI.Box(position, "", "Box");
                EditorGUI.BeginChangeCheck();
                SerializedObjectWrapperDrawer.DrawGUI(position, wrapper);
                if (EditorGUI.EndChangeCheck())
                {
                    json.stringValue = JsonUtility.ToJson(wrapper.Value);
                }
            }
            EditorGUI.EndProperty();
        }

        private float GetPropertyHeightBase(SerializedProperty property)
        {
            var reference = property.FindPropertyRelative("serializedTypeString");
            var objectHandleProp = property.FindPropertyRelative("objectHandle");
            var handle = new SoftObjectHandle(objectHandleProp.ulongValue);
            var type = SerializedType.FromString(reference.stringValue);
            var wrapper = SerializedObjectWrapperManager.CreateWrapper(type, ref handle);
            if (objectHandleProp.ulongValue != handle.Handle)
            {
                objectHandleProp.ulongValue = handle.Handle;
                property.serializedObject.ApplyModifiedProperties();
            }

            return SerializedObjectWrapperDrawer.CalculatePropertyHeight(wrapper);
        }

        private static void DrawGUIBase(Rect position, SerializedProperty property, GUIContent label)
        {
            var reference = property.FindPropertyRelative("serializedTypeString");
            var json = property.FindPropertyRelative("jsonData");
            var objectHandleProp = property.FindPropertyRelative("objectHandle");
            var handle = new SoftObjectHandle(objectHandleProp.ulongValue);
            var type = SerializedType.FromString(reference.stringValue);
            SerializedObjectWrapper wrapper = SerializedObjectWrapperManager.GetWrapper(type, handle);
            if (type != null && !wrapper)
            {
                wrapper = SerializedObjectWrapperManager.CreateWrapper(type, ref handle);
                objectHandleProp.ulongValue = handle.Handle;
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.BeginProperty(position, label, property);
            var totalHeight = position.height;
            position.height = EditorGUIUtility.singleLineHeight;

            float width = position.width;
            float x = position.x;
            position.width = GUI.skin.label.CalcSize(label).x;
            GUI.Label(position, label);
            position.x += position.width + 10;
            position.width = width - position.width - 10;
            position.x = x;
            if (wrapper && !string.IsNullOrEmpty(json.stringValue))
            {
                JsonUtility.FromJsonOverwrite(json.stringValue, wrapper.Value);
            }
            if (wrapper)
            {
                position.x = x;
                position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
                position.height = totalHeight - position.height - EditorGUIUtility.standardVerticalSpacing;
                position.width = width;
                GUI.Box(position, "", "Box");
                EditorGUI.BeginChangeCheck();
                SerializedObjectWrapperDrawer.DrawGUI(position, wrapper);
                if (EditorGUI.EndChangeCheck())
                {
                    json.stringValue = JsonUtility.ToJson(wrapper.Value);
                }
            }
            EditorGUI.EndProperty();
        }
    }
}