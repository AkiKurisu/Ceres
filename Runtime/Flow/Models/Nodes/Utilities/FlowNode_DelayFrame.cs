using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Timing")]
    [CeresLabel("Delay Frames")]
    [NodeInfo("Waits for the specified number of frames before continuing execution.")]
    public class FlowNode_DelayFrame : ForwardNode
    {
        [InputPort, CeresLabel("Frames")]
        public CeresPort<int> frames = new(1);

        [OutputPort(false), CeresLabel("")]
        public NodePort exec;

        protected override async UniTask Execute(ExecutionContext executionContext)
        {
            await UniTask.DelayFrame(Mathf.Max(0, frames.Value), cancellationToken: FlowNode_Delay.GetCancellationToken(executionContext));
            executionContext.SetNext(exec);
        }
    }
}
