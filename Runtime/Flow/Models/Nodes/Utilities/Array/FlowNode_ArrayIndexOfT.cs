using System;
using System.Collections.Generic;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("Index Of")]
    [NodeInfo("Returns the first index of the item in the array, or -1 when it is not found.")]
    [CeresMetadata("style = ConstNode", "path = Dependency")]
    public sealed class FlowNode_ArrayIndexOfT<T> : ExecutableNode
    {
        [InputPort(true), CeresLabel(""), HideInGraphEditor]
        public CeresPort<IReadOnlyList<T>> array;

        [InputPort, CeresLabel("Item")]
        public CeresPort<T> item;

        [OutputPort, CeresLabel("Index")]
        public CeresPort<int> index;

        protected override UniTask Execute(ExecutionContext executionContext)
        {
            var list = array.Value;
            index.Value = list == null ? -1 : IndexOf(list, item.Value);
            return UniTask.CompletedTask;
        }

        internal static int IndexOf(IReadOnlyList<T> list, T item)
        {
            var comparer = EqualityComparer<T>.Default;
            for (var i = 0; i < list.Count; i++)
            {
                if (comparer.Equals(list[i], item)) return i;
            }
            return -1;
        }
    }
}
