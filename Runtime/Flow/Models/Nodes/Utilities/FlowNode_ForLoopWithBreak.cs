using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Flow Control")]
    [CeresLabel("For Loop With Break")]
    [NodeInfo("Executes a for loop that can be stopped early through the Break input.")]
    public class FlowNode_ForLoopWithBreak : FlowNode_ForLoop
    {
        [InputPort, CeresLabel("Break")]
        public NodePort breakInput;

        [NonSerialized]
        private bool _breakRequested;

        protected override async UniTask Execute(ExecutionContext executionContext)
        {
            if (executionContext.CurrentInputPortId == nameof(breakInput))
            {
                _breakRequested = true;
                return;
            }

            _breakRequested = false;
            var stepValue = step.Value == 0 ? 1 : step.Value;
            if (stepValue > 0)
            {
                for (var i = firstIndex.Value; i <= lastIndex.Value; i += stepValue)
                {
                    index.Value = i;
                    await executionContext.Forward(loopBody);
                    if (_breakRequested) break;
                }
            }
            else
            {
                for (var i = firstIndex.Value; i >= lastIndex.Value; i += stepValue)
                {
                    index.Value = i;
                    await executionContext.Forward(loopBody);
                    if (_breakRequested) break;
                }
            }

            executionContext.SetNext(completed);
        }
    }
}
