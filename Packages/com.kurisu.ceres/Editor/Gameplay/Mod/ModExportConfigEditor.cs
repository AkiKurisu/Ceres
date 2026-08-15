using Ceres.Editor.Flow;
using Ceres.Flow;
using UnityEditor;
using UnityEngine;

namespace Ceres.Gameplay.Mod.Editor
{
    [CustomEditor(typeof(ModExportConfig))]
    public class ModExportConfigEditor : UnityEditor.Editor
    {
        private ModExportConfig _config;
        private SerializedProperty _authorNameProp;
        private SerializedProperty _modNameProp;
        private SerializedProperty _versionProp;
        private SerializedProperty _descriptionProp;
        private SerializedProperty _iconDataProp;
        private SerializedProperty _customBuildersProp;
        
        private Texture2D _previewTexture;
        private bool _isEditMode;
        
        private static class Styles
        {
            public static readonly GUIStyle HeaderStyle;
            public static readonly GUIStyle BoxStyle;
            public static readonly GUIStyle InfoLabelStyle;
            public static readonly GUIStyle FlowGraphButtonStyle;
            public static readonly Texture2D FlowGraphIcon;
            
            static Styles()
            {
                HeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    margin = new RectOffset(0, 0, 10, 5)
                };
                
                BoxStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(10, 10, 10, 10),
                    margin = new RectOffset(0, 0, 5, 5)
                };
                
                InfoLabelStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontSize = 11,
                    padding = new RectOffset(5, 5, 5, 5)
                };
                FlowGraphButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 30,
                    margin = new RectOffset(0, 0, 5, 5)
                };
                
                FlowGraphIcon = Resources.Load<Texture2D>("Ceres/editor_icon");
            }
        }
        
        /// <summary>
        /// Set config to edit mode for ModExportWindow
        /// </summary>
        /// <param name="isEditMode"></param>
        public void SetEditMode(bool isEditMode)
        {
            _isEditMode = isEditMode;
        }
        
        private void OnEnable()
        {
            _config = (ModExportConfig)target;
            _authorNameProp = serializedObject.FindProperty("authorName");
            _modNameProp = serializedObject.FindProperty("modName");
            _versionProp = serializedObject.FindProperty("version");
            _descriptionProp = serializedObject.FindProperty("description");
            _iconDataProp = serializedObject.FindProperty("iconData");
            _customBuildersProp = serializedObject.FindProperty("customBuilders");
            
            // Load preview texture from icon data
            LoadPreviewTexture();
        }
        
        private void OnDisable()
        {
            // Clean up preview texture
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            if (_isEditMode)
            {
                DrawBasicInfoEdit();
                DrawDescriptionEdit();
                DrawCustomBuildersEdit();
                DrawFlowGraphButton();
                DrawValidationInfo();
            }
            else
            {
                DrawBasicInfoReadOnly();
                DrawDescriptionReadOnly();
                DrawCustomBuildersReadOnly();
                DrawFlowGraphButton();
                DrawOpenInWindowButton();
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawFlowGraphButton()
        {
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(89 / 255f, 133 / 255f, 141 / 255f);
            
            var buttonContent = new GUIContent("Open Flow Graph", Styles.FlowGraphIcon, "Open the Flow Graph editor for this mod configuration");
            if (GUILayout.Button(buttonContent, Styles.FlowGraphButtonStyle))
            {
                if (_config is IFlowGraphContainer flowGraphContainer)
                {
                    FlowGraphEditorWindow.Show(flowGraphContainer);
                }
            }
            
            GUI.backgroundColor = originalColor;
        }
        
        private void DrawBasicInfoEdit()
        {
            GUILayout.Label("Basic Information", Styles.HeaderStyle);
            
            EditorGUILayout.BeginVertical(Styles.BoxStyle);
            
            EditorGUILayout.BeginHorizontal();
            
            // Icon on the left
            EditorGUILayout.BeginVertical(GUILayout.Width(120));
            if (_previewTexture != null)
            {
                var rect = GUILayoutUtility.GetRect(100, 100, GUILayout.Width(100), GUILayout.Height(100));
                EditorGUI.DrawPreviewTexture(rect, _previewTexture);
            }
            else
            {
                var rect = GUILayoutUtility.GetRect(100, 100, GUILayout.Width(100), GUILayout.Height(100));
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
                GUI.Label(rect, "No Icon", new GUIStyle(GUI.skin.label) 
                { 
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.gray }
                });
            }
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);
            
            // Basic info fields on the right
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            
            EditorGUILayout.PropertyField(_modNameProp, new GUIContent("Mod Name"));
            EditorGUILayout.PropertyField(_versionProp, new GUIContent("Version"));
            EditorGUILayout.PropertyField(_authorNameProp, new GUIContent("Author"));

            if (GUILayout.Button("Select PNG", GUILayout.Width(100)))
            {
                SelectIconFile();
            }
            using (new EditorGUI.DisabledScope(_config.iconData == null || _config.iconData.Length == 0))
            {
                if (GUILayout.Button("Clear", GUILayout.Width(100)))
                {
                    ClearIcon();
                }
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawBasicInfoReadOnly()
        {
            GUILayout.Label("Basic Information", Styles.HeaderStyle);
            
            EditorGUILayout.BeginVertical(Styles.BoxStyle);
            
            EditorGUILayout.BeginHorizontal();
            
            // Icon on the left
            EditorGUILayout.BeginVertical(GUILayout.Width(120));
            
            if (_previewTexture != null)
            {
                var rect = GUILayoutUtility.GetRect(100, 100, GUILayout.Width(100), GUILayout.Height(100));
                EditorGUI.DrawPreviewTexture(rect, _previewTexture);
            }
            else
            {
                var rect = GUILayoutUtility.GetRect(100, 100, GUILayout.Width(100), GUILayout.Height(100));
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
                GUI.Label(rect, "No Icon", new GUIStyle(GUI.skin.label) 
                { 
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.gray }
                });
            }
            
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);
            
            // Basic info fields on the right (read-only)
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            
            DrawReadOnlyField("Mod Name", _config.modName);
            DrawReadOnlyField("Version", _config.version);
            DrawReadOnlyField("Author", _config.authorName);
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawDescriptionEdit()
        {
            EditorGUILayout.Space(5);
            GUILayout.Label("Description", Styles.HeaderStyle);
            
            EditorGUILayout.BeginVertical(Styles.BoxStyle);
            _descriptionProp.stringValue = EditorGUILayout.TextArea(_descriptionProp.stringValue, 
                GUILayout.MinHeight(60));
            EditorGUILayout.EndVertical();
        }
        
        private void DrawDescriptionReadOnly()
        {
            EditorGUILayout.Space(5);
            GUILayout.Label("Description", Styles.HeaderStyle);
            
            EditorGUILayout.BeginVertical(Styles.BoxStyle);
            
            if (string.IsNullOrEmpty(_config.description))
            {
                EditorGUILayout.LabelField("No description provided.", EditorStyles.wordWrappedLabel);
            }
            else
            {
                EditorGUILayout.LabelField(_config.description, EditorStyles.wordWrappedLabel);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawCustomBuildersEdit()
        {
            EditorGUILayout.Space(5);
            GUILayout.Label("Custom Builders", Styles.HeaderStyle);
            
            EditorGUILayout.BeginVertical(Styles.BoxStyle);
            EditorGUILayout.PropertyField(_customBuildersProp, true);
            
            // Display custom builder information
            if (_config.customBuilders is { Length: > 0 })
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Active Builders:", EditorStyles.boldLabel);
                
                foreach (var customBuilder in _config.customBuilders)
                {
                    var builder = customBuilder?.GetObject();
                    if (builder != null)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField($"● {builder.GetType().Name}", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(string.IsNullOrEmpty(builder.Description) ? "No Description" : builder.Description, Styles.InfoLabelStyle);
                        EditorGUILayout.EndVertical();
                    }
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawCustomBuildersReadOnly()
        {
            if (_config.customBuilders == null || _config.customBuilders.Length == 0)
                return;
                
            EditorGUILayout.Space(5);
            GUILayout.Label("Custom Builders", Styles.HeaderStyle);
            
            EditorGUILayout.BeginVertical(Styles.BoxStyle);
            
            foreach (var customBuilder in _config.customBuilders)
            {
                var builder = customBuilder?.GetObject();
                if (builder != null)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"● {builder.GetType().Name}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(string.IsNullOrEmpty(builder.Description) ? "No Description" : builder.Description, Styles.InfoLabelStyle);
                    EditorGUILayout.EndVertical();
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawValidationInfo()
        {
            GUILayout.FlexibleSpace();
            
            if (!_config.Validate())
            {
                EditorGUILayout.HelpBox("Configuration is incomplete. Please fill in all required fields (Author, Mod Name, Version).", 
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Configuration is valid and ready to export.", 
                    MessageType.Info);
            }
        }
        
        private void LoadPreviewTexture()
        {
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }
            
            if (_config.iconData != null && _config.iconData.Length > 0)
            {
                _previewTexture = new Texture2D(2, 2);
                _previewTexture.LoadImage(_config.iconData);
            }
        }
        
        private void SelectIconFile()
        {
            string path = EditorUtility.OpenFilePanel("Select Mod Icon", "", "png");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    byte[] fileData = System.IO.File.ReadAllBytes(path);
                    
                    // Validate that it's a valid PNG by trying to load it
                    Texture2D testTexture = new Texture2D(2, 2);
                    if (testTexture.LoadImage(fileData))
                    {
                        DestroyImmediate(testTexture);
                        
                        _iconDataProp.serializedObject.Update();
                        _config.iconData = fileData;
                        _iconDataProp.serializedObject.ApplyModifiedProperties();
                        
                        LoadPreviewTexture();
                        EditorUtility.SetDirty(_config);
                    }
                    else
                    {
                        DestroyImmediate(testTexture);
                        EditorUtility.DisplayDialog("Invalid Image", 
                            "The selected file is not a valid PNG image.", 
                            "OK");
                    }
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", 
                        $"Failed to load image file:\n{e.Message}", 
                        "OK");
                }
            }
        }
        
        private void ClearIcon()
        {
            _iconDataProp.serializedObject.Update();
            _config.iconData = null;
            _iconDataProp.serializedObject.ApplyModifiedProperties();
            
            LoadPreviewTexture();
            EditorUtility.SetDirty(_config);
        }
        
        private static void DrawReadOnlyField(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField(value ?? string.Empty, EditorStyles.label);
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawOpenInWindowButton()
        {
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
            
            if (GUILayout.Button("Open in Mod Export Window", GUILayout.Height(30)))
            {
                ModExportWindow.OpenWithConfig(_config);
            }
            
            GUI.backgroundColor = originalColor;
        }
    }
}

