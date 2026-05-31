using System;
using System.Collections.Generic;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("Contains")]
    [NodeInfo("Returns true when the array contains the specified item.")]
    [CeresMetadata("style = ConstNode", "path = Dependency")]
    public sealed class FlowNode_ArrayContainsT<T> : ExecutableNode
    {
        [InputPort(true), CeresLabel(""), HideInGraphEditor]
        public CeresPort<IReadOnlyList<T>> array;

        [InputPort, CeresLabel("Item")]
        public CeresPort<T> item;

        [OutputPort, CeresLabel("Result")]
        public CeresPort<bool> result;

        protected override UniTask Execute(ExecutionContext executionContext)
        {
            var list = array.Value;
            result.Value = list != null && FlowNode_ArrayIndexOfT<T>.IndexOf(list, item.Value) >= 0;
            return UniTask.CompletedTask;
        }
    }
}
