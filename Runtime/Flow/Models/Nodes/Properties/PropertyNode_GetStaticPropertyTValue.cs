using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ceres.Graph.Flow.Properties
{
    [Serializable]
    [CeresGroup("Hidden")]
    [CeresLabel("Get Static {0}")]
    public sealed class PropertyNode_GetStaticPropertyTValue<T>: PropertyNode_PropertyValue,
        ISerializationCallbackReceiver
    {
        [OutputPort, CeresLabel("Value")]
        public CeresPort<T> outputValue;

        private ExecutableFunc<object> _delegate;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            outputValue.Value = _delegate.Invoke<T>(null);
            return UniTask.CompletedTask;
        }
        
        public void OnBeforeSerialize()
        {
            
        }
    
        public void OnAfterDeserialize()
        {
            _delegate = ExecutableReflection<object>.GetFunction(ExecutableFunctionType.StaticPropertyGetter, propertyName).ExecutableFunc;
        }
    }
}
