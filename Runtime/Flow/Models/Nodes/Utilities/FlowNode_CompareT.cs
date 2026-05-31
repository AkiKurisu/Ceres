using System;
using System.Collections.Generic;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities")]
    [CeresLabel("Compare")]
    [NodeInfo("Compares two values that implement IComparable and returns -1, 0, or 1.")]
    [CeresMetadata("style = ConstNode", "path = Dependency")]
    public class FlowNode_CompareT<T> : ExecutableNode
    {
        [InputPort]
        public CeresPort<T> value1;

        [InputPort]
        public CeresPort<T> value2;

        [OutputPort, CeresLabel("Result")]
        public CeresPort<int> resultValue;

        protected override UniTask Execute(ExecutionContext executionContext)
        {
            resultValue.Value = Comparer<T>.Default.Compare(value1.Value, value2.Value);
            return UniTask.CompletedTask;
        }
    }
}
