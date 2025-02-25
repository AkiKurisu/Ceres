using System;

namespace Ceres.Graph
{
    /// <summary>
    /// SubGraph container for <see cref="CeresGraph"/>
    /// </summary>
    public class CeresSubGraphSlot
    {
        /// <summary>
        /// SubGraph name
        /// </summary>
        public string Name;

        /// <summary>
        /// SubGraph persistent id
        /// </summary>
        public string Guid;

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
        public string name;
        
        public string guid;
        
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