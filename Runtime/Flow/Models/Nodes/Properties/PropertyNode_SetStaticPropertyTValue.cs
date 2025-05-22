using System;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ceres.Graph.Flow.Properties
{
    [Serializable]
    [CeresGroup("Hidden")]
    [CeresLabel("Set Static {0}")]
    [CeresMetadata("style = PropertyNode", "path = Forward")]
    public sealed class PropertyNode_SetStaticPropertyTValue<T>: PropertyNode_PropertyValue,
        ISerializationCallbackReceiver
    {
        [InputPort, CeresLabel("")]
        public NodePort input;
        
        [InputPort, CeresLabel("Value")]
        public CeresPort<T> inputValue;
        
        [OutputPort(false), CeresLabel("")]
        public NodePort exec;

        private ExecutableAction<object> _delegate;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            _delegate.Invoke(null, inputValue.Value);
            executionContext.SetNext(exec.GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }
        
        public void OnBeforeSerialize()
        {
            
        }
    
        public void OnAfterDeserialize()
        {
            _delegate = ExecutableReflection<object>.GetFunction(ExecutableFunctionType.StaticPropertySetter, propertyName).ExecutableAction;
        }
    }
}
