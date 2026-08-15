using System;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Ceres.Editor;
using Unity.CodeEditor;

namespace Ceres.Schedulers.Editor
{
    // EditorWindow is modified from R3.Unity
    public class SchedulerDebuggerEditorWindow : EditorWindow
    {
        private static SchedulerRunner Runner => Application.isPlaying ? SchedulerRunner.Get() : null;

        private static int ManagedScheduledCount => Runner == null ? 0 : Runner.ScheduledItems.Count;
        
        private static int ManagedScheduledCapacity => Runner == null ? 0 : Runner.ScheduledItems.InternalCapacity;
        
        private static SchedulerDebuggerEditorWindow _window;

        [MenuItem("Tools/Ceres/Debug/Scheduler Debugger", false, 1)]
        public static void OpenWindow()
        {
            if (_window != null)
            {
                _window.Close();
            }

            // will called OnEnable(singleton instance will be set).
            GetWindow<SchedulerDebuggerEditorWindow>("Scheduler Debugger").Show();
        }

        private static readonly GUILayoutOption[] EmptyLayoutOption = Array.Empty<GUILayoutOption>();

        private SchedulerDebuggerTreeView _treeView;
        
        private object _splitterState;

        private void OnEnable()
        {
            _window = this; // set singleton.
            _splitterState = SplitterGUILayout.CreateSplitterState(new[] { 75f, 25f }, new[] { 32, 32 }, null);
            _treeView = new SchedulerDebuggerTreeView();
        }
        
        private void Update()
        {
            _treeView.ReloadAndSort();
            Repaint();
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

        private static readonly GUIContent CancelAllHeadContent = EditorGUIUtility.TrTextContent("Cancel All", "Cancel all scheduled tasks");

        private void RenderHeadPanel()
        {
            EditorGUILayout.BeginVertical(EmptyLayoutOption);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, EmptyLayoutOption);

            GUILayout.Label($"Managed scheduled task count: {ManagedScheduledCount} capacity: {ManagedScheduledCapacity}");
            GUILayout.FlexibleSpace();

            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button(CancelAllHeadContent, EditorStyles.toolbarButton, EmptyLayoutOption))
            {
                Runner.CancelAll();
                _treeView.ReloadAndSort();
                Repaint();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region TableColumn

        private Vector2 tableScroll;
        private GUIStyle tableListStyle;

        private void RenderTable()
        {
            if (tableListStyle == null)
            {
                tableListStyle = new GUIStyle("CN Box");
                tableListStyle.margin.top = 0;
                tableListStyle.padding.left = 3;
            }

            EditorGUILayout.BeginVertical(tableListStyle, EmptyLayoutOption);

            tableScroll = EditorGUILayout.BeginScrollView(tableScroll, new[]
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

        private static GUIStyle detailsStyle;
        private static GUIStyle stackTraceButtonStyle;
        private Vector2 detailsScroll;
        private void RenderDetailsPanel()
        {
            if (detailsStyle == null)
            {
                detailsStyle = new GUIStyle("CN Message")
                {
                    wordWrap = false,
                    stretchHeight = true
                };
                detailsStyle.margin.right = 15;
            }

            stackTraceButtonStyle ??= new(GUI.skin.button)
            {
                wordWrap = true,
                fontSize = 12
            };

            SchedulerDebuggerTreeView.ViewItem viewItem = null;
            var selected = _treeView.state.selectedIDs;
            if (selected.Count > 0)
            {
                var first = selected[0];
                if (_treeView.CurrentBindingItems.FirstOrDefault(x => x.id == first) is SchedulerDebuggerTreeView.ViewItem item)
                {
                    viewItem = item;
                }
            }
            detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll, EmptyLayoutOption);
            if (viewItem != null)
            {
                if (SchedulerRegistry.TryGetListener(viewItem.ScheduledItem.Value, out var listener))
                {
                    GUILayout.Label($"{listener.fileName} {listener.lineNumber}", detailsStyle);
                    if (GUILayout.Button($"Open in Code Editor", stackTraceButtonStyle))
                    {
                        CodeEditor.Editor.CurrentCodeEditor.OpenProject(listener.fileName, listener.lineNumber);
                    }
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Resume", stackTraceButtonStyle))
                    {
                        Runner.Resume(viewItem.ScheduledItem.Value.Handle);
                    }
                    if (GUILayout.Button("Pause", stackTraceButtonStyle))
                    {
                        Runner.Pause(viewItem.ScheduledItem.Value.Handle);
                    }
                    GUILayout.EndHorizontal();
                    if (GUILayout.Button("Cancel", stackTraceButtonStyle))
                    {
                        Runner.Cancel(viewItem.ScheduledItem.Value.Handle);
                    }
                }
                else
                {
                    GUILayout.Label($"Enable Stack Trace in CeresSettings to track all scheduled tasks.");
                }
            }
            EditorGUILayout.EndScrollView();
        }
        #endregion
    }
}

