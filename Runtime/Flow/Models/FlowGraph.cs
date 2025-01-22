using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Chris.Events;
using Cysharp.Threading.Tasks;
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
        FlowGraph GetFlowGraph();
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
    
    public class FlowGraph : CeresGraph
    {
        public ExecutableEvent[] Events { get; internal set; }

        private bool _hasCompiled;

        private List<ExecutionContext> _executionList;

        public FlowGraph(FlowGraphData flowGraphData) : base(flowGraphData)
        {
            /* Pre-cache dependency path from serialization data */
            if(flowGraphData.nodeDependencyPath != null)
            {
                SetDependencyPath(flowGraphData.nodeDependencyPath.Select(x => x.path).ToArray());
            }
        }
        
        public override void Compile()
        {
            if(_hasCompiled) return;
            base.Compile();
            _executionList = ListPool<ExecutionContext>.Get();
            _hasCompiled = true;
        }

        public override void Dispose()
        {
            base.Dispose();
            if(_executionList != null)
            {
                ListPool<ExecutionContext>.Release(_executionList);
            }
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
                LogWarning($"Can not find ExecutionEvent with name {eventName}");
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
                if (evt.eventName == eventName) return evt;
            }

            return null;
        }

        protected override void LinkPort(CeresPort port, CeresNode ownerNode, CeresPortData portData)
        {
            if (port is IDelegatePort delegatePort && portData.connections.Length > 0)
            {
                if(ownerNode is ExecutableEvent eventNode)
                {
                    delegatePort.CreateDelegate(this, eventNode.eventName);
                }
                else
                {
                    LogWarning($"Only {nameof(ExecutableEvent)} can have delegate port");
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
            _executionList.Add(context);
        }
        
        internal void PopContext(ExecutionContext context)
        {
            _executionList.Remove(context);
        }
    }

    /// <summary>
    /// Metadata for <see cref="FlowGraph"/>
    /// </summary>
    [Serializable]
    public class FlowGraphData: CeresGraphData
    {
        [Serializable]
        public struct DependencyCache
        {
            public int[] path;
        }
        
        public DependencyCache[] nodeDependencyPath;
        
        public override void BuildGraph(CeresGraph graph)
        {
            if(graph is not FlowGraph flowGraph) return;
            base.BuildGraph(flowGraph);
            flowGraph!.Events = nodes.OfType<ExecutableEvent>().ToArray();
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
            var graph = new FlowGraph(CloneT<FlowGraphData>());
            graph.AOT();
            nodeDependencyPath = graph.GetDependencyPaths().Select(x=> new DependencyCache
            {
                path = x
            }).ToArray();
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
        public static void ProcessEventUber(this IFlowGraphRuntime runtime, object[] parameters, [CallerMemberName] string eventName = "")
        {
            using var evt = ExecuteFlowEvent.Create(eventName, parameters);
            runtime.Graph.TryExecuteEvent(runtime.Object, evt.FunctionName, evt);
        }
    }
}
