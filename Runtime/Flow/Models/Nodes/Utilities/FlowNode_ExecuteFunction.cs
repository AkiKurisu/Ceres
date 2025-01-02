using System;
using System.Reflection;
using Ceres.Annotations;
using UnityEngine;
using UnityEngine.Scripting;
using UObject = UnityEngine.Object;
namespace Ceres.Graph.Flow.Utilities
{
    [Serializable]
    public abstract class FlowNode_ExecuteFunction: FlowNode
    {
        [HideInGraphEditor]
        public string methodName;

        [HideInGraphEditor] 
        public bool isStatic;
        
        [HideInGraphEditor] 
        public bool isSelfTarget;
        
        [HideInGraphEditor] 
        public bool isScriptMethod;

        public virtual MethodInfo GetExecuteFunction(Type targetType)
        {
            if (isStatic)
            {
                return targetType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
            }
            return targetType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
        
        protected void ReallocateDelegateIfNeed<TDelegate>(ref TDelegate outDelegate, MethodInfo methodInfo, object target) where TDelegate: Delegate
        {
            if (isStatic)
            {
                outDelegate ??= (TDelegate)methodInfo.CreateDelegate(typeof(TDelegate));
                return;
            }

            if (outDelegate == null || outDelegate.Target != target)
            {
                outDelegate = (TDelegate)methodInfo.CreateDelegate(typeof(TDelegate), target);
            }
        }

        protected TValue GetTargetOrDefault<TValue>(CeresPort<TValue> inputPort, ExecutionContext context)
        {
            bool isNull;
            if( inputPort.Value is UObject uObject)
            {
                isNull = !uObject;
            }
            else
            {
                isNull = inputPort.Value != null;
            }
            
            if (isSelfTarget && isNull && context.Context is TValue tmpTarget)
            {
                return tmpTarget;
            }
            return inputPort.Value;
        }
    }
    
    public abstract class FlowNode_ExecuteFunctionUber: FlowNode_ExecuteFunction
    {

    }
    
    public abstract class FlowNode_ExecuteFunctionVoid: FlowNode_ExecuteFunction
    {

    }
    
    public abstract class FlowNode_ExecuteFunctionReturn: FlowNode_ExecuteFunction
    {

    }
    
    public abstract class FlowNode_ExecuteFunctionReturnT<TTarget>: FlowNode_ExecuteFunctionReturn, ISerializationCallbackReceiver
    {
        protected MethodInfo MethodInfo;
        
        public override MethodInfo GetExecuteFunction(Type targetType)
        {
            return ExecutableFunctionTable<TTarget>.GetFunction(isStatic, methodName);
        }
        
        public void OnBeforeSerialize()
        {
  
        }

        public void OnAfterDeserialize()
        {
            MethodInfo = GetExecuteFunction(typeof(TTarget));
        }
    }
    
    public abstract class FlowNode_ExecuteFunctionVoidT<TTarget>: FlowNode_ExecuteFunctionReturn, ISerializationCallbackReceiver
    {
        protected MethodInfo MethodInfo;
        
        public override MethodInfo GetExecuteFunction(Type targetType)
        {
            return ExecutableFunctionTable<TTarget>.GetFunction(isStatic, methodName);
        }
        
        public void OnBeforeSerialize()
        {
  
        }

        public void OnAfterDeserialize()
        {
            MethodInfo = GetExecuteFunction(typeof(TTarget));
        }
    }

    // Non-optimized function node
    [Preserve]
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionT<TTarget> : FlowNode_ExecuteFunctionUber, ISerializationCallbackReceiver
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        /* Inputs and outputs will be remapped to function parameters */
        [InputPort]
        public CeresPort<object>[] inputs;

        [OutputPort]
        public CeresPort<object>[] outputs;

        private object[] _parameters;

        private MethodInfo _methodInfo;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            object targetObject = target.Value == null ? executionContext.Context : target.Value;
            for (int i = 0; i < _parameters.Length; ++i)
            {
                _parameters[i] = inputs[i].Value;
                if (i == 0 && isSelfTarget)
                {
                    _parameters[i] = GetTargetOrDefault(inputs[i], executionContext);
                }
            }
            var result = _methodInfo!.Invoke(isStatic ? null : targetObject, _parameters);
            if(outputs != null && outputs.Length > 0)
            {
                outputs[0].SetValue(result);
            }
        }
        
        public void OnBeforeSerialize()
        {
            
        }

        public void OnAfterDeserialize()
        {
            _methodInfo = GetExecuteFunction(typeof(TTarget));
            var length = _methodInfo!.GetParameters().Length;
            _parameters = new object[length];
            inputs = new CeresPort<object>[length];
            for (int i = 0; i < length; i++)
            {
                inputs[i] = new CeresPort<object>();
            }

            if (_methodInfo.ReturnType == typeof(void))
            {
                // Should be assigned since ILPP will validate field not be null
                outputs = CeresPort<object>.DefaultArray;
            }
            else
            {
                outputs = new CeresPort<object>[] { new() };
            }
        }
    }

    #region Optimized Function Node
    
    [Preserve]
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTVoid<TTarget> : FlowNode_ExecuteFunctionVoidT<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
    
        private Action _delegate;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            object targetObject = target.Value == null ? executionContext.Context : target.Value;
            ReallocateDelegateIfNeed(ref _delegate, MethodInfo, targetObject);
            _delegate.Invoke();
        }
    }
    
    [Preserve]
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTReturn<TTarget, TR> : FlowNode_ExecuteFunctionReturnT<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
    
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;
    
        private Func<TR> _delegate;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            object targetObject = target.Value == null ? executionContext.Context : target.Value;
            ReallocateDelegateIfNeed(ref _delegate, MethodInfo, targetObject);
            output.Value = _delegate.Invoke();
        }
    }
    
    [Preserve]
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTVoid<TTarget, TP1> : FlowNode_ExecuteFunctionVoidT<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        [InputPort]
        public CeresPort<TP1> input1;
    
        private Action<TP1> _delegate;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            object targetObject = target.Value == null ? executionContext.Context : target.Value;
            ReallocateDelegateIfNeed(ref _delegate, MethodInfo, targetObject);
            _delegate.Invoke(GetTargetOrDefault(input1, executionContext));
        }
    }
    
    [Preserve]
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTReturn<TTarget, TP1, TR> : FlowNode_ExecuteFunctionReturnT<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        [InputPort]
        public CeresPort<TP1> input1;
    
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;
    
        private Func<TP1, TR> _delegate;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            object targetObject = target.Value == null ? executionContext.Context : target.Value;
            ReallocateDelegateIfNeed(ref _delegate, MethodInfo, targetObject);
            output.Value = _delegate.Invoke(GetTargetOrDefault(input1, executionContext));
        }
    }
    
    [Preserve]
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTVoid<TTarget, TP1, TP2> : FlowNode_ExecuteFunctionVoidT<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
    
        private Action<TP1, TP2> _delegate;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            object targetObject = target.Value == null ? executionContext.Context : target.Value;
            ReallocateDelegateIfNeed(ref _delegate, MethodInfo, targetObject);
            _delegate.Invoke(GetTargetOrDefault(input1, executionContext), input2.Value);
        }
    }
    
    [Preserve]
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTReturn<TTarget, TP1, TP2, TR> : FlowNode_ExecuteFunctionReturnT<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
    
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;
    
        private Func<TP1, TP2, TR> _delegate;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            object targetObject = target.Value == null ? executionContext.Context : target.Value;
            ReallocateDelegateIfNeed(ref _delegate, MethodInfo, targetObject);
            output.Value = _delegate.Invoke(GetTargetOrDefault(input1, executionContext), input2.Value);
        }
    }
    
    [Preserve]
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTVoid<TTarget, TP1, TP2, TP3> : FlowNode_ExecuteFunctionVoidT<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
        
        [InputPort]
        public CeresPort<TP3> input3;
    
        private Action<TP1, TP2, TP3> _delegate;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            object targetObject = target.Value == null ? executionContext.Context : target.Value;
            ReallocateDelegateIfNeed(ref _delegate, MethodInfo, targetObject);
            _delegate.Invoke(GetTargetOrDefault(input1, executionContext), input2.Value, input3.Value);
        }
    }
    
    [Preserve]
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTReturn<TTarget, TP1, TP2, TP3, TR> : FlowNode_ExecuteFunctionReturnT<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
        
        [InputPort]
        public CeresPort<TP3> input3;
    
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;
    
        private Func<TP1, TP2, TP3, TR> _delegate;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            object targetObject = target.Value == null ? executionContext.Context : target.Value;
            ReallocateDelegateIfNeed(ref _delegate, MethodInfo, targetObject);
            output.Value = _delegate.Invoke(GetTargetOrDefault(input1, executionContext), input2.Value, input3.Value);
        }
    }
    
    [Preserve]
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTVoid<TTarget, TP1, TP2, TP3, TP4> : FlowNode_ExecuteFunctionVoidT<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
        
        [InputPort]
        public CeresPort<TP3> input3;

        [InputPort] 
        public CeresPort<TP4> input4;
    
        private Action<TP1, TP2, TP3, TP4> _delegate;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            object targetObject = target.Value == null ? executionContext.Context : target.Value;
            ReallocateDelegateIfNeed(ref _delegate, MethodInfo, targetObject);
            _delegate.Invoke(GetTargetOrDefault(input1, executionContext), input2.Value, input3.Value, input4.Value);
        }
    }
    
    [Preserve]
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTReturn<TTarget, TP1, TP2, TP3, TP4, TR> : FlowNode_ExecuteFunctionReturnT<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
        
        [InputPort]
        public CeresPort<TP3> input3;
        
        [InputPort]
        public CeresPort<TP4> input4;
    
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;
    
        private Func<TP1, TP2, TP3, TP4, TR> _delegate;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            object targetObject = target.Value == null ? executionContext.Context : target.Value;
            ReallocateDelegateIfNeed(ref _delegate, MethodInfo, targetObject);
            output.Value = _delegate.Invoke(GetTargetOrDefault(input1, executionContext), input2.Value, input3.Value, input4.Value);
        }
    }
    
    #endregion Optimized Function Node
}