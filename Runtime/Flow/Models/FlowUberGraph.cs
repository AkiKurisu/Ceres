﻿using System;

namespace Ceres.Graph.Flow
{
    /// <summary>
    /// Top level graph type of <see cref="FlowGraph"/>, contains all sub-graphs.
    /// </summary>
    [Serializable]
    public class FlowUberGraph: FlowGraph
    {
        public FlowUberGraph(FlowGraphData flowGraphData) : base(flowGraphData)
        {
        }

        public sealed override bool IsUberGraph()
        {
            return true;
        }
        
        public override void Compile()
        {
            base.Compile();
            /* Compile subGraphs */
            foreach (var subGraphSlot in SubGraphSlots)
            {
                subGraphSlot.Graph.Compile();
            }
        }
    }
}