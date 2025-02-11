using System;
using Cysharp.Threading.Tasks;
namespace Ceres.Graph.Flow
{
    public class FlowGraphTracker: IDisposable
    {
        public readonly struct TrackerAutoScope: IDisposable
        {
            private readonly FlowGraphTracker _flowGraphTracker;

            private readonly FlowGraphTracker _cachedTracker;
                
            internal TrackerAutoScope(FlowGraphTracker tracker)
            {
                _cachedTracker = GetActiveTracker();
                _flowGraphTracker = tracker;
                SetActiveTracker(tracker);
            }
            
            public void Dispose()
            {
                _flowGraphTracker.Dispose();
                if (!_cachedTracker._isDisposed)
                {
                    SetActiveTracker(_cachedTracker);
                }
            }
        }
        
        private static readonly FlowGraphTracker Empty = new();

        private static FlowGraphTracker _defaultTracker = Empty;

        private static FlowGraphTracker Default
        {
            get
            {
                if (_defaultTracker == null || _defaultTracker._isDisposed)
                {
                    _defaultTracker = null;
                    return Empty;
                }
                return _defaultTracker;
            }
            set => _defaultTracker = value;
        }

        private static FlowGraphTracker _activeTracer;

        private bool _isDisposed;

        protected FlowGraphTracker()
        {
            
        }

        /// <summary>
        /// Creates a helper struct for the scoped using blocks.
        /// </summary>
        /// <returns>IDisposable struct which calls Begin and End automatically.</returns>
        public TrackerAutoScope Auto()
        {
            return new TrackerAutoScope(this);
        }
        
        public static FlowGraphTracker GetActiveTracker()
        {
            return _activeTracer ?? Default;
        }

        internal static void SetDefaultTracker(FlowGraphTracker tracker)
        {
            Default = tracker;
        }

        /// <summary>
        /// Set current active <see cref="FlowGraphTracker"/>
        /// </summary>
        /// <param name="tracker"></param>
        public static void SetActiveTracker(FlowGraphTracker tracker)
        {
            _activeTracer = tracker;
        }

        public virtual UniTask EnterNode(ExecutableNode node)
        {
            return UniTask.CompletedTask;
        }
        
        public virtual UniTask ExitNode(ExecutableNode node)
        {
            return UniTask.CompletedTask;
        }

        public virtual void Dispose()
        {
            if (_activeTracer == this)
            {
                _activeTracer = null;
            }
            _isDisposed = true;
        }
    }

    /// <summary>
    /// A helper tracker to dump nodes dependencies
    /// </summary>
    public class FlowGraphDependencyTracker : FlowGraphTracker
    {
        private readonly FlowGraph _flowGraph;
        
        public FlowGraphDependencyTracker(FlowGraph flowGraph)
        {
            _flowGraph = flowGraph;
        }
        
        public override UniTask EnterNode(ExecutableNode node)
        {
            CeresAPI.Log($"Enter node >>> [{node.GetTypeName()}]({node.Guid})");
            var dependencies = node.NodeData.GetDependencies();
            if (dependencies == null) return UniTask.CompletedTask;
            foreach (var dependency in dependencies)
            {
                var dependencyNode = _flowGraph.FindNode(dependency);
                if (dependencyNode == null)
                {
                    CeresAPI.LogWarning($"Missing dependency node {dependency}");
                    continue;
                }
                CeresAPI.Log($"Find dependency node [{dependencyNode.GetTypeName()}]({dependencyNode.Guid})");
            }
            return UniTask.CompletedTask;
        }
        
        public override UniTask ExitNode(ExecutableNode node)
        {
            CeresAPI.Log($"Exit node <<< [{node.GetTypeName()}]({node.Guid})");
            return UniTask.CompletedTask;
        }
    }
}
