#if URP_INSTALL
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UEditor = UnityEditor.Editor;

namespace Ceres.Gameplay.Graphics.Editor
{
    [CustomEditor(typeof(GraphicsController))]
    public class GraphicsControllerEditor : UEditor
    {
        private GraphicsController _target;

        private SerializedProperty _graphicsConfigProperty;

        private bool _lookDevMode;

        private static Type[] _cachedModuleTypes;

        private readonly Dictionary<string, bool> _status = new();

        private DynamicVolumePlatform _platform;
        
        private int _qualityLevel;

        private void OnEnable()
        {
#if UNITY_ANDROID || UNITY_IOS
            _platform = DynamicVolumePlatform.Mobile;
#elif UNITY_XBOXONE || UNITY_PS5
            _platform = DynamicVolumePlatform.Console;
#else
            _platform = DynamicVolumePlatform.Windows;
#endif
            _target = (GraphicsController)target;
            _target.InitializeIfNeed();
            _graphicsConfigProperty = serializedObject.FindProperty(nameof(GraphicsController.settingsAsset));

            // Reload LookDev mode
            _lookDevMode = EditorPrefs.GetBool(GraphicsController.LookDevModeKey, false);

            // Initialize quality level
            _qualityLevel = QualitySettings.GetQualityLevel();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawBasicConfig();

            if (_target.settingsAsset)
            {
                EditorGUILayout.Space();

                DrawVolumeSettingsPanel();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBasicConfig()
        {
            // LookDev Mode Toggle
            EditorGUILayout.BeginHorizontal();

            // Create styled button with color change when active
            var originalBackgroundColor = GUI.backgroundColor;
            var originalContentColor = GUI.contentColor;

            if (_lookDevMode)
            {
                GUI.backgroundColor = new Color(0.2f, 0.5f, 1f, 1f); // Blue background when active
                GUI.contentColor = Color.white; // White text/icon when active
            }

            var buttonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fixedHeight = 25,
                fontStyle = _lookDevMode ? FontStyle.Bold : FontStyle.Normal
            };

            if (GUILayout.Button(Styles.LookDevContent, buttonStyle, GUILayout.Width(140)))
            {
                _lookDevMode = !_lookDevMode;

                EditorPrefs.SetBool(GraphicsController.LookDevModeKey, _lookDevMode);

                if (_lookDevMode)
                {
                    ApplyLookDevSettings();
                }
                else
                {
                    _target.ApplyDynamicVolumeProfiles();
                }
            }

            // Restore original colors
            GUI.backgroundColor = originalBackgroundColor;
            GUI.contentColor = originalContentColor;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(_graphicsConfigProperty);

            using (new EditorGUI.DisabledScope(!_target.settingsAsset))
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField("Platform Profiles", GUILayout.Width(110));

                var newPlatform = (DynamicVolumePlatform)EditorGUILayout.EnumPopup(_platform, GUILayout.Height(20));
                if (newPlatform != _platform)
                {
                    _platform = newPlatform;
                    _target.ApplyVolumeProfiles(_platform);
                }

                if (GUILayout.Button(EditorGUIUtility.IconContent("Refresh", "Reload"), GUILayout.Width(30),
                        GUILayout.Height(18)))
                {
                    _target.ApplyDynamicVolumeProfiles();
                }

                EditorGUILayout.EndHorizontal();

                // Quality Level selection
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField("Quality Level", GUILayout.Width(110));

                var qualityNames = QualitySettings.names;
                var newQualityLevel = EditorGUILayout.Popup(_qualityLevel, qualityNames, GUILayout.Height(20));
                if (newQualityLevel != _qualityLevel)
                {
                    _qualityLevel = newQualityLevel;
                    QualitySettings.SetQualityLevel(_qualityLevel, true);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawVolumeSettingsPanel()
        {
            if (!Foldout("Volume Settings")) return;

            EditorGUI.indentLevel++;

            if (_lookDevMode)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.HelpBox("LookDev Mode Active: Post process effects are disabled for material preview.", MessageType.Info);
                EditorGUILayout.EndVertical();
            }

            var manager = DynamicVolumeProfileTableManager.Get();
            foreach (var volumeType in manager.GetDataTable(DynamicVolumeProfileTableManager.TableKey).GetRowMap().Keys)
            {
                var volume = _target.GetVolume(volumeType);
                if (volume)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(volumeType);
                    GUILayout.FlexibleSpace();

                    // In LookDev mode, disable sliders and show current values
                    if (_lookDevMode)
                    {
                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.Slider(volume.weight, 0f, 1f);
                        }

                        // Show LookDev status for each volume
                        var isEnabled = volume.enabled;
                        var statusText = isEnabled ? "ON" : "OFF";
                        var statusColor = isEnabled ? Color.green : Color.red;

                        var originalColor = GUI.color;
                        GUI.color = statusColor;
                        EditorGUILayout.LabelField(statusText, GUILayout.Width(40));
                        GUI.color = originalColor;
                    }
                    else
                    {
                        using (new EditorGUI.DisabledScope(!volume.enabled))
                        {
                            var newWeight = EditorGUILayout.Slider(volume.weight, 0f, 1f, GUILayout.Width(150));
                            if (Mathf.Abs(newWeight - volume.weight) > 0.001f)
                            {
                                volume.weight = newWeight;
                            }

                            if (GUILayout.Button("0", GUILayout.Width(25)))
                            {
                                volume.weight = 0f;
                            }

                            if (GUILayout.Button("1", GUILayout.Width(25)))
                            {
                                volume.weight = 1f;
                            }
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUI.indentLevel--;
        }
        
        private void ApplyLookDevSettings()
        {
            var manager = DynamicVolumeProfileTableManager.Get();
            foreach (var volumeType in manager.GetDataTable(DynamicVolumeProfileTableManager.TableKey).GetRowMap().Keys)
            {
                var volume = _target.GetVolume(volumeType);
                if (volume)
                {
                    var shouldEnableInLookDev = GraphicsController.IsLookDevVolumeType(volumeType);
                    volume.enabled = shouldEnableInLookDev;
                }
            }
        }

        private bool Foldout(string content)
        {
            bool status;
            if (!_status.ContainsKey(content))
            {
                status = EditorPrefs.GetBool($"GraphicsController_{content}", false);
                _status[content] = status;
            }
            else
            {
                status = _status.GetValueOrDefault(content);
            }
            var color = GUI.backgroundColor;
            GUI.backgroundColor = Color.white;

            var rect = GUILayoutUtility.GetRect(new GUIContent(content), Styles.FoldoutStyle);
            GUI.Box(rect, content, Styles.FoldoutStyle);
            GUI.backgroundColor = color;

            var e = Event.current;
            if (e.type == EventType.Repaint)
            {
                var arrowRect = new Rect(rect.x + 4f, rect.y + 2f, 13f, 13f);
                EditorStyles.foldout.Draw(arrowRect, false, false, status, false);
            }

            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                status = !status;
                _status[content] = status;
                EditorPrefs.SetBool($"GraphicsController_{content}", status);
                e.Use();
            }

            return status;
        }

        private static class Styles
        {
            public static readonly GUIStyle FoldoutStyle = new("ShurikenModuleTitle")
            {
                font = new GUIStyle(EditorStyles.label).font,
                border = new RectOffset(15, 7, 4, 4),
                fixedHeight = 22,
                contentOffset = new Vector2(20f, -2f)
            };

            public static readonly GUIContent LookDevContent = new(" Look Dev Mode", EditorGUIUtility.IconContent("d_SceneViewFX@2x").image);
        }
    }
}
#endif