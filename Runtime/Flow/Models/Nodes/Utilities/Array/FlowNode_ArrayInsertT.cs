using System;
using Ceres.Annotations;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("Insert")]
    [NodeInfo("Inserts an item into the list at the specified index.")]
    public sealed class FlowNode_ArrayInsertT<T> : FlowNode_ListMutationT<T>
    {
        [InputPort, CeresLabel("Index")]
        public CeresPort<int> index;

        [InputPort, CeresLabel("Item")]
        public CeresPort<T> item;

        protected override void LocalExecute(ExecutionContext executionContext)
        {
            var target = list.Value;
            if (target == null) return;
            target.Insert(Math.Max(0, Math.Min(index.Value, target.Count)), item.Value);
        }
    }
}
