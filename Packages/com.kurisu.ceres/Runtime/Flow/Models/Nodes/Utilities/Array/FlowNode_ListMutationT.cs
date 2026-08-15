using Ceres.Graph;
using System.Collections.Generic;
using Ceres.Annotations;

namespace Ceres.Flow.Utilities
{
    public abstract class FlowNode_ListMutationT<T> : FlowNode
    {
        [InputPort(true), CeresLabel("List"), HideInGraphEditor]
        public CeresPort<IList<T>> list;
    }
}
