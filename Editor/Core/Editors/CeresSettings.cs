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
        private bool disableCodeGen;
        
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
        
        private const string DisableCodeGenSymbol = "CERES_DISABLE_CODEGEN";
        
        private class Styles
        {
            public static readonly GUIContent EnableGraphEditorLogStyle = new("Enable Graph Editor Log",
                "Enable to log editor information");
            
            public static readonly GUIContent DisableCodeGenStyle = new("Disable CodeGen", 
                "Disable code generation, default Ceres will emit il to speed up graph runtime compile");
        }

        private CeresSettingsProvider(string path, SettingsScope scope = SettingsScope.User) : base(path, scope) { }
        
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _serializedObject = new SerializedObject(CeresSettings.instance);
        }
        
        public override void OnGUI(string searchContext)
        {
            var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            var disableCodeGenProp = _serializedObject.FindProperty("disableCodeGen");
            GUILayout.Label("Editor Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            disableCodeGenProp.boolValue = ScriptingSymbol.ContainsScriptingSymbol(DisableCodeGenSymbol);
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("enableGraphEditorLog"), Styles.EnableGraphEditorLogStyle);
            GUILayout.EndVertical();

            GUILayout.Label("Runtime Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(disableCodeGenProp, Styles.DisableCodeGenStyle);
            GUILayout.EndVertical();
            if (_serializedObject.ApplyModifiedPropertiesWithoutUndo())
            {
                if (disableCodeGenProp.boolValue)
                    ScriptingSymbol.AddScriptingSymbol(DisableCodeGenSymbol);
                else
                    ScriptingSymbol.RemoveScriptingSymbol(DisableCodeGenSymbol);
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