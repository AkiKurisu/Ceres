using System;
using System.Threading;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Timing")]
    [CeresLabel("Retriggerable Delay")]
    [NodeInfo("Waits for the specified duration; triggering it again restarts the countdown.")]
    public class FlowNode_RetriggerableDelay : FlowNode_Delay
    {
        [NonSerialized]
        private CancellationTokenSource _delayCancellation;

        protected override async UniTask Execute(ExecutionContext executionContext)
        {
            _delayCancellation?.Cancel();
            _delayCancellation?.Dispose();
            _delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(GetCancellationToken(executionContext));

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, seconds.Value)),
                    cancellationToken: _delayCancellation.Token);
                executionContext.SetNext(exec);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public override void Dispose()
        {
            _delayCancellation?.Cancel();
            _delayCancellation?.Dispose();
            _delayCancellation = null;
            base.Dispose();
        }
    }
}
