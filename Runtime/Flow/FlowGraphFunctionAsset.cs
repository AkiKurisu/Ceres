using System;
using System.Linq;
using Ceres.Graph.Flow.CustomFunctions;
using UnityEngine;

namespace Ceres.Graph.Flow
{
    [Serializable]
    public class FlowGraphFunctionSerializedInfo
    {
        public CustomFunctionOutputParameter returnParameter = new();

        public CustomFunctionInputParameter[] inputParameters = Array.Empty<CustomFunctionInputParameter>();
    }
    
    /// <summary>
    /// Asset contains custom function that can be shared between multi <see cref="IFlowGraphRuntime"/> instances.
    /// </summary>
    [CreateAssetMenu(fileName = "FlowGraphFunctionAsset", menuName = "Ceres/Flow Graph Function")]
    public class FlowGraphFunctionAsset : FlowGraphAsset
    {
        [HideInInspector]
        public FlowGraphFunctionSerializedInfo serializedInfo = new ();

#if UNITY_EDITOR
        internal static Action<FlowGraphFunctionAsset> OnFunctionUpdate;
#endif
        
        public override FlowGraph GetFlowGraph()
        {
#if UNITY_EDITOR
            var graphData = GetGraphData();
            if (graphData.nodeData == null || graphData.nodeData.Length == 0)
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

        private const string InputGuid = "af2fe73f-c2fe-4530-b687-405e58726e9a";
        
        private const string OutputGuid = "3201ffe8-b9dc-47ea-b46e-662e8c4b5e6f";
        
        public override void SetGraphData(CeresGraphData graphData)
        {
            var flowGraphData = (FlowGraphSerializedData)graphData;
            base.SetGraphData(flowGraphData);
            var inputData = flowGraphData.nodeData.First(x => x.guid == InputGuid);
            var outputData = flowGraphData.nodeData.First(x => x.guid == OutputGuid);
            var inputNode = JsonUtility.FromJson<CustomFunctionInput>(inputData.serializedData);
            var outputNode = JsonUtility.FromJson<CustomFunctionOutput>(outputData.serializedData);
            serializedInfo = new FlowGraphFunctionSerializedInfo
            {
                inputParameters = inputNode.parameters.ToArray(),
                returnParameter = outputNode.parameter
            };
#if UNITY_EDITOR
            OnFunctionUpdate?.Invoke(this);
#endif
        }
        
        public override Type GetRuntimeType()
        {
            return runtimeType.GetObjectType();
        }
    }
}
