using System;
using Ceres.Annotations;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("Append")]
    [NodeInfo("Adds an item to the end of the list.")]
    public sealed class FlowNode_ArrayAppendT<T> : FlowNode_ListMutationT<T>
    {
        [InputPort, CeresLabel("Item")]
        public CeresPort<T> item;

        protected override void LocalExecute(ExecutionContext executionContext)
        {
            list.Value?.Add(item.Value);
        }
    }
}
