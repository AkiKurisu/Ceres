using System;
using Ceres.Annotations;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("Remove At")]
    [NodeInfo("Removes the item at the specified index from the list.")]
    public sealed class FlowNode_ArrayRemoveAtT<T> : FlowNode_ListMutationT<T>
    {
        [InputPort, CeresLabel("Index")]
        public CeresPort<int> index;

        protected override void LocalExecute(ExecutionContext executionContext)
        {
            var target = list.Value;
            if (target == null || index.Value < 0 || index.Value >= target.Count) return;
            target.RemoveAt(index.Value);
        }
    }
}
