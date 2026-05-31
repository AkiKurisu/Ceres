using System.Linq;
using Ceres.Graph.Flow;
using Chris.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Editor
{
    [BaseConfig]
    public class FlowSettings : ConfigSingleton<FlowSettings>
    {
        private static FlowSettings _setting;

        [SerializeField]
        private bool logExecutableReflection;

        [SerializeField]
        private string[] alwaysIncludedAssemblyWildcards = FlowConfig.DefaultIncludedAssemblyWildcards.ToArray();

        [SerializeField]
        private FlowGeneratedRuntimeProfile generatedRuntimeProfile = FlowGeneratedRuntimeProfile.OptimizedSafe;

        [SerializeField]
        private FlowGeneratedRuntimeCancellationMode generatedRuntimeCancellationMode =
            FlowGeneratedRuntimeCancellationMode.Auto;

        [SerializeField]
        private FlowGeneratedRuntimeVariableStorageMode generatedRuntimeVariableStorageMode =
            FlowGeneratedRuntimeVariableStorageMode.LocalFieldsForUnshared;

        [SerializeField]
        private FlowGeneratedRuntimeSerializedTypeMode generatedRuntimeSerializedTypeMode =
            FlowGeneratedRuntimeSerializedTypeMode.DirectType;

        public static FlowGeneratedRuntimeProfile GeneratedRuntimeProfile => Instance.generatedRuntimeProfile;

        public static FlowGeneratedRuntimeCancellationMode GeneratedRuntimeCancellationMode =>
            Instance.generatedRuntimeCancellationMode;

        public static FlowGeneratedRuntimeVariableStorageMode GeneratedRuntimeVariableStorageMode =>
            Instance.generatedRuntimeVariableStorageMode;

        public static FlowGeneratedRuntimeSerializedTypeMode GeneratedRuntimeSerializedTypeMode =>
            Instance.generatedRuntimeSerializedTypeMode;

        public static void SaveSettings()
        {
            Instance.Save(true);
            var config = FlowConfig.Get();
            config.logExecutableReflection = Instance.logExecutableReflection;
            config.alwaysIncludedAssemblyWildcards = Instance.alwaysIncludedAssemblyWildcards;
            config.generatedRuntimeProfile = Instance.generatedRuntimeProfile;
            config.generatedRuntimeCancellationMode = Instance.generatedRuntimeCancellationMode;
            config.generatedRuntimeVariableStorageMode = Instance.generatedRuntimeVariableStorageMode;
            config.generatedRuntimeSerializedTypeMode = Instance.generatedRuntimeSerializedTypeMode;
            Serialize(config);
        }
    }

    internal class FlowSettingsProvider : SettingsProvider
    {
        private SerializedObject _serializedObject;

        private class Styles
        {
            public static readonly GUIContent LogExecutableReflectionLabel = new("Executable Reflection",
                "Log executable reflection.");

            public static readonly GUIContent AlwaysIncludedAssemblyWildcardsLabel = new("Always Included Assembly Wildcards",
                "Add wildcards to define which assemblies should Flow always include in the executable reflection system.");

            public static readonly GUIContent GeneratedRuntimeProfileLabel = new("Generated Runtime Profile",
                "Select how Flow C# runtime source should be generated.");

            public static readonly GUIContent GeneratedRuntimeCancellationModeLabel = new("Cancellation Checks",
                "Control cancellation checks emitted by generated Flow C# runtime.");

            public static readonly GUIContent GeneratedRuntimeVariableStorageModeLabel = new("Variable Storage",
                "Control how generated Flow C# runtime stores non-shared variables.");

            public static readonly GUIContent GeneratedRuntimeSerializedTypeModeLabel = new("Serialized Type",
                "Control how generated Flow C# runtime emits statically selected SerializedType inputs.");
        }

        private FlowSettingsProvider(string path, SettingsScope scope = SettingsScope.User) : base(path, scope) { }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _serializedObject = new SerializedObject(FlowSettings.Instance);
        }

        public override void OnGUI(string searchContext)
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            
            GUILayout.Label("Reflection System", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("alwaysIncludedAssemblyWildcards"), Styles.AlwaysIncludedAssemblyWildcardsLabel);
            GUILayout.EndVertical();

            GUILayout.Label("Generated Runtime", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("generatedRuntimeProfile"), Styles.GeneratedRuntimeProfileLabel);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("generatedRuntimeCancellationMode"), Styles.GeneratedRuntimeCancellationModeLabel);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("generatedRuntimeVariableStorageMode"), Styles.GeneratedRuntimeVariableStorageModeLabel);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("generatedRuntimeSerializedTypeMode"), Styles.GeneratedRuntimeSerializedTypeModeLabel);
            GUILayout.EndVertical();
            
            GUILayout.Label("Log Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("logExecutableReflection"), Styles.LogExecutableReflectionLabel);
            GUILayout.EndVertical();
            
            if (_serializedObject.ApplyModifiedPropertiesWithoutUndo())
            {
                FlowSettings.SaveSettings();
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateFlowSettingsProvider()
        {
            var provider = new FlowSettingsProvider("Project/Ceres/Flow Settings", SettingsScope.Project)
            {
                keywords = GetSearchKeywordsFromGUIContentProperties<Styles>()
            };

            return provider;
        }
    }
}
