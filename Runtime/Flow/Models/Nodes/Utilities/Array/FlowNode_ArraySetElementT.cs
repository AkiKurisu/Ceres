using System;
using Ceres.Annotations;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("Set")]
    [NodeInfo("Sets the list item at the specified index.")]
    public sealed class FlowNode_ArraySetElementT<T> : FlowNode_ListMutationT<T>
    {
        [InputPort, CeresLabel("Index")]
        public CeresPort<int> index;

        [InputPort, CeresLabel("Item")]
        public CeresPort<T> item;

        protected override void LocalExecute(ExecutionContext executionContext)
        {
            var target = list.Value;
            if (target == null || index.Value < 0 || index.Value >= target.Count) return;
            target[index.Value] = item.Value;
        }
    }
}
