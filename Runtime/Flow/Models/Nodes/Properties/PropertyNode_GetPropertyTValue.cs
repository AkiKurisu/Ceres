using System;
using System.Reflection;
using Ceres.Annotations;
using Ceres.Graph.Flow.Utilities;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UObject = UnityEngine.Object;
namespace Ceres.Graph.Flow.Properties
{
    [Serializable]
    [NodeGroup("Hidden")]
    [CeresLabel("Get {0}")]
    public sealed class PropertyNode_GetPropertyTValue<TTarget, T>: PropertyNode, ISerializationCallbackReceiver where TTarget: UObject
    {
        [OutputPort, CeresLabel("Value")]
        public CeresPort<T> outputValue;
        
        private MethodInfo _methodInfo;

        private ExecutableReflection<TTarget>.ExecutableFunc _delegate;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            _delegate.ReallocateDelegateIfNeed<T>(_methodInfo);
            outputValue.Value = _delegate.Invoke<T>((TTarget)executionContext.Context);
            return UniTask.CompletedTask;
        }
        
        public void OnBeforeSerialize()
        {
            
        }
    
        public void OnAfterDeserialize()
        {
            _methodInfo =  ExecutableReflection<TTarget>.GetFunction(ExecutableFunctionType.PropertyGetter, propertyName);
        }
    }
}