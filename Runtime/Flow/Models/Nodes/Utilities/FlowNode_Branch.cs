using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities")]
    [CeresLabel("Branch")]
    public class FlowNode_Branch: ForwardNode
    {
        [InputPort]
        public CeresPort<bool> condition;
        
        [OutputPort, CeresLabel("True")]
        public NodePort trueOutput;
        
        [OutputPort, CeresLabel("False")]
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