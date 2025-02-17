﻿namespace Ceres.Graph.Flow
{
    /// <summary>
    /// Top level graph type of <see cref="FlowGraph"/>, contains all sub-graphs.
    /// </summary>
    public class FlowUberGraph: FlowGraph
    {
        public FlowUberGraph(FlowGraphData flowGraphData) : base(flowGraphData)
        {
        }
    }
}