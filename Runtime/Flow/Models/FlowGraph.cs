using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Chris;
using Chris.Collections;
using Chris.Events;
using Cysharp.Threading.Tasks;
using R3;
using R3.Chris;
using UnityEngine;
using UnityEngine.Pool;
using UObject = UnityEngine.Object;

namespace Ceres.Graph.Flow
{
    /// <summary>
    /// Interface for <see cref="UObject"/> contains <see cref="FlowGraph"/> data
    /// </summary>
    public interface IFlowGraphContainer: ICeresGraphContainer
    {
        /// <summary>
        /// Get a <see cref="FlowGraph"/> instance from this container
        /// </summary>
        /// <returns></returns>
        /// <remarks>Instance is isolated from persistent data</remarks>
        FlowGraph GetFlowGraph();

        /// <summary>
        /// Get persistent <see cref="FlowGraphData"/> from this container
        /// </summary>
        /// <returns></returns>
        FlowGraphData GetFlowGraphData();
    }
    
    /// <summary>
    /// Interface for <see cref="UObject"/> contains <see cref="FlowGraph"/> runtime instance
    /// </summary>
    public interface IFlowGraphRuntime
    {
        /// <summary>
        /// Runtime context <see cref="UObject"/>
        /// </summary>
        /// <value></value>
        UObject Object { get; }
        
        /// <summary>
        /// Get runtime <see cref="FlowGraph"/> instance
        /// </summary>
        /// <returns></returns>
        FlowGraph Graph { get; }
    }
    
    [Serializable]
    public class FlowGraph : CeresGraph
    {
        internal sealed class EventHandler: CallbackEventHandler, IDisposable
        {
            public override IEventCoordinator Coordinator => EventSystem.Instance;

            private FlowGraph _flowGraph;

            private UObject _contextObject;

            public EventHandler(FlowGraph flowGraph, UObject contextObject)
            {
                _flowGraph = flowGraph;
                _contextObject = contextObject;
            }
            
            public override void SendEvent(EventBase e, DispatchMode dispatchMode = DispatchMode.Default)
            {
                e.Target = this;
                Coordinator.EventDispatcher.Dispatch(e, Coordinator, dispatchMode);
            }

            protected override void ExecuteDefaultAction(EventBase evt)
            {
                base.ExecuteDefaultAction(evt);
                ExecuteCustomEvent(evt);
            }

            internal void ExecuteCustomEvent(EventBase eventBase)
            {
                if (!_contextObject) return;
                /* Get event name if it has generated executable event */
                var eventName = CustomExecutionEvent.GetEventName(eventBase.EventTypeId);
                if (string.IsNullOrEmpty(eventName)) return;
                _flowGraph.TryExecuteEvent(_contextObject, eventName, eventBase);
            }

            public void Dispose()
            {
                _flowGraph = null;
                _contextObject = null;
            }
        }
        
        /// <summary>
        /// All <see cref="ExecutableEvent"/> inside this graph
        /// </summary>
        public ExecutableEvent[] Events { get; internal set; }

        private bool _hasCompiled;

        private List<ExecutionContext> _executionList;

        private EventHandler _eventHandler;

        public FlowGraph(FlowGraphSerializedData flowGraphData) : base(flowGraphData)
        {
            /* Pre-cache dependency path from serialization data */
            if (flowGraphData.nodeDependencyPath != null)
            {
                SetDependencyPath(flowGraphData.nodeDependencyPath.Select(x => x.path).ToArray());
            }
        }
        
        /// <inheritdoc />
        public override void Compile()
        {
            if(_hasCompiled) return;
            base.Compile();
            _executionList = ListPool<ExecutionContext>.Get();
            _hasCompiled = true;
        }

        /// <summary>
        /// Get or create a <see cref="CallbackEventHandler"/> bound to an <see cref="UObject"/>
        /// </summary>
        /// <param name="contextObject"></param>
        internal CallbackEventHandler GetOrCreateEventHandler(UObject contextObject)
        {
            _eventHandler ??= new EventHandler(this, contextObject);
            return _eventHandler;
        }

        public override void Dispose()
        {
            if(_executionList != null)
            {
                ListPool<ExecutionContext>.Release(_executionList);
                _executionList = null;
            }
            _eventHandler?.Dispose();
            _eventHandler = null;
            base.Dispose();
        }

        /// <summary>
        /// Execute graph event
        /// </summary>
        /// <param name="contextObject">Graph execution context object</param>
        /// <param name="eventName">The name of event to execute</param>
        /// <param name="evtBase">Source event</param>
        public void ExecuteEvent(UObject contextObject, string eventName, EventBase evtBase = null)
        {
            ExecuteEventAsyncInternal(contextObject, eventName, evtBase).Forget();
        }
        
        /// <summary>
        /// Try to execute graph event
        /// </summary>
        /// <param name="contextObject"></param>
        /// <param name="eventName"></param>
        /// <param name="evtBase"></param>
        /// <returns></returns>
        public bool TryExecuteEvent(UObject contextObject, string eventName, EventBase evtBase = null)
        {
            var evt = FindEvent(eventName);
            if (evt == null)
            {
                return false;
            }
            
            ExecuteEventAsync(contextObject, evt, evtBase).Forget();
            return true;
        }
        
        internal async UniTask ExecuteEventAsyncInternal(UObject contextObject, string eventName, EventBase evtBase = null)
        {
            var evt = FindEvent(eventName);
            if (evt == null)
            {
                CeresLogger.LogWarning($"Can not find ExecutionEvent with name {eventName}");
                return;
            }
            await ExecuteEventAsync(contextObject, evt, evtBase);
        }

        private async UniTask ExecuteEventAsync(UObject contextObject, ExecutableEvent executionEvent, EventBase evtBase = null)
        {
            using var execution = ExecutionContext.GetPooled(contextObject,this, evtBase);
            await execution.Forward(executionEvent);
        }

        internal void AOT()
        {
            InitPorts_Imp(this);
            CollectDependencyPath(this);
        }

        private ExecutableEvent FindEvent(string eventName)
        {
            foreach (var evt in Events)
            {
                if (evt.GetEventName() == eventName) return evt;
            }

            return null;
        }

        protected override void LinkPort(CeresPort port, CeresNode ownerNode, CeresPortData portData)
        {
            if (port is IDelegatePort delegatePort && portData.connections.Length > 0)
            {
                if(ownerNode is ExecutionEventBase eventNode)
                {
                    delegatePort.CreateDelegate(this, eventNode.eventName);
                }
                else
                {
                    CeresLogger.LogWarning($"Only {nameof(ExecutionEventBase)} can have delegate port");
                }
            }
            base.LinkPort(port, ownerNode, portData);
        }

        /// <summary>
        /// Get current execution context
        /// </summary>
        /// <returns></returns>
        public ExecutionContext GetExecutionContext()
        {
            return _executionList.LastOrDefault();
        }
        
        internal void PushContext(ExecutionContext context)
        {
            _executionList?.Add(context);
        }
        
        internal void PopContext(ExecutionContext context)
        {
            _executionList?.Remove(context);
        }
        
        internal bool AddFlowSubGraph(string name, string guid, FlowGraphUsage usage, FlowGraph graph)
        {
           return AddSubGraphSlot<FlowGraph>(new FlowSubGraphSlot
            {
                Name = name,
                Usage = usage,
                Guid = guid,
                Graph = graph
            });
        }
    }

    /// <summary>
    /// Flow graph usage type
    /// </summary>
    public enum FlowGraphUsage
    {
        /// <summary>
        /// Event graph
        /// </summary>
        Event,
        /// <summary>
        /// Function graph, should always be subGraph
        /// </summary>
        Function
    }
    
    /// <summary>
    /// Serialized data for <see cref="FlowGraph"/>
    /// </summary>
    [Serializable]
    public class FlowGraphSerializedData : CeresGraphData
    {
        [Serializable]
        internal struct DependencyCache
        {
            public int[] path;
        }

        [SerializeField] 
        internal DependencyCache[] nodeDependencyPath;
        
        public override void BuildGraph(CeresGraph graph)
        {
            if(graph is not FlowGraph flowGraph) return;
            base.BuildGraph(flowGraph);
            flowGraph!.Events = flowGraph.nodes.OfType<ExecutableEvent>().ToArray();
        }

        protected override CeresNode GetFallbackNode(CeresNodeData fallbackNodeData, int index)
        {
            return new InvalidExecutableNode
            {
                nodeType = fallbackNodeData.nodeType.ToString(),
                serializedData = fallbackNodeData.serializedData
            };
        }
        
        public override void PreSerialization()
        {
            base.PreSerialization();
            /* Pre-cache dependency path before serialization */
            var graph = new FlowGraph(CloneT<FlowGraphSerializedData>());
            graph.AOT();
            nodeDependencyPath = graph.GetDependencyPaths().Select(array=> new DependencyCache
            {
                path = array
            }).ToArray();
        }
    }

    /// <summary>
    /// SubGraph slot for <see cref="FlowGraphData"/>
    /// </summary>
    [Serializable]
    public class FlowSubGraphData : CeresSubGraphData<FlowGraphSerializedData>
    {
        public FlowGraphUsage usage;
    }

    public class FlowSubGraphSlot: CeresSubGraphSlot
    {
        public FlowGraphUsage Usage;
    }

    /// <summary>
    /// Metadata for <see cref="FlowGraph"/>
    /// </summary>
    [Serializable]
    public class FlowGraphData: FlowGraphSerializedData
    {
        public FlowSubGraphData[] subGraphData;

        public override void BuildGraph(CeresGraph graph)
        {
            if(graph is not FlowGraph flowGraph) return;
            base.BuildGraph(flowGraph);
            /* Build subGraphs */
            flowGraph.SubGraphSlots = new CeresSubGraphSlot[subGraphData?.Length ?? 0];
            for (int i = 0; i < flowGraph.SubGraphSlots.Length; i++)
            {
                flowGraph.SubGraphSlots[i] = new FlowSubGraphSlot
                {
                    Name = subGraphData![i].name,
                    Guid = subGraphData![i].guid,
                    Graph = new FlowGraph(subGraphData![i].graphData),
                    Usage = subGraphData![i].usage
                };
            }
        }
        
        public override void PreSerialization()
        {
            base.PreSerialization();
            if (subGraphData == null) return;
            
            /* Pr-serialize subGraphs */
            foreach (var data in subGraphData)
            {
                data.graphData.PreSerialization();
            }
        }

        public void SetSubGraphData(FlowSubGraphSlot slot, FlowGraphSerializedData data)
        {
            subGraphData ??= Array.Empty<FlowSubGraphData>();
            if (subGraphData.All(x => x.guid != slot.Guid))
            {
                ArrayUtils.Add(ref subGraphData, new FlowSubGraphData
                {
                    name = slot.Name,
                    guid = slot.Guid,
                    graphData = data,
                    usage = slot.Usage
                });
            }
            else
            {
                var flowSubGraphData = subGraphData.First(graphData => graphData.guid == slot.Guid);
                flowSubGraphData.graphData = data;
                flowSubGraphData.usage = slot.Usage;
                flowSubGraphData.name = slot.Name;
            }
        }

        public bool RemoveSubGraphData(string guid)
        {
            var data = subGraphData?.FirstOrDefault(x => x.guid == guid);
            if (data == null) return false;
            ArrayUtils.Remove(ref subGraphData, data);
            return true;
        }
        
        public bool RenameSubGraphData(string guid, string newName)
        {
            var data = subGraphData?.FirstOrDefault(graphData => graphData.guid == guid);
            if (data == null) return false;
            data.name = newName;
            return true;
        }

        /// <summary>
        /// Create a <see cref="FlowUberGraph"/> instance from this data
        /// </summary>
        /// <returns></returns>
        public FlowUberGraph CreateFlowGraphInstance()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                return new FlowUberGraph(this);
            }
            /* Keep persistent data safe in editor */
            return new FlowUberGraph(CloneT<FlowGraphData>());
#else
            return new FlowUberGraph(this);
#endif
        }
    }

    public static class FlowGraphRuntimeExtensions
    {
        /// <summary>
        /// Invoke flow graph event
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="eventName"></param>
        public static void ProcessEvent(this IFlowGraphRuntime runtime, [CallerMemberName] string eventName = "")
        {
            using var evt = ExecuteFlowEvent.Create(eventName, ExecuteFlowEvent.DefaultArgs);
            runtime.Graph.TryExecuteEvent(runtime.Object, evt.FunctionName, evt);
        }

        /// <summary>
        /// Invoke flow graph event with parameters
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="arg1"></param>
        /// <param name="eventName"></param>
        /// <typeparam name="T1"></typeparam>
        public static void ProcessEvent<T1>(this IFlowGraphRuntime runtime, T1 arg1, [CallerMemberName] string eventName = "")
        {
            using var evt = ExecuteFlowEvent<T1>.Create(eventName, arg1);
            runtime.Graph.TryExecuteEvent(runtime.Object, evt.FunctionName, evt);
        }

        /// <summary>
        /// Invoke flow graph event with parameters
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="eventName"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        public static void ProcessEvent<T1, T2>(this IFlowGraphRuntime runtime, T1 arg1, T2 arg2, [CallerMemberName] string eventName = "")
        {
            using var evt = ExecuteFlowEvent<T1, T2>.Create(eventName, arg1, arg2);
            runtime.Graph.TryExecuteEvent(runtime.Object, evt.FunctionName, evt);
        }

        /// <summary>
        /// Invoke flow graph event with parameters
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        /// <param name="eventName"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        public static void ProcessEvent<T1, T2, T3>(this IFlowGraphRuntime runtime, T1 arg1, T2 arg2, T3 arg3, [CallerMemberName] string eventName = "")
        {
            using var evt = ExecuteFlowEvent<T1, T2, T3>.Create(eventName, arg1, arg2, arg3);
            runtime.Graph.TryExecuteEvent(runtime.Object, evt.FunctionName, evt);
        }

        /// <summary>
        /// Invoke flow graph event with parameters
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        /// <param name="arg4"></param>
        /// <param name="eventName"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        public static void ProcessEvent<T1, T2, T3, T4>(this IFlowGraphRuntime runtime, T1 arg1, T2 arg2, T3 arg3, T4 arg4, [CallerMemberName] string eventName = "")
        {
            using var evt = ExecuteFlowEvent<T1, T2, T3, T4>.Create(eventName, arg1, arg2, arg3, arg4);
            runtime.Graph.TryExecuteEvent(runtime.Object, evt.FunctionName, evt);
        }

        /// <summary>
        /// Invoke flow graph event with parameters
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        /// <param name="arg4"></param>
        /// <param name="arg5"></param>
        /// <param name="eventName"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        /// <typeparam name="T5"></typeparam>
        public static void ProcessEvent<T1, T2, T3, T4, T5>(this IFlowGraphRuntime runtime, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, [CallerMemberName] string eventName = "")
        {
            using var evt = ExecuteFlowEvent<T1, T2, T3, T4, T5>.Create(eventName, arg1, arg2, arg3, arg4, arg5);
            runtime.Graph.TryExecuteEvent(runtime.Object, evt.FunctionName, evt);
        }

        /// <summary>
        /// Invoke flow graph event with parameters
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        /// <param name="arg4"></param>
        /// <param name="arg5"></param>
        /// <param name="arg6"></param>
        /// <param name="eventName"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        /// <typeparam name="T5"></typeparam>
        /// <typeparam name="T6"></typeparam>
        public static void ProcessEvent<T1, T2, T3, T4, T5, T6>(this IFlowGraphRuntime runtime, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, [CallerMemberName] string eventName = "")
        {
            using var evt = ExecuteFlowEvent<T1, T2, T3, T4, T5, T6>.Create(eventName, arg1, arg2, arg3, arg4, arg5, arg6);
            runtime.Graph.TryExecuteEvent(runtime.Object, evt.FunctionName, evt);
        }
        
        /// <summary>
        /// Invoke flow graph event with any parameters
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="parameters"></param>
        /// <param name="eventName"></param>
        /// <remarks>Note that this method causes boxing and unboxing, which results in allocation.
        /// Consider using an overload method with fewer parameters.</remarks>
        public static void ProcessEventUber(this IFlowGraphRuntime runtime, object[] parameters, [CallerMemberName] string eventName = "")
        {
            using var evt = ExecuteFlowEvent.Create(eventName, parameters);
            runtime.Graph.TryExecuteEvent(runtime.Object, evt.FunctionName, evt);
        }

        /// <summary>
        /// Get runtime <see cref="FlowGraph"/> instance
        /// </summary>
        /// <param name="runtime"></param>
        /// <returns></returns>
        /// <remarks>Convenient for inheritors to implicitly obtain the runtime graph instance
        /// when <see cref="IFlowGraphRuntime.Graph"/> is explicitly implemented.</remarks>
        public static FlowGraph GetRuntimeFlowGraph(this IFlowGraphRuntime runtime)
        {
            return runtime.Graph;
        }
        
        /// <summary>
        /// Get the <see cref="CallbackEventHandler"/> bound to runtime <see cref="FlowGraph"/> instance
        /// </summary>
        /// <param name="runtime"></param>
        public static CallbackEventHandler GetEventHandler(this IFlowGraphRuntime runtime)
        {   
            return runtime.GetRuntimeFlowGraph().GetOrCreateEventHandler(runtime.Object);
        }

        /// <summary>
        /// Send event to runtime <see cref="FlowGraph"/> instance
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="event"></param>
        public static void SendEvent(this IFlowGraphRuntime runtime, EventBase @event)
        {
            runtime.GetEventHandler().SendEvent(@event);
        }

        /// <summary>
        /// Override graph implementation of <see cref="EventBase{TEventType}"/>>
        /// </summary>
        /// <param name="runtime"></param>
        /// <param name="implementation"></param>
        /// <typeparam name="TEventType"></typeparam>
        /// <returns></returns>
        [StackTraceFrame]
        public static IDisposable OverrideEventImplementation<TEventType>(this IFlowGraphRuntime runtime, EventCallback<TEventType> implementation)
            where TEventType : EventBase<TEventType>, new()
        {
            /* Check custom event registered */
            if (!CustomExecutionEvent.HasEvent(EventBase<TEventType>.TypeId())) return Disposable.Empty;
            return runtime.GetEventHandler().AsObservable<TEventType>().SubscribeSafe(implementation);
        }

        /// <summary>
        /// Subscribe an execution of <see cref="EventBase{TEventType}"/>> as callback
        /// </summary>
        /// <param name="eventHandler"></param>
        /// <param name="runtime"></param>
        /// <typeparam name="TEventType"></typeparam>
        /// <returns></returns>
        [StackTraceFrame]
        public static IDisposable SubscribeExecution<TEventType>(this CallbackEventHandler eventHandler, IFlowGraphRuntime runtime)
            where TEventType : EventBase<TEventType>, new()
        {
            /* Check custom event registered */
            if (!CustomExecutionEvent.HasEvent(EventBase<TEventType>.TypeId())) return Disposable.Empty;
            return eventHandler.AsObservable<TEventType>()
                .SubscribeSafe(((FlowGraph.EventHandler)runtime.GetEventHandler()).ExecuteCustomEvent);
        }
    }
}
