using System;
using System.Linq;
using Ceres.Graph;
using UnityEditor;
using UnityEngine;
using UEditor = UnityEditor.Editor;

namespace Ceres.Editor
{
    [CustomEditor(typeof(APIUpdateConfig))]
    public class APIUpdateConfigEditor : UEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (GUILayout.Button("Update API"))
            {
                APIUpdater.UpdateAPI(typeof(ScriptableObject), (APIUpdateConfig)target);
            }
        }
    }
    
    public static class APIUpdater
    {
        public static void UpdateAPI(Type filterAssetType, APIUpdateConfig updateConfig)
        {
            var assets = AssetDatabase.FindAssets($"t:{filterAssetType}")
                .Select(x => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(x)))
                .Where(x => x is ICeresGraphContainer)
                .ToList();
            foreach (var asset in assets)
            {
                UpdateAPI(asset, updateConfig);
            }
        }

        private static void UpdateAPI(ScriptableObject asset, APIUpdateConfig updateConfig)
        {
            var serializeObject = new SerializedObject(asset)
            {
                forceChildVisibility = true
            };
            bool isDirty = false;
            SerializedProperty iterator = serializeObject.GetIterator();
            while (iterator.NextVisible(true))
            {
                if (iterator.propertyType == SerializedPropertyType.ManagedReference)
                {
                    foreach (var pair in updateConfig.nodeRedirectors)
                    {
                        if (iterator.managedReferenceFullTypename == pair.source.GetFullTypeName())
                        {
                            iterator.managedReferenceValue = JsonUtility.FromJson(JsonUtility.ToJson(iterator.managedReferenceValue), pair.target.ToType());
                            isDirty = true;
                        }
                    }
                }
            }
            if (isDirty)
            {
                CeresLogger.Log($"{asset.name} api update succeed");
                serializeObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
            }
            serializeObject.Dispose();
        }
    }
}