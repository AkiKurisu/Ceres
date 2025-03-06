using System;

namespace Ceres.Graph.Flow
{
    /// <summary>
    /// Top level graph type of <see cref="FlowGraph"/>, contains all sub-graphs.
    /// </summary>
    [Serializable]
    public sealed class FlowUberGraph: FlowGraph
    {
        public FlowUberGraph(FlowGraphData flowGraphData) : base(flowGraphData)
        {
        }

        /// <inheritdoc />
        public override bool IsUberGraph()
        {
            return true;
        }
        
        /// <inheritdoc />
        public override void Compile(CeresGraphCompiler compiler)
        {
            base.Compile(compiler);
            /* Compile subGraphs */
            foreach (var subGraphSlot in SubGraphSlots)
            {
                subGraphSlot.Graph.Compile(compiler);
            }
        }
    }
}