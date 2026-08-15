using System;
using System.Linq;
using Ceres.Configs;
using Ceres.DataDriven;
using Ceres.DataDriven.Editor;
using Ceres.Modules;
using Ceres.Schedulers;
using Ceres.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Editor
{
    [BaseConfig]
    public class CeresSettings : ConfigSingleton<CeresSettings>
    {
        public SerializedType<RuntimeModule>[] modules;

        public bool schedulerStackTrace = true;

        public bool initializeDataTableManagerOnLoad;

        public bool validateDataTableBeforeLoad = true;

        public SerializedType<IDataTableEditorSerializer> dataTableEditorSerializer = SerializedType<IDataTableEditorSerializer>.FromType(typeof(DataTableEditorJsonSerializer));

        public bool inlineRowReadOnly;

        public SerializedType<ISerializeFormatter> configSerializer = SerializedType<ISerializeFormatter>.FromType(typeof(TextSerializeFormatter));

        public string password;
        
        internal static void SaveSettings()
        {
            Instance.Save(true);

            ConfigFileLocation location = "Ceres";
            var configFile = ConfigSystem.GetProjectConfigFile(location);

            var schedulerSettings = SchedulerConfig.Get();
            schedulerSettings.enableStackTrace = Instance.schedulerStackTrace;
            configFile.SetConfig(SchedulerConfig.Location, schedulerSettings);

            var dataDrivenSettings = DataDrivenConfig.Get();
            dataDrivenSettings.initializeDataTableManagerOnLoad = Instance.initializeDataTableManagerOnLoad;
            dataDrivenSettings.validateDataTableBeforeLoad = Instance.validateDataTableBeforeLoad;
            configFile.SetConfig(DataDrivenConfig.Location, dataDrivenSettings);

            var configsSettings = ConfigsConfig.Get();
            configsSettings.configSerializer = Instance.configSerializer;
            configsSettings.password = Instance.password;
            configFile.SetConfig(ConfigsConfig.Location, configsSettings);

            var moduleConfig = ModuleConfig.Get();
            moduleConfig.Modules = Instance.modules.ToArray();
            configFile.SetConfig(ModuleConfig.Location, moduleConfig);

            Serialize(location, configFile);
        }
    }

    internal class CeresSettingsProvider : SettingsProvider
    {
        private SerializedObject _settingsObject;
        
        private class Styles
        {
            public static readonly GUIContent StackTraceSchedulerLabel = new("Stack Trace",
                "Allow trace scheduled task in editor.");

            public static readonly GUIContent DataTableSerializerLabel = new("Editor Serializer",
                "Set the serializer type in DataTable Editor.");

            public static readonly GUIContent InitializeDataTableManagerOnLoadLabel = new("Initialize Managers",
                "Initialize all DataTableManager instances before the scene loads.");

            public static readonly GUIContent ValidateDataTableBeforeLoadLabel = new("Validate Before Load",
                "Verify the existence of a DataTable before loading it." +
                "Disabling this feature may cause exceptions to be thrown on load, resulting in unexpected behavior. " +
                "Disable only after checking that all DataTables exist.");

            public static readonly GUIContent InlineRowReadOnlyLabel = new("Inline Row ReadOnly",
                "Enable to make the DataTableRow in the inspector list view read-only.");

            public static readonly GUIContent ConfigSerializerLabel = new("Config Serializer",
                "Set the serializer type for user data config files.");
            
            public static readonly GUIContent PasswordLabel = new("Encrypt Password",
                "Set the user data serializer encrypt password.");

            public static readonly GUIContent ModulesLabel = new("Modules",
                "List of RuntimeModule types to initialize at startup. " +
                "Used instead of assembly scanning under IL2CPP to ensure reliable module discovery.");

            public static readonly GUIContent RegisterAllModulesButton = new("Register All",
                "Scan all non-Editor assemblies and register every RuntimeModule subclass.");
        }

        private CeresSettingsProvider(string path, SettingsScope scope = SettingsScope.User) : base(path, scope) { }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            if (!CeresSettings.Instance.dataTableEditorSerializer.IsValid())
            {
                CeresSettings.Instance.dataTableEditorSerializer =
                    SerializedType<IDataTableEditorSerializer>.FromType(typeof(DataTableEditorJsonSerializer));
                CeresSettings.SaveSettings();
            }
            if (!CeresSettings.Instance.configSerializer.IsValid())
            {
                CeresSettings.Instance.configSerializer = SerializedType<ISerializeFormatter>.FromType(typeof(TextSerializeFormatter));
                CeresSettings.SaveSettings();
            }
            _settingsObject = new SerializedObject(CeresSettings.Instance);
        }

        public override void OnDeactivate()
        {
            CeresSettings.SaveSettings();
        }

        public override void OnGUI(string searchContext)
        {
            DrawModuleSettings();
            DrawSchedulerSettings();
            DrawDataTableSettings();
            DrawConfigSettings();
        }

        private void DrawModuleSettings()
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUILayout.Label("Module Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(_settingsObject.FindProperty(nameof(CeresSettings.modules)), Styles.ModulesLabel, true);
            if (_settingsObject.ApplyModifiedPropertiesWithoutUndo())
            {
                CeresSettings.SaveSettings();
            }
            if (GUILayout.Button(Styles.RegisterAllModulesButton))
            {
                RegisterAllModules();
            }
            GUILayout.EndVertical();
        }

        private void RegisterAllModules()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.GetName().Name.Contains(".Editor"))
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(RuntimeModule).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                .OrderBy(t => t.FullName)
                .ToArray();

            CeresSettings.Instance.modules = types
                .Select(SerializedType<RuntimeModule>.FromType)
                .ToArray();

            EditorUtility.SetDirty(CeresSettings.Instance);
            CeresSettings.SaveSettings();
            _settingsObject.Update();
        }

        private void DrawSchedulerSettings()
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUILayout.Label("Scheduler Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(_settingsObject.FindProperty(nameof(CeresSettings.schedulerStackTrace)), Styles.StackTraceSchedulerLabel);
            if (_settingsObject.ApplyModifiedPropertiesWithoutUndo())
            {
                CeresSettings.SaveSettings();
            }
            GUILayout.EndVertical();
        }

        private void DrawDataTableSettings()
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUILayout.Label("DataTable Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(_settingsObject.FindProperty(nameof(CeresSettings.initializeDataTableManagerOnLoad)), Styles.InitializeDataTableManagerOnLoadLabel);
            EditorGUILayout.PropertyField(_settingsObject.FindProperty(nameof(CeresSettings.validateDataTableBeforeLoad)), Styles.ValidateDataTableBeforeLoadLabel);
            EditorGUILayout.PropertyField(_settingsObject.FindProperty(nameof(CeresSettings.dataTableEditorSerializer)), Styles.DataTableSerializerLabel);
            EditorGUILayout.PropertyField(_settingsObject.FindProperty(nameof(CeresSettings.inlineRowReadOnly)), Styles.InlineRowReadOnlyLabel);
            if (_settingsObject.ApplyModifiedPropertiesWithoutUndo())
            {
                if (!CeresSettings.Instance.dataTableEditorSerializer.IsValid())
                {
                    CeresSettings.Instance.dataTableEditorSerializer = SerializedType<IDataTableEditorSerializer>.FromType(typeof(DataTableEditorJsonSerializer));
                }
                CeresSettings.SaveSettings();
            }
            GUILayout.EndVertical();
        }

        private void DrawConfigSettings()
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUILayout.Label("Config Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(_settingsObject.FindProperty(nameof(CeresSettings.configSerializer)), Styles.ConfigSerializerLabel);
            EditorGUILayout.PropertyField(_settingsObject.FindProperty(nameof(CeresSettings.password)), Styles.PasswordLabel);
            if (_settingsObject.ApplyModifiedPropertiesWithoutUndo())
            {
                if (!CeresSettings.Instance.configSerializer.IsValid())
                {
                    CeresSettings.Instance.configSerializer = SerializedType<ISerializeFormatter>.FromType(typeof(TextSerializeFormatter));
                }
                CeresSettings.SaveSettings();
            }
            GUILayout.EndVertical();
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new CeresSettingsProvider("Project/Ceres", SettingsScope.Project)
            {
                keywords = GetSearchKeywordsFromGUIContentProperties<Styles>()
            };
            return provider;
        }
    }
}
