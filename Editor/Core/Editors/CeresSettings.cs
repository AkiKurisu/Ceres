using System;
using Chris.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace Ceres.Editor
{
    [FilePath("ProjectSettings/CeresSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class CeresSettings : ScriptableSingleton<CeresSettings>
    {
        public enum GraphEditorDisplayMode
        {
            Normal,
            Debug
        }
        
        private static CeresSettings _setting;

        [SerializeField] 
        private GraphEditorDisplayMode graphEditorDisplayMode;
        
        [SerializeField]
        private string[] preservedTypes = Array.Empty<string>();
        
        /// <summary>
        /// Ceres graph editor view display mode
        /// </summary>
        public static GraphEditorDisplayMode DisplayMode => instance.graphEditorDisplayMode;

        /// <summary>
        /// Ceres graph editor will display in debug mode
        /// </summary>
        public static bool DisplayDebug => DisplayMode == GraphEditorDisplayMode.Debug;

        /// <summary>
        /// Save <see cref="CeresSettings"/>.
        /// </summary>
        public static void SaveSettings()
        {
            instance.Save(true);
        }

        internal static void AddPreservedType(Type type)
        {
            Assert.IsNotNull(type);
            var serializedType = SerializedType.ToString(type);
            if (ArrayUtility.IndexOf(instance.preservedTypes, serializedType) >= 0) return;
            ArrayUtility.Add(ref instance.preservedTypes, serializedType);
        }

        internal static string[] GetPreservedTypes()
        {
            return instance.preservedTypes;
        }
    }

    internal class CeresSettingsProvider : SettingsProvider
    {
        private SerializedObject _serializedObject;
        
        private class Styles
        {
            public static readonly GUIContent GraphEditorDisplayModeLabel = new("Display Mode",
                "Set graph editor display mode.");
            
            public static readonly GUIContent PreserveTypesLabel = new("Preserved Types",
                "Define types need to be preserved in build when using IL2CPP scripting backend.");
        }

        private CeresSettingsProvider(string path, SettingsScope scope = SettingsScope.User) : base(path, scope) { }
        
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _serializedObject = new SerializedObject(CeresSettings.instance);
        }
        
        public override void OnGUI(string searchContext)
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUILayout.Label("Editor Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("graphEditorDisplayMode"), Styles.GraphEditorDisplayModeLabel);
            GUILayout.EndVertical();
            GUILayout.Label("Stripping Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("preservedTypes"), Styles.PreserveTypesLabel);
            GUILayout.EndVertical();
            if (_serializedObject.ApplyModifiedPropertiesWithoutUndo())
            {
                CeresSettings.SaveSettings();
            }
        }
        
        [SettingsProvider]
        public static SettingsProvider CreateCeresSettingsProvider()
        {
            var provider = new CeresSettingsProvider("Project/Ceres", SettingsScope.Project)
            {
                keywords = GetSearchKeywordsFromGUIContentProperties<Styles>()
            };
            
            return provider;
        }
    }
}