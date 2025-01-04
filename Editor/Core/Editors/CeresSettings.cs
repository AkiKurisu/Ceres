using Chris.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Ceres.Editor
{
    [FilePath("ProjectSettings/CeresSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class CeresSettings : ScriptableSingleton<CeresSettings>
    {
        private static CeresSettings _setting;
        
        [SerializeField, HideInInspector]
        private bool enableGraphEditorLog;
        
        [SerializeField, HideInInspector]
        private bool disableILPostProcess;
        
        /// <summary>
        /// Whether ceres graph editor can log in console
        /// </summary>
        public static bool EnableGraphEditorLog => instance.enableGraphEditorLog;

        public static void SaveSettings()
        {
            instance.Save(true);
        }
    }

    internal class CeresSettingsProvider : SettingsProvider
    {
        private SerializedObject _serializedObject;
        
        private const string DisableILPostProcessSymbol = "CERES_DISABLE_ILPP";
        
        private class Styles
        {
            public static readonly GUIContent EnableGraphEditorLogStyle = new("Enable Graph Editor Log",
                "Enable to log editor information");
            
            public static readonly GUIContent DisableILPostProcessStyle = new("Disable ILPP", 
                "Disable IL Post Process, default Ceres will emit il after syntax analysis step to enhance runtime performance, " +
                "disable can speed up editor compilation, recommend to enable in final production build");
        }

        private CeresSettingsProvider(string path, SettingsScope scope = SettingsScope.User) : base(path, scope) { }
        
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _serializedObject = new SerializedObject(CeresSettings.instance);
        }
        
        public override void OnGUI(string searchContext)
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            var disableILPostProcessProp = _serializedObject.FindProperty("disableILPostProcess");
            GUILayout.Label("Editor Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            disableILPostProcessProp.boolValue = ScriptingSymbol.ContainsScriptingSymbol(DisableILPostProcessSymbol);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("enableGraphEditorLog"), Styles.EnableGraphEditorLogStyle);
            GUILayout.EndVertical();
            _serializedObject.ApplyModifiedPropertiesWithoutUndo();

            GUILayout.Label("Runtime Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(disableILPostProcessProp, Styles.DisableILPostProcessStyle);
            GUILayout.EndVertical();
            if (_serializedObject.ApplyModifiedPropertiesWithoutUndo())
            {
                if (disableILPostProcessProp.boolValue)
                {
                    if (CeresSettings.EnableGraphEditorLog)
                    {
                        Debug.Log("[Ceres] Disable ILPP");
                    }
                    ScriptingSymbol.AddScriptingSymbol(DisableILPostProcessSymbol);
                }
                else
                {
                    if (CeresSettings.EnableGraphEditorLog)
                    {
                        Debug.Log("[Ceres] Enable ILPP");
                    }
                    ScriptingSymbol.RemoveScriptingSymbol(DisableILPostProcessSymbol);
                }
                CeresSettings.SaveSettings();
            }
        }
        
        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            var provider = new CeresSettingsProvider("Project/Ceres Settings", SettingsScope.Project)
            {
                keywords = GetSearchKeywordsFromGUIContentProperties<Styles>()
            };
            
            return provider;
        }
    }
}