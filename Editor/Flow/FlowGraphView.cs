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
            
            var serializableNodes = nodes.OfType<ExecutableNodeElement>().ToArray();
            var nodeInstances = serializableNodes.Select(x => x.View.CompileNode() as CeresNode).ToArray();
            var nodeGroups = graphElements.OfType<ExecutableNodeGroup>().ToList();
            var data = new List<NodeGroup>();
            foreach (var group in nodeGroups)
            {
                group.Commit(data);
            }
            
            var flowGraphData = new FlowGraphData
            {
                nodes = nodeInstances,
                nodeData = nodeInstances.Select(x=>x.GetSerializedData()).ToArray(),
                variables = SharedVariables.ToArray(),
                nodeGroups = data.ToArray()
            };
            flowGraphData.PreSerialization();
            flowGraphContainer.SetGraphData(flowGraphData);
            EditorUtility.SetDirty(container.Object);
            AssetDatabase.SaveAssetIfDirty(container.Object);
            return true;
        }

        public override void DeserializeGraph(ICeresGraphContainer container)
        {
            var graph = (FlowGraph)container.GetGraph();
            // Restore node views
            foreach (var nodeInstance in graph.nodes)
            {
                var nodeView = NodeViewFactory.Get().CreateInstance(nodeInstance.GetType(), this) as CeresNodeView;
                /* Missing node class should be handled before get graph */
                Assert.IsNotNull(nodeView, $"Can not construct node view for type {nodeInstance.GetType()}");
                AddNodeView(nodeView);
                nodeView.SetNodeInstance(nodeInstance);
            }
            foreach (var nodeView in NodeViews.OfType<ExecutableNodeView>())
            {
                // Restore edges
                nodeView.ReconnectEdges();
                // Restore breakpoints
                if (DebugState.breakpoints.Contains(nodeView.Guid))
                {
                    nodeView.AddBreakpoint();
                }
            }
            // Restore variables
            AddSharedVariables(graph.variables, true);
            
            // Restore node groups
            NodeGroupHandler.RestoreGroups(graph.nodeGroups);
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
                    if (CeresSettings.EnableGraphEditorLog)
                    {
                        Debug.Log($"[Ceres] Enter node [{node.GetType().Name}]({node.Guid})");
                    }
                    /* Reset skip frame flag */
                    _breakOnNext = false;
                    await UniTask.WaitUntil(CanSkipFrame);
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
