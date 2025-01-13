using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UObject = UnityEngine.Object;
namespace Ceres.Graph.Flow.Properties
{
    public abstract class PropertyNode_PropertyValue : PropertyNode
    {
    }

    [Serializable]
    [NodeGroup("Hidden")]
    [CeresLabel("Get {0}")]
    public sealed class PropertyNode_GetPropertyTValue<TTarget, T>: PropertyNode_PropertyValue,
        ISerializationCallbackReceiver 
        where TTarget: UObject
    {
        [OutputPort, CeresLabel("Value")]
        public CeresPort<T> outputValue;

        private ExecutableReflection<TTarget>.ExecutableFunc _delegate;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            outputValue.Value = _delegate.Invoke<T>((TTarget)executionContext.Context);
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