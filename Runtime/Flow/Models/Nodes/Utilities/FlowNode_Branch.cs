using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Branch will route the execution flow depending on the value of the condition input.
    /// </summary>
    [Serializable]
    [CeresGroup("Utilities")]
    [CeresLabel("Branch")]
    public class FlowNode_Branch: ForwardNode
    {
        [InputPort]
        public CeresPort<bool> condition;
        
        [OutputPort(false), CeresLabel("True")]
        public NodePort trueOutput;
        
        [OutputPort(false), CeresLabel("False")]
        public NodePort falseOutput;

        protected sealed override UniTask Execute(ExecutionContext executionContext)
        {
            executionContext.SetNext(PrepareOutputsExecution());
            return UniTask.CompletedTask;
        }

        private ExecutableNode PrepareOutputsExecution()
        {
            return (condition.Value ? trueOutput : falseOutput).GetT<ExecutableNode>();
        }
    }
}