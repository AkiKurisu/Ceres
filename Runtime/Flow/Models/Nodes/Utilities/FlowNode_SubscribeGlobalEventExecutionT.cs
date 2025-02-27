using System;
using Ceres.Annotations;
using Chris.Events;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Rx")]
    [CeresLabel("Global Subscribe {0}")]
    public class FlowNode_SubscribeGlobalEventExecutionT<TEventType>: FlowNode
        where TEventType : EventBase<TEventType>, new()
    {
        [OutputPort]
        public CeresPort<IDisposable> subscription;

        protected override void LocalExecute(ExecutionContext executionContext)
        {
            if (executionContext.Context is not IFlowGraphRuntime runtime)
            {
                return;
            }
            subscription.Value = EventSystem.EventHandler.SubscribeExecution<TEventType>(runtime);
        }
    }
}