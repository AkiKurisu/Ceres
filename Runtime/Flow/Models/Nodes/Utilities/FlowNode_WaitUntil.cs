using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Timing")]
    [CeresLabel("Wait Until")]
    [NodeInfo("Waits until the condition becomes true, then continues execution.")]
    public class FlowNode_WaitUntil : ForwardNode
    {
        [InputPort]
        public CeresPort<bool> condition = new(false);

        [OutputPort(false), CeresLabel("")]
        public NodePort exec;

        protected override async UniTask Execute(ExecutionContext executionContext)
        {
            var cancellationToken = FlowNode_Delay.GetCancellationToken(executionContext);
            await UniTask.WaitUntil(() => condition.Value, cancellationToken: cancellationToken);
            executionContext.SetNext(exec);
        }
    }
}
