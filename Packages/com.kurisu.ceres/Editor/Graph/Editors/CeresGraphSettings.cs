using System;
using Ceres.Editor;
using Ceres.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace Ceres.Editor.Graph
{
    [BaseConfig]
    public class CeresGraphSettings : ConfigSingleton<CeresGraphSettings>
    {
        public enum GraphEditorDisplayMode
        {
            Normal,
            Debug
        }

        private static CeresGraphSettings _setting;

        [SerializeField]
        private GraphEditorDisplayMode graphEditorDisplayMode;

        [SerializeField]
        private bool cleanLogAuto = true;

        [SerializeField]
        private string[] preservedTypes = Array.Empty<string>();

        [SerializeField]
        private LogType logLevel = LogType.Log;

        public static bool CleanLogAuto => Instance.cleanLogAuto;
        
        /// <summary>
        /// Ceres graph editor view display mode
        /// </summary>
        public static GraphEditorDisplayMode DisplayMode => Instance.graphEditorDisplayMode;

        /// <summary>
        /// Ceres graph editor will display in debug mode
        /// </summary>
        public static bool DisplayDebug => DisplayMode == GraphEditorDisplayMode.Debug;

        /// <summary>
        /// Save <see cref="CeresGraphSettings"/>.
        /// </summary>
        public static void SaveSettings()
        {
            Instance.Save(true);
            var config = CeresConfig.Get();
            config.logLevel = Instance.logLevel;
            Serialize(config);
        }

        internal static void AddPreservedType(Type type)
        {
            Assert.IsNotNull(type);
            var serializedType = SerializedType.ToString(type);
            if (ArrayUtility.IndexOf(Instance.preservedTypes, serializedType) >= 0) return;
            ArrayUtility.Add(ref Instance.preservedTypes, serializedType);
        }

        internal static string[] GetPreservedTypes()
        {
            return Instance.preservedTypes;
        }
    }

    internal class CeresGraphSettingsProvider : SettingsProvider
    {
        private SerializedObject _serializedObject;

        private class Styles
        {
            public static readonly GUIContent GraphEditorDisplayModeLabel = new("Display Mode",
                "Set graph editor display mode.");

            public static readonly GUIContent CleanLogAutoLabel = new("Clean Log Auto",
                "Clean console log automatically after save graph successfully.");

            public static readonly GUIContent PreserveTypesLabel = new("Preserved Types",
                "Define types need to be preserved in build when using IL2CPP scripting backend.");

            public static readonly GUIContent LogLevelLabel = new("Log Level",
                "Ceres log level.");
        }

        private CeresGraphSettingsProvider(string path, SettingsScope scope = SettingsScope.User) : base(path, scope) { }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _serializedObject = new SerializedObject(CeresGraphSettings.Instance);
        }

        public override void OnGUI(string searchContext)
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            
            GUILayout.Label("Editor Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("graphEditorDisplayMode"), Styles.GraphEditorDisplayModeLabel);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("cleanLogAuto"), Styles.CleanLogAutoLabel);
            GUILayout.EndVertical();
            
            GUILayout.Label("Runtime Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("logLevel"), Styles.LogLevelLabel);
            GUILayout.EndVertical();
            
            GUILayout.Label("Stripping Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("preservedTypes"), Styles.PreserveTypesLabel);
            GUILayout.EndVertical();
            
            if (_serializedObject.ApplyModifiedPropertiesWithoutUndo())
            {
                CeresGraphSettings.SaveSettings();
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateCeresGraphSettingsProvider()
        {
            var provider = new CeresGraphSettingsProvider("Project/Ceres/Graph", SettingsScope.Project)
            {
                keywords = GetSearchKeywordsFromGUIContentProperties<Styles>()
            };

            return provider;
        }
    }
}
