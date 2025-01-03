using System;
using Ceres.Annotations;
using UnityEngine.Scripting;
namespace Ceres.Graph.Flow.Utilities
{
    [Preserve]
    [Serializable]
    [NodeGroup("Utilities")]
    [CeresLabel("Equals")]
    public class FlowNode_EqualsT<T>: FlowNode
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<T> value1;
        
        [InputPort, HideInGraphEditor]
        public CeresPort<T> value2;
                
        [OutputPort, CeresLabel("Result")]
        public CeresPort<bool> resultValue;

        protected override void LocalExecute(ExecutionContext executionContext)
        {
            resultValue.Value = value1.Value.Equals(value2.Value);
        }
    }
}