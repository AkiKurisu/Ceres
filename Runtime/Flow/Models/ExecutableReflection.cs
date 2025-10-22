using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Ceres.Graph.Flow.Utilities;
using UnityEngine;
using UnityEngine.Assertions;

namespace Ceres.Graph.Flow
{
    public class InvalidExecutableFunctionException : Exception
    {
        public InvalidExecutableFunctionException(string message) : base($"[Ceres] {message}")
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
        PropertyGetter,
        
        /// <summary>
        /// Set method from static property
        /// </summary>
        StaticPropertySetter,
        
        /// <summary>
        /// Get method from static property
        /// </summary>
        StaticPropertyGetter
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
            CeresLogger.Assert(function != null, $"Can not get executable function {methodInfo} from {declareType} which is not expected");
            return function;
        }

        protected abstract ExecutableFunction GetFunction_Imp(MethodInfo methodInfo);

        protected static void RegisterReflection<T>(ExecutableReflection instance)
        {
            TypeMap.Add(typeof(T), instance);
        }

        internal static IEnumerable<MethodInfo> GetInstanceExecutableFunctions(Type type)
        {
            if (FlowConfig.IsIncludedAssembly(type.Assembly))
            {
                return type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(methodInfo =>
                    {
                        if (methodInfo.GetCustomAttribute<ExecutableFunctionAttribute>() != null) return true;
                        if (methodInfo.GetCustomAttribute<ObsoleteAttribute>() != null) return false;
                        if (NativeBindingUtility.IsNativeMethod(methodInfo)) return false;
                        return methodInfo.IsPublic && !methodInfo.IsSpecialName; // remove property getter and setter
                    });
            }
            return type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(methodInfo => methodInfo.GetCustomAttribute<ExecutableFunctionAttribute>() != null);
        }
    }

    public abstract class ExecutableFunction
    {
        public class ExecutableDocumentation
        {
            public string Summary { get; }

            public (string Name, string Summary)[] Parameters { get; }

            public string ReturnValue { get; }

            public override string ToString()
            {
                if (string.IsNullOrEmpty(Summary)) return string.Empty;
                var sb = new StringBuilder();
                sb.AppendLine(Summary);
                foreach (var parameter in Parameters)
                {
                    if (!string.IsNullOrEmpty(parameter.Summary))
                    {
                        sb.AppendLine($"{parameter.Name}: {parameter.Summary}");
                    }
                }

                if (!string.IsNullOrEmpty(ReturnValue))
                {
                    sb.AppendLine($"Return value: {ReturnValue}");
                }
                return sb.ToString();
            }

            internal ExecutableDocumentation(string sourceCodePath, int methodEntryLine)
            {
                if (string.IsNullOrEmpty(sourceCodePath))
                {
                    return;
                }
                string[] lines = File.ReadAllLines(sourceCodePath);
                int startLine = -1;

                for (int i = methodEntryLine - 1; i >= 0; i--)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        return;
                    }
                    if (lines[i].Trim().StartsWith("/// <summary>"))
                    {
                        startLine = i;
                        break;
                    }
                }

                if (startLine == -1) return;

                string summary = "";
                bool isSummary = false;
                var parameters = new List<(string, string)>();

                for (int i = startLine; i < methodEntryLine; i++)
                {
                    string line = lines[i].Trim();
                    if (line.StartsWith("///"))
                    {
                        line = Regex.Replace(line, "^/// ?", "");

                        if (line.StartsWith("<summary>"))
                        {
                            isSummary = true;
                            summary += Regex.Replace(line, "<summary>", "").Trim() + " ";
                        }
                        else if (line.StartsWith("</summary>"))
                        {
                            isSummary = false;
                        }
                        else if (isSummary)
                        {
                            summary += line + " ";
                        }
                        else if (line.StartsWith("<param name=\""))
                        {
                            var match = Regex.Match(line, "<param name=\"(.*?)\">(.*?)");
                            if (match.Success)
                            {
                                parameters.Add((match.Groups[1].Value, CleanXmlCrefLabel(match.Groups[2].Value)));
                            }
                        }
                        else if (line.StartsWith("<returns>"))
                        {
                            var returnValue = Regex.Replace(line, "<.*?>", "").Trim();
                            if (!string.IsNullOrEmpty(returnValue))
                            {
                                ReturnValue = CleanXmlCrefLabel(returnValue);
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                Parameters = parameters.ToArray();
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    Summary = CleanXmlCrefLabel(summary.Trim());
                }
            }

            private static string CleanXmlCrefLabel(string input)
            {
                const string pattern = @"<see\s+cref=""([^""]+)""\s*/>";
                return Regex.Replace(input, pattern, match => match.Groups[1].Value);
            }
        }

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

        public readonly MethodInfo MethodInfo;

#if DEVELOPMENT_BUILD || UNITY_EDITOR

        private ExecutableAttribute _attribute;
        
        /// <summary>
        /// Attribute metadata for editor lookup, should not access it at runtime
        /// </summary>
        internal ExecutableAttribute Attribute => _attribute ??= new ExecutableAttribute(MethodInfo);
        
        internal string FilePath;

        internal int LineNumber;

        internal ExecutableDocumentation Documentation;
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

    public readonly struct ExecutableFunctionInfo : IEquatable<ExecutableFunctionInfo>
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

    public class ExecutableReflection<TTarget> : ExecutableReflection
    {
        public class ExecutableFunction : Flow.ExecutableFunction
        {
            public readonly ExecutableFunctionInfo FunctionInfo;

            internal readonly ExecutableAction<TTarget> ExecutableAction;

            internal readonly ExecutableFunc<TTarget> ExecutableFunc;

            internal ExecutableFunction(ExecutableFunctionInfo functionInfo, MethodInfo methodInfo) : base(methodInfo)
            {
                FunctionInfo = functionInfo;
                ExecutableAction = new ExecutableAction<TTarget>(MethodInfo);
                ExecutableFunc = new ExecutableFunc<TTarget>(MethodInfo);
            }

            internal ExecutableFunction(ExecutableFunctionInfo functionInfo, IntPtr functionPtr, bool isStatic) : base(null)
            {
                FunctionInfo = functionInfo;
                ExecutableAction = new ExecutableAction<TTarget>(functionPtr, isStatic);
                ExecutableFunc = new ExecutableFunc<TTarget>(functionPtr, isStatic);
            }

            internal ExecutableFunction(ExecutableFunctionInfo functionInfo, MethodInfo methodInfo, IntPtr functionPtr) : base(methodInfo)
            {
                FunctionInfo = functionInfo;
                ExecutableAction = new ExecutableAction<TTarget>(functionPtr, true);
                ExecutableFunc = new ExecutableFunc<TTarget>(functionPtr, true);
            }
        }

        private readonly List<ExecutableFunction> _functions = new();

#if ENABLE_IL2CPP
        private readonly IntPtr _il2cppClass;

        private bool _isAlwaysIncluded;
#endif

        private ExecutableReflection()
        {
            _instance = this;
            var targetType = typeof(TTarget);
            RegisterReflection<TTarget>(_instance);
            if (targetType.IsSubclassOf(typeof(ExecutableFunctionLibrary)))
            {
#if UNITY_EDITOR
                targetType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Where(x => x.GetCustomAttribute<ExecutableFunctionAttribute>() != null)
                    .ToList()
                    .ForEach(methodInfo =>
                    {
                        RegisterExecutableFunction(ExecutableFunctionType.StaticMethod, methodInfo);
                    });
#endif
                Activator.CreateInstance(targetType);
                return;
            }

#if ENABLE_IL2CPP
            _isAlwaysIncluded = FlowConfig.IsIncludedAssembly(targetType.Assembly);
            // We haven't injected IL in always included assembly, use legacy way in this case.
            if (!_isAlwaysIncluded)
            {
#if UNITY_STANDALONE_WIN
                string assemblyName = targetType.Module.Name;
                string @namespace = targetType.Namespace ?? string.Empty;
                string className = targetType.Name;
                _il2cppClass = IL2CPP.GetIl2CppClass(assemblyName, @namespace, className);
#else
                targetType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                                .Where(x=> x.GetCustomAttribute<ExecutableFunctionAttribute>() != null)
                                .ToList()
                                .ForEach(RegisterExecutableFunctionInvoker);
#endif
                return;
            }
#endif
            
            GetInstanceExecutableFunctions(targetType)
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

#if ENABLE_IL2CPP && !UNITY_STANDALONE_WIN
        private void RegisterExecutableFunctionInvoker(MethodInfo methodInfo)
        {
            var functionInfo = new ExecutableFunctionInfo(ExecutableFunctionType.InstanceMethod,
                methodInfo.Name[7..], // Strip "Invoke_"
                methodInfo.GetParameters().Length - 1); // Skip self parameter
            var functionPtr = methodInfo.MethodHandle.Value;
            var functionStructure = new ExecutableFunction(functionInfo, functionPtr, false);
            _functions.Add(functionStructure);
        }
#endif

        private void RegisterExecutableFunction(ExecutableFunctionType functionType, MethodInfo methodInfo)
        {
            var functionInfo = new ExecutableFunctionInfo(functionType, methodInfo.Name, methodInfo.GetParameters().Length);
#if UNITY_EDITOR && DEBUG
            CeresLogger.Log($"{typeof(TTarget).Name} RegisterExecutableFunction {functionInfo}");
#endif
            var functionStructure = new ExecutableFunction(functionInfo, methodInfo);
            _functions.Add(functionStructure);
        }

        internal static void RegisterStaticExecutableFunctionPtr(string functionName, int parameterCount, IntPtr functionPtr)
        {
            var functionInfo = new ExecutableFunctionInfo(ExecutableFunctionType.StaticMethod, functionName, parameterCount);
#if UNITY_EDITOR && DEBUG
            CeresLogger.Log($"{typeof(TTarget).Name} RegisterStaticExecutableFunctionPtr {functionInfo}");
#endif
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
            var functionStructure = new ExecutableFunction(functionInfo, functionPtr, true);
            Instance._functions.Add(functionStructure);
        }

        private ExecutableFunction FindFunction_Internal(ExecutableFunctionInfo functionInfo)
        {
            /* Ambiguous search */
            if (functionInfo.ParameterCount < 0)
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
        
        private static bool IsStaticPropertyMethod(ExecutableFunctionType functionType)
        {
            return functionType is ExecutableFunctionType.StaticPropertyGetter or ExecutableFunctionType.StaticPropertySetter;
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

#if ENABLE_IL2CPP && UNITY_STANDALONE_WIN
            unsafe
            {
                if (functionType == ExecutableFunctionType.InstanceMethod && !_isAlwaysIncluded)
                {
                    // Find invoker
                    int invokeParameterCount = functionInfo.ParameterCount >=0 ? functionInfo.ParameterCount + 1 : -1;
                    var ptr = IL2CPP.GetIl2CppMethod(_il2cppClass, $"Invoke_{functionName}", invokeParameterCount);
                    if (ptr == IntPtr.Zero)
                    {
                        throw new InvalidExecutableFunctionException($"Can not find executable function from {nameof(ExecutableFunctionInfo)} [{functionInfo}]");
                    }
                    functionStructure = new ExecutableFunction(functionInfo, ptr, false);
                    _functions.Add(functionStructure);
                    return functionStructure;
                }
            }
#endif

            var methodInfo = functionType switch
            {
                ExecutableFunctionType.PropertySetter => typeof(TTarget).GetProperty(functionName,
                    BindingFlags.Public | BindingFlags.Instance)?.SetMethod,
                ExecutableFunctionType.PropertyGetter => typeof(TTarget).GetProperty(functionName,
                    BindingFlags.Public | BindingFlags.Instance)?.GetMethod,
                ExecutableFunctionType.StaticPropertySetter => typeof(TTarget).GetProperty(functionName,
                    BindingFlags.Public | BindingFlags.Static)?.SetMethod,
                ExecutableFunctionType.StaticPropertyGetter => typeof(TTarget).GetProperty(functionName,
                    BindingFlags.Public | BindingFlags.Static)?.GetMethod,
                ExecutableFunctionType.InstanceMethod or ExecutableFunctionType.StaticMethod => null,
                _ => null
            };

            if (methodInfo == null)
            {
                throw new InvalidExecutableFunctionException($"Can not find executable function from {nameof(ExecutableFunctionInfo)} [{functionInfo}]");
            }
            
            if (IsStaticPropertyMethod(functionType))
            {
                Assert.IsTrue(methodInfo.IsStatic);
                // TODO: Weave a wrapper for static property setter and getter to use function ptr
                functionStructure = new ExecutableFunction(functionInfo, methodInfo);
            }
            else
            {
                functionStructure = new ExecutableFunction(functionInfo, methodInfo);
            }
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
        private Delegate _delegate;

        private readonly MethodInfo _methodInfo;

        private readonly IntPtr _functionPtr;

        public readonly bool IsStatic;

        internal ExecutableAction(MethodInfo methodInfo)
        {
            _functionPtr = IntPtr.Zero;
            _methodInfo = methodInfo;
            IsStatic = methodInfo.IsStatic;
        }

        internal ExecutableAction(IntPtr functionPtr, bool isStatic)
        {
            IsStatic = isStatic;
            _functionPtr = functionPtr;
        }

        private static void ReallocateDelegateIfNeed<TDelegate>(ref Delegate outDelegate, MethodInfo methodInfo) where TDelegate : Delegate
        {
            if (methodInfo == null)
            {
                return;
            }
            try
            {
                if (outDelegate is TDelegate) return;
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
        
        private void ReallocateStaticDelegateIfNeed()
        {
            ReallocateDelegateIfNeed<Action>(ref _delegate, _methodInfo);
        }

        private void ReallocateStaticDelegateIfNeed<T1>()
        {
            ReallocateDelegateIfNeed<Action<T1>>(ref _delegate, _methodInfo);
        }

        private void ReallocateStaticDelegateIfNeed<T1, T2>()
        {
            ReallocateDelegateIfNeed<Action<T1, T2>>(ref _delegate, _methodInfo);
        }

        private void ReallocateStaticDelegateIfNeed<T1, T2, T3>()
        {
            ReallocateDelegateIfNeed<Action<T1, T2, T3>>(ref _delegate, _methodInfo);
        }

        private void ReallocateStaticDelegateIfNeed<T1, T2, T3, T4>()
        {
            ReallocateDelegateIfNeed<Action<T1, T2, T3, T4>>(ref _delegate, _methodInfo);
        }

        private void ReallocateStaticDelegateIfNeed<T1, T2, T3, T4, T5>()
        {
            ReallocateDelegateIfNeed<Action<T1, T2, T3, T4, T5>>(ref _delegate, _methodInfo);
        }

        private void ReallocateStaticDelegateIfNeed<T1, T2, T3, T4, T5, T6>()
        {
            ReallocateDelegateIfNeed<Action<T1, T2, T3, T4, T5, T6>>(ref _delegate, _methodInfo);
        }

        public void Invoke(TTarget target)
        {
            if (IsStatic)
            {
                if (_functionPtr != IntPtr.Zero)
                {
                    ((delegate*<void>)_functionPtr)();
                    return;
                }
                ReallocateStaticDelegateIfNeed();
                Assert.IsNotNull(_delegate);
                ((Action)_delegate).Invoke();
                return;
            }
#if ENABLE_IL2CPP
            if (_functionPtr != IntPtr.Zero)
            {
                ((delegate* <TTarget, void>)_functionPtr)(target);
                return;
            }
#endif
            ReallocateDelegateIfNeed();
            Assert.IsNotNull(_delegate);
            ((Action<TTarget>)_delegate).Invoke(target);
        }

        public void Invoke<T1>(TTarget target, T1 arg1)
        {
            if (IsStatic)
            {
                if (_functionPtr != IntPtr.Zero)
                {
                    ((delegate*<T1, void>)_functionPtr)(arg1);
                    return;
                }
                ReallocateStaticDelegateIfNeed<T1>();
                Assert.IsNotNull(_delegate);
                ((Action<T1>)_delegate).Invoke(arg1);
                return;
            }
#if ENABLE_IL2CPP
            if (_functionPtr != IntPtr.Zero)
            {
                ((delegate* <TTarget, T1, void>)_functionPtr)(target, arg1);
                return;
            }
#endif
            ReallocateDelegateIfNeed<T1>();
            Assert.IsNotNull(_delegate);
            ((Action<TTarget, T1>)_delegate).Invoke(target, arg1);
        }

        public void Invoke<T1, T2>(TTarget target, T1 arg1, T2 arg2)
        {
            if (IsStatic)
            {
                if (_functionPtr != IntPtr.Zero)
                {
                    ((delegate*<T1, T2, void>)_functionPtr)(arg1, arg2);
                    return;
                }
                ReallocateStaticDelegateIfNeed<T1, T2>();
                Assert.IsNotNull(_delegate);
                ((Action<T1, T2>)_delegate).Invoke(arg1, arg2);
                return;
            }
#if ENABLE_IL2CPP
            if (_functionPtr != IntPtr.Zero)
            {
                ((delegate* <TTarget, T1, T2, void>)_functionPtr)(target, arg1, arg2);
                return;
            }
#endif
            ReallocateDelegateIfNeed<T1, T2>();
            Assert.IsNotNull(_delegate);
            ((Action<TTarget, T1, T2>)_delegate).Invoke(target, arg1, arg2);
        }

        public void Invoke<T1, T2, T3>(TTarget target, T1 arg1, T2 arg2, T3 arg3)
        {
            if (IsStatic)
            {
                if (_functionPtr != IntPtr.Zero)
                {
                    ((delegate*<T1, T2, T3, void>)_functionPtr)(arg1, arg2, arg3);
                    return;
                }
                ReallocateStaticDelegateIfNeed<T1, T2, T3>();
                Assert.IsNotNull(_delegate);
                ((Action<T1, T2, T3>)_delegate).Invoke(arg1, arg2, arg3);
                return;
            }
#if ENABLE_IL2CPP
            if (_functionPtr != IntPtr.Zero)
            {
                ((delegate* <TTarget, T1, T2, T3, void>)_functionPtr)(target, arg1, arg2, arg3);
                return;
            }
#endif
            ReallocateDelegateIfNeed<T1, T2, T3>();
            Assert.IsNotNull(_delegate);
            ((Action<TTarget, T1, T2, T3>)_delegate).Invoke(target, arg1, arg2, arg3);
        }

        public void Invoke<T1, T2, T3, T4>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (IsStatic)
            {
                if (_functionPtr != IntPtr.Zero)
                {
                    ((delegate*<T1, T2, T3, T4, void>)_functionPtr)(arg1, arg2, arg3, arg4);
                    return;
                }
                ReallocateStaticDelegateIfNeed<T1, T2, T3, T4>();
                Assert.IsNotNull(_delegate);
                ((Action<T1, T2, T3, T4>)_delegate).Invoke(arg1, arg2, arg3, arg4);
                return;
            }
#if ENABLE_IL2CPP
            if (_functionPtr != IntPtr.Zero)
            {
                ((delegate* <TTarget, T1, T2, T3, T4, void>)_functionPtr)(target, arg1, arg2, arg3, arg4);
                return;
            }
#endif
            ReallocateDelegateIfNeed<T1, T2, T3, T4>();
            Assert.IsNotNull(_delegate);
            ((Action<TTarget, T1, T2, T3, T4>)_delegate).Invoke(target, arg1, arg2, arg3, arg4);
        }

        public void Invoke<T1, T2, T3, T4, T5>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            if (IsStatic)
            {
                if (_functionPtr != IntPtr.Zero)
                {
                    ((delegate*<T1, T2, T3, T4, T5, void>)_functionPtr)(arg1, arg2, arg3, arg4, arg5);
                    return;
                }
                ReallocateStaticDelegateIfNeed<T1, T2, T3, T4, T5>();
                Assert.IsNotNull(_delegate);
                ((Action<T1, T2, T3, T4, T5>)_delegate).Invoke(arg1, arg2, arg3, arg4, arg5);
                return;
            }
#if ENABLE_IL2CPP
            if (_functionPtr != IntPtr.Zero)
            {
                ((delegate* <TTarget, T1, T2, T3, T4, T5, void>)_functionPtr)(target, arg1, arg2, arg3, arg4, arg5);
                return;
            }
#endif
            ReallocateDelegateIfNeed<T1, T2, T3, T4, T5>();
            Assert.IsNotNull(_delegate);
            ((Action<TTarget, T1, T2, T3, T4, T5>)_delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5);
        }

        public void Invoke<T1, T2, T3, T4, T5, T6>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            if (IsStatic)
            {
                if (_functionPtr != IntPtr.Zero)
                {
                    ((delegate*<T1, T2, T3, T4, T5, T6, void>)_functionPtr)(arg1, arg2, arg3, arg4, arg5, arg6);
                    return;
                }
                ReallocateStaticDelegateIfNeed<T1, T2, T3, T4, T5, T6>();
                Assert.IsNotNull(_delegate);
                ((Action<T1, T2, T3, T4, T5, T6>)_delegate).Invoke(arg1, arg2, arg3, arg4, arg5, arg6);
                return;
            }
#if ENABLE_IL2CPP
            if (_functionPtr != IntPtr.Zero)
            {
                ((delegate* <TTarget, T1, T2, T3, T4, T5, T6, void>)_functionPtr)(target, arg1, arg2, arg3, arg4, arg5, arg6);
                return;
            }
#endif
            ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, T6>();
            Assert.IsNotNull(_delegate);
            ((Action<TTarget, T1, T2, T3, T4, T5, T6>)_delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5, arg6);
        }
    }

    internal unsafe class ExecutableFunc<TTarget>
    {
        private Delegate _delegate;

        private readonly MethodInfo _methodInfo;

        private readonly IntPtr _functionPtr;

        public readonly bool IsStatic;

        internal ExecutableFunc(MethodInfo methodInfo)
        {
            _functionPtr = IntPtr.Zero;
            _methodInfo = methodInfo;
            IsStatic = methodInfo.IsStatic;
        }

        internal ExecutableFunc(IntPtr functionPtr, bool isStatic)
        {
            IsStatic = isStatic;
            _functionPtr = functionPtr;
        }

        private static void ReallocateDelegateIfNeed<TDelegate>(ref Delegate outDelegate, MethodInfo methodInfo) where TDelegate : Delegate
        {
            if (methodInfo == null)
            {
                return;
            }
            try
            {
                if (outDelegate is TDelegate) return;
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
        
        private void ReallocateStaticDelegateIfNeed<TR>()
        {
            ReallocateDelegateIfNeed<Func<TR>>(ref _delegate, _methodInfo);
        }

        private void ReallocateStaticDelegateIfNeed<T1, TR>()
        {
            ReallocateDelegateIfNeed<Func<T1, TR>>(ref _delegate, _methodInfo);
        }

        private void ReallocateStaticDelegateIfNeed<T1, T2, TR>()
        {
            ReallocateDelegateIfNeed<Func<T1, T2, TR>>(ref _delegate, _methodInfo);
        }

        private void ReallocateStaticDelegateIfNeed<T1, T2, T3, TR>()
        {
            ReallocateDelegateIfNeed<Func<T1, T2, T3, TR>>(ref _delegate, _methodInfo);
        }

        private void ReallocateStaticDelegateIfNeed<T1, T2, T3, T4, TR>()
        {
            ReallocateDelegateIfNeed<Func<T1, T2, T3, T4, TR>>(ref _delegate, _methodInfo);
        }

        private void ReallocateStaticDelegateIfNeed<T1, T2, T3, T4, T5, TR>()
        {
            ReallocateDelegateIfNeed<Func<T1, T2, T3, T4, T5, TR>>(ref _delegate, _methodInfo);
        }

        private void ReallocateStaticDelegateIfNeed<T1, T2, T3, T4, T5, T6, TR>()
        {
            ReallocateDelegateIfNeed<Func<T1, T2, T3, T4, T5, T6, TR>>(ref _delegate, _methodInfo);
        }

        public TR Invoke<TR>(TTarget target)
        {
            if (IsStatic)
            {
                if (_functionPtr != IntPtr.Zero)
                {
                    return ((delegate*<TR>)_functionPtr)();
                }
                ReallocateStaticDelegateIfNeed<TR>();
                Assert.IsNotNull(_delegate);
                return ((Func<TR>)_delegate).Invoke();
            }
#if ENABLE_IL2CPP
            if (_functionPtr != IntPtr.Zero)
            {
                return ((delegate* <TTarget, TR>)_functionPtr)(target);
            }
#endif
            ReallocateDelegateIfNeed<TR>();
            Assert.IsNotNull(_delegate);
            return ((Func<TTarget, TR>)_delegate).Invoke(target);
        }

        public TR Invoke<T1, TR>(TTarget target, T1 arg1)
        {
            if (IsStatic)
            {
                if (_functionPtr != IntPtr.Zero)
                {
                    return ((delegate*<T1, TR>)_functionPtr)(arg1);
                }
                ReallocateStaticDelegateIfNeed<T1, TR>();
                Assert.IsNotNull(_delegate);
                return ((Func<T1, TR>)_delegate).Invoke(arg1);
            }
#if ENABLE_IL2CPP
            if (_functionPtr != IntPtr.Zero)
            {
                return ((delegate* <TTarget, T1, TR>)_functionPtr)(target, arg1);
            }
#endif
            ReallocateDelegateIfNeed<T1, TR>();
            Assert.IsNotNull(_delegate);
            return ((Func<TTarget, T1, TR>)_delegate).Invoke(target, arg1);
        }

        public TR Invoke<T1, T2, TR>(TTarget target, T1 arg1, T2 arg2)
        {
            if (IsStatic)
            {
                if (_functionPtr != IntPtr.Zero)
                {
                    return ((delegate*<T1, T2, TR>)_functionPtr)(arg1, arg2);
                }
                ReallocateStaticDelegateIfNeed<T1, T2, TR>();
                Assert.IsNotNull(_delegate);
                return ((Func<T1, T2, TR>)_delegate).Invoke(arg1, arg2);
            }
#if ENABLE_IL2CPP
            if (_functionPtr != IntPtr.Zero)
            {
                return ((delegate* <TTarget, T1, T2, TR>)_functionPtr)(target, arg1, arg2);
            }
#endif
            ReallocateDelegateIfNeed<T1, T2, TR>();
            Assert.IsNotNull(_delegate);
            return ((Func<TTarget, T1, T2, TR>)_delegate).Invoke(target, arg1, arg2);
        }

        public TR Invoke<T1, T2, T3, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3)
        {
            if (IsStatic)
            {
                if (_functionPtr != IntPtr.Zero)
                {
                    return ((delegate*<T1, T2, T3, TR>)_functionPtr)(arg1, arg2, arg3);
                }
                ReallocateStaticDelegateIfNeed<T1, T2, T3, TR>();
                Assert.IsNotNull(_delegate);
                return ((Func<T1, T2, T3, TR>)_delegate).Invoke(arg1, arg2, arg3);
            }
#if ENABLE_IL2CPP
            if (_functionPtr != IntPtr.Zero)
            {
                return ((delegate* <TTarget, T1, T2, T3, TR>)_functionPtr)(target, arg1, arg2, arg3);
            }
#endif
            ReallocateDelegateIfNeed<T1, T2, T3, TR>();
            Assert.IsNotNull(_delegate);
            return ((Func<TTarget, T1, T2, T3, TR>)_delegate).Invoke(target, arg1, arg2, arg3);
        }

        public TR Invoke<T1, T2, T3, T4, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (IsStatic)
            {
                if (_functionPtr != IntPtr.Zero)
                {
                    return ((delegate*<T1, T2, T3, T4, TR>)_functionPtr)(arg1, arg2, arg3, arg4);
                }
                ReallocateStaticDelegateIfNeed<T1, T2, T3, T4, TR>();
                Assert.IsNotNull(_delegate);
                return ((Func<T1, T2, T3, T4, TR>)_delegate).Invoke(arg1, arg2, arg3, arg4);
            }
#if ENABLE_IL2CPP
            if (_functionPtr != IntPtr.Zero)
            {
                return ((delegate* <TTarget, T1, T2, T3, T4, TR>)_functionPtr)(target, arg1, arg2, arg3, arg4);
            }
#endif
            ReallocateDelegateIfNeed<T1, T2, T3, T4, TR>();
            Assert.IsNotNull(_delegate);
            return ((Func<TTarget, T1, T2, T3, T4, TR>)_delegate).Invoke(target, arg1, arg2, arg3, arg4);
        }

        public TR Invoke<T1, T2, T3, T4, T5, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            if (IsStatic)
            {
                if (_functionPtr != IntPtr.Zero)
                {
                    return ((delegate*<T1, T2, T3, T4, T5, TR>)_functionPtr)(arg1, arg2, arg3, arg4, arg5);
                }
                ReallocateStaticDelegateIfNeed<T1, T2, T3, T4, T5, TR>();
                Assert.IsNotNull(_delegate);
                return ((Func<T1, T2, T3, T4, T5, TR>)_delegate).Invoke(arg1, arg2, arg3, arg4, arg5);
            }
#if ENABLE_IL2CPP
            if (_functionPtr != IntPtr.Zero)
            {
                return ((delegate* <TTarget, T1, T2, T3, T4, T5, TR>)_functionPtr)(target, arg1, arg2, arg3, arg4, arg5);
            }
#endif
            ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, TR>();
            Assert.IsNotNull(_delegate);
            return ((Func<TTarget, T1, T2, T3, T4, T5, TR>)_delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5);
        }

        public TR Invoke<T1, T2, T3, T4, T5, T6, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            if (IsStatic)
            {
                if (_functionPtr != IntPtr.Zero)
                {
                    return ((delegate*<T1, T2, T3, T4, T5, T6, TR>)_functionPtr)(arg1, arg2, arg3, arg4, arg5, arg6);
                }
                ReallocateStaticDelegateIfNeed<T1, T2, T3, T4, T5, T6, TR>();
                Assert.IsNotNull(_delegate);
                return ((Func<T1, T2, T3, T4, T5, T6, TR>)_delegate).Invoke(arg1, arg2, arg3, arg4, arg5, arg6);
            }
#if ENABLE_IL2CPP
            if (_functionPtr != IntPtr.Zero)
            {
                return ((delegate* <TTarget, T1, T2, T3, T4, T5, T6, TR>)_functionPtr)(target, arg1, arg2, arg3, arg4, arg5, arg6);
            }
#endif
            ReallocateDelegateIfNeed<T1, T2, T3, T4, T5, T6, TR>();
            Assert.IsNotNull(_delegate);
            return ((Func<TTarget, T1, T2, T3, T4, T5, T6, TR>)_delegate).Invoke(target, arg1, arg2, arg3, arg4, arg5, arg6);
        }
    }
}
