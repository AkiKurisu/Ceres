using System.Collections.Generic;
using Ceres.Annotations;

namespace Ceres.Graph.Flow.Utilities
{
    public abstract class FlowNode_ListMutationT<T> : FlowNode
    {
        [InputPort(true), CeresLabel("List"), HideInGraphEditor]
        public CeresPort<IList<T>> list;
    }
}
