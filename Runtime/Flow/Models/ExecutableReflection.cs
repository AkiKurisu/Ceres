// #define CERES_IL2CPP_OPTIMIZE
// CERES_IL2CPP_OPTIMIZE requires on modification on Unity.IL2CPP.dll to support calli on instance method
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
            CeresLogger.Assert(function != null,$"Can not get executable function {methodInfo} from {declareType} which is not expected");
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
            
            public string SearchName { get; }
        
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

                SearchName = attribute.SearchName;
                ExecuteInDependency = attribute.ExecuteInDependency;
            }
        }

        private ExecutableAttribute _attribute;

        
        public readonly MethodInfo MethodInfo;

        /// <summary>
        /// Metadata for editor lookup, should not access it at runtime
        /// </summary>
        internal ExecutableAttribute Attribute => _attribute ??= new ExecutableAttribute(MethodInfo);
        
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        internal string FilePath;

        internal int LineNumber;
#endif

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
        public class ExecutableFunction: Flow.ExecutableFunction
        {
            public readonly ExecutableFunctionInfo FunctionInfo;

            internal readonly ExecutableAction<TTarget> ExecutableAction;
            
            internal readonly ExecutableFunc<TTarget> ExecutableFunc;

            internal ExecutableFunction(ExecutableFunctionInfo functionInfo, MethodInfo methodInfo): base(methodInfo)
            {
                FunctionInfo = functionInfo;
                ExecutableAction = new ExecutableAction<TTarget>(MethodInfo);
                ExecutableFunc = new ExecutableFunc<TTarget>(MethodInfo);
            }
            
            internal unsafe ExecutableFunction(ExecutableFunctionInfo functionInfo, void* functionPtr): base(null)
            {
                FunctionInfo = functionInfo;
                ExecutableAction = new ExecutableAction<TTarget>(functionPtr);
                ExecutableFunc = new ExecutableFunc<TTarget>(functionPtr);
            }
            
            internal unsafe ExecutableFunction(ExecutableFunctionInfo functionInfo, MethodInfo methodInfo, void* functionPtr): base(methodInfo)
            {
                FunctionInfo = functionInfo;
                ExecutableAction = new ExecutableAction<TTarget>(functionPtr);
                ExecutableFunc = new ExecutableFunc<TTarget>(functionPtr);
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
        
        internal static unsafe void RegisterStaticExecutableFunctionPtr(string functionName, int parameterCount, void* functionPtr)
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

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        internal static void RegisterStaticExecutableFunctionFileInfo(string functionName, int parameterCount, string filePath, int lineNumber)
        {
            var functionInfo = new ExecutableFunctionInfo(ExecutableFunctionType.StaticMethod, functionName, parameterCount);
            var functionStructure = Instance.FindFunction_Internal(functionInfo);
            functionStructure.FilePath = filePath;
            functionStructure.LineNumber = lineNumber + 3 /* Instruction start */;
        }
#endif
    }
        
    internal unsafe class ExecutableAction<TTarget>
    {
#if !(ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE)
        private Delegate _delegate;

        private readonly MethodInfo _methodInfo;
#endif

        private readonly void* _functionPtr;

        public readonly bool IsStatic;
        
        internal ExecutableAction(MethodInfo methodInfo)
        {
#if !UNITY_EDITOR
            Assert.IsFalse(methodInfo.IsStatic);
#endif
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            // RuntimeMethod* in IL2CPP
            _functionPtr = (void*)methodInfo.MethodHandle.Value;
#else
            _methodInfo = methodInfo;
#endif
            IsStatic = false;
        }
        
        internal ExecutableAction(void* functionPtr)
        {
            IsStatic = true;
            _functionPtr = functionPtr;
        }

#if !(ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE)
        private static void ReallocateDelegateIfNeed<TDelegate>(ref Delegate outDelegate, MethodInfo methodInfo) where TDelegate: Delegate
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
                CeresLogger.LogError($"Can not create delegate for {methodInfo}");
                throw;
            }
        }
        
        private void ReallocateDelegateIfNeed()
        {
            ReallocateDelegateIfNeed<Action<TTarget>>(ref _delegate, _methodInfo);
        }
        
        private void ReallocateDelegateIfNeed<T1>()
        {
            ReallocateDelegateIfNeed<Action<TTarget, T1>>(ref _delegate, _methodInfo);
        }
        
        private void ReallocateDelegateIfNeed<T1, T2>()
        {
            ReallocateDelegateIfNeed<Action<TTarget, T1, T2>>(ref _delegate, _methodInfo);
        }
        
        private void ReallocateDelegateIfNeed<T1, T2, T3>()
        {
            ReallocateDelegateIfNeed<Action<TTarget, T1, T2, T3>>(ref _delegate, _methodInfo);
        }
        
        private void ReallocateDelegateIfNeed<T1, T2, T3, T4>()
        {
            ReallocateDelegateIfNeed<Action<TTarget, T1, T2, T3, T4>>(ref _delegate, _methodInfo);
        }
        
        private void ReallocateDelegateIfNeed<T1, T2, T3, T4, T5>()
        {
            ReallocateDelegateIfNeed<Action<TTarget, T1, T2, T3, T4, T5>>(ref _delegate, _methodInfo);
        }
        
        private void ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, T6>()
        {
            ReallocateDelegateIfNeed<Action<TTarget, T1, T2, T3, T4, T5, T6>>(ref _delegate, _methodInfo);
        }
#endif
        
        public void Invoke(TTarget target)
        {
            if (IsStatic)
            {
                ((delegate* <void>)_functionPtr)();
                return;
            }
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            FuncIL2CPP.Void(target, _functionPtr);
#else
            ReallocateDelegateIfNeed();
            Assert.IsNotNull(_delegate);
            ((Action<TTarget>)_delegate).Invoke(target);
#endif
        }
        
        public void Invoke<T1>(TTarget target, T1 arg1)
        {
            if (IsStatic)
            {
                ((delegate* <T1, void>)_functionPtr)(arg1);
                return;
            }
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            FuncIL2CPP.Void(target, arg1, _functionPtr);
#else
            ReallocateDelegateIfNeed<T1>();
            Assert.IsNotNull(_delegate);
            ((Action<TTarget, T1>)_delegate).Invoke(target, arg1);
#endif
        }
        
        public void Invoke<T1, T2>(TTarget target, T1 arg1, T2 arg2)
        {
            if (IsStatic)
            {
                ((delegate* <T1, T2, void>)_functionPtr)(arg1, arg2);
                return;
            }
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            FuncIL2CPP.Void(target, arg1, arg2, _functionPtr);
#else
            ReallocateDelegateIfNeed<T1, T2>();
            Assert.IsNotNull(_delegate);
            ((Action<TTarget, T1, T2>)_delegate).Invoke(target, arg1, arg2);
#endif
        }
        
        public void Invoke<T1, T2, T3>(TTarget target, T1 arg1, T2 arg2, T3 arg3)
        {
            if (IsStatic)
            {
                ((delegate* <T1, T2, T3, void>)_functionPtr)(arg1, arg2, arg3);
                return;
            }
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            FuncIL2CPP.Void(target, arg1, arg2, arg3, _functionPtr);
#else
            ReallocateDelegateIfNeed<T1, T2, T3>();
            Assert.IsNotNull(_delegate);
            ((Action<TTarget, T1, T2, T3>)_delegate).Invoke(target, arg1, arg2, arg3);
#endif
        }
        
        public void Invoke<T1, T2, T3, T4>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (IsStatic)
            {
                ((delegate* <T1, T2, T3, T4, void>)_functionPtr)(arg1, arg2, arg3, arg4);
                return;
            }
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            FuncIL2CPP.Void(target, arg1, arg2, arg3, arg4, _functionPtr);
#else
            ReallocateDelegateIfNeed<T1, T2, T3, T4>();
            Assert.IsNotNull(_delegate);
            ((Action<TTarget, T1, T2, T3, T4>)_delegate).Invoke(target, arg1, arg2, arg3, arg4);
#endif
        }
        
        public void Invoke<T1, T2, T3, T4, T5>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            if (IsStatic)
            {
                ((delegate* <T1, T2, T3, T4, T5, void>)_functionPtr)(arg1, arg2, arg3, arg4, arg5);
                return;
            }
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            FuncIL2CPP.Void(target, arg1, arg2, arg3, arg4, arg5, _functionPtr);
#else
            ReallocateDelegateIfNeed<T1, T2, T3, T4, T5>();
            Assert.IsNotNull(_delegate);
            ((Action<TTarget, T1, T2, T3, T4, T5>)_delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5);
#endif
        }
        
        public void Invoke<T1, T2, T3, T4, T5, T6>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            if (IsStatic)
            {
                ((delegate* <T1, T2, T3, T4, T5, T6, void>)_functionPtr)(arg1, arg2, arg3, arg4, arg5, arg6);
                return;
            }
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            FuncIL2CPP.Void(target, arg1, arg2, arg3, arg4, arg5, arg6, _functionPtr);
#else
            ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, T6>();
            Assert.IsNotNull(_delegate);
            ((Action<TTarget, T1, T2, T3, T4, T5, T6>)_delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5, arg6);
#endif
        }
    }
    
    internal unsafe class ExecutableFunc<TTarget>
    {
#if !(ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE)
        private Delegate _delegate;

        private readonly MethodInfo _methodInfo;
#endif

        private readonly void* _functionPtr;

        public readonly bool IsStatic;
        
        internal ExecutableFunc(MethodInfo methodInfo)
        {
#if !UNITY_EDITOR
            Assert.IsFalse(methodInfo.IsStatic);
#endif
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            // RuntimeMethod* in IL2CPP
            _functionPtr = (void*)methodInfo.MethodHandle.Value;
#else
            _methodInfo = methodInfo;
#endif
            IsStatic = false;
        }
        
        internal ExecutableFunc(void* functionPtr)
        {
            IsStatic = true;
            _functionPtr = functionPtr;
        }

#if !(ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE)
        private static void ReallocateDelegateIfNeed<TDelegate>(ref Delegate outDelegate, MethodInfo methodInfo) where TDelegate: Delegate
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
                CeresLogger.LogError($"Can not create delegate for {methodInfo}");
                throw;
            }
        }
        
        private void ReallocateDelegateIfNeed<TR>()
        {
            ReallocateDelegateIfNeed<Func<TTarget, TR>>(ref _delegate, _methodInfo);
        }
        
        private void ReallocateDelegateIfNeed<T1, TR>()
        {
            ReallocateDelegateIfNeed<Func<TTarget, T1, TR>>(ref _delegate, _methodInfo);
        }
        
        private void ReallocateDelegateIfNeed<T1, T2, TR>()
        {
            ReallocateDelegateIfNeed<Func<TTarget, T1, T2, TR>>(ref _delegate, _methodInfo);
        }
        
        private void ReallocateDelegateIfNeed<T1, T2, T3, TR>()
        {
            ReallocateDelegateIfNeed<Func<TTarget, T1, T2, T3, TR>>(ref _delegate, _methodInfo);
        }
        
        private void ReallocateDelegateIfNeed<T1, T2, T3, T4, TR>()
        {
            ReallocateDelegateIfNeed<Func<TTarget, T1, T2, T3, T4, TR>>(ref _delegate, _methodInfo);
        }
        
        private void ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, TR>()
        {
            ReallocateDelegateIfNeed<Func<TTarget, T1, T2, T3, T4, T5, TR>>(ref _delegate, _methodInfo);
        }
        
        private void ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, T6, TR>()
        {
            ReallocateDelegateIfNeed<Func<TTarget, T1, T2, T3, T4, T5, T6, TR>>(ref _delegate, _methodInfo);
        }
#endif
        
        public TR Invoke<TR>(TTarget target)
        {
            if (IsStatic)
            {
                return ((delegate* <TR>)_functionPtr)();
            }
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            return FuncIL2CPP.Generic<TR, TTarget>(target, _functionPtr);
#else
            ReallocateDelegateIfNeed<TR>();
            Assert.IsNotNull(_delegate);
            return ((Func<TTarget, TR>)_delegate).Invoke(target);
#endif
        }
        
        public TR Invoke<T1, TR>(TTarget target, T1 arg1)
        {
            if (IsStatic)
            {
                return ((delegate* <T1, TR>)_functionPtr)(arg1);
            }
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            return FuncIL2CPP.Generic<TR, TTarget, T1>(target, arg1, _functionPtr);
#else
            ReallocateDelegateIfNeed<T1, TR>();
            Assert.IsNotNull(_delegate);
            return ((Func<TTarget, T1, TR>)_delegate).Invoke(target, arg1);
#endif
        }
        
        public TR Invoke<T1, T2, TR>(TTarget target, T1 arg1, T2 arg2)
        {
            if (IsStatic)
            {
                return ((delegate* <T1, T2, TR>)_functionPtr)(arg1, arg2);
            }
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            return FuncIL2CPP.Generic<TR, TTarget, T1, T2>(target, arg1, arg2, _functionPtr);
#else
            ReallocateDelegateIfNeed<T1, T2, TR>();
            Assert.IsNotNull(_delegate);
            return ((Func<TTarget, T1, T2, TR>)_delegate).Invoke(target, arg1, arg2);
#endif
        }
        
        public TR Invoke<T1, T2, T3, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3)
        {
            if (IsStatic)
            {
                return ((delegate* <T1, T2, T3, TR>)_functionPtr)(arg1, arg2, arg3);
            }
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            return FuncIL2CPP.Generic<TR, TTarget, T1, T2, T3>(target, arg1, arg2, arg3, _functionPtr);
#else
            ReallocateDelegateIfNeed<T1, T2, T3, TR>();
            Assert.IsNotNull(_delegate);
            return ((Func<TTarget, T1, T2, T3, TR>)_delegate).Invoke(target, arg1, arg2, arg3);
#endif
        }
        
        public TR Invoke<T1, T2, T3, T4, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (IsStatic)
            {
                return ((delegate* <T1, T2, T3, T4, TR>)_functionPtr)(arg1, arg2, arg3, arg4);
            }
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            return FuncIL2CPP.Generic<TR, TTarget, T1, T2, T3, T4>(target, arg1, arg2, arg3, arg4, _functionPtr);
#else
            ReallocateDelegateIfNeed<T1, T2, T3, T4, TR>();
            Assert.IsNotNull(_delegate);
            return ((Func<TTarget, T1, T2, T3, T4, TR>)_delegate).Invoke(target, arg1, arg2, arg3, arg4);
#endif
        }
        
        public TR Invoke<T1, T2, T3, T4, T5, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            if (IsStatic)
            {
                return ((delegate* <T1, T2, T3, T4, T5, TR>)_functionPtr)(arg1, arg2, arg3, arg4, arg5);
            }
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            return FuncIL2CPP.Generic<TR, TTarget, T1, T2, T3, T4, T5>(target, arg1, arg2, arg3, arg4, arg5, _functionPtr);
#else
            ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, TR>();
            Assert.IsNotNull(_delegate);
            return ((Func<TTarget, T1, T2, T3, T4, T5, TR>)_delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5);
#endif
        }
        
        public TR Invoke<T1, T2, T3, T4, T5, T6, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            if (IsStatic)
            {
                return ((delegate* <T1, T2, T3, T4, T5, T6, TR>)_functionPtr)(arg1, arg2, arg3, arg4, arg5, arg6);
            }
#if ENABLE_IL2CPP && CERES_IL2CPP_OPTIMIZE
            return FuncIL2CPP.Generic<TR, TTarget, T1, T2, T3, T4, T5, T6>(target, arg1, arg2, arg3, arg4, arg5, arg6, _functionPtr);
#else
            ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, T6, TR>();
            Assert.IsNotNull(_delegate);
            return ((Func<TTarget, T1, T2, T3, T4, T5, T6, TR>)_delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5, arg6);
#endif
        }
    }
}