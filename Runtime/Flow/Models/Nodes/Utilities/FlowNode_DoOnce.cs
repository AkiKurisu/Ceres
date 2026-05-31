using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Flow Control")]
    [CeresLabel("Do Once")]
    [NodeInfo("Allows execution to pass through once, then blocks until Reset is triggered.")]
    public class FlowNode_DoOnce : ForwardNode
    {
        [InputPort, CeresLabel("Reset")]
        public NodePort reset;

        [InputPort, CeresLabel("Start Closed")]
        public CeresPort<bool> startClosed = new(false);

        [OutputPort(false), CeresLabel("")]
        public NodePort exec;

        [NonSerialized]
        private bool _initialized;

        [NonSerialized]
        private bool _hasFired;

        protected override UniTask Execute(ExecutionContext executionContext)
        {
            if (executionContext.CurrentInputPortId == nameof(reset))
            {
                _initialized = true;
                _hasFired = false;
                return UniTask.CompletedTask;
            }

            if (!_initialized)
            {
                _initialized = true;
                _hasFired = startClosed.Value;
            }

            if (!_hasFired)
            {
                _hasFired = true;
                executionContext.SetNext(exec);
            }

            return UniTask.CompletedTask;
        }
    }
}
