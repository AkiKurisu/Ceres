using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Flow Control")]
    [CeresLabel("While Loop")]
    [NodeInfo("Repeats the loop body while the condition is true, up to the maximum iteration limit, then continues through Completed.")]
    public class FlowNode_WhileLoop : ForwardNode
    {
        [InputPort]
        public CeresPort<bool> condition = new(false);

        [InputPort, CeresLabel("Max Iterations")]
        public CeresPort<int> maxIterations = new(-1);

        [OutputPort(false), CeresLabel("Loop Body")]
        public NodePort loopBody;

        [OutputPort(false), CeresLabel("Completed")]
        public NodePort completed;

        protected override async UniTask Execute(ExecutionContext executionContext)
        {
            var iteration = 0;
            while (condition.Value)
            {
                if (maxIterations.Value > 0 && iteration++ >= maxIterations.Value)
                {
                    break;
                }
                await executionContext.Forward(loopBody);
            }

            executionContext.SetNext(completed);
        }
    }
}
