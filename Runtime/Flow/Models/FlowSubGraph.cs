namespace Ceres.Graph.Flow
{
    /// <summary>
    /// A special type of <see cref="FlowGraph"/>
    /// </summary>
    internal class FlowSubGraph: FlowGraph
    {
        public FlowSubGraph(FlowGraphSerializedData flowGraphData) : base(flowGraphData)
        {
        }
    }
}