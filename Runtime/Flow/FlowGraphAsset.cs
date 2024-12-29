using UnityEngine;
namespace Ceres.Graph.Flow
{
    [CreateAssetMenu(fileName = "FlowGraphAsset", menuName = "Ceres/Flow Graph Asset")]
    public class FlowGraphAsset: ScriptableObject, IFlowGraphContainer
    {
        public Object Object => this;

        [SerializeField] 
        private FlowGraphData graphData;
        
        public CeresGraph GetGraph()
        {
            return GetFlowGraph();
        }
        
        public FlowGraph GetFlowGraph()
        {
            return new FlowGraph(graphData.CloneT<FlowGraphData>());
        }

        public void SetGraphData(CeresGraphData graph)
        {
            graphData = (FlowGraphData)graph;
        }
    }
}