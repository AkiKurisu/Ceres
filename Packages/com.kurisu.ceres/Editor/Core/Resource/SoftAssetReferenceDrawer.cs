using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Linq;
using System;
using Ceres.Editor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UObject = UnityEngine.Object;

namespace Ceres.Resource.Editor
{
    public abstract class AssetReferenceDrawer : PropertyDrawer
    {
        private const string AddressPropertyName = "Address";
        
        private const string GuidPropertyName = "Guid";
        
        private const string LockPropertyName = "Locked";
        
        private static readonly GUIContent LockedContent = new("", "when address is in locked, validation will prefer to use Object value");
        
        private static readonly GUIStyle LockedStyle = new("IN LockButton");
        
        protected abstract Type GetAssetType();
        
        public sealed override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GetPropertyMetaData(
                out var assetGroup,
                out var assetType,
                out var processMethod,
                out var forceMoveToGroup,
                out var registerAddressable);
            var addressProp = property.FindPropertyRelative(AddressPropertyName);
            var guidProp = property.FindPropertyRelative(GuidPropertyName);
            var lockProp = property.FindPropertyRelative(LockPropertyName);

            // Get cache asset from hard reference
            UObject Object = SoftAssetReferenceEditorUtils.GetAssetFromGUID(guidProp.stringValue);

            // TODO: Only need validate once
            ValidateAddress();

            ListenDragAndDrop(position, property);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            position.height = EditorGUIUtility.singleLineHeight;
            Object = EditorGUI.ObjectField(position, label, Object, assetType, false);
            if (EditorGUI.EndChangeCheck())
            {
                if (Object)
                {
                    AssignAddress(
                        property,
                        Object,
                        processMethod,
                        assetGroup,
                        forceMoveToGroup,
                        registerAddressable);
                }
                else
                {
                    addressProp.stringValue = string.Empty;
                    guidProp.stringValue = string.Empty;
                }
            }
            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
            position.width -= 30;
            GUI.enabled = !lockProp.boolValue;
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(position, addressProp, GUIContent.none, true);
            GUI.enabled = true;
            position.x += position.width + 10;
            position.width = 20;
            lockProp.boolValue = GUI.Toggle(position, lockProp.boolValue, LockedContent, LockedStyle);
            if (EditorGUI.EndChangeCheck())
            {
                Object = ResourceEditorUtils.FindAssetEntry(addressProp.stringValue, assetType)?.MainAsset;
                guidProp.stringValue = Object.GetAssetGUID();
            }
            EditorGUI.EndProperty();
            return;

            void ValidateAddress()
            {
                if (!Object && !string.IsNullOrEmpty(addressProp.stringValue))
                {
                    Object = ResourceEditorUtils.FindAssetEntry(addressProp.stringValue, assetType)?.MainAsset;
                    guidProp.stringValue = Object.GetAssetGUID();
                }
                else if (Object && string.IsNullOrEmpty(addressProp.stringValue))
                {
                    // when reference is in locked mode, prefer to Object value
                    if (lockProp.boolValue)
                    {
                        AssignAddress(
                            property,
                            Object,
                            processMethod,
                            assetGroup,
                            forceMoveToGroup,
                            registerAddressable);
                    }
                    else
                    {
                        Object = null;
                        guidProp.stringValue = string.Empty;
                    }
                }
            }
        }
        
        private static void AssignAddress(
            SerializedProperty property,
            UObject Object,
            string processMethod,
            AddressableAssetGroup assetGroup,
            bool forceMoveToGroup,
            bool registerAddressable)
        {
            var addressProp = property.FindPropertyRelative(AddressPropertyName);
            var guidProp = property.FindPropertyRelative(GuidPropertyName);
            // Already exists => use entry current address, ensure to not affect other references
            string path = AssetDatabase.GetAssetPath(Object);
            var existingEntry = Object.ToAddressableAssetEntry();
            if (existingEntry != null &&
                (!registerAddressable || !(forceMoveToGroup && existingEntry.parentGroup != assetGroup)))
            {
                addressProp.stringValue = existingEntry.address;
                guidProp.stringValue = Object.GetAssetGUID();
            }
            else
            {
                // Format a stable address, then register only when the field contract requests it.
                if (string.IsNullOrEmpty(processMethod))
                {
                    addressProp.stringValue = path;
                }
                else
                {
                    var target = ReflectionEditorUtility.GetTargetObjectWithProperty(property);
                    var method = target
                        .GetType()
                        .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => m.GetParameters().Length == 1)
                        .FirstOrDefault(x => x.Name == processMethod);
                    if (method != null)
                        addressProp.stringValue = (string)method.Invoke(target, new object[] { Object });
                    else
                        addressProp.stringValue = path;
                }
                guidProp.stringValue = Object.GetAssetGUID();
                if (registerAddressable)
                {
                    using (assetGroup.Modify())
                    {
                        var entry = assetGroup.AddAsset(Object);
                        entry.address = addressProp.stringValue;
                    }
                }
            }
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing;
        }
        
        private void ListenDragAndDrop(Rect rect, SerializedProperty property)
        {
            if (!fieldInfo.FieldType.IsArray)
            {
                return;
            }
            int index = property.propertyPath.LastIndexOf(".Array.data[", StringComparison.Ordinal);
            string parentPath = property.propertyPath[..index];
            var parentProp = property.serializedObject.FindProperty(parentPath);
            var evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!rect.Contains(evt.mousePosition))
                    {
                        break;
                    }

                    if (evt.type == EventType.DragPerform)
                    {
                        GetPropertyMetaData(
                            out var assetGroup,
                            out var assetType,
                            out var processMethod,
                            out var forceMoveToGroup,
                            out var registerAddressable);
                        var array = DragAndDrop.objectReferences.Where(asset =>
                        {
                            return asset.GetType() == assetType || asset.GetType().IsSubclassOf(assetType);
                        }).ToArray();

                        if (array.Length <= 1) return;

                        DragAndDrop.AcceptDrag();
                        int startId = 0;
                        var lstProp = parentProp.GetArrayElementAtIndex(parentProp.arraySize - 1);
                        var lstGuid = lstProp.FindPropertyRelative(GuidPropertyName).stringValue;
                        // If last slot is empty, assign it
                        if (string.IsNullOrEmpty(lstGuid))
                        {
                            startId = 1;
                            AssignAddress(
                                lstProp,
                                array[0],
                                processMethod,
                                assetGroup,
                                forceMoveToGroup,
                                registerAddressable);
                            lstProp.FindPropertyRelative(LockPropertyName).boolValue = true;
                        }
                        for (int i = startId; i < array.Length; ++i)
                        {
                            parentProp.InsertArrayElementAtIndex(parentProp.arraySize);
                            var childProp = parentProp.GetArrayElementAtIndex(parentProp.arraySize - 1);
                            AssignAddress(
                                childProp,
                                array[i],
                                processMethod,
                                assetGroup,
                                forceMoveToGroup,
                                registerAddressable);
                            childProp.FindPropertyRelative(LockPropertyName).boolValue = true;
                        }
                        Event.current.Use();
                    }
                    break;
            }
        }
        
        private void GetPropertyMetaData(
            out AddressableAssetGroup assetGroup,
            out Type assetType,
            out string processMethod,
            out bool forceMoveToGroup,
            out bool registerAddressable)
        {
            assetGroup = AddressableAssetSettingsDefaultObject.Settings.DefaultGroup;
            assetType = GetAssetType();
            processMethod = null;
            forceMoveToGroup = false;
            registerAddressable = true;
            var constraint = fieldInfo.GetCustomAttribute<AssetReferenceConstraintAttribute>(false);
            if (constraint != null)
            {
                // Attribute constraint should match generic constraint
                if (constraint.AssetType != null && constraint.AssetType.IsSubclassOf(assetType))
                    assetType = constraint.AssetType;

                processMethod = constraint.Formatter;
                forceMoveToGroup = constraint.ForceGroup;
                registerAddressable = constraint.RegisterAddressable;
                if (!string.IsNullOrEmpty(constraint.Group))
                {
                    assetGroup = ResourceEditorUtils.GetOrCreateAssetGroup(constraint.Group);
                }
            }
        }
    }
    
    [CustomPropertyDrawer(typeof(SoftAssetReference<>))]
    public class SoftAssetReferenceTDrawer : AssetReferenceDrawer
    {
        protected override Type GetAssetType()
        {
            Type assetType = fieldInfo.FieldType;
            if (assetType.IsArray)
            {
                assetType = assetType.GetElementType();
            }
            return assetType!.GetGenericArguments()[0];
        }
    }
    
    [CustomPropertyDrawer(typeof(SoftAssetReference))]
    public class SoftAssetReferenceDrawer : AssetReferenceDrawer
    {
        protected override Type GetAssetType()
        {
            return typeof(UObject);
        }
    }
}
