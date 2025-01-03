using System;
using System.Collections.Generic;
using Ceres.Graph.Flow;
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
            StructVisualElements();
            Key = Container!.Object;
            if (Key is Component component)
            {
                Key = component.gameObject;
            }
            var icon = Resources.Load<Texture>("Ceres/editor_icon");
            titleContent = new GUIContent($"Flow ({Key.name})",icon);
            _graphView.DeserializeGraph(ContainerT);
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
            if (asset is not IFlowGraphContainer flowGraphAsset) return false;
            
            Show(flowGraphAsset);
            return false;
        }
#pragma warning restore IDE0051

        public static void Show(IFlowGraphContainer container)
        {
            var window = GetOrCreateEditorWindow(container);
            window.Focus();
            window.Show();
        }
        
        private VisualElement CreateToolBar()
        {
            return new IMGUIContainer(
                () =>
                {
                    if (!Key)
                    {
                        /* Should only happen when object destroyed */
                        return;
                    }
                    GUILayout.BeginHorizontal(EditorStyles.toolbar);
                    GUI.enabled = !Application.isPlaying;
                    var image  = EditorGUIUtility.IconContent("SaveAs@2x").image;
                    if (GUILayout.Button(new GUIContent(image,$"Save flow and serialize data to {Key.name}"), EditorStyles.toolbarButton))
                    {
                        var guiContent = new GUIContent();
                        if (_graphView.SerializeGraph(Container))
                        {
                            guiContent.text = $"Update flow {Key.name} succeed!";
                            ShowNotification(guiContent, 0.5f);
                        }
                        else
                        {
                            guiContent.text = $"Failed to save flow {Key.name}!";
                            ShowNotification(guiContent, 0.5f);
                        }
                    }
                    GUI.enabled &= _graphView.CanSimulate();
                    image = EditorGUIUtility.IconContent("d_PlayButton On@2x").image;
                    if (GUILayout.Button(new GUIContent("Run Flow", image, "Execute selected execution event in graph editor"), EditorStyles.toolbarButton))
                    {
                        DoSimulation().Forget();
                    }
                    GUI.enabled = true;
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
        
        protected override void Reload()
        {
            if (!Key) return;
            
            Container = GetContainer();
            StructVisualElements();
            _graphView.DeserializeGraph(ContainerT);
            Repaint();
        }
    }
}