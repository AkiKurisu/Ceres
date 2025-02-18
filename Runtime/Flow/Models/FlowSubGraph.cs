namespace Ceres.Graph.Flow
{
    /// <summary>
    /// A special type of <see cref="FlowGraph"/>
    /// </summary>
    public class FlowSubGraph: FlowGraph
    {
        public FlowSubGraph(FlowGraphSerializedData flowGraphData) : base(flowGraphData)
        {
        }
        
        public sealed override bool IsUberGraph()
        {
            return false;
        }
    }
}