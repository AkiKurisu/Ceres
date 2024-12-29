using System;
using System.Reflection;
using Ceres.Annotations;
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

        private Func<T> _delegate;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            if (_delegate == null || (UObject)_delegate.Target != executionContext.Context)
            {
                _delegate ??= (Func<T>)_methodInfo.CreateDelegate(typeof(Func<T>), executionContext.Context);
            }

            outputValue.Value = _delegate.Invoke();
            return UniTask.CompletedTask;
        }
        
        public void OnBeforeSerialize()
        {
            
        }
    
        public void OnAfterDeserialize()
        {
            _methodInfo = typeof(TTarget).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!.GetMethod;
        }
    }
}