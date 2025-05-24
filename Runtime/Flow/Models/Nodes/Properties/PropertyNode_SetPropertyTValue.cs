using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ceres.Graph.Flow.Properties
{
    [Serializable]
    [CeresGroup("Hidden")]
    [CeresLabel("Set {0}")]
    [CeresMetadata("style = PropertyNode", "path = Forward")]
    public sealed class PropertyNode_SetPropertyTValue<TTarget, T>: PropertyNode_PropertyValue,
        ISerializationCallbackReceiver
    {
        /// <summary>
        /// Dependency node port
        /// </summary>
        [InputPort, CeresLabel("")]
        public NodePort input;
        
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        [InputPort, CeresLabel("Value")]
        public CeresPort<T> inputValue;
        
        [OutputPort(false), CeresLabel("")]
        public NodePort exec;

        private ExecutableAction<TTarget> _delegate;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            _delegate.Invoke(GetTargetOrDefault(target, executionContext), inputValue.Value);
            executionContext.SetNext(exec.GetT<ExecutableNode>());
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
            _delegate.Invoke(GetTargetOrDefault(target, executionContext), inputValue.Value);
        }
#endif
        
        public void OnBeforeSerialize()
        {
            
        }
    
        public void OnAfterDeserialize()
        {
            var functionType = isStatic ? ExecutableFunctionType.StaticPropertySetter : ExecutableFunctionType.PropertySetter;
            _delegate =  ExecutableReflection<TTarget>.GetFunction(functionType, propertyName).ExecutableAction;
        }
    }
}