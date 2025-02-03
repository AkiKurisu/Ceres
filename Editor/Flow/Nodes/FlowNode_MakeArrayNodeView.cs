using System;
using Ceres.Graph.Flow.Utilities;
namespace Ceres.Editor.Graph.Flow
{
    /// <summary>
    /// Node view for <see cref="FlowNode_MakeArray"/>
    /// </summary>
    [CustomNodeView(typeof(FlowNode_MakeArray), true)]
    public sealed class FlowNode_MakeArrayNodeView: ExecutablePortArrayNodeView
    {
        public FlowNode_MakeArrayNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
        }
    }
}