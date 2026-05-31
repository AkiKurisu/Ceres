using System;
using System.Collections.Generic;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("Is Valid Index")]
    [NodeInfo("Returns true when the index is inside the array bounds.")]
    [CeresMetadata("style = ConstNode", "path = Dependency")]
    public sealed class FlowNode_ArrayIsValidIndexT<T> : ExecutableNode
    {
        [InputPort(true), CeresLabel(""), HideInGraphEditor]
        public CeresPort<IReadOnlyList<T>> array;

        [InputPort, CeresLabel("Index")]
        public CeresPort<int> index;

        [OutputPort, CeresLabel("Result")]
        public CeresPort<bool> result;

        protected override UniTask Execute(ExecutionContext executionContext)
        {
            var list = array.Value;
            result.Value = list != null && index.Value >= 0 && index.Value < list.Count;
            return UniTask.CompletedTask;
        }
    }
}
