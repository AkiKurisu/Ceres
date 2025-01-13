using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
namespace Ceres.Graph.Flow.Properties
{
    public abstract class PropertyNode_SharedVariableValue : PropertyNode
    {
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    [CeresLabel("Get {0}")]
    public sealed class PropertyNode_GetSharedVariableTValue<T, TVariableValue, TOutValue>: PropertyNode_SharedVariableValue 
        where T: SharedVariable<TVariableValue>
        where TOutValue: TVariableValue
    {
        [OutputPort, CeresLabel("Value")]
        public CeresPort<TOutValue> outputValue;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            if (executionContext.Graph.BlackBoard.GetSharedVariable(propertyName) is T variable) 
                outputValue.Value = (TOutValue)variable.Value;
            return UniTask.CompletedTask;
        }
    }
}