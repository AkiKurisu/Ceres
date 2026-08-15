using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Linq;
using Ceres.Resource.Editor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Callbacks;
using Debug = UnityEngine.Debug;
using UEditor = UnityEditor.Editor;

namespace Ceres.Gameplay.Mod.Editor
{
    public class ModExportWindow : EditorWindow
    {
        private Vector2 _scrollPosition;

        private ModExportConfig _exportConfig;

        private UEditor _configEditor;

        private static readonly FieldInfo MUseCustomPaths = typeof(BundledAssetGroupSchema).GetField("m_UseCustomPaths", BindingFlags.Instance | BindingFlags.NonPublic);

        private static string ConfigGuidKey => Application.productName + "_ModConfigGUID";

        [MenuItem("Tools/Ceres/Mod/Mod Exporter", false, 100)]
        public static void OpenEditor()
        {
            var window = GetWindow<ModExportWindow>("Mod Exporter");
            window.minSize = new Vector2(600, 720);
        }

        public static void OpenWithConfig(ModExportConfig config)
        {
            var window = GetWindow<ModExportWindow>("Mod Exporter");
            window.minSize = new Vector2(600, 720);
            window._exportConfig = config;
            EditorPrefs.SetString(ConfigGuidKey, GetGuid(config));
            CreateCachedEditor(config, ref window._configEditor);
            window.Repaint();
        }
        
#pragma warning disable IDE0051
        [OnOpenAsset(-1000)]
        private static bool OnOpenAsset(int instanceId, int _)
        {
#if UNITY_6000_0_OR_NEWER
            var asset = EditorUtility.EntityIdToObject(instanceId);
#else
            var asset = EditorUtility.InstanceIDToObject(instanceId);
#endif
            if (asset is not ModExportConfig config) return false;

            OpenWithConfig(config);
            return true;
        }
#pragma warning restore IDE0051

        private void OnGUI()
        {
            DrawHeader();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawConfigSelector();
            DrawConfigInspector();
            EditorGUILayout.EndScrollView();

            DrawActionButtons();
        }

        private static void CreateCachedEditor(ModExportConfig exportConfig, ref UEditor editor)
        {
            UEditor.CreateCachedEditor(exportConfig, null, ref editor);
            ((ModExportConfigEditor)editor).SetEditMode(true);
        }

        private static void DrawHeader()
        {
            EditorGUILayout.HelpBox($"Mod Export Tool\nCurrent Platform: {EditorUserBuildSettings.activeBuildTarget}", MessageType.Info);
            EditorGUILayout.Space(5);
        }

        private void DrawConfigSelector()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var newConfig = EditorGUILayout.ObjectField("Export Config", _exportConfig, typeof(ModExportConfig), false) as ModExportConfig;
            if (EditorGUI.EndChangeCheck() && newConfig)
            {
                _exportConfig = newConfig;
                EditorPrefs.SetString(ConfigGuidKey, GetGuid(_exportConfig));
                CreateCachedEditor(_exportConfig, ref _configEditor);
            }

            if (GUILayout.Button("Browse...", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFilePanel("Select export config", Application.dataPath, "asset");
                if (!string.IsNullOrEmpty(path))
                {
                    path = path.Replace(Application.dataPath, string.Empty);
                    var config = AssetDatabase.LoadAssetAtPath($"Assets{path}", typeof(ModExportConfig)) as ModExportConfig;
                    if (config != null)
                    {
                        _exportConfig = config;
                        EditorPrefs.SetString(ConfigGuidKey, GetGuid(_exportConfig));
                        CreateCachedEditor(_exportConfig, ref _configEditor);
                    }
                    else
                    {
                        ShowNotification(new GUIContent($"Invalid path: Assets{path}"));
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfigInspector()
        {
            if (_exportConfig != null)
            {
                if (_configEditor == null)
                {
                    CreateCachedEditor(_exportConfig, ref _configEditor);
                }

                if (_configEditor != null)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    _configEditor.OnInspectorGUI();
                    EditorGUILayout.EndVertical();
                }
            }
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            var isValid = _exportConfig != null && _exportConfig.Validate();
            using (new EditorGUI.DisabledScope(!isValid))
            {
                // Create Group Button
                var createGroupColor = new Color(0.7f, 0.9f, 1f);
                GUI.backgroundColor = createGroupColor;
                if (GUILayout.Button(new GUIContent("Create Addressable Group",
                    "Create or update the Addressable Asset Group for this mod"),
                    GUILayout.Height(35)))
                {
                    CreateAddressableGroup();
                }

                // Export Button
                GUI.backgroundColor = new Color(253 / 255f, 163 / 255f, 255 / 255f);
                if (GUILayout.Button(new GUIContent("Export Mod", "Export the mod to the output directory"),
                    GUILayout.Height(35), GUILayout.MinWidth(120)))
                {
                    ExportMod();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void CreateAddressableGroup()
        {
            var group = _exportConfig.CreateAssetGroup();
            // Set not include in packed build
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.IncludeInBuild = false;
            schema.BuildPath.SetVariableByName(AddressableAssetSettingsDefaultObject.Settings,
                AddressableAssetSettings.kRemoteBuildPath);
            schema.LoadPath.SetVariableByName(AddressableAssetSettingsDefaultObject.Settings,
                AddressableAssetSettings.kRemoteLoadPath);
#pragma warning disable UNT0018 // System.Reflection features in performance critical messages
            MUseCustomPaths.SetValue(schema, false);
#pragma warning restore UNT0018 // System.Reflection features in performance critical messages

            EditorUtility.DisplayDialog("Success",
                $"Addressable group 'Mod_{_exportConfig.modName}' created/updated successfully!",
                "OK");
        }

        private void ExportMod()
        {
            _exportConfig.CreateResourceExporter().Export();
            Process.Start(ResourceExporter.ExportPath);
            EditorUtility.SetDirty(_exportConfig);
            AssetDatabase.SaveAssets();

            ShowNotification(new GUIContent("Export completed!"));
        }

        private void OnEnable()
        {
            if (!_exportConfig)
            {
                _exportConfig = LoadExportConfig();
            }

            if (_exportConfig && !_configEditor)
            {
                CreateCachedEditor(_exportConfig, ref _configEditor);
            }
        }

        private void OnDisable()
        {
            if (_configEditor)
            {
                DestroyImmediate(_configEditor);
            }

            _configEditor = null;
        }

        private static ModExportConfig LoadExportConfig()
        {
            var configs = AssetDatabase.FindAssets($"t:{typeof(ModExportConfig)}")
                .Select(x => AssetDatabase.LoadAssetAtPath<ModExportConfig>(AssetDatabase.GUIDToAssetPath(x)))
                .ToArray();
            ModExportConfig config = null;
            if (configs.Length != 0)
            {
                string configGuid = EditorPrefs.GetString(ConfigGuidKey, null);
                config = configs.FirstOrDefault(x => GetGuid(x) == configGuid);
                if (config == null)
                {
                    config = configs[0];
                    EditorPrefs.SetString(ConfigGuidKey, GetGuid(config));
                }
            }
            if (!config)
            {
                config = CreateInstance<ModExportConfig>();
                string settingsPath = "Assets/ModExportConfig.asset";
                Debug.Log($"<color=#3aff48>Exporter</color>: Mod export config saved to {settingsPath}");
                AssetDatabase.CreateAsset(config, settingsPath);
                AssetDatabase.SaveAssets();
            }
            return config;
        }

        private static string GetGuid(Object asset)
        {
            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        }
    }
}
