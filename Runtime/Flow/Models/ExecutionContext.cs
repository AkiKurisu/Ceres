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
        // ReSharper disable once InconsistentNaming
        private static readonly ObjectPool<ExecutionContext> _pool = new(() => new ExecutionContext());
        
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

#if UNITY_EDITOR
        private FlowGraphTracker _tracker;
#endif

        private EventBase _event;
        
        private ExecutionContext()
        {

        }
        
        public static ExecutionContext GetPooled(UObject context, FlowGraph graph, EventBase evt = null)
        {
            Assert.IsTrue((bool)context);
            Assert.IsNotNull(graph);
            var executionContext = _pool.Get();
            executionContext.Context = context;
            executionContext.Graph = graph;
            graph.PushContext(executionContext);
            executionContext._isPooled = true;
            executionContext._forwardPath = ListPool<string>.Get();
#if UNITY_EDITOR
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
        public bool GetNext(out ExecutableNode nextNode)
        {
            nextNode = _nextNode;
            _nextNode = null;
            return nextNode != null;
        }

        /// <summary>
        /// Execute next node in forward path
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public UniTask Forward(ExecutableNode node)
        {
            return Forward(node, GetCancellationToken());
        }

        /// <summary>
        /// Execute next node in forward path
        /// </summary>
        /// <param name="node"></param>
        /// <param name="cancellationToken"></param>
        public async UniTask Forward(ExecutableNode node, CancellationToken cancellationToken)
        {
            /* Execute dependency path */
            await ExecuteDependencyPath(node.Guid, cancellationToken);
            /* Execute forward path */
            _forwardPath?.Add(node.Guid);
#if UNITY_EDITOR
            await _tracker.EnterNode(node);
#endif
            await node.ExecuteNode(this).AttachExternalCancellation(cancellationToken);
#if UNITY_EDITOR
            await _tracker.ExitNode(node);
#endif
            while (GetNext(out var nextNode))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Forward(nextNode, cancellationToken);
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
                await Forward(node, cancellationToken);
            }
        }

        /// <summary>
        /// Get whether node with guid has been executed
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public bool HasNodeExecuted(string guid)
        {
            return _forwardPath?.Contains(guid) ?? false;
        }

        /// <summary>
        /// Get <see cref="CancellationToken"/> for async support
        /// </summary>
        /// <returns></returns>
        public CancellationToken GetCancellationToken()
        {
            if (Context is GameObject gameObject) return gameObject.GetCancellationTokenOnDestroy();
            if (Context is MonoBehaviour monoBehaviour) return monoBehaviour.GetCancellationTokenOnDestroy();
            if (Context is Component component) return component.GetCancellationTokenOnDestroy();
            return default;
        }

        public void Dispose()
        {
            _nextNode = null;
            Graph.PopContext(this);
            Graph = null;
            
            /* Release list */
            if(_forwardPath != null)
            {
                ListPool<string>.Release(_forwardPath);
                _forwardPath = null;
            }

            if (_event != null)
            {
                _event.Dispose();;
                _event = null;
            }
            
            /* Push to pool */
            if (!_isPooled) return;
            
            _isPooled = false;
            _pool.Release(this);
        }

        public EventBase GetEvent()
        {
            return _event;
        }
        
        public T GetEventT<T>() where T: EventBase<T>, new()
        {
            return (T)GetEvent();
        }
    }
}