using System;
using Ceres.Annotations;
namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    [NodeGroup("Utilities")]
    [CeresLabel("Enum to Int")]
    public class FlowNode_EnumToIntT<TEnum>: FlowNode where TEnum: Enum
    {
        [InputPort, CeresLabel("Enum")]
        public CeresPort<TEnum> enumValue;
                
        [OutputPort, CeresLabel("Result")]
        public CeresPort<int> resultValue;

        protected override void LocalExecute(ExecutionContext executionContext)
        {
            resultValue.Value = enumValue.Value.GetHashCode();
        }
    }
}