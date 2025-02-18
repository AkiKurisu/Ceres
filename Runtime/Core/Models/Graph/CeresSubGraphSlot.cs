using System;

namespace Ceres.Graph
{
    /// <summary>
    /// SubGraph container for <see cref="CeresGraph"/>
    /// </summary>
    public class CeresSubGraphSlot
    {
        /// <summary>
        /// Slot name
        /// </summary>
        public string Name;

        /// <summary>
        /// SubGraph instance
        /// </summary>
        /// <returns></returns>
        public CeresGraph Graph;
    }
    
    /// <summary>
    /// SubGraph metadata
    /// </summary>
    [Serializable]
    public abstract class CeresSubGraphData
    {
        public string slotName;
        
        public abstract CeresGraphData GetSubGraphData();
    }
    
    /// <summary>
    /// SubGraph metadata of <see cref="TGraphData"/>>
    /// </summary>
    /// <typeparam name="TGraphData"></typeparam>
    [Serializable]
    public abstract class CeresSubGraphData<TGraphData>: CeresSubGraphData where TGraphData: CeresGraphData
    {
        public TGraphData graphData;
        
        public override CeresGraphData GetSubGraphData()
        {
            return graphData;
        }
    }
}