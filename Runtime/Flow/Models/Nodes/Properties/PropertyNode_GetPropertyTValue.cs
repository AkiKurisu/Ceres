using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ceres.Graph.Flow.Properties
{
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

        private ExecutableFunc<TTarget> _delegate;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            outputValue.Value = _delegate.Invoke<T>(GetTargetOrDefault(target, executionContext));
            return UniTask.CompletedTask;
        }
        
#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// Do test for single node execution
        /// </summary>
        /// <param name="executionContext"></param>
        internal void ExecuteTest(ExecutionContext executionContext)
        {
            OnAfterDeserialize();
            outputValue.Value = _delegate.Invoke<T>(GetTargetOrDefault(target, executionContext));
        }
#endif
        
        public void OnBeforeSerialize()
        {
            
        }
    
        public void OnAfterDeserialize()
        {
            var functionType = isStatic ? ExecutableFunctionType.StaticPropertyGetter : ExecutableFunctionType.PropertyGetter;
            _delegate =  ExecutableReflection<TTarget>.GetFunction(functionType, propertyName).ExecutableFunc;
        }
    }
}