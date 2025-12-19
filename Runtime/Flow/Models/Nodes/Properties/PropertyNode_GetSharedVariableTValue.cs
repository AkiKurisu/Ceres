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
            TOutValue outValue = default(TOutValue);
            if (executionContext.Graph.Blackboard.GetSharedVariable(propertyName) is T variable)
            {
                if (_isValueType)
                {
                    outValue = (TOutValue)variable.Value;
                }
                else if (variable.Value is TOutValue tValue) // Not null and can cast to TOutValue.
                {
                    outValue = tValue;
                }
            }
            outputValue.Value = outValue;
            return UniTask.CompletedTask;
        }

        static PropertyNode_GetSharedVariableTValue()
        {
            _isValueType = typeof(TOutValue).IsValueType;
        }
    }
}