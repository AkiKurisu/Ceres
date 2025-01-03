using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [NodeGroup("Utilities")]
    [CeresLabel("For Each Loop")]
    public sealed class FlowNode_ForEachLoop: ForwardNode
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<Array> array;
        
        [OutputPort]
        public NodePort loopBody;
        
        [OutputPort]
        public CeresPort<object> arrayElement;
        
        [OutputPort]
        public NodePort completed;

        protected sealed override async UniTask Execute(ExecutionContext executionContext)
        {
            if(array.Value != null)
            {
                foreach (var element in array.Value)
                {
                    arrayElement.Value = element;
                    await executionContext.Forward(loopBody.GetT<ExecutableNode>());
                }
            }
            executionContext.SetNext(completed.GetT<ExecutableNode>());
        }
    }
}