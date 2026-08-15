using Ceres.Graph;
using UnityEngine;
namespace Ceres.Flow
{
    /// <summary>
    /// <see cref="MonoBehaviour"/> only contains runtime Flow Graph instance
    /// </summary>
    public class FlowGraphInstanceObject: FlowGraphObjectBase
    {
        [SerializeField]
        internal FlowGraphAsset graphAsset;
        
        protected sealed override FlowGraph CreateRuntimeFlowGraphInstance()
        {
            return graphAsset.GetFlowGraph();
        }
    }
}