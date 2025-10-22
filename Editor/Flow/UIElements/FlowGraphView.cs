using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Properties;
using Ceres.Editor.Graph.Flow.Properties;
using Ceres.Editor.Graph.Flow.CustomFunctions;
using Chris;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace Ceres.Editor.Graph.Flow
{
    public class FlowGraphView : CeresGraphView, IDisposable
    {
        /// <summary>
        /// Editor debug state
        /// </summary>
        public FlowGraphDebugState DebugState { get; }

        /// <summary>
        /// Bound editor window
        /// </summary>
        public FlowGraphEditorWindow FlowGraphEditorWindow { get; }

        private FlowGraphDebugger _debugger;

        private bool _isEditingSubGraph;

        /// <summary>
        /// Bound graph verified name
        /// </summary>
        public string GraphName { get; internal set; }

        public FlowGraphView(FlowGraphEditorWindow editorWindow) : base(editorWindow)
        {
            FlowGraphEditorWindow = editorWindow;
            DebugState = editorWindow.DebugState;
            AddStyleSheet("Ceres/Flow/GraphView");
            AddSearchWindow<ExecutableNodeSearchWindow>();
            AddNodeGroupHandler(new ExecutableNodeGroupHandler(this));
            AddBlackboard(new FlowBlackboard(this));
            FlowGraphTracker.SetDefaultTracker(_debugger = new FlowGraphDebugger(this));
            RegisterCallback<KeyDownEvent>(HandleKeyBoardCommands);
        }

        /// <summary>
        /// Serialize flow graph to editor data container
        /// </summary>
        /// <param name="editorObject">Serialization target</param>
        /// <returns>Whether serialization is succeeded</returns>
        public bool SerializeGraph(FlowGraphEditorObject editorObject)
        {
            /* Compile validation */
            using var validator = new FlowGraphValidator();
            var nodeViews = NodeViews.OfType<ExecutableNodeView>().ToArray();
            foreach (var nodeView in nodeViews)
            {
                /* Skip if no connections at all */
                if (nodeView.IsIsolated()) continue;
                nodeView.Validate(validator);
            }

            var invalidNodeViews = nodeViews.Where(x => x.IsInvalid()).ToArray();
            if (invalidNodeViews.Any())
            {
                ClearSelection();
                Array.ForEach(invalidNodeViews, view => AddToSelection(view.NodeElement));
                schedule.Execute(() => FrameSelection()).ExecuteLater(10);
                return false;
            }

            var editableData = editorObject.GraphData;
            var flowGraphData = new CopyPasteGraph(this, graphElements).SerializeGraph();
            if (_isEditingSubGraph)
            {
                editableData ??= new FlowGraphData();
                var slot = (FlowSubGraphSlot)FlowGraphEditorWindow.EditorObject.GraphInstance.SubGraphSlots
                            .FirstOrDefault(x => x.Name == GraphName);
                CeresLogger.Assert(slot != null, $"Can not find subGraph named {GraphName}");
                /*
                 * Subgraph need be serialized with outer.
                 * Notice there is object-slicing since serialized data structure is typeof(FlowGraphSerializedData).
                 */
                editableData.SetSubGraphData(slot, flowGraphData);
            }
            else
            {
                /* Move SubGraph data to new UberGraph data */
                if (editableData?.subGraphData != null)
                {
                    flowGraphData.subGraphData = editableData.subGraphData;
                }
                editableData = flowGraphData;
            }
            editorObject.GraphData = editableData;
            return true;
        }

        protected override string OnCopySerializedGraph(IEnumerable<GraphElement> elements)
        {
            return JsonUtility.ToJson(new CopyPasteGraph(this, elements).SerializeGraph(true));
        }

        protected override bool CanPasteSerializedGraph(string serializedData)
        {
            try
            {
                var flowGraphData = JsonUtility.FromJson<FlowGraphData>(serializedData);
                return flowGraphData != null;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnPasteSerializedGraph(string operationName, string serializedData)
        {
            var flowGraphData = JsonUtility.FromJson<FlowGraphData>(serializedData);
            var flowGraph = new FlowGraph(flowGraphData);
            new CopyPasteGraph(this, graphElements).DeserializeGraph(flowGraph, true);
        }

        public void DeserializeGraph(FlowGraphEditorObject editorObject)
        {
            new CopyPasteGraph(this, graphElements).DeserializeGraph(editorObject.GraphInstance);
            _isEditingSubGraph = false;
            GraphName = string.Empty;
            ClearDirty();
        }

        public void DeserializeSubGraph(FlowGraphEditorObject editorObject, string slotName)
        {
            var subGraph = editorObject.GraphInstance.FindSubGraph<FlowGraph>(slotName);
            new CopyPasteGraph(this, graphElements).DeserializeGraph(subGraph);
            _isEditingSubGraph = true;
            GraphName = slotName;
            ClearDirty();
        }

        private void HandleKeyBoardCommands(KeyDownEvent evt)
        {
            if (!evt.ctrlKey || evt.keyCode != KeyCode.S)
            {
                return;
            }

            if (!FlowGraphEditorWindow) return;
            FlowGraphEditorWindow.SaveGraphData();
        }

        /// <summary>
        /// Execute simulate event in editor if existed
        /// </summary>
        public async UniTask SimulateExecution()
        {
            var flowGraphData = new CopyPasteGraph(this, graphElements).SerializeGraph();
            var eventName = ((ExecutionEventBaseNodeView)((ExecutableNodeElement)selection[0]).View).GetEventName();
            flowGraphData.PreSerialization();
            using var graph = new FlowGraph(flowGraphData);
            using var context = FlowGraphCompilationContext.GetPooled();
            using var compiler = CeresGraphCompiler.GetPooled(graph, context);
            graph.Compile(compiler);
            await graph.ExecuteEventAsyncInternal(EditorWindow.Container.Object, eventName);
        }

        public bool CanSimulate()
        {
            if (IsPaused()) return false;

            if (selection.Count == 0) return false;
            return selection[0] is ExecutableNodeElement { View: ExecutionEventNodeView view } &&
                   view.NodeType == typeof(ExecutionEvent);
        }

        protected override void OnDragDropElementPerform(List<ISelectable> selectables, GraphElement graphElement, Vector3 mousePosition)
        {
            /* Need perform on background */
            if (graphElement != null)
            {
                return;
            }

            foreach (var selectable in selectables)
            {
                if (selectable is not BlackboardField blackboardField) continue;


                var variableName = blackboardField.text;
                var variable = Blackboard.GetSharedVariable(variableName);
                if (variable == null) continue;
                Rect newRect = new(contentViewContainer.WorldToLocal(mousePosition), new Vector2(100, 100));

                if (variable is LocalFunction customFunction)
                {
                    var (returnType, inputTypes) = FlowGraphEditorWindow.ResolveFunctionTypes(customFunction);
                    if (returnType == null)
                    {
                        return;
                    }
                    // Create execute custom function node
                    var nodeType = ExecutableNodeReflectionHelper.PredictCustomFunctionNodeType(returnType, inputTypes);
                    if (returnType == typeof(void))
                    {
                        var view = (ExecuteCustomFunctionNodeView)NodeViewFactory.Get().CreateInstanceResolved(nodeType, this, inputTypes);
                        this.AddNodeView(view, newRect);
                        view.SetLocalFunction(customFunction.Name);
                    }
                    else
                    {
                        var view = (ExecuteCustomFunctionNodeView)NodeViewFactory.Get().CreateInstanceResolved(nodeType, this, inputTypes.Append(returnType).ToArray());
                        this.AddNodeView(view, newRect);
                        view.SetLocalFunction(customFunction.Name);
                    }
                    return;
                }


                var menu = new GenericMenu();
                menu.AddItem(new GUIContent($"Get {variableName}"), false, () =>
                {
                    var view = (PropertyNodeView)NodeViewFactory.Get().CreateInstanceResolved(typeof(PropertyNode_GetSharedVariableTValue<,,>),
                        this, variable.GetType(), ReflectionUtility.GetGenericArgumentType(variable.GetType()), variable.GetValueType());
                    this.AddNodeView(view, newRect);
                    view.SetPropertyName(variableName);
                });
                menu.AddItem(new GUIContent($"Set {variableName}"), false, () =>
                {
                    var view = (PropertyNodeView)NodeViewFactory.Get().CreateInstanceResolved(typeof(PropertyNode_SetSharedVariableTValue<,,>),
                        this, variable.GetType(), ReflectionUtility.GetGenericArgumentType(variable.GetType()), variable.GetValueType());
                    this.AddNodeView(view, newRect);
                    view.SetPropertyName(variableName);
                });
                menu.ShowAsContext();
            }
        }

        public void SetDebugEnabled(bool enabled)
        {
            DebugState.enableDebug = enabled;
            var guiContent = new GUIContent(enabled ? "Flow debugger attached" : "Flow debugger detached");
            EditorWindow.ShowNotification(guiContent, 0.5f);
        }

        public bool IsPaused()
        {
            return _debugger?.IsPaused ?? false;
        }

        public void NextFrame()
        {
            _debugger.NextFrame();
        }

        public void NextBreakpoint()
        {
            _debugger.NextBreakpoint();
        }

        protected override void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            _debugger?.Dispose();
            _debugger = null;
        }

        private class CopyPasteGraph
        {
            private readonly FlowGraphView _graphView;

            private readonly GraphElement[] _graphElements;

            public CopyPasteGraph(FlowGraphView flowGraphView, IEnumerable<GraphElement> elements)
            {
                _graphView = flowGraphView;
                _graphElements = elements.ToArray();
            }

            /// <summary>
            /// Serialize <see cref="FlowGraphView"/> to <see cref="FlowGraphData"/>
            /// </summary>
            /// <param name="copyPaste"></param>
            /// <returns></returns>
            public FlowGraphData SerializeGraph(bool copyPaste = false)
            {
                var serializableNodes = _graphElements
                    .OfType<ExecutableNodeElement>()
                    .ToArray();
                var nodeGroups = _graphElements
                    .OfType<ExecutableNodeGroup>().ToArray();
                var idMap = serializableNodes
                    .ToDictionary(nodeElement => nodeElement, nodeElement => nodeElement.View.Guid);

                /* Collect relay nodes */
                var relayNodes = _graphView.nodes
                    .Where(n => n.userData is RelayNodeView)
                    .Select(n => n.userData as RelayNodeView)
                    .ToList();

                /* Need assign new guid before serialization */
                if (copyPaste)
                {
                    foreach (var nodeElement in serializableNodes)
                    {
                        nodeElement.View.Guid = Guid.NewGuid().ToString();
                    }
                    foreach (var relay in relayNodes)
                    {
                        relay.Guid = Guid.NewGuid().ToString();
                    }
                }

                /* Record relay node connections BEFORE CompileNode flattens them */
                var relayNodeData = relayNodes.Select(view => view.Compile()).ToArray();

                var nodeInstances = serializableNodes.Select(element => (CeresNode)element.View.CompileNode()).ToArray();
                var nodeGroupData = new List<NodeGroup>();
                foreach (var group in nodeGroups)
                {
                    group.Commit(nodeGroupData);
                }

                /* Restore node element guid */
                if (copyPaste)
                {
                    foreach (var nodeElement in serializableNodes)
                    {
                        nodeElement.View.Guid = idMap[nodeElement];
                    }
                }

                /* Copy and paste may log warning for missing connect nodes which is expected. */
                using (CeresLogger.LogScope(copyPaste ? LogType.Error : LogType.Log))
                {
                    var linker = new CeresLinker(FlowConfig.IsIncludedAssembly);
                    var flowGraphData = new FlowGraphData
                    {
                        nodeData = nodeInstances.Select(node => LinkAndGetNodeSerializedData(linker, node)).ToArray(),
                        variableData = _graphView.SharedVariables.Where(variable => variable is not LocalFunction)
                                                                .Select(variable => variable.GetSerializedData())
                                                                .ToArray(),
                        nodeGroups = nodeGroupData.ToArray(),
                        relayNodes = relayNodeData
                    };
                    flowGraphData.PreSerialization();
                    // Save graph linker data
                    linker.Save();
                    return flowGraphData;
                }

            }

            private static CeresNodeData LinkAndGetNodeSerializedData(CeresLinker linker, CeresNode node)
            {
                var data = node.GetSerializedData();
                if (data.genericParameters.Length > 0)
                {
                    linker.LinkTypes(node.GetType().GetGenericArguments());
                }
                return data;
            }

            /// <summary>
            /// Serialize <see cref="FlowGraphView"/> from <see cref="FlowGraphData"/>
            /// </summary>
            /// <param name="flowGraph"></param>
            /// <param name="copyPaste"></param>
            public void DeserializeGraph(FlowGraph flowGraph, bool copyPaste = false)
            {
                using var collection = ListPool<GraphElement>.Get(out var newElements);
                if (copyPaste)
                {
                    foreach (var nodeInstance in flowGraph.nodes)
                    {
                        var rect = nodeInstance.GraphPosition;
                        rect.x += 30;
                        rect.y += 30;
                        nodeInstance.GraphPosition = rect;
                    }

                    foreach (var nodeGroup in flowGraph.nodeGroups)
                    {
                        var rect = nodeGroup.position;
                        rect.x += 30;
                        rect.y += 30;
                        nodeGroup.position = rect;
                    }
                }

                // Restore variables
                _graphView.AddSharedVariables(flowGraph.variables, !copyPaste);

                // Restore node views
                foreach (var nodeInstance in flowGraph.nodes)
                {
                    var nodeView = (ExecutableNodeView)NodeViewFactory.Get().CreateInstance(nodeInstance.GetType(), _graphView);
                    /* Missing node class should be handled before deserializing graph */
                    CeresLogger.Assert(nodeView != null, $"Can not construct node view for type {nodeInstance.GetType()}");
                    try
                    {
                        nodeView!.SetNodeInstance(nodeInstance);
                        _graphView.AddNodeView(nodeView);
                        newElements.Add(nodeView!.NodeElement);
                    }
                    catch (Exception e)
                    {
                        CeresLogger.LogError($"Failed to construct node view for type {nodeInstance} with exception thrown:\n{e}");
                        /* Replace with illegal property node */
                        nodeView = new IllegalExecutableNodeView(typeof(IllegalExecutableNode), _graphView);
                        nodeView!.SetNodeInstance(new IllegalExecutableNode
                        {
                            nodeType = nodeInstance.NodeData.nodeType.ToString(),
                            serializedData = nodeInstance.NodeData.serializedData
                        });
                        _graphView.AddNodeView(nodeView);
                        newElements.Add(nodeView!.NodeElement);
                    }
                }
                foreach (var nodeView in newElements.OfType<ExecutableNodeElement>().Select(element => element.View).ToArray())
                {
                    // Restore edges
                    nodeView.ReconnectEdges();
                    // Restore breakpoints
                    if (_graphView.DebugState.breakpoints.Contains(nodeView.Guid))
                    {
                        nodeView.AddBreakpoint();
                    }
                }

                // Restore node groups
                newElements.AddRange(_graphView.NodeGroupHandler.RestoreGroups(flowGraph.nodeGroups));

                // Restore relay nodes
                RestoreRelayNodes(flowGraph.relayNodes, newElements, copyPaste);

                if (copyPaste)
                {
                    _graphView.ClearSelection();
                    newElements.ForEach(x => _graphView.AddToSelection(x));
                    _graphView.schedule.Execute(() => _graphView.FrameSelection()).ExecuteLater(10);
                }
            }

            /// <summary>
            /// Restore relay nodes from serialized data
            /// </summary>
            private void RestoreRelayNodes(List<RelayNode> relayNodeData, List<GraphElement> newElements, bool copyPaste)
            {
                if (relayNodeData == null || relayNodeData.Count == 0)
                    return;

                // Create all relay nodes with saved port type
                var relayViews = new List<RelayNodeView>();
                foreach (var relayData in relayNodeData)
                {
                    // Adjust position for copy-paste
                    if (copyPaste)
                    {
                        var rect = relayData.graphPosition;
                        rect.x += 30;
                        rect.y += 30;
                        relayData.graphPosition = rect;
                    }

                    // Create relay node view with saved port type
                    var portType = relayData.GetPortType();
                    var relayView = new RelayNodeView(_graphView, relayData, portType);
                    _graphView.AddElement(relayView.NodeElement);
                    newElements.Add(relayView.NodeElement);
                    relayViews.Add(relayView);
                }

                // Rebuild connections
                for (int i = 0; i < relayViews.Count; i++)
                {
                    RestoreRelayConnections(relayViews[i], relayNodeData[i]);
                }
            }

            /// <summary>
            /// Restore relay node connections in the editor
            /// </summary>
            private void RestoreRelayConnections(RelayNodeView relay, RelayNode data)
            {
                var inputPort = relay.GetInputPort();
                var outputPort = relay.GetOutputPort();

                // Connect inputs
                foreach (var input in data.inputs)
                {
                    CeresPortElement sourcePortElement = null;

                    if (input.connectionType == RelayConnection.ConnectionType.CeresNode)
                    {
                        // Find CeresNode's port
                        var sourceNode = _graphView.FindNodeView<ExecutableNodeView>(input.nodeId);
                        var sourcePort = sourceNode?.FindPortView(input.portId, input.portIndex);
                        sourcePortElement = sourcePort?.PortElement;
                    }
                    else if (input.connectionType == RelayConnection.ConnectionType.RelayNode)
                    {
                        // Find RelayNode's output port
                        var sourceRelayNode = _graphView.nodes
                            .FirstOrDefault(n => n.userData is RelayNodeView rv && rv.Guid == input.nodeId);
                        if (sourceRelayNode?.userData is RelayNodeView sourceRelay)
                        {
                            sourcePortElement = sourceRelay.GetOutputPort();
                        }
                    }

                    // Create connection to this relay's input
                    if (sourcePortElement != null)
                    {
                        _graphView.ConnectPorts(sourcePortElement, inputPort);
                    }
                }

                // Connect outputs
                foreach (var output in data.outputs)
                {
                    CeresPortElement targetPortElement = null;

                    if (output.connectionType == RelayConnection.ConnectionType.CeresNode)
                    {
                        // Find ExecutableNode's port
                        var targetNode = _graphView.FindNodeView<ExecutableNodeView>(output.nodeId);
                        var targetPort = targetNode?.FindPortView(output.portId, output.portIndex);
                        targetPortElement = targetPort?.PortElement;
                    }
                    else if (output.connectionType == RelayConnection.ConnectionType.RelayNode)
                    {
                        // Find RelayNode's input port
                        var targetRelayNode = _graphView.nodes
                            .FirstOrDefault(n => n.userData is RelayNodeView rv && rv.Guid == output.nodeId);
                        if (targetRelayNode?.userData is RelayNodeView targetRelay)
                        {
                            targetPortElement = targetRelay.GetInputPort();
                        }
                    }

                    // Create connection from this relay's output
                    if (targetPortElement != null)
                    {
                        _graphView.ConnectPorts(outputPort, targetPortElement);
                    }
                }
            }
        }

        private class FlowGraphDebugger : FlowGraphTracker
        {
            private FlowGraphView _graphView;

            public bool IsPaused
            {
                get => _debugState.isPaused;
                private set => _debugState.isPaused = value;
            }

            private bool _isDestroyed;

            private ExecutableNodeView _currentView;
            
            private ExecutableNodeView CurrentView
            {
                get => _currentView;
                set
                {
                    _currentView = value;
                    _debugState.currentNode = _currentView?.Guid ?? string.Empty;
                }
            }

            private bool _breakOnNext;

            private readonly FlowGraphDebugState _debugState;

            public FlowGraphDebugger(FlowGraphView graphView)
            {
                _graphView = graphView;
                _debugState = graphView.DebugState;
            }

            public override async UniTask EnterNode(ExecutableNode node)
            {
                CurrentView = (ExecutableNodeView)_graphView.FindNodeView(node.Guid);
                if (_debugState.enableDebug)
                {
                    if (CurrentView != null)
                    {
                        CurrentView.NodeElement.AddToClassList("status_pending");
                        CurrentView.NodeElement.AddToClassList("status_execute");
                        _graphView.ClearSelection();
                        _graphView.AddToSelection(CurrentView.NodeElement);
                        _graphView.FrameSelection();
                        foreach (var portView in CurrentView.GetAllPortViews())
                        {
                            // Only debug input
                            if (portView.Binding.GetDirection() == Direction.Output)
                            {
                                continue;
                            }
                            
                            // Hidden field (Exec)
                            if (string.IsNullOrEmpty(portView.Binding.DisplayName.Value))
                            {
                                continue;
                            }
                            
                            string portName = portView.Binding.GetPortName();
                            if (!node.Ports.TryGetValue(portName, out var port))
                            {
                                continue;
                            }

                            var portValueType = port.GetValueType();
                            if (!IsDebuggablePortType(portValueType))
                            {
                                continue;
                            }
                            
                            portView.StartDebug();
                            var value = port.GetValue();
                            portView.SetDebugData(value == null ? "Null" : JsonConvert.SerializeObject(value));
                        }
                    }
                    IsPaused = true;
                }
                if (!CanSkipFrame() && CanPauseOnCurrentNode())
                {
                    CeresLogger.Log($"Enter node >>> [{node.GetTypeName()}]({node.Guid})");
                    /* Reset skip frame flag */
                    _breakOnNext = false;
                    Time.timeScale = 0;
                    EditorApplication.isPaused = true;
                    await UniTask.WaitUntil(CanSkipFrame);
                    CeresLogger.Log($"Exit node <<< [{node.GetTypeName()}]({node.Guid})");
                }
                CurrentView?.NodeElement.RemoveFromClassList("status_execute");
            }

            private static bool IsDebuggablePortType(Type portValueType)
            {
                if (ReflectionUtility.IsSerializableNumericTypes(portValueType))
                {
                    return true;
                }
                        
                if (ReflectionUtility.IsUnityBuiltinTypes(portValueType))
                {
                    return true;
                }

                if (Attribute.IsDefined(portValueType, typeof(SerializableAttribute), true))
                {
                    return true;
                }

                return false;
            }

            public override UniTask ExitNode(ExecutableNode node)
            {
                if (_graphView?.FindNodeView(node.Guid) is ExecutableNodeView nodeView)
                {
                    foreach (var portView in nodeView.GetAllPortViews())
                    {
                        portView.EndDebug();
                    }
                    nodeView.NodeElement.RemoveFromClassList("status_pending");
                }
                return UniTask.CompletedTask;
            }

            private bool CanSkipFrame()
            {
                if (!IsPaused || !_debugState.enableDebug || _isDestroyed)
                {
                    return true;
                }

                return false;
            }

            private bool CanPauseOnCurrentNode()
            {
                var hasBreakpoint = false;
                if (CurrentView != null)
                {
                    hasBreakpoint = _debugState.breakpoints.Contains(CurrentView.Guid);
                }
                return _breakOnNext || hasBreakpoint;
            }

            public void NextFrame()
            {
                _breakOnNext = true;
                IsPaused = false;
                EditorApplication.isPaused = false;
            }

            public void NextBreakpoint()
            {
                _breakOnNext = false;
                IsPaused = false;
                EditorApplication.isPaused = false;
            }

            public override void Dispose()
            {
                if (IsPaused)
                {
                    IsPaused = false;
                    EditorApplication.isPaused = false;
                }
                _isDestroyed = true;
                _graphView = null;
                _currentView = null;
                SetDefaultTracker(null);
                base.Dispose();
            }
        }
    }
}
