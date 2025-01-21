using System;
using Ceres.Graph.Flow.Utilities;
namespace Ceres.Editor.Graph.Flow
{
    /// <summary>
    /// Node view for <see cref="FlowNode_Sequence"/>
    /// </summary>
    [CustomNodeView(typeof(FlowNode_Sequence))]
    public sealed class FlowNode_SequenceNodeView: ExecutablePortArrayNodeView
    {
        public FlowNode_SequenceNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
        }
    }
}