using Ceres.Graph;
using UnityEditor.Experimental.GraphView;
namespace Ceres.Editor.Graph.Flow
{
    public class FlowBlackboard: CeresBlackboard
    {
        public FlowBlackboard(IVariableSource source, GraphView graphView) : base(source, graphView)
        {
        }

        public FlowBlackboard(CeresGraphView graphView) : base(graphView)
        {
        }
    }
}