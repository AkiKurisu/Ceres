using System;
using System.Collections.Generic;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("Length")]
    [NodeInfo("Returns the number of items in the array.")]
    [CeresMetadata("style = ConstNode", "path = Dependency")]
    public sealed class FlowNode_ArrayLengthT<T> : ExecutableNode
    {
        [InputPort(true), CeresLabel(""), HideInGraphEditor]
        public CeresPort<IReadOnlyList<T>> array;

        [OutputPort, CeresLabel("Length")]
        public CeresPort<int> length;

        protected override UniTask Execute(ExecutionContext executionContext)
        {
            length.Value = array.Value?.Count ?? 0;
            return UniTask.CompletedTask;
        }
    }
}
