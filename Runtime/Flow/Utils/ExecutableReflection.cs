using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace Ceres.Graph.Flow.Utilities
{
    public enum ExecutableFunctionType
    {
        InstanceMethod,
        StaticMethod,
        PropertySetter,
        PropertyGetter
    }

    /// <summary>
    /// Reflection runtime helper for executable functions
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
                Debug.LogError($"[Ceres] Can not create delegate for {methodInfo}");
                throw;
            }
        }

    }

    // ReSharper disable once ClassNeverInstantiated.Global
    public class ExecutableReflection<TTarget>: ExecutableReflection
    {
        private static readonly Dictionary<string, MethodInfo> Functions = new();
            
        public static MethodInfo GetFunction(ExecutableFunctionType functionType, string functionName)
        {
            if (Functions.TryGetValue(functionName, out var methodInfo))
            {
                return methodInfo;
            }

            methodInfo = functionType switch
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
            Functions.Add(functionName, methodInfo);
            return methodInfo;
        }
        
        public struct ExecutableAction
        {
            private Delegate _delegate;
            
            private bool _isStatic;
            
            public void ReallocateDelegateIfNeed(MethodInfo methodInfo)
            {
                if (methodInfo.IsStatic)
                {
                    _isStatic = true;
                    ExecutableReflection.ReallocateDelegateIfNeed<Action>(ref _delegate, methodInfo);
                    return;
                }

                _isStatic = false;
                ExecutableReflection.ReallocateDelegateIfNeed<Action<TTarget>>(ref _delegate, methodInfo);
            }
            
            public void ReallocateDelegateIfNeed<T1>(MethodInfo methodInfo)
            {
                if (methodInfo.IsStatic)
                {
                    _isStatic = true;
                    ExecutableReflection.ReallocateDelegateIfNeed<Action<T1>>(ref _delegate, methodInfo);
                    return;
                }
                
                _isStatic = false;
                ExecutableReflection.ReallocateDelegateIfNeed<Action<TTarget, T1>>(ref _delegate, methodInfo);
            }
            
            public void ReallocateDelegateIfNeed<T1, T2>(MethodInfo methodInfo)
            {
                if (methodInfo.IsStatic)
                {
                    _isStatic = true;
                    ExecutableReflection.ReallocateDelegateIfNeed<Action<T1, T2>>(ref _delegate, methodInfo);
                    return;
                }
                
                _isStatic = false;
                ExecutableReflection.ReallocateDelegateIfNeed<Action<TTarget, T1, T2>>(ref _delegate, methodInfo);
            }
            
            public void ReallocateDelegateIfNeed<T1, T2, T3>(MethodInfo methodInfo)
            {
                if (methodInfo.IsStatic)
                {
                    _isStatic = true;
                    ExecutableReflection.ReallocateDelegateIfNeed<Action<T1, T2, T3>>(ref _delegate, methodInfo);
                    return;
                }
                
                _isStatic = false;
                ExecutableReflection.ReallocateDelegateIfNeed<Action<TTarget, T1, T2, T3>>(ref _delegate, methodInfo);
            }
            
            public void ReallocateDelegateIfNeed<T1, T2, T3, T4>(MethodInfo methodInfo)
            {
                if (methodInfo.IsStatic)
                {
                    _isStatic = true;
                    ExecutableReflection.ReallocateDelegateIfNeed<Action<T1, T2, T3, T4>>(ref _delegate, methodInfo);
                    return;
                }
                
                _isStatic = false;
                ExecutableReflection.ReallocateDelegateIfNeed<Action<TTarget, T1, T2, T3, T4>>(ref _delegate, methodInfo);
            }
            
            public void Invoke(TTarget target)
            {
                if (_isStatic)
                {
                    ((Action)_delegate).Invoke();
                    return;
                }
                ((Action<TTarget>)_delegate).Invoke(target);
            }
            
            public void Invoke<T1>(TTarget target, T1 arg1)
            {
                if (_isStatic)
                {
                    ((Action<T1>)_delegate).Invoke(arg1);
                    return;
                }
                ((Action<TTarget, T1>)_delegate).Invoke(target, arg1);
            }
            
            public void Invoke<T1, T2>(TTarget target, T1 arg1, T2 arg2)
            {
                if (_isStatic)
                {
                    ((Action<T1, T2>)_delegate).Invoke(arg1, arg2);
                    return;
                }
                ((Action<TTarget, T1, T2>)_delegate).Invoke(target, arg1, arg2);
            }
            
            public void Invoke<T1, T2, T3>(TTarget target, T1 arg1, T2 arg2, T3 arg3)
            {
                if (_isStatic)
                {
                    ((Action<T1, T2, T3>)_delegate).Invoke(arg1, arg2, arg3);
                    return;
                }
                ((Action<TTarget, T1, T2, T3>)_delegate).Invoke(target, arg1, arg2, arg3);
            }
            
            public void Invoke<T1, T2, T3, T4>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            {
                if (_isStatic)
                {
                    ((Action<T1, T2, T3, T4>)_delegate).Invoke(arg1, arg2, arg3, arg4);
                    return;
                }
                ((Action<TTarget, T1, T2, T3, T4>)_delegate).Invoke(target, arg1, arg2, arg3, arg4);
            }
        }
        
        public struct ExecutableFunc
        {
            private Delegate _delegate;

            private bool _isStatic;
            
            public void ReallocateDelegateIfNeed<TR>(MethodInfo methodInfo)
            {
                if (methodInfo.IsStatic)
                {
                    _isStatic = true;
                    ExecutableReflection.ReallocateDelegateIfNeed<Func<TR>>(ref _delegate, methodInfo);
                    return;
                }

                _isStatic = false;
                ExecutableReflection.ReallocateDelegateIfNeed<Func<TTarget, TR>>(ref _delegate, methodInfo);
            }
            
            public void ReallocateDelegateIfNeed<T1, TR>(MethodInfo methodInfo)
            {
                if (methodInfo.IsStatic)
                {
                    _isStatic = true;
                    ExecutableReflection.ReallocateDelegateIfNeed<Func<T1, TR>>(ref _delegate, methodInfo);
                    return;
                }
                
                _isStatic = false;
                ExecutableReflection.ReallocateDelegateIfNeed<Func<TTarget, T1, TR>>(ref _delegate, methodInfo);
            }
            
            public void ReallocateDelegateIfNeed<T1, T2, TR>(MethodInfo methodInfo)
            {
                if (methodInfo.IsStatic)
                {
                    _isStatic = true;
                    ExecutableReflection.ReallocateDelegateIfNeed<Func<T1, T2, TR>>(ref _delegate, methodInfo);
                    return;
                }
                
                _isStatic = false;
                ExecutableReflection.ReallocateDelegateIfNeed<Func<TTarget, T1, T2, TR>>(ref _delegate, methodInfo);
            }
            
            public void ReallocateDelegateIfNeed<T1, T2, T3, TR>(MethodInfo methodInfo)
            {
                if (methodInfo.IsStatic)
                {
                    _isStatic = true;
                    ExecutableReflection.ReallocateDelegateIfNeed<Func<T1, T2, T3, TR>>(ref _delegate, methodInfo);
                    return;
                }
                
                _isStatic = false;
                ExecutableReflection.ReallocateDelegateIfNeed<Func<TTarget, T1, T2, T3, TR>>(ref _delegate, methodInfo);
            }
            
            public void ReallocateDelegateIfNeed<T1, T2, T3, T4, TR>(MethodInfo methodInfo)
            {
                if (methodInfo.IsStatic)
                {
                    _isStatic = true;
                    ExecutableReflection.ReallocateDelegateIfNeed<Func<T1, T2, T3, T4, TR>>(ref _delegate, methodInfo);
                    return;
                }
                
                _isStatic = false;
                ExecutableReflection.ReallocateDelegateIfNeed<Func<TTarget, T1, T2, T3, T4, TR>>(ref _delegate, methodInfo);
            }

            public TR Invoke<TR>(TTarget target)
            {
                if (_isStatic)
                {
                    return ((Func<TR>)_delegate).Invoke();
                }
                return ((Func<TTarget, TR>)_delegate).Invoke(target);
            }
            
            public TR Invoke<T1, TR>(TTarget target, T1 arg1)
            {
                if (_isStatic)
                {
                    return ((Func<T1, TR>)_delegate).Invoke(arg1);
                }
                return ((Func<TTarget, T1, TR>)_delegate).Invoke(target, arg1);
            }
            
            public TR Invoke<T1, T2, TR>(TTarget target, T1 arg1, T2 arg2)
            {
                if (_isStatic)
                {
                    return ((Func<T1, T2, TR>)_delegate).Invoke(arg1, arg2);
                }
                return ((Func<TTarget, T1, T2, TR>)_delegate).Invoke(target, arg1, arg2);
            }
            
            public TR Invoke<T1, T2, T3, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3)
            {
                if (_isStatic)
                {
                    return ((Func<T1, T2, T3, TR>)_delegate).Invoke(arg1, arg2, arg3);
                }
                return ((Func<TTarget, T1, T2, T3, TR>)_delegate).Invoke(target, arg1, arg2, arg3);
            }
            
            public TR Invoke<T1, T2, T3, T4, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            {
                if (_isStatic)
                {
                    return ((Func<T1, T2, T3, T4, TR>)_delegate).Invoke(arg1, arg2, arg3, arg4);
                }
                return ((Func<TTarget, T1, T2, T3, T4, TR>)_delegate).Invoke(target, arg1, arg2, arg3, arg4);
            }
        }
    }
}