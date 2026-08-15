using Ceres.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Gameplay.Editor
{
    public enum RemoteUpdateSerializeMode
    {
        AssetBundle,
        PreferText,
        ForceText
    }
    
    [BaseConfig]
    public class CeresGameplaySettings : ConfigSingleton<CeresGameplaySettings>
    {
        public bool enableRemoteUpdate;
        
        public RemoteUpdateSerializeMode remoteUpdateSerializeMode = RemoteUpdateSerializeMode.AssetBundle;

        public bool subsystemForceInitializeBeforeGet = true;
        
        internal static void SaveSettings()
        {
            Instance.Save(true);
            var config = GameplayConfig.Get();
            config.enableRemoteUpdate = Instance.enableRemoteUpdate;
            config.subsystemForceInitializeBeforeGet = Instance.subsystemForceInitializeBeforeGet;
            Serialize(config);
        }
    }

    internal class CeresGameplaySettingsProvider : SettingsProvider
    {
        private SerializedObject _settingsObject;
        
        private class Styles
        {
            public static readonly GUIContent RemoteUpdateSerializeModeLabel = new("Serialize Mode", 
                "Asset Bundle: Always export a single AssetBundle. " +
                "Prefer Text: Export plain text if has no dependencies. " +
                "Force Text: Always export plain text which may loose assets dependencies.");
            
            public static readonly GUIContent SubsystemForceInitializeBeforeGetLabel = new("Force Initialize Before Get", 
                "Whether to ensure that world subsystem is initialized before getting the system instance.");
            
            public static readonly GUIContent EnableRemoteUpdateLabel = new("Enable Remote Update", 
                "Whether to enable per-actor remote update.");
        }

        private CeresGameplaySettingsProvider(string path, SettingsScope scope = SettingsScope.User)
            : base(path, scope)
        {
            
        }
        
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _settingsObject = new SerializedObject(CeresGameplaySettings.Instance);
        }
        
        public override void OnGUI(string searchContext)
        {
            DrawRemoteUpdateSettings();
            DrawWorldSubsystemSettings();
        }
        
        private void DrawRemoteUpdateSettings()
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUILayout.Label("Remote Update", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(_settingsObject.FindProperty(nameof(CeresGameplaySettings.enableRemoteUpdate)),
                Styles.EnableRemoteUpdateLabel);
            EditorGUILayout.PropertyField(_settingsObject.FindProperty(nameof(CeresGameplaySettings.remoteUpdateSerializeMode)),
                Styles.RemoteUpdateSerializeModeLabel);
            if (_settingsObject.ApplyModifiedPropertiesWithoutUndo())
            {
                CeresGameplaySettings.SaveSettings();
            }
            GUILayout.EndVertical();
        }
        
        private void DrawWorldSubsystemSettings()
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUILayout.Label("World Subsystem", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(_settingsObject.FindProperty(nameof(CeresGameplaySettings.subsystemForceInitializeBeforeGet)),
                Styles.SubsystemForceInitializeBeforeGetLabel);
            if (_settingsObject.ApplyModifiedPropertiesWithoutUndo())
            {
                CeresGameplaySettings.SaveSettings();
            }
            GUILayout.EndVertical();
        }
        
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new CeresGameplaySettingsProvider("Project/Ceres/Gameplay Settings", SettingsScope.Project)
            {
                keywords = GetSearchKeywordsFromGUIContentProperties<Styles>()
            };
            return provider;
        }
    }
}
