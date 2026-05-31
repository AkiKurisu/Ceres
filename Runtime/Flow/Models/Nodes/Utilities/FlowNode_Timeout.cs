using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Timing")]
    [CeresLabel("Timeout")]
    [NodeInfo("Waits for the specified duration before continuing through Completed.")]
    public class FlowNode_Timeout : ForwardNode
    {
        [InputPort, CeresLabel("Seconds")]
        public CeresPort<float> seconds = new(1f);

        [OutputPort(false), CeresLabel("Completed")]
        public NodePort completed;

        protected override async UniTask Execute(ExecutionContext executionContext)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, seconds.Value)),
                cancellationToken: FlowNode_Delay.GetCancellationToken(executionContext));
            executionContext.SetNext(completed);
        }
    }
}
