using System;
using Ceres.Annotations;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("Reverse")]
    [NodeInfo("Reverses the order of items in the list.")]
    public sealed class FlowNode_ArrayReverseT<T> : FlowNode_ListMutationT<T>
    {
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            var target = list.Value;
            if (target == null) return;
            for (var left = 0; left < target.Count / 2; left++)
            {
                var right = target.Count - 1 - left;
                (target[left], target[right]) = (target[right], target[left]);
            }
        }
    }
}
