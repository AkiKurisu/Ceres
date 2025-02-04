using System;
using Ceres.Annotations;
using R3;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Rx")]
    [CeresLabel("Subscribe")]
    public class FlowNode_ObservableSubscribeT<T>: FlowNode
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<Observable<T>> subject;
                
        [InputPort]
        public DelegatePort<EventDelegate<T>> onNext;
        
        [OutputPort]
        public CeresPort<IDisposable> subscription;

        protected override void LocalExecute(ExecutionContext executionContext)
        {
            subscription.Value = subject.Value.Subscribe(onNext.Value);
        }
    }
}