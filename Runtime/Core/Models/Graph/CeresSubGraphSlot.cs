using System;

namespace Ceres.Graph
{
    /// <summary>
    /// SubGraph data container for <see cref="CeresGraphData"/>
    /// </summary>
    [Serializable]
    public abstract class CeresSubGraphSlot
    {
        /// <summary>
        /// Slot guid that is persistent in uber graph scope
        /// </summary>
        public string guid;

        /// <summary>
        /// SubGraph displayed name
        /// </summary>
        public string name;
        
        public abstract CeresGraphData GetGraphData();
    }
    
    /// <summary>
    /// SubGraph data container for <see cref="TGraphData"/>
    /// </summary>
    [Serializable]
    public abstract class CeresSubGraphSlot<TGraphData>: CeresSubGraphSlot where TGraphData: CeresGraphData
    {
        public TGraphData graphData;
        
        public override CeresGraphData GetGraphData()
        {
            return graphData;
        }
    }
}