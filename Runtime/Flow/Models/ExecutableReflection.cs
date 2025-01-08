using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;

namespace Ceres.Graph.Flow
{
    public enum ExecutableFunctionType
    {
        /// <summary>
        /// Method from class instance
        /// </summary>
        InstanceMethod,
        /// <summary>
        /// Method from static class
        /// </summary>
        StaticMethod,
        /// <summary>
        /// Set method from instance property
        /// </summary>
        PropertySetter,
        /// <summary>
        /// Get method from instance property
        /// </summary>
        PropertyGetter
    }

    /// <summary>
    /// Runtime reflection helper for executable functions
    /// </summary>
    public class ExecutableReflection
    {
        protected static void ReallocateDelegateIfNeed<TDelegate>(ref Delegate outDelegate, MethodInfo methodInfo) where TDelegate: Delegate
        {
            try
            {
                if(outDelegate is TDelegate) return;
                
                if (methodInfo.IsStatic)
                {
                    outDelegate = methodInfo.CreateDelegate(typeof(TDelegate));
                    return;
                }
                /* Force create open delegate */
                outDelegate = Delegate.CreateDelegate(typeof(TDelegate), null, methodInfo);
            }
            catch
            {
                CeresGraph.LogError($"Can not create delegate for {methodInfo}");
                throw;
            }
        }
    }

    public readonly struct ExecutableFunctionInfo: IEquatable<ExecutableFunctionInfo>
    {
        public readonly ExecutableFunctionType FunctionType;
        
        public readonly string FunctionName;

        public ExecutableFunctionInfo(ExecutableFunctionType functionType, string functionName)
        {
            FunctionType = functionType;
            FunctionName = functionName;
        }

        public bool Equals(ExecutableFunctionInfo other)
        {
            return FunctionType == other.FunctionType && FunctionName == other.FunctionName;
        }

        public override bool Equals(object obj)
        {
            return obj is ExecutableFunctionInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)FunctionType, FunctionName);
        }

        public override string ToString()
        {
            return $"{nameof(ExecutableFunctionInfo)} [Name {FunctionName} Type {FunctionType}]";
        }
    }

    // ReSharper disable once ClassNeverInstantiated.Global
    public class ExecutableReflection<TTarget>: ExecutableReflection
    {
        public class ExecutableFunction
        {
            public readonly ExecutableFunctionInfo FunctionInfo;

            public readonly MethodInfo MethodInfo;

            public readonly ExecutableAction ExecutableAction;
            
            public readonly ExecutableFunc ExecutableFunc;

            public readonly bool HasReturnValue;

            public ExecutableFunction(ExecutableFunctionInfo functionInfo, MethodInfo methodInfo)
            {
                FunctionInfo = functionInfo;
                MethodInfo = methodInfo;
                HasReturnValue = methodInfo.ReturnType != typeof(void);
                ExecutableAction = new ExecutableAction(MethodInfo);
                ExecutableFunc = new ExecutableFunc(MethodInfo);
            }

            public ExecutableDelegate GetDelegate()
            {
                return HasReturnValue ? ExecutableFunc : ExecutableAction;
            }
        }

        private static readonly Dictionary<ExecutableFunctionInfo, ExecutableFunction> Functions = new();
        
        public static ExecutableFunction GetFunction(ExecutableFunctionType functionType, string functionName)
        {
            return GetFunction(new ExecutableFunctionInfo(functionType, functionName));
        }
        
        public static ExecutableFunction GetFunction(ExecutableFunctionInfo functionInfo)
        {
            if (Functions.TryGetValue(functionInfo, out var functionStructure))
            {
                return functionStructure;
            }

            var functionType = functionInfo.FunctionType;
            var functionName = functionInfo.FunctionName;
            var methodInfo = functionType switch
            {
                ExecutableFunctionType.StaticMethod => typeof(TTarget).GetMethod(functionName,
                    BindingFlags.Static | BindingFlags.Public),
                ExecutableFunctionType.InstanceMethod => typeof(TTarget).GetMethod(functionName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                ExecutableFunctionType.PropertySetter => typeof(TTarget).GetProperty(functionName,
                    BindingFlags.Public | BindingFlags.Instance)!.SetMethod,
                ExecutableFunctionType.PropertyGetter => typeof(TTarget).GetProperty(functionName,
                    BindingFlags.Public | BindingFlags.Instance)!.GetMethod,
                _ => throw new ArgumentException(nameof(functionType))
            };
            if (methodInfo == null) throw new ArgumentException($"Can not get function from {functionInfo}");
            functionStructure = new ExecutableFunction(functionInfo, methodInfo);
            Functions.Add(functionInfo, functionStructure);
            return functionStructure;
        }
        
        
        public abstract class ExecutableDelegate
        {
            protected Delegate Delegate;

            protected readonly bool IsStatic;

            protected readonly MethodInfo MethodInfo;

            protected ExecutableDelegate(MethodInfo methodInfo)
            {
                MethodInfo = methodInfo;
                IsStatic = methodInfo.IsStatic;
            }
        }
        
        public class ExecutableAction: ExecutableDelegate
        {
            public ExecutableAction(MethodInfo methodInfo) : base(methodInfo)
            {
            }
            
            private void ReallocateDelegateIfNeed()
            {
                if (IsStatic)
                {
                    ExecutableReflection.ReallocateDelegateIfNeed<Action>(ref Delegate, MethodInfo);
                    return;
                }
                
                ExecutableReflection.ReallocateDelegateIfNeed<Action<TTarget>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1>()
            {
                if (IsStatic)
                {
                    ExecutableReflection.ReallocateDelegateIfNeed<Action<T1>>(ref Delegate, MethodInfo);
                    return;
                }
                
                ExecutableReflection.ReallocateDelegateIfNeed<Action<TTarget, T1>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2>()
            {
                if (IsStatic)
                {
                    ExecutableReflection.ReallocateDelegateIfNeed<Action<T1, T2>>(ref Delegate, MethodInfo);
                    return;
                }
                
                ExecutableReflection.ReallocateDelegateIfNeed<Action<TTarget, T1, T2>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3>()
            {
                if (IsStatic)
                {
                    ExecutableReflection.ReallocateDelegateIfNeed<Action<T1, T2, T3>>(ref Delegate, MethodInfo);
                    return;
                }
                
                ExecutableReflection.ReallocateDelegateIfNeed<Action<TTarget, T1, T2, T3>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3, T4>()
            {
                if (IsStatic)
                {
                    ExecutableReflection.ReallocateDelegateIfNeed<Action<T1, T2, T3, T4>>(ref Delegate, MethodInfo);
                    return;
                }
                
                ExecutableReflection.ReallocateDelegateIfNeed<Action<TTarget, T1, T2, T3, T4>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3, T4, T5>()
            {
                if (IsStatic)
                {
                    ExecutableReflection.ReallocateDelegateIfNeed<Action<T1, T2, T3, T4, T5>>(ref Delegate, MethodInfo);
                    return;
                }
                
                ExecutableReflection.ReallocateDelegateIfNeed<Action<TTarget, T1, T2, T3, T4, T5>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, T6>()
            {
                if (IsStatic)
                {
                    ExecutableReflection.ReallocateDelegateIfNeed<Action<T1, T2, T3, T4, T5, T6>>(ref Delegate, MethodInfo);
                    return;
                }
                
                ExecutableReflection.ReallocateDelegateIfNeed<Action<TTarget, T1, T2, T3, T4, T5, T6>>(ref Delegate, MethodInfo);
            }
            
            public void Invoke(TTarget target)
            {
                ReallocateDelegateIfNeed();
                if (IsStatic)
                {
                    ((Action)Delegate).Invoke();
                    return;
                }
                ((Action<TTarget>)Delegate).Invoke(target);
            }
            
            public void Invoke<T1>(TTarget target, T1 arg1)
            {
                ReallocateDelegateIfNeed<T1>();
                if (IsStatic)
                {
                    ((Action<T1>)Delegate).Invoke(arg1);
                    return;
                }
                ((Action<TTarget, T1>)Delegate).Invoke(target, arg1);
            }
            
            public void Invoke<T1, T2>(TTarget target, T1 arg1, T2 arg2)
            {
                ReallocateDelegateIfNeed<T1, T2>();
                if (IsStatic)
                {
                    ((Action<T1, T2>)Delegate).Invoke(arg1, arg2);
                    return;
                }
                ((Action<TTarget, T1, T2>)Delegate).Invoke(target, arg1, arg2);
            }
            
            public void Invoke<T1, T2, T3>(TTarget target, T1 arg1, T2 arg2, T3 arg3)
            {
                ReallocateDelegateIfNeed<T1, T2, T3>();
                if (IsStatic)
                {
                    ((Action<T1, T2, T3>)Delegate).Invoke(arg1, arg2, arg3);
                    return;
                }
                ((Action<TTarget, T1, T2, T3>)Delegate).Invoke(target, arg1, arg2, arg3);
            }
            
            public void Invoke<T1, T2, T3, T4>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            {
                ReallocateDelegateIfNeed<T1, T2, T3, T4>();
                if (IsStatic)
                {
                    ((Action<T1, T2, T3, T4>)Delegate).Invoke(arg1, arg2, arg3, arg4);
                    return;
                }
                ((Action<TTarget, T1, T2, T3, T4>)Delegate).Invoke(target, arg1, arg2, arg3, arg4);
            }
            
            public void Invoke<T1, T2, T3, T4, T5>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            {
                ReallocateDelegateIfNeed<T1, T2, T3, T4, T5>();
                if (IsStatic)
                {
                    ((Action<T1, T2, T3, T4, T5>)Delegate).Invoke(arg1, arg2, arg3, arg4, arg5);
                    return;
                }
                ((Action<TTarget, T1, T2, T3, T4, T5>)Delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5);
            }
            
            public void Invoke<T1, T2, T3, T4, T5, T6>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            {
                ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, T6>();
                if (IsStatic)
                {
                    ((Action<T1, T2, T3, T4, T5, T6>)Delegate).Invoke(arg1, arg2, arg3, arg4, arg5, arg6);
                    return;
                }
                ((Action<TTarget, T1, T2, T3, T4, T5, T6>)Delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5, arg6);
            }
        }
        
        public class ExecutableFunc: ExecutableDelegate
        {
            public ExecutableFunc(MethodInfo methodInfo) : base(methodInfo)
            {
            }
            
            private void ReallocateDelegateIfNeed<TR>()
            {
                if (IsStatic)
                {
                    ExecutableReflection.ReallocateDelegateIfNeed<Func<TR>>(ref Delegate, MethodInfo);
                    return;
                }
                
                ExecutableReflection.ReallocateDelegateIfNeed<Func<TTarget, TR>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, TR>()
            {
                if (IsStatic)
                {
                    ExecutableReflection.ReallocateDelegateIfNeed<Func<T1, TR>>(ref Delegate, MethodInfo);
                    return;
                }
                
                ExecutableReflection.ReallocateDelegateIfNeed<Func<TTarget, T1, TR>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, TR>()
            {
                if (IsStatic)
                {
                    ExecutableReflection.ReallocateDelegateIfNeed<Func<T1, T2, TR>>(ref Delegate, MethodInfo);
                    return;
                }
                
                ExecutableReflection.ReallocateDelegateIfNeed<Func<TTarget, T1, T2, TR>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3, TR>()
            {
                if (IsStatic)
                {
                    ExecutableReflection.ReallocateDelegateIfNeed<Func<T1, T2, T3, TR>>(ref Delegate, MethodInfo);
                    return;
                }
                
                ExecutableReflection.ReallocateDelegateIfNeed<Func<TTarget, T1, T2, T3, TR>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3, T4, TR>()
            {
                if (IsStatic)
                {
                    ExecutableReflection.ReallocateDelegateIfNeed<Func<T1, T2, T3, T4, TR>>(ref Delegate, MethodInfo);
                    return;
                }
                
                ExecutableReflection.ReallocateDelegateIfNeed<Func<TTarget, T1, T2, T3, T4, TR>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, TR>()
            {
                if (IsStatic)
                {
                    ExecutableReflection.ReallocateDelegateIfNeed<Func<T1, T2, T3, T4, T5, TR>>(ref Delegate, MethodInfo);
                    return;
                }
                
                ExecutableReflection.ReallocateDelegateIfNeed<Func<TTarget, T1, T2, T3, T4, T5, TR>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, T6, TR>()
            {
                if (IsStatic)
                {
                    ExecutableReflection.ReallocateDelegateIfNeed<Func<T1, T2, T3, T4, T5, T6, TR>>(ref Delegate, MethodInfo);
                    return;
                }
                
                ExecutableReflection.ReallocateDelegateIfNeed<Func<TTarget, T1, T2, T3, T4, T5, T6, TR>>(ref Delegate, MethodInfo);
            }

            public TR Invoke<TR>(TTarget target)
            {
                ReallocateDelegateIfNeed<TR>();
                if (IsStatic)
                {
                    return ((Func<TR>)Delegate).Invoke();
                }
                return ((Func<TTarget, TR>)Delegate).Invoke(target);
            }
            
            public TR Invoke<T1, TR>(TTarget target, T1 arg1)
            {
                ReallocateDelegateIfNeed<T1, TR>();
                if (IsStatic)
                {
                    return ((Func<T1, TR>)Delegate).Invoke(arg1);
                }
                return ((Func<TTarget, T1, TR>)Delegate).Invoke(target, arg1);
            }
            
            public TR Invoke<T1, T2, TR>(TTarget target, T1 arg1, T2 arg2)
            {
                ReallocateDelegateIfNeed<T1, T2, TR>();
                if (IsStatic)
                {
                    return ((Func<T1, T2, TR>)Delegate).Invoke(arg1, arg2);
                }
                return ((Func<TTarget, T1, T2, TR>)Delegate).Invoke(target, arg1, arg2);
            }
            
            public TR Invoke<T1, T2, T3, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3)
            {
                ReallocateDelegateIfNeed<T1, T2, T3, TR>();
                if (IsStatic)
                {
                    return ((Func<T1, T2, T3, TR>)Delegate).Invoke(arg1, arg2, arg3);
                }
                return ((Func<TTarget, T1, T2, T3, TR>)Delegate).Invoke(target, arg1, arg2, arg3);
            }
            
            public TR Invoke<T1, T2, T3, T4, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            {
                ReallocateDelegateIfNeed<T1, T2, T3, T4, TR>();
                if (IsStatic)
                {
                    return ((Func<T1, T2, T3, T4, TR>)Delegate).Invoke(arg1, arg2, arg3, arg4);
                }
                return ((Func<TTarget, T1, T2, T3, T4, TR>)Delegate).Invoke(target, arg1, arg2, arg3, arg4);
            }
            
            public TR Invoke<T1, T2, T3, T4, T5, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            {
                ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, TR>();
                if (IsStatic)
                {
                    return ((Func<T1, T2, T3, T4, T5, TR>)Delegate).Invoke(arg1, arg2, arg3, arg4, arg5);
                }
                return ((Func<TTarget, T1, T2, T3, T4, T5, TR>)Delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5);
            }
            
            public TR Invoke<T1, T2, T3, T4, T5, T6, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            {
                ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, T6, TR>();
                if (IsStatic)
                {
                    return ((Func<T1, T2, T3, T4, T5, T6, TR>)Delegate).Invoke(arg1, arg2, arg3, arg4, arg5, arg6);
                }
                return ((Func<TTarget, T1, T2, T3, T4, T5, T6, TR>)Delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5, arg6);
            }
        }
    }
}