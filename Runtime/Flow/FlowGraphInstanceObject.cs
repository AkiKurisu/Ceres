using UnityEngine;
namespace Ceres.Graph.Flow
{
    /// <summary>
    /// <see cref="MonoBehaviour"/> only contains runtime Flow Graph instance
    /// </summary>
    public class FlowGraphInstanceObject: FlowGraphObjectBase
    {
        [SerializeField]
        private FlowGraphAsset graphAsset;
        
        protected sealed override FlowGraph CreateRuntimeFlowGraphInstance()
        {
            return graphAsset.GetFlowGraph();
        }
    }
}