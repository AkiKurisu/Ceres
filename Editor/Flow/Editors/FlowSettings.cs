using System.Linq;
using Ceres.Graph.Flow;
using Chris.Configs.Editor;
using Chris.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Editor
{
    public class FlowSettings : ConfigSingleton<FlowSettings>
    {
        private static FlowSettings _setting;

        [SerializeField]
        private bool logExecutableReflection;

        [SerializeField]
        private string[] alwaysIncludedAssemblyWildcards = FlowConfig.DefaultIncludedAssemblyWildcards.ToArray();

        public static void SaveSettings()
        {
            Instance.Save(true);
            var serializer = ConfigsEditorUtils.GetConfigSerializer();
            var config = FlowConfig.Get();
            config.logExecutableReflection = Instance.logExecutableReflection;
            config.alwaysIncludedAssemblyWildcards = Instance.alwaysIncludedAssemblyWildcards;
            config.Save(serializer);
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