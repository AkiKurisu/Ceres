using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Ceres.Graph.Flow.Utilities;
using UnityEngine.Assertions;
namespace Ceres.Graph.Flow
{
    public class InvalidExecutableFunctionException : Exception
    {
        public InvalidExecutableFunctionException(string message) : base(message)
        {
            
        }
    }
    
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
    public abstract class ExecutableReflection
    {
        private static readonly Dictionary<Type, ExecutableReflection> TypeMap = new();
        
        /// <summary>
        /// Get <see cref="ExecutableFunction"/> from <see cref="MethodInfo"/>
        /// </summary>
        /// <param name="methodInfo"></param>
        /// <returns></returns>
        public static ExecutableFunction GetFunction(MethodInfo methodInfo)
        {
            var declareType = methodInfo.DeclaringType;
            if (!TypeMap.TryGetValue(declareType!, out var instance))
            {
                var instanceType = typeof(ExecutableReflection<>).MakeGenericType(declareType);
                instance = (ExecutableReflection)Activator.CreateInstance(instanceType, true);
            }
            var function = instance.GetFunction_Imp(methodInfo);
            CeresAPI.Assert(function != null,$"Can not get executable function {methodInfo} from {declareType} which is not expected");
            return function;
        }
        
        protected abstract ExecutableFunction GetFunction_Imp(MethodInfo methodInfo);

        protected static void RegisterReflection<T>(ExecutableReflection instance)
        {
            TypeMap.Add(typeof(T), instance);
        }
    }

    public abstract class ExecutableFunction
    {
        public class ExecutableAttribute
        {
            public bool IsScriptMethod { get; }
        
            public bool ExecuteInDependency { get; }
        
            public bool DisplayTarget { get; }
        
            public bool IsSelfTarget { get; }
        
            public bool IsNeedResolveReturnType { get; }
        
            public ParameterInfo ResolveReturnTypeParameter { get; }
            
            public Type ScriptTargetType { get; }

            public ExecutableAttribute(MethodInfo methodInfo)
            {
                if (!methodInfo.IsStatic) return;
                
                var parameters = methodInfo.GetParameters();
                var attribute = methodInfo.GetCustomAttribute<ExecutableFunctionAttribute>();
                if (parameters.Length >= 1)
                {
                    if (methodInfo.ReturnType != typeof(void))
                    {
                        ResolveReturnTypeParameter = parameters.FirstOrDefault(x => x.GetCustomAttribute<ResolveReturnAttribute>() != null);
                        IsNeedResolveReturnType = ResolveReturnTypeParameter != null;
                    }

                    IsScriptMethod = attribute.IsScriptMethod;
                    DisplayTarget = attribute.IsScriptMethod && attribute.DisplayTarget;
                    IsSelfTarget = attribute.IsSelfTarget;
                    if (attribute.IsScriptMethod)
                    {
                        ScriptTargetType = parameters[0].ParameterType;
                    }
                }

                ExecuteInDependency = attribute.ExecuteInDependency;
            }
        }

        private ExecutableAttribute _attribute;

        
        public readonly MethodInfo MethodInfo;

        /// <summary>
        /// Metadata for editor lookup, should not access it at runtime
        /// </summary>
        internal ExecutableAttribute Attribute => _attribute ??= new ExecutableAttribute(MethodInfo);

        protected ExecutableFunction(MethodInfo methodInfo)
        {
            MethodInfo = methodInfo;
        }
        
        public static string GetFunctionName(MethodInfo methodInfo, bool richText = true)
        {
            var labelAttribute = methodInfo.GetCustomAttribute<CeresLabelAttribute>();
            return labelAttribute != null ? labelAttribute.GetLabel(richText) : methodInfo.Name.Replace("Flow_", string.Empty);
        }
    }

    public readonly struct ExecutableFunctionInfo: IEquatable<ExecutableFunctionInfo>
    {
        public readonly ExecutableFunctionType FunctionType;
        
        public readonly string FunctionName;

        public readonly int ParameterCount;

        public ExecutableFunctionInfo(ExecutableFunctionType functionType, string functionName, int parameterCount = -1)
        {
            FunctionType = functionType;
            FunctionName = functionName;
            ParameterCount = parameterCount;
        }

        internal ExecutableFunctionInfo(MethodInfo methodInfo)
        {
            FunctionType = methodInfo.IsStatic ? ExecutableFunctionType.StaticMethod : ExecutableFunctionType.InstanceMethod;
            FunctionName = methodInfo.Name;
            ParameterCount = methodInfo.GetParameters().Length;
        }

        public bool Equals(ExecutableFunctionInfo other)
        {
            return FunctionType == other.FunctionType && FunctionName == other.FunctionName && ParameterCount == other.ParameterCount;
        }

        public override bool Equals(object obj)
        {
            return obj is ExecutableFunctionInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)FunctionType, FunctionName, ParameterCount);
        }

        public override string ToString()
        {
            return $"Name {FunctionName}_{ParameterCount} Type {FunctionType}";
        }

        public bool AmbiguousEquals(ExecutableFunctionInfo other)
        {
            return FunctionType == other.FunctionType && FunctionName == other.FunctionName;
        }
    }
    
    public class ExecutableReflection<TTarget>: ExecutableReflection
    {
        public unsafe class ExecutableFunction: Flow.ExecutableFunction
        {
            public readonly ExecutableFunctionInfo FunctionInfo;

            public readonly ExecutableAction ExecutableAction;
            
            public readonly ExecutableFunc ExecutableFunc;

            internal ExecutableFunction(ExecutableFunctionInfo functionInfo, MethodInfo methodInfo): base(methodInfo)
            {
                FunctionInfo = functionInfo;
                ExecutableAction = new ExecutableAction(MethodInfo);
                ExecutableFunc = new ExecutableFunc(MethodInfo);
            }
            
            internal ExecutableFunction(ExecutableFunctionInfo functionInfo, void* functionPtr): base(null)
            {
                FunctionInfo = functionInfo;
                ExecutableAction = new ExecutableAction(functionPtr);
                ExecutableFunc = new ExecutableFunc(functionPtr);
            }
            
            internal ExecutableFunction(ExecutableFunctionInfo functionInfo, MethodInfo methodInfo, void* functionPtr): base(methodInfo)
            {
                FunctionInfo = functionInfo;
                ExecutableAction = new ExecutableAction(functionPtr);
                ExecutableFunc = new ExecutableFunc(functionPtr);
            }
        }

        private readonly List<ExecutableFunction> _functions = new();

        private ExecutableReflection()
        {
            _instance = this;
            RegisterReflection<TTarget>(_instance);
            if (typeof(TTarget).IsSubclassOf(typeof(ExecutableFunctionLibrary)))
            {
#if UNITY_EDITOR
                typeof(TTarget).GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Where(x => x.GetCustomAttribute<ExecutableFunctionAttribute>() != null)
                    .ToList()
                    .ForEach(methodInfo =>
                    {
                        RegisterExecutableFunction(ExecutableFunctionType.StaticMethod, methodInfo);
                    });
#endif
                Activator.CreateInstance(typeof(TTarget));
                return;
            }

            typeof(TTarget).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x=> x.GetCustomAttribute<ExecutableFunctionAttribute>() != null)
                .ToList()
                .ForEach(methodInfo =>
                {
                    RegisterExecutableFunction(ExecutableFunctionType.InstanceMethod, methodInfo);
                });
        }

        private static ExecutableReflection<TTarget> _instance;

        private static ExecutableReflection<TTarget> Instance
        {
            get
            {
                return _instance ??= new ExecutableReflection<TTarget>();
            }
        }

        protected override Flow.ExecutableFunction GetFunction_Imp(MethodInfo methodInfo)
        {
            return GetFunction_Internal(new ExecutableFunctionInfo(methodInfo));
        }
        
        public static ExecutableFunction GetFunction(ExecutableFunctionType functionType, string functionName, int parameterCount = -1)
        {
            return Instance.GetFunction_Internal(new ExecutableFunctionInfo(functionType, functionName, parameterCount));
        }

        private void RegisterExecutableFunction(ExecutableFunctionType functionType, MethodInfo methodInfo)
        {
            var functionInfo = new ExecutableFunctionInfo(functionType, methodInfo.Name, methodInfo.GetParameters().Length);
            var functionStructure = new ExecutableFunction(functionInfo, methodInfo);
            _functions.Add(functionStructure);
        }
        
        internal static unsafe void RegisterStaticExecutableFunction(string functionName, int parameterCount, void* functionPtr)
        {
            var functionInfo = new ExecutableFunctionInfo(ExecutableFunctionType.StaticMethod, functionName, parameterCount);
#if UNITY_EDITOR
            var function = Instance.FindFunction_Internal(functionInfo);
            if (function != null)
            {
                Instance._functions.Remove(function);
                var overrideStructure = new ExecutableFunction(functionInfo, function.MethodInfo, functionPtr);
                Instance._functions.Add(overrideStructure);
                return;
            }
#endif
            var functionStructure = new ExecutableFunction(functionInfo, functionPtr);
            Instance._functions.Add(functionStructure);
        }

        private ExecutableFunction FindFunction_Internal(ExecutableFunctionInfo functionInfo)
        {
            /* Ambiguous search */
            if(functionInfo.ParameterCount < 0)
            {
                foreach (var function in _functions)
                {
                    if (function.FunctionInfo.AmbiguousEquals(functionInfo))
                    {
                        return function;
                    }
                }

                return null;
            }

            foreach (var function in _functions)
            {
                if (function.FunctionInfo.Equals(functionInfo))
                {
                    return function;
                }
            }

            return null;
        }
        
        private ExecutableFunction GetFunction_Internal(ExecutableFunctionInfo functionInfo)
        {
            var functionStructure = FindFunction_Internal(functionInfo);
            if (functionStructure != null)
            {
                return functionStructure;
            }

            var functionType = functionInfo.FunctionType;
            var functionName = functionInfo.FunctionName;
            var methodInfo = functionType switch
            {
                ExecutableFunctionType.PropertySetter => typeof(TTarget).GetProperty(functionName,
                    BindingFlags.Public | BindingFlags.Instance)!.SetMethod,
                ExecutableFunctionType.PropertyGetter => typeof(TTarget).GetProperty(functionName,
                    BindingFlags.Public | BindingFlags.Instance)!.GetMethod,
                ExecutableFunctionType.InstanceMethod or ExecutableFunctionType.StaticMethod => null,
                _ => null
            };

            if (methodInfo == null) throw new InvalidExecutableFunctionException($"[Ceres] Can not find executable function from {nameof(ExecutableFunctionInfo)} [{functionInfo}]");
            functionStructure = new ExecutableFunction(functionInfo, methodInfo);
            _functions.Add(functionStructure);
            return functionStructure;
        }
        
        
        public abstract unsafe class ExecutableDelegate
        {
            protected Delegate Delegate;

            protected readonly void* FunctionPtr;

            public readonly bool IsStatic;

            protected readonly MethodInfo MethodInfo;

            protected ExecutableDelegate(MethodInfo methodInfo)
            {
#if !UNITY_EDITOR
                Assert.IsFalse(methodInfo.IsStatic);
#endif
                MethodInfo = methodInfo;
                IsStatic = false;
            }
            
            protected ExecutableDelegate(void* functionPtr)
            {
                IsStatic = true;
                FunctionPtr = functionPtr;
            }
            
            protected static void ReallocateDelegateIfNeed<TDelegate>(ref Delegate outDelegate, MethodInfo methodInfo) where TDelegate: Delegate
            {
                if (methodInfo == null || methodInfo.IsStatic)
                {
                    return;
                }
                try
                {
                    if(outDelegate is TDelegate) return;
                    /* Force create open delegate */
                    outDelegate = Delegate.CreateDelegate(typeof(TDelegate), null, methodInfo);
                }
                catch
                {
                    CeresAPI.LogError($"Can not create delegate for {methodInfo}");
                    throw;
                }
            }
        }
        
        public unsafe class ExecutableAction: ExecutableDelegate
        {
            internal ExecutableAction(MethodInfo methodInfo) : base(methodInfo)
            {
            }
            
            internal ExecutableAction(void* functionPtr) : base(functionPtr)
            {
            }
            
            private void ReallocateDelegateIfNeed()
            {
                ReallocateDelegateIfNeed<Action<TTarget>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1>()
            {
                ReallocateDelegateIfNeed<Action<TTarget, T1>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2>()
            {
                ReallocateDelegateIfNeed<Action<TTarget, T1, T2>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3>()
            {
                ReallocateDelegateIfNeed<Action<TTarget, T1, T2, T3>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3, T4>()
            {
                ReallocateDelegateIfNeed<Action<TTarget, T1, T2, T3, T4>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3, T4, T5>()
            {
                ReallocateDelegateIfNeed<Action<TTarget, T1, T2, T3, T4, T5>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, T6>()
            {
                ReallocateDelegateIfNeed<Action<TTarget, T1, T2, T3, T4, T5, T6>>(ref Delegate, MethodInfo);
            }
            
            public void Invoke(TTarget target)
            {
                ReallocateDelegateIfNeed();
                if (IsStatic)
                {
                    ((delegate* <void>)FunctionPtr)();
                    return;
                }
                Assert.IsNotNull(Delegate);
                ((Action<TTarget>)Delegate).Invoke(target);
            }
            
            public void Invoke<T1>(TTarget target, T1 arg1)
            {
                ReallocateDelegateIfNeed<T1>();
                if (IsStatic)
                {
                    ((delegate* <T1, void>)FunctionPtr)(arg1);
                    return;
                }
                Assert.IsNotNull(Delegate);
                ((Action<TTarget, T1>)Delegate).Invoke(target, arg1);
            }
            
            public void Invoke<T1, T2>(TTarget target, T1 arg1, T2 arg2)
            {
                ReallocateDelegateIfNeed<T1, T2>();
                if (IsStatic)
                {
                    ((delegate* <T1, T2, void>)FunctionPtr)(arg1, arg2);
                    return;
                }
                Assert.IsNotNull(Delegate);
                ((Action<TTarget, T1, T2>)Delegate).Invoke(target, arg1, arg2);
            }
            
            public void Invoke<T1, T2, T3>(TTarget target, T1 arg1, T2 arg2, T3 arg3)
            {
                ReallocateDelegateIfNeed<T1, T2, T3>();
                if (IsStatic)
                {
                    ((delegate* <T1, T2, T3, void>)FunctionPtr)(arg1, arg2, arg3);
                    return;
                }
                Assert.IsNotNull(Delegate);
                ((Action<TTarget, T1, T2, T3>)Delegate).Invoke(target, arg1, arg2, arg3);
            }
            
            public void Invoke<T1, T2, T3, T4>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            {
                ReallocateDelegateIfNeed<T1, T2, T3, T4>();
                if (IsStatic)
                {
                    ((delegate* <T1, T2, T3, T4, void>)FunctionPtr)(arg1, arg2, arg3, arg4);
                    return;
                }
                Assert.IsNotNull(Delegate);
                ((Action<TTarget, T1, T2, T3, T4>)Delegate).Invoke(target, arg1, arg2, arg3, arg4);
            }
            
            public void Invoke<T1, T2, T3, T4, T5>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            {
                ReallocateDelegateIfNeed<T1, T2, T3, T4, T5>();
                if (IsStatic)
                {
                    ((delegate* <T1, T2, T3, T4, T5, void>)FunctionPtr)(arg1, arg2, arg3, arg4, arg5);
                    return;
                }
                Assert.IsNotNull(Delegate);
                ((Action<TTarget, T1, T2, T3, T4, T5>)Delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5);
            }
            
            public void Invoke<T1, T2, T3, T4, T5, T6>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            {
                ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, T6>();
                if (IsStatic)
                {
                    ((delegate* <T1, T2, T3, T4, T5, T6, void>)FunctionPtr)(arg1, arg2, arg3, arg4, arg5, arg6);
                    return;
                }
                Assert.IsNotNull(Delegate);
                ((Action<TTarget, T1, T2, T3, T4, T5, T6>)Delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5, arg6);
            }
        }
        
        public unsafe class ExecutableFunc: ExecutableDelegate
        {
            internal ExecutableFunc(MethodInfo methodInfo) : base(methodInfo)
            {
            }
            
            internal ExecutableFunc(void* functionPtr) : base(functionPtr)
            {
            }
            
            private void ReallocateDelegateIfNeed<TR>()
            {
                ReallocateDelegateIfNeed<Func<TTarget, TR>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, TR>()
            {
                ReallocateDelegateIfNeed<Func<TTarget, T1, TR>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, TR>()
            {
                ReallocateDelegateIfNeed<Func<TTarget, T1, T2, TR>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3, TR>()
            {
                ReallocateDelegateIfNeed<Func<TTarget, T1, T2, T3, TR>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3, T4, TR>()
            {
                ReallocateDelegateIfNeed<Func<TTarget, T1, T2, T3, T4, TR>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, TR>()
            {
                ReallocateDelegateIfNeed<Func<TTarget, T1, T2, T3, T4, T5, TR>>(ref Delegate, MethodInfo);
            }
            
            private void ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, T6, TR>()
            {
                ReallocateDelegateIfNeed<Func<TTarget, T1, T2, T3, T4, T5, T6, TR>>(ref Delegate, MethodInfo);
            }

            public TR Invoke<TR>(TTarget target)
            {
                ReallocateDelegateIfNeed<TR>();
                if (IsStatic)
                {
                    return ((delegate* <TR>)FunctionPtr)();
                }
                Assert.IsNotNull(Delegate);
                return ((Func<TTarget, TR>)Delegate).Invoke(target);
            }
            
            public TR Invoke<T1, TR>(TTarget target, T1 arg1)
            {
                ReallocateDelegateIfNeed<T1, TR>();
                if (IsStatic)
                {
                    return ((delegate* <T1, TR>)FunctionPtr)(arg1);
                }
                Assert.IsNotNull(Delegate);
                return ((Func<TTarget, T1, TR>)Delegate).Invoke(target, arg1);
            }
            
            public TR Invoke<T1, T2, TR>(TTarget target, T1 arg1, T2 arg2)
            {
                ReallocateDelegateIfNeed<T1, T2, TR>();
                if (IsStatic)
                {
                    return ((delegate* <T1, T2, TR>)FunctionPtr)(arg1, arg2);
                }
                Assert.IsNotNull(Delegate);
                return ((Func<TTarget, T1, T2, TR>)Delegate).Invoke(target, arg1, arg2);
            }
            
            public TR Invoke<T1, T2, T3, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3)
            {
                ReallocateDelegateIfNeed<T1, T2, T3, TR>();
                if (IsStatic)
                {
                    return ((delegate* <T1, T2, T3, TR>)FunctionPtr)(arg1, arg2, arg3);
                }
                Assert.IsNotNull(Delegate);
                return ((Func<TTarget, T1, T2, T3, TR>)Delegate).Invoke(target, arg1, arg2, arg3);
            }
            
            public TR Invoke<T1, T2, T3, T4, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            {
                ReallocateDelegateIfNeed<T1, T2, T3, T4, TR>();
                if (IsStatic)
                {
                    return ((delegate* <T1, T2, T3, T4, TR>)FunctionPtr)(arg1, arg2, arg3, arg4);
                }
                Assert.IsNotNull(Delegate);
                return ((Func<TTarget, T1, T2, T3, T4, TR>)Delegate).Invoke(target, arg1, arg2, arg3, arg4);
            }
            
            public TR Invoke<T1, T2, T3, T4, T5, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
            {
                ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, TR>();
                if (IsStatic)
                {
                    return ((delegate* <T1, T2, T3, T4, T5, TR>)FunctionPtr)(arg1, arg2, arg3, arg4, arg5);
                }
                Assert.IsNotNull(Delegate);
                return ((Func<TTarget, T1, T2, T3, T4, T5, TR>)Delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5);
            }
            
            public TR Invoke<T1, T2, T3, T4, T5, T6, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
            {
                ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, T6, TR>();
                if (IsStatic)
                {
                    return ((delegate* <T1, T2, T3, T4, T5, T6, TR>)FunctionPtr)(arg1, arg2, arg3, arg4, arg5, arg6);
                }
                Assert.IsNotNull(Delegate);
                return ((Func<TTarget, T1, T2, T3, T4, T5, T6, TR>)Delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5, arg6);
            }
        }
    }
}