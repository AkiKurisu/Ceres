using System;
using Ceres.Annotations;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("Remove Item")]
    [NodeInfo("Removes the first matching item from the list.")]
    public sealed class FlowNode_ArrayRemoveItemT<T> : FlowNode_ListMutationT<T>
    {
        [InputPort, CeresLabel("Item")]
        public CeresPort<T> item;

        protected override void LocalExecute(ExecutionContext executionContext)
        {
            list.Value?.Remove(item.Value);
        }
    }
}
