using System;
using Ceres.Annotations;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("Clear")]
    [NodeInfo("Removes all items from the list.")]
    public sealed class FlowNode_ArrayClearT<T> : FlowNode_ListMutationT<T>
    {
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            list.Value?.Clear();
        }
    }
}
