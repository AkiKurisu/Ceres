using Ceres.Graph;
using Chris.Editor;
using UnityEditor;
using UnityEngine;
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

        [SerializeField, HideInInspector] 
        private GraphEditorDisplayMode graphEditorDisplayMode;
        
        [SerializeField, HideInInspector]
        private bool disableILPostProcess;
        
        /// <summary>
        /// Ceres graph editor view display mode
        /// </summary>
        public static GraphEditorDisplayMode DisplayMode => instance.graphEditorDisplayMode;

        /// <summary>
        /// Ceres graph editor will display in debug mode
        /// </summary>
        public static bool DisplayDebug => DisplayMode == GraphEditorDisplayMode.Debug;
        
        /// <summary>
        /// Ceres graph editor will display in normal mode
        /// </summary>
        public static bool DisplayNormal => DisplayMode == GraphEditorDisplayMode.Normal;

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
            public static readonly GUIContent GraphEditorDisplayModeStyle = new("Display Mode",
                "Set graph editor display mode");
            
            public static readonly GUIContent DisableILPostProcessStyle = new("Disable ILPP", 
                "Disable IL Post Process, default Ceres will emit il after syntax analysis step to enhance " +
                "runtime performance, disable can speed up editor compilation, recommend to enable in final " +
                "production build");
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
            EditorGUILayout.PropertyField(_serializedObject.FindProperty("graphEditorDisplayMode"), Styles.GraphEditorDisplayModeStyle);
            GUILayout.EndVertical();
            if (_serializedObject.ApplyModifiedPropertiesWithoutUndo())
            {
                CeresSettings.SaveSettings();
            }

            GUILayout.Label("Runtime Settings", titleStyle);
            GUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(disableILPostProcessProp, Styles.DisableILPostProcessStyle);
            GUILayout.EndVertical();
            if (_serializedObject.ApplyModifiedPropertiesWithoutUndo())
            {
                if (disableILPostProcessProp.boolValue)
                {
                    CeresAPI.Log("Disable ILPP");
                    ScriptingSymbol.AddScriptingSymbol(DisableILPostProcessSymbol);
                }
                else
                {
                    CeresAPI.Log("Enable ILPP");
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