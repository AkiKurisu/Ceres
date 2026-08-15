using Ceres.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.RuntimeConsole.Editor
{
    [BaseConfig]
    public class RuntimeConsoleSettings : ConfigSingleton<RuntimeConsoleSettings>
    {
        public bool enableConsoleInReleaseBuild;

        internal static void SaveSettings()
        {
            Instance.Save(true);
            var config = RuntimeConsoleConfig.Get();
            config.enableConsoleInReleaseBuild = Instance.enableConsoleInReleaseBuild;
            Serialize(config);
        }
    }

    internal class RuntimeConsoleSettingsProvider : SettingsProvider
    {
        private SerializedObject _settingsObject;

        private class Styles
        {
            public static readonly GUIContent EnableConsoleInReleaseBuildLabel = new("Enable Console In Release Build",
                "Allow runtime console using in release build.");
        }

        private RuntimeConsoleSettingsProvider(string path, SettingsScope scope = SettingsScope.User) : base(path, scope) { }
        
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _settingsObject = new SerializedObject(RuntimeConsoleSettings.Instance);
        }
        
        public override void OnGUI(string searchContext)
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUILayout.Label("Runtime Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(_settingsObject.FindProperty(nameof(RuntimeConsoleSettings.enableConsoleInReleaseBuild)), Styles.EnableConsoleInReleaseBuildLabel);
            if (_settingsObject.ApplyModifiedPropertiesWithoutUndo())
            {
                RuntimeConsoleSettings.SaveSettings();
            }
            GUILayout.EndVertical();
        }
        
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new RuntimeConsoleSettingsProvider("Project/Ceres/Runtime Console Settings", SettingsScope.Project)
            {
                keywords = GetSearchKeywordsFromGUIContentProperties<Styles>()
            };
            return provider;
        }
    }
}