using System;
using System.Collections.Generic;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities")]
    [CeresLabel("Not Equals")]
    [NodeInfo("Returns true when the two input values are not equal.")]
    [CeresMetadata("style = ConstNode", "path = Dependency")]
    public class FlowNode_NotEqualsT<T> : ExecutableNode
    {
        [InputPort]
        public CeresPort<T> value1;

        [InputPort]
        public CeresPort<T> value2;

        [OutputPort, CeresLabel("Result")]
        public CeresPort<bool> resultValue;

        protected override UniTask Execute(ExecutionContext executionContext)
        {
            resultValue.Value = !EqualityComparer<T>.Default.Equals(value1.Value, value2.Value);
            return UniTask.CompletedTask;
        }
    }
}
