using System;
using Cysharp.Threading.Tasks;
namespace Ceres.Graph.Flow
{
    public class FlowGraphTracker: IDisposable
    {
        public readonly struct TrackerAutoScope: IDisposable
        {
            private readonly FlowGraphTracker _flowGraphTracker;

            internal TrackerAutoScope(FlowGraphTracker tracker)
            {
                _flowGraphTracker = tracker;
                SetActiveTracker(tracker);
            }
            
            public void Dispose()
            {
                _flowGraphTracker.Dispose();
            }
        }
        
        private static readonly FlowGraphTracker DefaultTracker = new();

        private static FlowGraphTracker _activeTracer;

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
            return _activeTracer ?? DefaultTracker;
        }

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
            CeresGraph.Log($">>> Enter node [{node.GetTypeName()}]({node.Guid})");
            var dependencies = node.NodeData.GetDependencies();
            if (dependencies == null) return UniTask.CompletedTask;
            foreach (var dependency in dependencies)
            {
                var dependencyNode = _flowGraph.FindNode(dependency);
                if (dependencyNode == null)
                {
                    CeresGraph.LogWarning($"Missing dependency node {dependency}");
                    continue;
                }
                CeresGraph.Log($"Find dependency node [{dependencyNode.GetTypeName()}]({dependencyNode.Guid})");
            }
            return UniTask.CompletedTask;
        }
        
        public override UniTask ExitNode(ExecutableNode node)
        {
            CeresGraph.Log($">>> Exit node [{node.GetTypeName()}]({node.Guid})");
            return UniTask.CompletedTask;
        }
    }
}
