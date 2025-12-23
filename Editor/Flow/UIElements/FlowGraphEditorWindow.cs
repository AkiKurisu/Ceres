using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.CustomFunctions;
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

        public bool isPaused;

        public string currentNode;

        public void AddBreakpoint(string guid)
        {
            if (breakpoints.Contains(guid)) return;
            breakpoints.Add(guid);
        }

        public void RemoveBreakpoint(string guid)
        {
            breakpoints.Remove(guid);
        }
    }

    public class FlowGraphEditorWindow : CeresGraphEditorWindow<IFlowGraphContainer, FlowGraphEditorWindow>
    {
        /// <summary>
        /// Editor debug state
        /// </summary>
        [field: SerializeField]
        public FlowGraphDebugState DebugState { get; private set; } = new();

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

        private FlowGraphInspectorPanel _inspectorPanel;

        private VisualElement _graphViewContainer;

        protected override void OnInitialize()
        {
            var icon = Resources.Load<Texture>("Ceres/editor_icon");
            titleContent = new GUIContent($"Flow ({Identifier.boundObject.name})", icon);
            EditorObject = FlowGraphEditorObject.CreateTemporary(ContainerT);
            _inspectorPanel = new FlowGraphInspectorPanel(this);
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

        /// <summary>
        /// Update hot reload border style based on enabled state
        /// </summary>
        private void UpdateHotReloadBorderStyle()
        {
            if (_graphViewContainer == null) return;

            var hotReloadEnabled = FlowGraphHotReloadManager.IsHotReloadEnabled;

            if (hotReloadEnabled)
            {
                CurrentGraphView.AddToClassList("hot-reload-enabled");
            }
            else
            {
                CurrentGraphView.RemoveFromClassList("hot-reload-enabled");
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
            rootVisualElement.Add(new IMGUIContainer(OnToolBarGUI));

            // Load inspector panel stylesheet
            rootVisualElement.styleSheets.Add(CeresGraphView.GetOrLoadStyleSheet("Ceres/Flow/InspectorPanel"));

            // Create split view for graph view and inspector
            var splitView = new TwoPaneSplitView(1, 400, TwoPaneSplitViewOrientation.Horizontal)
            {
                name = "GraphSplitView"
            };

            // Left pane: Graph View
            _graphViewContainer = new VisualElement
            {
                name = "GraphViewContainer"
            };
            var graphView = GetOrCreateGraphView(index);
            _graphViewContainer.Add(graphView);
            splitView.Add(_graphViewContainer);

            // Update hot reload border style
            UpdateHotReloadBorderStyle();

            // Right pane: Inspector
            var inspectorContainer = _inspectorPanel.CreatePanel();
            splitView.Add(inspectorContainer);

            rootVisualElement.Add(splitView);

            // Setup selection listener for inspector
            _inspectorPanel.AttachSelectionListener(rootVisualElement);
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
                /* Trigger hot reload check if in play mode and hot reload is enabled */
                if (Application.isPlaying && FlowGraphHotReloadManager.IsHotReloadEnabled)
                {
                    FlowGraphHotReloadManager.RefreshContainerTimestamps();
                }
                /* Commit graph data */
                ContainerT.SetGraphData(EditorObject.GraphData);
                /* Refresh editor object */
                EditorObject.DestroyTemporary();
                EditorObject = FlowGraphEditorObject.CreateTemporary(ContainerT);
                /* Refresh outer asset */
                EditorUtility.SetDirty(Container.Object);
                AssetDatabase.SaveAssetIfDirty(Container.Object);

                if (Application.isPlaying && FlowGraphHotReloadManager.IsHotReloadEnabled)
                {
                    FlowGraphHotReloadManager.CheckForChanges();
                }

                /* Reload graph view */
                var currentViewPosition = CurrentGraphView.viewTransform.position;
                _graphViews.Clear();
                StructVisualElements(GraphIndex);
                CurrentGraphView.viewTransform.position = currentViewPosition;
                if (CeresSettings.CleanLogAuto)
                {
                    EditorInternalUtil.ClearConsole();
                }
                /* Notify user */
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

        private void OnToolBarGUI()
        {
            if (!Identifier.IsValid() || CurrentGraphView == null)
            {
                /* Should only happen when object destroyed */
                return;
            }

            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            // ========================= Left ToolBar ============================= //
            /* Draw save button */
            var hotReloadEnabled = FlowGraphHotReloadManager.IsHotReloadEnabled;
            using (new EditorGUI.DisabledScope(Application.isPlaying && !hotReloadEnabled))
            {
                var image = EditorGUIUtility.IconContent("SaveAs@2x").image;
                if (GUILayout.Button(new GUIContent(image, $"Save flow and serialize data to {Identifier.boundObject.name}"), EditorStyles.toolbarButton))
                {
                    SaveGraphData();
                }
            }

            /* Draw simulation button */
            if (ContainerCanSimulate())
            {
                using (new EditorGUI.DisabledScope(!CurrentGraphView.CanSimulate()))
                {
                    var image = EditorGUIUtility.IconContent("d_PlayButton On@2x").image;
                    if (GUILayout.Button(
                            new GUIContent("Run Flow", image, "Execute selected execution event in graph editor"),
                            EditorStyles.toolbarButton))
                    {
                        DoSimulation().Forget();
                    }
                }
            }

            /* Draw subGraph popup */
            if (EditorObject.GraphInstance.IsUberGraph())
            {
                var newIndex = EditorGUILayout.Popup(GraphIndex, EditorObject.GraphNameContents, EditorStyles.toolbarPopup, GUILayout.MinWidth(100));
                if (newIndex != GraphIndex)
                {
                    StructVisualElements(GraphIndex = newIndex);
                }
            }

            // ========================= Left ToolBar ============================= //

            GUILayout.FlexibleSpace();

            // ========================= Right ToolBar ============================= //
            using (new EditorGUI.DisabledScope(!DebugState.enableDebug || !CurrentGraphView.IsPaused()))
            {
                var image = EditorGUIUtility.IconContent("Animation.NextKey").image;
                if (GUILayout.Button(new GUIContent(image, $"Next Breakpoint"), EditorStyles.toolbarButton))
                {
                    CurrentGraphView.NextBreakpoint();
                }

                image = EditorGUIUtility.IconContent("Animation.Play").image;
                if (GUILayout.Button(new GUIContent(image, $"Next Frame"), EditorStyles.toolbarButton))
                {
                    CurrentGraphView.NextFrame();
                }
            }

            /* Draw hot reload toggle button */
            var originalColor = GUI.color;
            var originalBgColor = GUI.backgroundColor;
            var originalContentColor = GUI.contentColor;

            if (hotReloadEnabled)
            {
                // Cool pulsing effect when hot reload is enabled
                var pulse = (Mathf.Sin((float)EditorApplication.timeSinceStartup * 3f) + 1f) * 0.5f;
                var glowIntensity = 0.7f + pulse * 0.3f;

                // Orange/red glowing effect
                GUI.color = new Color(1f, 0.5f + pulse * 0.2f, 0.1f, 1f);
                GUI.backgroundColor = new Color(1f, 0.4f, 0.1f, 0.4f * glowIntensity);
                GUI.contentColor = new Color(1f, 0.9f, 0.7f, 1f); // Bright text
            }
            else
            {
                GUI.contentColor = new Color(0.7f, 0.7f, 0.7f, 1f); // Dimmed text
            }

            var hotReloadIcon = hotReloadEnabled
                ? EditorGUIUtility.IconContent("d_Refresh@2x").image
                : EditorGUIUtility.IconContent("Refresh@2x").image;
            var hotReloadLabel = hotReloadEnabled ? "🔥 Hot Reload" : "Hot Reload";
            var hotReloadTooltip = hotReloadEnabled
                ? "Hot Reload: ON - Graph will auto-reload when saved during play mode"
                : "Hot Reload: OFF - Click to enable hot reload during play mode";

            if (GUILayout.Button(new GUIContent(hotReloadLabel, hotReloadIcon, hotReloadTooltip), EditorStyles.toolbarButton))
            {
                FlowGraphHotReloadManager.IsHotReloadEnabled = !FlowGraphHotReloadManager.IsHotReloadEnabled;
                // Update border style when toggling hot reload
                UpdateHotReloadBorderStyle();
            }

            GUI.color = originalColor;
            GUI.backgroundColor = originalBgColor;
            GUI.contentColor = originalContentColor;


            if (DebugState.enableDebug)
            {
                var image = EditorGUIUtility.IconContent("DebuggerAttached@2x").image;
                if (GUILayout.Button(new GUIContent(image, $"Disable Debug Mode"), EditorStyles.toolbarButton))
                {
                    CurrentGraphView.SetDebugEnabled(false);
                }
            }
            else
            {
                var image = EditorGUIUtility.IconContent("DebuggerDisabled@2x").image;
                if (GUILayout.Button(new GUIContent(image, $"Enable Debug Mode"), EditorStyles.toolbarButton))
                {
                    CurrentGraphView.SetDebugEnabled(true);
                }
            }
            // ========================= Right ToolBar ============================= //

            GUILayout.EndHorizontal();
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
            _inspectorPanel = new FlowGraphInspectorPanel(this);
            InitializeFlowGraphView();
        }

        private void InitializeFlowGraphView()
        {
            try
            {
                if (Container is IRedirectFlowGraphRuntimeType redirector)
                {
                    /* Redirect container type */
                    SetContainerType(redirector.GetRuntimeType());
                }
                DisplayProgressBar("Initialize field factory", 0f);
                {
                    FieldResolverFactory.Get();
                }
                DisplayProgressBar("Initialize node view factory", 0.3f);
                {
                    NodeViewFactory.Get();
                }
                DisplayProgressBar("Initialize executable function registry", 0.5f);
                {
                    ExecutableFunctionRegistry.Get();
                }
                DisplayProgressBar("Initialize flow graph function registry", 0.7f);
                {
                    FlowGraphFunctionRegistry.Get();
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
            var json = Resources.Load<TextAsset>("Ceres/Flow/FunctionSubGraphData").text;
            var templateSubGraph = new FlowGraph(JsonUtility.FromJson<FlowGraphSerializedData>(json));
            var functionName = "New Function";
            var id = 1;
            while (EditorObject.GraphNames.Contains(functionName))
            {
                functionName = $"New Function {id++}";
            }

            var function = new LocalFunction(functionName);
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

        public bool CanCreateFunctionSubGraph()
        {
            // Can only create function in uber graph
            return GraphIndex == 0 && EditorObject.GraphInstance.IsUberGraph();
        }

        /// <summary>
        /// Resolve custom function parameter types
        /// </summary>
        /// <param name="localFunction"></param>
        /// <returns></returns>
        public (Type, Type[]) ResolveFunctionTypes(LocalFunction localFunction)
        {
            var slot = EditorObject.GraphInstance.SubGraphSlots.FirstOrDefault(subGraphSlot => subGraphSlot.Guid == localFunction.Value);
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
        /// <param name="localFunction"></param>
        /// <returns></returns>
        public CustomFunctionInputParameter[] ResolveFunctionInputParameters(LocalFunction localFunction)
        {
            var slot = EditorObject.GraphInstance.SubGraphSlots.FirstOrDefault(subGraphSlot => subGraphSlot.Guid == localFunction.Value);
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