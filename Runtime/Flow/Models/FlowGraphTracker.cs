using System;
using Cysharp.Threading.Tasks;
namespace Ceres.Graph.Flow
{
    public class FlowGraphTracker: IDisposable
    {
        private static readonly FlowGraphTracker DefaultTracker = new();

        private static FlowGraphTracker _activeTracer;

        protected FlowGraphTracker()
        {
            
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
}
