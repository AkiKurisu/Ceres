using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.CustomFunctions;
using Ceres.Graph.Flow.Utilities;
using Chris.Collections;
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
        /// <summary>
        /// Editor debug state
        /// </summary>
        [field: SerializeField]
        public FlowGraphDebugState DebugState { get; private set; }= new();

        /// <summary>
        /// Editing graph view index
        /// </summary>
        [field: SerializeField]
        public int GraphIndex { get; private set; }

        /// <summary>
        /// Editor view model
        /// </summary>
        [field: SerializeField]
        public FlowGraphEditorObject EditorObject { get; private set; }

        private readonly Dictionary<int, FlowGraphView> _graphViews = new();

        private FlowGraphView CurrentGraphView => _graphViews.GetValueOrDefault(GraphIndex);

        protected override void OnInitialize()
        {
            var icon = Resources.Load<Texture>("Ceres/editor_icon");
            titleContent = new GUIContent($"Flow ({Identifier.boundObject.name})", icon);
            EditorObject = FlowGraphEditorObject.CreateTemporary(ContainerT);
            InitializeFlowGraphView();
        }

        protected override void OnDisable()
        {
           if (EditorObject) EditorObject.DestroyTemporary();
           base.OnDisable();
        }

        private bool IsDirty()
        {
            foreach (var graphView in _graphViews.Values)
            {
                if (graphView.IsDirty()) return true;
            }

            return false;
        }
        
        private void ClearDirty()
        {
            foreach (var graphView in _graphViews.Values)
            {
                graphView.ClearDirty();
            }
        }

        private void OnGUI()
        {
            if (CurrentGraphView == null) return;
            if (IsDirty())
            {
                titleContent.text = $"Flow ({Identifier.boundObject.name})*";
            }
            else
            {
                titleContent.text = $"Flow ({Identifier.boundObject.name})";
            }
        }

        private static void DisplayProgressBar(string stepTitle, float progress)
        {
            EditorUtility.DisplayProgressBar(stepTitle, "First initialization requires a few seconds", progress);
        }
        
        private static void ClearProgressBar()
        {
            EditorUtility.ClearProgressBar();
        }
        
        private void StructVisualElements(int index)
        {
            rootVisualElement.Clear();
            rootVisualElement.Add(CreateToolBar());
            rootVisualElement.Add(GetOrCreateGraphView(index));
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

        /// <summary>
        /// Get current editing graph view
        /// </summary>
        /// <returns></returns>
        public FlowGraphView GetGraphView()
        {
            return CurrentGraphView;
        }
        
        /// <summary>
        /// Get or create graph view by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public FlowGraphView GetOrCreateGraphView(int id)
        {
            if (_graphViews.TryGetValue(id, out var view) && view != null) return view;
            
            _graphViews.Add(id, view = new FlowGraphView(this));
            if (id == 0)
            {
                CurrentGraphView.DeserializeGraph(EditorObject);
            }
            else
            {
                /* Ensure graph instance created */
                var slots = EditorObject.GraphInstance.SubGraphSlots;
                CeresLogger.Assert(id < slots.Length + 1, "Subgraph index out of range");
                CurrentGraphView.DeserializeSubGraph(EditorObject, EditorObject.GraphNames[id]);
            }
            return view;
        }

        private bool ContainerCanSimulate()
        {
            /* Whether explicit container type is assignable to editor container type */
            return GetContainerType().IsInstanceOfType(ContainerT);
        }

        /// <summary>
        /// Save current editing flow graph
        /// </summary>
        public void SaveGraphData()
        {
            var guiContent = new GUIContent();
            if (TrySerializeGraph())
            {
                ClearDirty();
                /* Commit graph data */
                ContainerT.SetGraphData(EditorObject.GraphData);
                /* Refresh editor object */
                EditorObject.DestroyTemporary();
                EditorObject = FlowGraphEditorObject.CreateTemporary(ContainerT);
                /* Refresh outer asset */
                EditorUtility.SetDirty(Container.Object);
                AssetDatabase.SaveAssetIfDirty(Container.Object);
                /* Reload graph view */
                _graphViews.Clear();
                StructVisualElements(GraphIndex);
                EditorInternalUtil.ClearConsole();
                guiContent.text = $"Save flow {Identifier.boundObject.name} succeed!";
                ShowNotification(guiContent, 0.5f);
            }
            else
            {
                guiContent.text = $"Failed to save flow {Identifier.boundObject.name}!";
                ShowNotification(guiContent, 0.5f);
            }
        }

        private bool TrySerializeGraph()
        {
            foreach (var view in _graphViews.Values)
            {
                if (!view.SerializeGraph(EditorObject))
                {
                    return false;
                }
            }

            return true;
        }
        
        private VisualElement CreateToolBar()
        {
            return new IMGUIContainer(
                () =>
                {
                    if (!Identifier.IsValid() || CurrentGraphView == null)
                    {
                        /* Should only happen when object destroyed */
                        return;
                    }
                    
                    GUILayout.BeginHorizontal(EditorStyles.toolbar);
                    // ========================= Left ToolBar ============================= //
                    /* Draw save button */
                    GUI.enabled = !Application.isPlaying;
                    var image  = EditorGUIUtility.IconContent("SaveAs@2x").image;
                    if (GUILayout.Button(new GUIContent(image,$"Save flow and serialize data to {Identifier.boundObject.name}"), EditorStyles.toolbarButton))
                    {
                        SaveGraphData();
                    }
                    
                    /* Draw simulation button */
                    if (ContainerCanSimulate())
                    {
                        GUI.enabled &= CurrentGraphView.CanSimulate();
                        image = EditorGUIUtility.IconContent("d_PlayButton On@2x").image;
                        if (GUILayout.Button(
                                new GUIContent("Run Flow", image, "Execute selected execution event in graph editor"),
                                EditorStyles.toolbarButton))
                        {
                            DoSimulation().Forget();
                        }

                        GUI.enabled = true;
                    }

                    /* Draw subGraph popup */
                    if (EditorObject.GraphInstance.IsUberGraph())
                    {
                        var newIndex = EditorGUILayout.Popup(GraphIndex, 
                            EditorObject.GraphNameContents,
                            EditorStyles.toolbarPopup,
                            GUILayout.MinWidth(100));
                        if (newIndex != GraphIndex)
                        {
                            StructVisualElements(GraphIndex = newIndex);
                        }
                    }
                        
                    // ========================= Left ToolBar ============================= //
                    
                    GUILayout.FlexibleSpace();
                    
                    // ========================= Right ToolBar ============================= //
                    GUI.enabled = DebugState.enableDebug && CurrentGraphView.IsPaused();
                    image = EditorGUIUtility.IconContent("Animation.NextKey").image;
                    if (GUILayout.Button(new GUIContent(image, $"Next Breakpoint"), EditorStyles.toolbarButton))
                    {
                        CurrentGraphView.NextBreakpoint();
                    }
                    
                    image = EditorGUIUtility.IconContent("Animation.Play").image;
                    if (GUILayout.Button(new GUIContent(image, $"Next Frame"), EditorStyles.toolbarButton))
                    {
                        CurrentGraphView.NextFrame();
                    }
                    
                    GUI.enabled = true;
                    if (DebugState.enableDebug)
                    {
                        image = EditorGUIUtility.IconContent("DebuggerAttached@2x").image;
                        if (GUILayout.Button(new GUIContent(image, $"Disable Debug Mode"), EditorStyles.toolbarButton))
                        {
                            CurrentGraphView.SetDebugEnabled(false);
                        }
                    }
                    else
                    {
                        image = EditorGUIUtility.IconContent("DebuggerDisabled@2x").image;
                        if (GUILayout.Button(new GUIContent(image, $"Enable Debug Mode"), EditorStyles.toolbarButton))
                        {
                            CurrentGraphView.SetDebugEnabled(true);
                        }
                    }
                    // ========================= Right ToolBar ============================= //
                    
                    GUILayout.EndHorizontal();
                }
            );
        }

        private async UniTask DoSimulation()
        {
            var guiContent = new GUIContent("Simulation Start");
            ShowNotification(guiContent, 1f);
            await CurrentGraphView.SimulateExecution();
            guiContent = new GUIContent("Simulation End");
            ShowNotification(guiContent, 1f);
        }
        
        protected override void OnReloadGraphView()
        {
            /* Create new editor container since it is not saved during play mode change  */
            if (EditorObject) EditorObject.DestroyTemporary();
            EditorObject = FlowGraphEditorObject.CreateTemporary(ContainerT);
            _graphViews.Clear();
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
                    StructVisualElements(GraphIndex);
                }
            }
            finally
            {
                ClearProgressBar();
            }
        }

        /// <summary>
        /// Create a new subGraph to present custom function
        /// </summary>
        public void CreateFunctionSubGraph()
        {
            var json = Resources.Load<TextAsset>("Ceres/Flow/TemplateSubGraphData").text;
            var templateSubGraph = new FlowGraph(JsonUtility.FromJson<FlowGraphSerializedData>(json));
            var functionName = "New Function";
            var id = 1;
            while (EditorObject.GraphNames.Contains(functionName))
            {
                functionName = $"New Function {id++}";
            }

            var function = new CustomFunction(functionName);
            if (!EditorObject.GraphInstance.AddFlowSubGraph(functionName, function.Value, FlowGraphUsage.Function, templateSubGraph))
            {
                /* Can not create function subGraph when function name has been registered as a subGraph name */
                CeresLogger.LogError($"A subGraph named {functionName} already exists!");
                return;
            }
            CurrentGraphView.Blackboard.AddVariable(function, true);
            EditorObject.Update();
            
            /* Display new subGraph view */
            OpenSubgraphView(functionName);
        }

        /// <summary>
        /// Resolve custom function parameter types
        /// </summary>
        /// <param name="customFunction"></param>
        /// <returns></returns>
        public (Type, Type[]) ResolveFunctionType(CustomFunction customFunction)
        {
            var slot = EditorObject.GraphInstance.SubGraphSlots.FirstOrDefault(subGraphSlot => subGraphSlot.Guid == customFunction.Value);
            if (slot == null) return (null, null);
            var input = slot.Graph.GetFirstNodeOfType<CustomFunctionInput>();
            var output = slot.Graph.GetFirstNodeOfType<CustomFunctionOutput>();
            CeresLogger.Assert(input != null && output != null, "Can not find function input and output node");
            var inputTypes = input!.parameters.Select(p => p.GetParameterType()).ToArray();
            var returnType = output!.parameter.hasReturn ? output.parameter.GetParameterType() : typeof(void);
            return (returnType, inputTypes);
        }
        
        /// <summary>
        /// Resolve custom function input parameters
        /// </summary>
        /// <param name="customFunction"></param>
        /// <returns></returns>
        public CustomFunctionInputParameter[] ResolveFunctionInputParameters(CustomFunction customFunction)
        {
            var slot = EditorObject.GraphInstance.SubGraphSlots.FirstOrDefault(subGraphSlot => subGraphSlot.Guid == customFunction.Value);
            var input = slot?.Graph.GetFirstNodeOfType<CustomFunctionInput>();
            return input?.parameters;
        }

        /// <summary>
        /// Open subGraph view by name
        /// </summary>
        /// <param name="subGraphName"></param>
        public void OpenSubgraphView(string subGraphName)
        {
            var index = Array.IndexOf(EditorObject.GraphNames, subGraphName);
            if (index == -1) return;
            StructVisualElements(GraphIndex = index);
        }

        /// <summary>
        /// Rename subGraph and update view
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="newName"></param>
        public void RenameSubgraph(string guid, string newName)
        {
            var slot = EditorObject.GraphInstance.SubGraphSlots.First(subGraphSlot => subGraphSlot.Guid == guid);
            /* Rename graph instance */
            slot.Name = newName;
            
            /* Rename subGraph data if exist */
            EditorObject.GraphData.RenameSubGraphData(guid, newName);
            
            /* Update graph view binding name */
            var slotIndex = Array.IndexOf(EditorObject.GraphInstance.SubGraphSlots, slot);
            if (slotIndex != -1 && _graphViews.TryGetValue(slotIndex + 1 /* graph index */, out var view))
            {
                view.GraphName = newName;
            }
            
            /* Update view model */
            EditorObject.Update();
        }

        /// <summary>
        /// Remove subGraph and update view
        /// </summary>
        /// <param name="guid"></param>
        public void RemoveSubgraph(string guid)
        {
            var slot = EditorObject.GraphInstance.SubGraphSlots.First(subGraphSlot => subGraphSlot.Guid == guid);
            var slotIndex = Array.IndexOf(EditorObject.GraphInstance.SubGraphSlots, slot);
            if (slotIndex == -1) return;
            
            /* Remove subGraph instance */
            ArrayUtils.Remove(ref EditorObject.GraphInstance.SubGraphSlots, slot);
            int graphIndex = slotIndex + 1;
            _graphViews.Remove(graphIndex);
            
            /* Remove subGraph data if exist */
            EditorObject.GraphData.RemoveSubGraphData(guid);
            
            /* Update view model */
            EditorObject.Update();

            /* Open uber graph if editing subGraph to remove */
            if (GraphIndex == graphIndex)
            {
                StructVisualElements(GraphIndex = 0);
            }
        }
    }
}