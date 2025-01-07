using System;
using System.Reflection;
using Ceres.Annotations;
using UnityEngine;
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
        
        public virtual MethodInfo GetMethodInfo(Type targetType)
        {
            if (isStatic)
            {
                return targetType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
            }
            return targetType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
        
                
        protected TValue GetTargetOrDefault<TValue>(CeresPort<TValue> inputPort, ExecutionContext context)
        {
            if (isStatic)
            {
                return default;
            }
            
            bool isNull;
            if(CeresPort<TValue>.GetValueType() == typeof(UObject))
            {
                isNull = !(inputPort.Value as UObject);
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
        
        protected TValue GetSelfTargetOrDefault<TValue>(CeresPort<TValue> inputPort, ExecutionContext context)
        {
            bool isNull;
            if(CeresPort<TValue>.GetValueType() == typeof(UObject))
            {
                isNull = !(inputPort.Value as UObject);
            }
            else
            {
                isNull = inputPort.Value == null;
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
    
    public abstract class FlowNode_ExecuteFunctionReturn<TTarget>: FlowNode_ExecuteFunctionReturn, ISerializationCallbackReceiver
    {
        protected ExecutableReflection<TTarget>.ExecutableFunc Delegate;
        
        public override MethodInfo GetMethodInfo(Type targetType)
        {
            return GetExecutableFunction().MethodInfo;
        }
        
        public ExecutableReflection<TTarget>.ExecutableFunction GetExecutableFunction()
        {
            return ExecutableReflection<TTarget>.GetFunction(isStatic ? ExecutableFunctionType.StaticMethod : ExecutableFunctionType.InstanceMethod, methodName);
        }
        
        public void OnBeforeSerialize()
        {
  
        }

        public void OnAfterDeserialize()
        {
            try
            {
                Delegate = GetExecutableFunction().ExecutableFunc;
            }
            catch(ArgumentException)
            {
                
            }
        }
    }
    
    public abstract class FlowNode_ExecuteFunctionVoid<TTarget>: FlowNode_ExecuteFunctionVoid, ISerializationCallbackReceiver
    {
        protected ExecutableReflection<TTarget>.ExecutableAction Delegate;
        
        public override MethodInfo GetMethodInfo(Type targetType)
        {
            return GetExecutableFunction().MethodInfo;
        }
        
        public ExecutableReflection<TTarget>.ExecutableFunction GetExecutableFunction()
        {
            return ExecutableReflection<TTarget>.GetFunction(isStatic ? ExecutableFunctionType.StaticMethod : ExecutableFunctionType.InstanceMethod, methodName);
        }
        
        public void OnBeforeSerialize()
        {
  
        }

        public void OnAfterDeserialize()
        {
            try
            {
                Delegate = GetExecutableFunction().ExecutableAction;
            }
            catch(ArgumentException)
            {
                
            }
        }
    }

    // Non-optimized function node
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
                    _parameters[i] = GetSelfTargetOrDefault(inputs[i], executionContext);
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
            _methodInfo = GetMethodInfo(typeof(TTarget));
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
    
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTVoid<TTarget> : FlowNode_ExecuteFunctionVoid<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            Delegate.Invoke(GetTargetOrDefault(target, executionContext));
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTReturn<TTarget, TR> : FlowNode_ExecuteFunctionReturn<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
    
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            output.Value = Delegate.Invoke<TR>(GetTargetOrDefault(target, executionContext));
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTVoid<TTarget, TP1> : FlowNode_ExecuteFunctionVoid<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        [InputPort]
        public CeresPort<TP1> input1;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            Delegate.Invoke(GetTargetOrDefault(target, executionContext), 
                    GetSelfTargetOrDefault(input1, executionContext));
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTReturn<TTarget, TP1, TR> : FlowNode_ExecuteFunctionReturn<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        [InputPort]
        public CeresPort<TP1> input1;
    
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            output.Value = Delegate.Invoke<TP1, TR>(GetTargetOrDefault(target, executionContext), 
                                    GetSelfTargetOrDefault(input1, executionContext));
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTVoid<TTarget, TP1, TP2> : 
        FlowNode_ExecuteFunctionVoid<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            Delegate.Invoke(GetTargetOrDefault(target, executionContext), 
                            GetSelfTargetOrDefault(input1, executionContext), input2.Value);
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTReturn<TTarget, TP1, TP2, TR> : 
        FlowNode_ExecuteFunctionReturn<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
    
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            output.Value = Delegate.Invoke<TP1, TP2, TR>(GetTargetOrDefault(target, executionContext), 
                                    GetSelfTargetOrDefault(input1, executionContext), input2.Value);
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTVoid<TTarget, TP1, TP2, TP3> : 
        FlowNode_ExecuteFunctionVoid<TTarget>
    {
        [InputPort, HideInGraphEditor]
        public CeresPort<TTarget> target;
        
        [InputPort]
        public CeresPort<TP1> input1;
        
        [InputPort]
        public CeresPort<TP2> input2;
        
        [InputPort]
        public CeresPort<TP3> input3;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            Delegate.Invoke(GetTargetOrDefault(target, executionContext), 
                    GetSelfTargetOrDefault(input1, executionContext), input2.Value, input3.Value);
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTReturn<TTarget, TP1, TP2, TP3, TR> : 
        FlowNode_ExecuteFunctionReturn<TTarget>
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
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            output.Value = Delegate.Invoke<TP1, TP2, TP3, TR>(GetTargetOrDefault(target, executionContext), 
                                    GetSelfTargetOrDefault(input1, executionContext), 
                                    input2.Value, input3.Value);
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTVoid<TTarget, TP1, TP2, TP3, TP4> : 
        FlowNode_ExecuteFunctionVoid<TTarget>
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
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            Delegate.Invoke(GetTargetOrDefault(target, executionContext), 
                    GetSelfTargetOrDefault(input1, executionContext), input2.Value, 
                    input3.Value, input4.Value);
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTReturn<TTarget, TP1, TP2, TP3, TP4, TR> : 
        FlowNode_ExecuteFunctionReturn<TTarget>
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
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            output.Value = Delegate.Invoke<TP1, TP2, TP3, TP4, TR>(GetTargetOrDefault(target, executionContext), 
                                    GetSelfTargetOrDefault(input1, executionContext), 
                                    input2.Value, input3.Value, input4.Value);
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTVoid<TTarget, TP1, TP2, TP3, TP4, TP5> : 
        FlowNode_ExecuteFunctionVoid<TTarget>
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
        
        [InputPort] 
        public CeresPort<TP5> input5;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            Delegate.Invoke(GetTargetOrDefault(target, executionContext), 
                    GetSelfTargetOrDefault(input1, executionContext), 
                    input2.Value, input3.Value, input4.Value, input5.Value);
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTReturn<TTarget, TP1, TP2, TP3, TP4, TP5, TR> : 
        FlowNode_ExecuteFunctionReturn<TTarget>
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
        
        [InputPort]
        public CeresPort<TP5> input5;
    
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            output.Value = Delegate.Invoke<TP1, TP2, TP3, TP4, TP5, TR>(
                                    GetTargetOrDefault(target, executionContext), 
                                    GetSelfTargetOrDefault(input1, executionContext), 
                                    input2.Value, input3.Value, input4.Value, input5.Value);
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTVoid<TTarget, TP1, TP2, TP3, TP4, TP5, TP6> : 
        FlowNode_ExecuteFunctionVoid<TTarget>
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
        
        [InputPort]
        public CeresPort<TP5> input5;
        
        [InputPort]
        public CeresPort<TP6> input6;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            Delegate.Invoke(GetTargetOrDefault(target, executionContext), 
                    GetSelfTargetOrDefault(input1, executionContext), 
                    input2.Value, input3.Value, input4.Value,
                    input5.Value, input6.Value);
        }
    }
    
    [Serializable]
    [NodeGroup("Hidden")]
    public sealed class FlowNode_ExecuteFunctionTReturn<TTarget, TP1, TP2, TP3, TP4, TP5, TP6, TR> : 
        FlowNode_ExecuteFunctionReturn<TTarget>
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
        
        [InputPort]
        public CeresPort<TP5> input5;
        
        [InputPort]
        public CeresPort<TP6> input6;
    
        [OutputPort, CeresLabel("Return Value")]
        public CeresPort<TR> output;
        
        protected override void LocalExecute(ExecutionContext executionContext)
        {
            output.Value = Delegate.Invoke<TP1, TP2, TP3, TP4, TP5, TP6, TR>(
                                    GetTargetOrDefault(target, executionContext), 
                                    GetSelfTargetOrDefault(input1, executionContext), 
                                    input2.Value, input3.Value, input4.Value, input5.Value, input6.Value);
        }
    }
    
    #endregion Optimized Function Node
}