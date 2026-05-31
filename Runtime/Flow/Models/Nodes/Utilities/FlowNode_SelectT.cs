using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities")]
    [CeresLabel("Select")]
    [NodeInfo("Returns one of two values based on a boolean condition.")]
    [CeresMetadata("style = ConstNode", "path = Dependency")]
    public class FlowNode_SelectT<T> : ExecutableNode
    {
        [InputPort, CeresLabel("Condition")]
        public CeresPort<bool> condition;

        [InputPort, CeresLabel("True")]
        public CeresPort<T> trueValue;

        [InputPort, CeresLabel("False")]
        public CeresPort<T> falseValue;

        [OutputPort, CeresLabel("Result")]
        public CeresPort<T> resultValue;

        protected override UniTask Execute(ExecutionContext executionContext)
        {
            resultValue.Value = condition.Value ? trueValue.Value : falseValue.Value;
            return UniTask.CompletedTask;
        }
    }
}
