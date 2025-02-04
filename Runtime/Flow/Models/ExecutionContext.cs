using System;
using System.Collections.Generic;
using System.Threading;
using Chris.Events;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Pool;
using UObject = UnityEngine.Object;
namespace Ceres.Graph.Flow
{
    /// <summary>
    /// Flow graph execution context
    /// </summary>
    public class ExecutionContext: IDisposable
    {
        private static readonly ObjectPool<ExecutionContext> Pool = new(() => new ExecutionContext());
        
        /// <summary>
        /// Execution owner graph
        /// </summary>
        public FlowGraph Graph { get; private set; }
        
        /// <summary>
        /// Execution source context object
        /// </summary>
        public UObject Context { get; private set; }

        private List<string> _forwardPath;

        private bool _isPooled;

        private ExecutableNode _nextNode;

#if !CERES_DISABLE_TRACKER
        private FlowGraphTracker _tracker;
#endif

        private EventBase _event;

        private CancellationToken? _cancellationToken;
        
        protected ExecutionContext()
        {

        }
        
        public static ExecutionContext GetPooled(UObject context, FlowGraph graph, EventBase evt = null)
        {
            Assert.IsTrue((bool)context);
            Assert.IsNotNull(graph);
            var executionContext = Pool.Get();
            executionContext._cancellationToken = null;
            executionContext.Context = context;
            executionContext.Graph = graph;
            graph.PushContext(executionContext);
            executionContext._isPooled = true;
            executionContext._forwardPath = ListPool<string>.Get();
#if !CERES_DISABLE_TRACKER
            executionContext._tracker = FlowGraphTracker.GetActiveTracker();
#endif
            executionContext._event = evt;
            evt?.Acquire();
            return executionContext;
        }
        
        /// <summary>
        /// Set execution flow next node
        /// </summary>
        /// <param name="node"></param>
        public void SetNext(ExecutableNode node)
        {
            _nextNode = node;
        }

        /// <summary>
        /// Try to get next executable node
        /// </summary>
        /// <param name="nextNode"></param>
        /// <returns></returns>
        private bool Next(out ExecutableNode nextNode)
        {
            nextNode = _nextNode;
            _nextNode = null;
            return nextNode != null;
        }

        private bool IsExecutedInOnDestroy()
        {
           const string onDestroyEventName = "OnDestroy";
           return Context && GetEvent() is ExecuteFlowEvent { FunctionName: onDestroyEventName };
        }
        
        /// <summary>
        /// Execute node in forward path
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public UniTask Forward(ExecutableNode node)
        {
            _cancellationToken ??= GetCancellationToken();
            /* Special case, we expect flow can still be executed in OnDestroy. */
            /* So we start execute forward path without cancellation. */
            if (_cancellationToken.Value.IsCancellationRequested && IsExecutedInOnDestroy())
            {
                _cancellationToken = new CancellationToken();
            }
            return Forward_Internal(node, _cancellationToken.Value);
        }
        
        private async UniTask Forward_Internal(ExecutableNode node, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            /* Execute dependency path */
            await ExecuteDependencyPath(node.Guid, cancellationToken);
            /* Execute forward path */
            _forwardPath?.Add(node.Guid);
#if !CERES_DISABLE_TRACKER
            await _tracker.EnterNode(node);
#endif
            await node.ExecuteNode(this);
#if !CERES_DISABLE_TRACKER
            await _tracker.ExitNode(node);
#endif
            while (Next(out var nextNode))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Forward_Internal(nextNode, cancellationToken);
            }
        }
        
        private async UniTask ExecuteDependencyPath(string guid, CancellationToken cancellationToken)
        {
            var path = Graph.GetNodeDependencyPath(guid);
            foreach (var id in path)
            {
                /* Find dependency root in current context */
                if(HasNodeExecuted(Graph.nodes[id].Guid)) continue;
                var node = (ExecutableNode)Graph.nodes[id];
                await Forward_Internal(node, cancellationToken);
            }
        }

        /// <summary>
        /// Get whether node with guid has been executed
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        private bool HasNodeExecuted(string guid)
        {
            return _forwardPath?.Contains(guid) ?? false;
        }

        /// <summary>
        /// Get <see cref="CancellationToken"/> for async support
        /// </summary>
        /// <returns></returns>
        private CancellationToken GetCancellationToken()
        {
            return Context switch
            {
                GameObject gameObject => gameObject.GetCancellationTokenOnDestroy(),
                MonoBehaviour monoBehaviour => monoBehaviour.GetCancellationTokenOnDestroy(),
                Component component => component.GetCancellationTokenOnDestroy(),
                _ => default
            };
        }

        public void Dispose()
        {
            _nextNode = null;
            Graph.PopContext(this);
            Graph = null;
            _cancellationToken = null;
            
            /* Release list */
            if(_forwardPath != null)
            {
                ListPool<string>.Release(_forwardPath);
                _forwardPath = null;
            }

            if (_event != null)
            {
                _event.Dispose();
                _event = null;
            }
            
            /* Push to pool */
            if (!_isPooled) return;
            
            _isPooled = false;
            Pool.Release(this);
        }

        /// <summary>
        /// Get source <see cref="EventBase"/> this execution fired from
        /// </summary>
        /// <returns></returns>
        public EventBase GetEvent()
        {
            return _event;
        }
        
        /// <summary>
        /// Get source <see cref="EventBase"/> this execution fired from with specific type check
        /// </summary>
        /// <returns></returns>
        public T GetEventT<T>() where T: EventBase<T>, new()
        {
            return (T)GetEvent();
        }
    }
}