using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UObject = UnityEngine.Object;
namespace Ceres.Graph.Flow.Properties
{
    [Serializable]
    public abstract class PropertyNode_PropertyValue : PropertyNode
    {
        [HideInGraphEditor] 
        public bool isSelfTarget;
        
        protected TValue GetTargetOrDefault<TValue>(CeresPort<TValue> inputPort, ExecutionContext context)
        {
            if (!isSelfTarget)
            {
                return inputPort.Value;
            }
            
            bool isNull;
            if(inputPort.Value is UObject value)
            {
                isNull = !value;
            }
            else
            {
                isNull = inputPort.Value == null;
            }
            
            if (isNull && context.Context is TValue tmpTarget)
            {
                return tmpTarget;
            }
            return inputPort.Value;
        }
    }

    [Serializable]
    [CeresGroup("Hidden")]
    [CeresLabel("Get {0}")]
    public sealed class PropertyNode_GetPropertyTValue<TTarget, T>: PropertyNode_PropertyValue,
        ISerializationCallbackReceiver
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        [OutputPort, CeresLabel("Value")]
        public CeresPort<T> outputValue;

        private ExecutableReflection<TTarget>.ExecutableFunc _delegate;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            outputValue.Value = _delegate.Invoke<T>(GetTargetOrDefault(target, executionContext));
            return UniTask.CompletedTask;
        }
        
        public void OnBeforeSerialize()
        {
            
        }
    
        public void OnAfterDeserialize()
        {
            _delegate =  ExecutableReflection<TTarget>.GetFunction(ExecutableFunctionType.PropertyGetter, propertyName).ExecutableFunc;
        }
    }
}