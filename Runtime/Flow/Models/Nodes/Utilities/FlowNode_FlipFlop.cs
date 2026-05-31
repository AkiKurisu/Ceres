using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Flow Control")]
    [CeresLabel("Flip Flop")]
    [NodeInfo("Alternates execution between output A and output B each time it is triggered.")]
    public class FlowNode_FlipFlop : ForwardNode
    {
        [OutputPort(false), CeresLabel("A")]
        public NodePort a;

        [OutputPort(false), CeresLabel("B")]
        public NodePort b;

        [OutputPort, CeresLabel("Is A")]
        public CeresPort<bool> isA;

        [NonSerialized]
        private bool _nextA = true;

        protected override UniTask Execute(ExecutionContext executionContext)
        {
            isA.Value = _nextA;
            executionContext.SetNext(_nextA ? a : b);
            _nextA = !_nextA;
            return UniTask.CompletedTask;
        }
    }
}
