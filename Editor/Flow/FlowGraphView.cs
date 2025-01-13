using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Properties;
using Chris;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Pool;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph.Flow
{
    public class FlowGraphView : CeresGraphView, IDisposable
    {
        private FlowGraphDebugTracker _tracker;
        
        public FlowGraphDebugState DebugState { get; }
        
        public FlowGraphView(FlowGraphEditorWindow editorWindow) : base(editorWindow)
        {
            DebugState = editorWindow.debugState;
            AddStyleSheet("Ceres/Flow/GraphView");
            AddSearchWindow<ExecutableNodeSearchWindow>();
            AddNodeGroupHandler(new ExecutableNodeGroupHandler(this));
            AddBlackboard(new FlowBlackboard(this));
            FlowGraphTracker.SetActiveTracker(_tracker = new FlowGraphDebugTracker(this));
        }

        public override bool SerializeGraph(ICeresGraphContainer container)
        {
            if (container is not IFlowGraphContainer flowGraphContainer) return false;
            var flowGraphData = new CopyPasteGraph(this, graphElements).SerializeGraph();
            flowGraphContainer.SetGraphData(flowGraphData);
            EditorUtility.SetDirty(container.Object);
            AssetDatabase.SaveAssetIfDirty(container.Object);
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

        public override void DeserializeGraph(ICeresGraphContainer container)
        {
            var graph = (FlowGraph)container.GetGraph();
            new CopyPasteGraph(this, graphElements).DeserializeGraph(graph);
        }

        /// <summary>
        /// Execute simulate event in editor if existed
        /// </summary>
        public async UniTask SimulateExecution()
        {
            var eventName = (((ExecutableNodeElement)selection[0]).View as ExecutableEventNodeView)!.GetEventName();
            var nodeInstances = nodes.OfType<ExecutableNodeElement>()
                                                .Select(x => x.View.CompileNode() as CeresNode)
                                                .ToArray();
            var flowGraphData = new FlowGraphData
            {
                nodes = nodeInstances,
                nodeData = nodeInstances.Select(x=>x.GetSerializedData()).ToArray(),
                variables = SharedVariables.ToArray()
            };
            flowGraphData.PreSerialization();
            using var graph = new FlowGraph(flowGraphData.CloneT<FlowGraphData>());
            graph.Compile();
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
                if(variable == null) continue;
                Rect newRect = new(contentViewContainer.WorldToLocal(mousePosition), new Vector2(100, 100));
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
            return _tracker.IsPaused;
        }

        public void NextFrame()
        {
            _tracker.NextFrame();
        }
        
        public void NextBreakpoint()
        {
            _tracker.NextBreakpoint();
        }

        protected override void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            _tracker?.Dispose();
            _tracker = null;
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
                var serializableNodes = _graphElements.OfType<ExecutableNodeElement>().ToArray();
                var nodeGroups = _graphElements.OfType<ExecutableNodeGroup>().ToArray();
                var idMap = serializableNodes.ToDictionary(x => x, x => x.View.Guid);
                
                /* Need assign new guid before serialization */
                if (copyPaste)
                {
                    foreach (var nodeElement in serializableNodes)
                    {
                        nodeElement.View.Guid = Guid.NewGuid().ToString();
                    }
                }
                
                var nodeInstances = serializableNodes.Select(x => (CeresNode)x.View.CompileNode()).ToArray();
                var data = new List<NodeGroup>();
                foreach (var group in nodeGroups)
                {
                    group.Commit(data);
                }

                /* Restore node element guid */
                if (copyPaste)
                {
                    foreach (var nodeElement in serializableNodes)
                    {
                        nodeElement.View.Guid = idMap[nodeElement];
                    }
                }
            
                var flowGraphData = new FlowGraphData
                {
                    nodes = nodeInstances,
                    nodeData = nodeInstances.Select(x=> x.GetSerializedData()).ToArray(),
                    variables = _graphView.SharedVariables.ToArray(),
                    nodeGroups = data.ToArray()
                };
                flowGraphData.PreSerialization();
                return flowGraphData;
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
                    var nodeView = NodeViewFactory.Get().CreateInstance(nodeInstance.GetType(), _graphView) as CeresNodeView;
                    /* Missing node class should be handled before get graph */
                    Assert.IsNotNull(nodeView, $"[Ceres] Can not construct node view for type {nodeInstance.GetType()}");
                    _graphView.AddNodeView(nodeView);
                    newElements.Add(nodeView.NodeElement);
                    try
                    {
                        nodeView.SetNodeInstance(nodeInstance);
                    }
                    catch (Exception e)
                    {
                        CeresGraph.LogError($"Failed to restore properties from {nodeInstance}\n{e}");
                    }
                }
                foreach (var nodeView in newElements.OfType<ExecutableNodeElement>().Select(x=>x.View).ToArray())
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

                if (copyPaste)
                {
                    _graphView.ClearSelection();
                    newElements.ForEach(x=> _graphView.AddToSelection(x));
                }
            }
        }

        private class FlowGraphDebugTracker: FlowGraphTracker
        {
            private readonly FlowGraphView _graphView;

            public bool IsPaused { get; private set; }

            private bool _isDestroyed;

            private ExecutableNodeView _currentView;

            private bool _breakOnNext;

            private readonly FlowGraphDebugState _debugState;
            
            public FlowGraphDebugTracker(FlowGraphView graphView)
            {
                _graphView = graphView;
                _debugState = graphView.DebugState;
            }
            
            public override async UniTask EnterNode(ExecutableNode node)
            {
                _currentView = (ExecutableNodeView)_graphView.FindNodeView(node.Guid);
                if (_currentView != null)
                {
                    _currentView.NodeElement.AddToClassList("status_pending");
                    _currentView.NodeElement.AddToClassList("status_execute");
                    _graphView.ClearSelection();
                    _graphView.AddToSelection(_currentView.NodeElement);
                    _graphView.FrameSelection();
                }
                
                if (_debugState.enableDebug)
                {
                    IsPaused = true;
                }
                Time.timeScale = 0;
                if (!CanSkipFrame() && CanPauseOnCurrentNode())
                {
                    if (CeresSettings.EnableLog)
                    {
                        CeresGraph.Log($"Enter node [{node.GetType().Name}]({node.Guid})");
                    }
                    /* Reset skip frame flag */
                    _breakOnNext = false;
                    await UniTask.WaitUntil(CanSkipFrame);
                    if (CeresSettings.EnableLog)
                    {
                        CeresGraph.Log($"Exit node [{node.GetType().Name}]({node.Guid})");
                    }
                }
                _currentView?.NodeElement.RemoveFromClassList("status_execute");
                Time.timeScale = 1;
            }

            public override UniTask ExitNode(ExecutableNode node)
            {
                var nodeView = _graphView.FindNodeView(node.Guid);
                nodeView?.NodeElement.RemoveFromClassList("status_pending");
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
                if (_currentView != null)
                {
                    hasBreakpoint = _debugState.breakpoints.Contains(_currentView.Guid);
                }
                return _breakOnNext || hasBreakpoint;
            }

            public void NextFrame()
            {
                _breakOnNext = true;
                IsPaused = false;
            }

            public void NextBreakpoint()
            {
                _breakOnNext = false;
                IsPaused = false;
            }

            public override void Dispose()
            {
                _isDestroyed = true;
                base.Dispose();
            }
        }
    }
}
