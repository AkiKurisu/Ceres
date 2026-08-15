using System;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Ceres.Editor;

namespace Ceres.Serialization.Editor
{
    // EditorWindow is modified from R3.Unity
    public class SerializationDebuggerEditorWindow : EditorWindow
    {
        private static SerializationDebuggerEditorWindow _window;

        [MenuItem("Tools/Ceres/Debug/Serialization Debugger", false, 1)]
        public static void OpenWindow()
        {
            if (_window != null)
            {
                _window.Close();
            }

            // will called OnEnable(singleton instance will be set).
            GetWindow<SerializationDebuggerEditorWindow>("Serialization Debugger").Show();
        }

        private static readonly GUILayoutOption[] EmptyLayoutOption = Array.Empty<GUILayoutOption>();

        private SerializationDebuggerTreeView _treeView;
        
        private object _splitterState;

        private void OnEnable()
        {
            _window = this; // set singleton.
            _splitterState = SplitterGUILayout.CreateSplitterState(new[] { 75f, 25f }, new[] { 32, 32 }, null);
            _treeView = new SerializationDebuggerTreeView();
        }
        
        private void Update()
        {
            if (GlobalObjectManager.CheckAndResetDirty())
            {
                _treeView.ReloadAndSort();
                Repaint();
            }
        }
        
        private void OnGUI()
        {
            // Head
            RenderHeadPanel();

            // Splittable
            SplitterGUILayout.BeginVerticalSplit(_splitterState, EmptyLayoutOption);
            {
                // Column Table
                RenderTable();

                // StackTrace details
                RenderDetailsPanel();
            }
            SplitterGUILayout.EndVerticalSplit();
        }

        #region HeadPanel

        private static readonly GUIContent CleanupHeadContent = EditorGUIUtility.TrTextContent("Cleanup", "Cleanup Global Objects");

        private void RenderHeadPanel()
        {
            EditorGUILayout.BeginVertical(EmptyLayoutOption);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, EmptyLayoutOption);
            GUILayout.Label($"Global Objects Count");
            GUILayout.Label($"{GlobalObjectManager.GetObjectNum()}");
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(CleanupHeadContent, EditorStyles.toolbarButton, EmptyLayoutOption))
            {
                GlobalObjectManager.Cleanup();
                _treeView.ReloadAndSort();
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region TableColumn

        private Vector2 _tableScroll;
        private GUIStyle _tableListStyle;

        private void RenderTable()
        {
            if (_tableListStyle == null)
            {
                _tableListStyle = new GUIStyle("CN Box");
                _tableListStyle.margin.top = 0;
                _tableListStyle.padding.left = 3;
            }

            EditorGUILayout.BeginVertical(_tableListStyle, EmptyLayoutOption);

            _tableScroll = EditorGUILayout.BeginScrollView(_tableScroll, new[]
            {
                GUILayout.ExpandWidth(true),
                GUILayout.MaxWidth(2000f)
            });
            var controlRect = EditorGUILayout.GetControlRect(new[]
            {
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true)
            });


            _treeView?.OnGUI(controlRect);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }



        #endregion

        #region Details

        private static GUIStyle _detailsStyle;
        private Vector2 _detailsScroll;

        private void RenderDetailsPanel()
        {
            if (_detailsStyle == null)
            {
                _detailsStyle = new GUIStyle("CN Message")
                {
                    wordWrap = false,
                    stretchHeight = true
                };
                _detailsStyle.margin.right = 15;
            }

            string message = "";
            var selected = _treeView.state.selectedIDs;
            if (selected.Count > 0)
            {
                var first = selected[0];
                if (_treeView.CurrentBindingItems.FirstOrDefault(x => x.id == first) is SerializationDebuggerTreeView.ViewItem item)
                {
                    message = item.Object != null ? item.Object.name : string.Empty;
                }
            }

            _detailsScroll = EditorGUILayout.BeginScrollView(this._detailsScroll, EmptyLayoutOption);
            var vector = _detailsStyle.CalcSize(new GUIContent(message));
            EditorGUILayout.SelectableLabel(message, _detailsStyle, new[]
            {
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true),
                GUILayout.MinWidth(vector.x),
                GUILayout.MinHeight(vector.y)
            });
            EditorGUILayout.EndScrollView();
        }

        #endregion
    }
}

