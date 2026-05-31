using System;
using System.Collections.Generic;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("First")]
    [NodeInfo("Outputs the first item in the array, or the default value when the array is empty.")]
    [CeresMetadata("style = ConstNode", "path = Dependency")]
    public sealed class FlowNode_ArrayFirstT<T> : ExecutableNode
    {
        [InputPort(true), CeresLabel(""), HideInGraphEditor]
        public CeresPort<IReadOnlyList<T>> array;

        [OutputPort, CeresLabel("Item")]
        public CeresPort<T> item;

        protected override UniTask Execute(ExecutionContext executionContext)
        {
            var list = array.Value;
            item.Value = list != null && list.Count > 0 ? list[0] : default;
            return UniTask.CompletedTask;
        }
    }
}
