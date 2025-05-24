using System;
using System.Collections.Generic;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Iterate over the output of given array.
    /// </summary>
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("For Each Loop")]
    public sealed class FlowNode_ForEachLoop: ForwardNode
    {
        [InputPort(true), HideInGraphEditor]
        public CeresPort<Array> array;
        
        [OutputPort(false)]
        public NodePort loopBody;
        
        [OutputPort]
        public CeresPort<object> arrayElement;
        
        [OutputPort(false)]
        public NodePort completed;

        protected override async UniTask Execute(ExecutionContext executionContext)
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
    
    public abstract class FlowNode_ForEachLoopGeneric : ForwardNode
    {
        
    }
    
    /// <summary>
    /// Iterate over the output of given array.
    /// </summary>
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("For Each Loop")]
    public sealed class FlowNode_ForEachLoopT<T>: FlowNode_ForEachLoopGeneric
    {
        [InputPort(true), HideInGraphEditor]
        public CeresPort<IReadOnlyList<T>> array;
        
        [OutputPort(false)]
        public NodePort loopBody;
        
        [OutputPort]
        public CeresPort<T> arrayElement;
        
        [OutputPort(false)]
        public NodePort completed;

        protected override async UniTask Execute(ExecutionContext executionContext)
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