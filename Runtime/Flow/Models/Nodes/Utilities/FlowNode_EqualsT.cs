using System;
using Ceres.Annotations;

namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [CeresGroup("Utilities")]
    [CeresLabel("Equals")]
    public class FlowNode_EqualsT<T>: FlowNode
    {
        [InputPort]
        public CeresPort<T> value1;
        
        [InputPort]
        public CeresPort<T> value2;
                
        [OutputPort, CeresLabel("Result")]
        public CeresPort<bool> resultValue;

        protected override void LocalExecute(ExecutionContext executionContext)
        {
            resultValue.Value = value1.Value.Equals(value2.Value);
        }
    }
}