using System;
using System.Collections.Generic;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("Random Element")]
    [NodeInfo("Outputs a random item from the array and its index.")]
    [CeresMetadata("style = ConstNode", "path = Dependency")]
    public sealed class FlowNode_ArrayRandomElementT<T> : ExecutableNode
    {
        [InputPort(true), CeresLabel(""), HideInGraphEditor]
        public CeresPort<IReadOnlyList<T>> array;

        [OutputPort, CeresLabel("Item")]
        public CeresPort<T> item;

        [OutputPort, CeresLabel("Index")]
        public CeresPort<int> index;

        protected override UniTask Execute(ExecutionContext executionContext)
        {
            var list = array.Value;
            if (list == null || list.Count == 0)
            {
                item.Value = default;
                index.Value = -1;
                return UniTask.CompletedTask;
            }

            index.Value = UnityEngine.Random.Range(0, list.Count);
            item.Value = list[index.Value];
            return UniTask.CompletedTask;
        }
    }
}
