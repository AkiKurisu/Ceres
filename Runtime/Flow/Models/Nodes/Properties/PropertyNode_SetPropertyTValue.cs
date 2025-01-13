using System;
using Ceres.Annotations;
using Ceres.Graph.Flow.Utilities;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UObject = UnityEngine.Object;
namespace Ceres.Graph.Flow.Properties
{
    [Serializable]
    [NodeGroup("Hidden")]
    [CeresLabel("Set {0}")]
    [CeresMetadata("style = PropertyNode", "path = Forward")]
    public sealed class PropertyNode_SetPropertyTValue<TTarget, T>: PropertyNode_PropertyValue,
        ISerializationCallbackReceiver 
        where TTarget: UObject
    {
        /// <summary>
        /// Dependency node port
        /// </summary>
        [InputPort, CeresLabel("")]
        public NodePort input;
        
        [InputPort, CeresLabel("Value")]
        public CeresPort<T> inputValue;
        
        [OutputPort(false), CeresLabel("")]
        public NodePort exec;

        private ExecutableReflection<TTarget>.ExecutableAction _delegate;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            _delegate.Invoke((TTarget)executionContext.Context, inputValue.Value);
            executionContext.SetNext(exec.GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }
        
        public void OnBeforeSerialize()
        {
            
        }
    
        public void OnAfterDeserialize()
        {
            _delegate =  ExecutableReflection<TTarget>.GetFunction(ExecutableFunctionType.PropertySetter, propertyName).ExecutableAction;
        }
    }
}