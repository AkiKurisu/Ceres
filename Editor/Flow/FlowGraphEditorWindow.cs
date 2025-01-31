using System;
using System.Collections.Generic;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Utilities;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph.Flow
{
    /// <summary>
    /// Editor window scope debug state
    /// </summary>
    [Serializable]
    public class FlowGraphDebugState
    {
        public bool enableDebug;
        
        public List<string> breakpoints = new();

        public void AddBreakpoint(string guid)
        {
            if(breakpoints.Contains(guid)) return;
            breakpoints.Add(guid);
        }
        
        public void RemoveBreakpoint(string guid)
        {
            breakpoints.Remove(guid);
        }
    }
    
    public class FlowGraphEditorWindow: CeresGraphEditorWindow<IFlowGraphContainer, FlowGraphEditorWindow>
    {
        private FlowGraphView _graphView;

        public FlowGraphDebugState debugState = new();
        
        protected override void OnInitialize()
        {
            var icon = Resources.Load<Texture>("Ceres/editor_icon");
            titleContent = new GUIContent($"Flow ({Identifier.boundObject.name})", icon);
            InitializeFlowGraphView();
        }

        private static void DisplayProgressBar(string stepTitle, float progress)
        {
            EditorUtility.DisplayProgressBar(stepTitle, "First initialization requires a few seconds", progress);
        }
        
        private static void ClearProgressBar()
        {
            EditorUtility.ClearProgressBar();
        }
        
        private void StructVisualElements()
        {
            rootVisualElement.Clear();
            _graphView?.Dispose();
            _graphView = new FlowGraphView(this);
            rootVisualElement.Add(CreateToolBar());
            rootVisualElement.Add(_graphView);
        }
        
#pragma warning disable IDE0051
        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int _)
        {
            var asset = EditorUtility.InstanceIDToObject(instanceId);
            if (asset is not FlowGraphScriptableObjectBase objectBase) return false;
            
            Show(objectBase);
            return false;
        }
#pragma warning restore IDE0051

        public FlowGraphView GetGraphView()
        {
            return _graphView;
        }

        private bool ContainerCanSimulate()
        {
            /* Whether explicit container type is assignable to editor container type */
            return GetContainerType().IsInstanceOfType(ContainerT);
        }

        /// <summary>
        /// Save current editing flow graph
        /// </summary>
        public void SaveGraph()
        {
            var guiContent = new GUIContent();
            if (_graphView.SerializeGraph(Container))
            {
                guiContent.text = $"Update flow {Identifier.boundObject.name} succeed!";
                ShowNotification(guiContent, 0.5f);
            }
            else
            {
                guiContent.text = $"Failed to save flow {Identifier.boundObject.name}!";
                ShowNotification(guiContent, 0.5f);
            }
        }
        
        private VisualElement CreateToolBar()
        {
            return new IMGUIContainer(
                () =>
                {
                    if (!Identifier.IsValid() || _graphView == null)
                    {
                        /* Should only happen when object destroyed */
                        return;
                    }
                    GUILayout.BeginHorizontal(EditorStyles.toolbar);
                    GUI.enabled = !Application.isPlaying;
                    var image  = EditorGUIUtility.IconContent("SaveAs@2x").image;
                    if (GUILayout.Button(new GUIContent(image,$"Save flow and serialize data to {Identifier.boundObject.name}"), EditorStyles.toolbarButton))
                    {
                        SaveGraph();
                    }
                    if (ContainerCanSimulate())
                    {
                        GUI.enabled &= _graphView.CanSimulate();
                        image = EditorGUIUtility.IconContent("d_PlayButton On@2x").image;
                        if (GUILayout.Button(
                                new GUIContent("Run Flow", image, "Execute selected execution event in graph editor"),
                                EditorStyles.toolbarButton))
                        {
                            DoSimulation().Forget();
                        }

                        GUI.enabled = true;
                    }
                    GUILayout.FlexibleSpace();
                    GUI.enabled = debugState.enableDebug && _graphView.IsPaused();
                    image = EditorGUIUtility.IconContent("Animation.NextKey").image;
                    if (GUILayout.Button(new GUIContent(image, $"Next Breakpoint"), EditorStyles.toolbarButton))
                    {
                        _graphView.NextBreakpoint();
                    }
                    image = EditorGUIUtility.IconContent("Animation.Play").image;
                    if (GUILayout.Button(new GUIContent(image, $"Next Frame"), EditorStyles.toolbarButton))
                    {
                        _graphView.NextFrame();
                    }
                    GUI.enabled = true;
                    if(debugState.enableDebug)
                    {
                        image = EditorGUIUtility.IconContent("DebuggerAttached@2x").image;
                        if (GUILayout.Button(new GUIContent(image, $"Disable Debug"), EditorStyles.toolbarButton))
                        {
                            _graphView.SetDebugEnabled(false);
                        }
                    }
                    else
                    {
                        image = EditorGUIUtility.IconContent("DebuggerDisabled@2x").image;
                        if (GUILayout.Button(new GUIContent(image, $"Enable Debug"), EditorStyles.toolbarButton))
                        {
                            _graphView.SetDebugEnabled(true);
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            );
        }

        private async UniTask DoSimulation()
        {
            var guiContent = new GUIContent("Simulation Start");
            ShowNotification(guiContent, 1f);
            await _graphView.SimulateExecution();
            guiContent = new GUIContent("Simulation End");
            ShowNotification(guiContent, 1f);
        }
        
        protected override void OnReloadGraphView()
        {
            InitializeFlowGraphView();
        }

        private void InitializeFlowGraphView()
        {
            try
            {
                /* Redirect container type */
                if (Container is FlowGraphScriptableObjectBase scriptableObjectBase)
                {
                    SetContainerType(scriptableObjectBase.GetRuntimeType());
                }
                DisplayProgressBar("Initialize field factory", 0f);
                {
                    FieldResolverFactory.Get();
                }
                DisplayProgressBar("Initialize node view factory", 0.3f);
                {
                    NodeViewFactory.Get();
                }
                DisplayProgressBar("Initialize executable function registry", 0.6f);
                {
                    ExecutableFunctionRegistry.Get();
                }
                DisplayProgressBar("Construct graph view", 0.9f);
                {
                    StructVisualElements();
                    _graphView.DeserializeGraph(ContainerT);
                }
            }
            finally
            {
                ClearProgressBar();
            }
        }
    }
}