using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Flow Control")]
    [CeresLabel("Do N")]
    [NodeInfo("Allows execution to pass through N times, then blocks until Reset is triggered.")]
    public class FlowNode_DoN : ForwardNode
    {
        [InputPort, CeresLabel("Reset")]
        public NodePort reset;

        [InputPort, CeresLabel("N")]
        public CeresPort<int> n = new(1);

        [OutputPort(false), CeresLabel("Exit")]
        public NodePort exec;

        [OutputPort, CeresLabel("Counter")]
        public CeresPort<int> counter;

        [NonSerialized]
        private int _count;

        protected override UniTask Execute(ExecutionContext executionContext)
        {
            if (executionContext.CurrentInputPortId == nameof(reset))
            {
                _count = 0;
                counter.Value = _count;
                return UniTask.CompletedTask;
            }

            if (_count < Math.Max(0, n.Value))
            {
                _count++;
                counter.Value = _count;
                executionContext.SetNext(exec);
            }

            return UniTask.CompletedTask;
        }
    }
}
