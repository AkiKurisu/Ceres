using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
namespace Ceres.Graph.Flow.Properties
{
    public abstract class PropertyNode_SharedVariableValue : PropertyNode
    {
    }
    
    [Serializable]
    [CeresGroup("Hidden")]
    [CeresLabel("Get {0}")]
    public sealed class PropertyNode_GetSharedVariableTValue<T, TVariableValue, TOutValue>: PropertyNode_SharedVariableValue 
        where T: SharedVariable<TVariableValue>
        where TOutValue: TVariableValue
    {
        [OutputPort, CeresLabel("Value")]
        public CeresPort<TOutValue> outputValue;

        private static bool _isValueType;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            if (executionContext.Graph.BlackBoard.GetSharedVariable(propertyName) is T variable)
            {
                if(!_isValueType || variable.Value != null)
                {
                    outputValue.Value = (TOutValue)variable.Value;
                }
            }
            return UniTask.CompletedTask;
        }

        static PropertyNode_GetSharedVariableTValue()
        {
            _isValueType = typeof(TOutValue).IsValueType;
        }
    }
}