using System;
using Ceres.Annotations;
using UnityEngine;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("Shuffle")]
    [NodeInfo("Randomly shuffles the items in the list.")]
    public sealed class FlowNode_ArrayShuffleT<T> : FlowNode_ListMutationT<T>
    {
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            var target = list.Value;
            if (target == null) return;
            for (var i = target.Count - 1; i > 0; i--)
            {
                var pick = UnityEngine.Random.Range(0, i + 1);
                (target[i], target[pick]) = (target[pick], target[i]);
            }
        }
    }
}
