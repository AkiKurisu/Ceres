using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Timing")]
    [CeresLabel("Next Frame")]
    [NodeInfo("Continues execution on the next frame.")]
    public class FlowNode_NextFrame : ForwardNode
    {
        [OutputPort(false), CeresLabel("")]
        public NodePort exec;

        protected override async UniTask Execute(ExecutionContext executionContext)
        {
            await UniTask.NextFrame(FlowNode_Delay.GetCancellationToken(executionContext));
            executionContext.SetNext(exec);
        }
    }
}
