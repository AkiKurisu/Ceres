using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities")]
    [CeresLabel("Is Null")]
    [NodeInfo("Returns true when the input value is null.")]
    [CeresMetadata("style = ConstNode", "path = Dependency")]
    public class FlowNode_IsNullT<T> : ExecutableNode
    {
        [InputPort, CeresLabel("Value")]
        public CeresPort<T> value;

        [OutputPort, CeresLabel("Result")]
        public CeresPort<bool> resultValue;

        protected override UniTask Execute(ExecutionContext executionContext)
        {
            resultValue.Value = value.Value is null;
            return UniTask.CompletedTask;
        }
    }
}
