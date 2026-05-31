using System;
using System.Threading;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Timing")]
    [CeresLabel("Delay")]
    [NodeInfo("Waits for the specified number of seconds before continuing execution.")]
    public class FlowNode_Delay : ForwardNode
    {
        [InputPort, CeresLabel("Seconds")]
        public CeresPort<float> seconds = new(1f);

        [OutputPort(false), CeresLabel("")]
        public NodePort exec;

        protected override async UniTask Execute(ExecutionContext executionContext)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, seconds.Value)),
                cancellationToken: GetCancellationToken(executionContext));
            executionContext.SetNext(exec);
        }

        internal static CancellationToken GetCancellationToken(ExecutionContext executionContext)
        {
            return executionContext.Context switch
            {
                GameObject gameObject => gameObject.GetCancellationTokenOnDestroy(),
                MonoBehaviour monoBehaviour => monoBehaviour.GetCancellationTokenOnDestroy(),
                Component component => component.GetCancellationTokenOnDestroy(),
                _ => CancellationToken.None
            };
        }
    }
}
