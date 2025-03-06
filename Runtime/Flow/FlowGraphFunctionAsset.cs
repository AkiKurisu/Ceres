using UnityEngine;

namespace Ceres.Graph.Flow
{
    /// <summary>
    /// Asset contains custom function that can be shared between multi <see cref="IFlowGraphRuntime"/> instances.
    /// </summary>
    [CreateAssetMenu(fileName = "FlowGraphFunctionAsset", menuName = "Ceres/Flow Graph Function")]
    public class FlowGraphFunctionAsset : FlowGraphAsset
    {
        public override FlowGraph GetFlowGraph()
        {
#if UNITY_EDITOR
            var graphData = GetGraphData();
            if (graphData.nodeData?.Length == 0)
            {
                var json = Resources.Load<TextAsset>("Ceres/Flow/FunctionSubGraphData").text;
                return new FlowGraph(JsonUtility.FromJson<FlowGraphSerializedData>(json));
            }
#endif
            return CreateFunctionGraphInstance();
        }
        
        private FlowGraph CreateFunctionGraphInstance()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                return new FlowGraph(GetGraphData());
            }
            /* Keep persistent data safe in editor */
            return new FlowGraph(GetGraphData().CloneT<FlowGraphSerializedData>());
#else
            return new FlowGraph(GetGraphData());
#endif
        }
    }
}
