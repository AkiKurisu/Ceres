using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Flow Control")]
    [CeresLabel("For Loop")]
    [NodeInfo("Executes the loop body for each index from First Index to Last Index, then continues through Completed.")]
    public class FlowNode_ForLoop : ForwardNode
    {
        [InputPort, CeresLabel("First Index")]
        public CeresPort<int> firstIndex = new(0);

        [InputPort, CeresLabel("Last Index")]
        public CeresPort<int> lastIndex = new(0);

        [InputPort, CeresLabel("Step")]
        public CeresPort<int> step = new(1);

        [OutputPort(false), CeresLabel("Loop Body")]
        public NodePort loopBody;

        [OutputPort, CeresLabel("Index")]
        public CeresPort<int> index;

        [OutputPort(false), CeresLabel("Completed")]
        public NodePort completed;

        protected override async UniTask Execute(ExecutionContext executionContext)
        {
            var stepValue = step.Value == 0 ? 1 : step.Value;
            if (stepValue > 0)
            {
                for (var i = firstIndex.Value; i <= lastIndex.Value; i += stepValue)
                {
                    index.Value = i;
                    await executionContext.Forward(loopBody);
                }
            }
            else
            {
                for (var i = firstIndex.Value; i >= lastIndex.Value; i += stepValue)
                {
                    index.Value = i;
                    await executionContext.Forward(loopBody);
                }
            }

            executionContext.SetNext(completed);
        }
    }
}
