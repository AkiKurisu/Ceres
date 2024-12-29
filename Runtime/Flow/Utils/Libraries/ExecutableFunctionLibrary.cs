using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using UnityEngine.Scripting;
using UObject = UnityEngine.Object;
namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Derived from this class to add custom static functions
    /// </summary>
    [Preserve]
    public abstract class ExecutableFunctionLibrary
    {

    }
    
    [Preserve]
    public class ExecutableFunctionRegistry
    {
        private readonly Dictionary<Type, MethodInfo[]> _libraryFunctionTables;

        private readonly Dictionary<Type, MethodInfo[]> _functionTables;
        
        private readonly MethodInfo[] _staticFunctions;

        private static ExecutableFunctionRegistry _instance;

        private ExecutableFunctionRegistry()
        {
            // Build library functions
            var methodInfos = SubClassSearchUtility.FindSubClassTypes(typeof(ExecutableFunctionLibrary))
                        .SelectMany(x => x.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        .Where(x=>x.GetCustomAttribute<ExecutableFunctionAttribute>() != null)
                        .Distinct()
                        .ToList();
            var groups = methodInfos.GroupBy(GetTargetType)
                .Where(x=>x.Key != null)
                .ToArray();
            _staticFunctions = methodInfos.Except(groups.SelectMany(x => x)).ToArray();
            _libraryFunctionTables = groups.ToDictionary(x => x.Key, x => x.ToArray());
            
            // Build managed functions
            _functionTables = SubClassSearchUtility.FindSubClassTypes(typeof(UObject))
                .Where(x=> GetExecutableFunctions(x).Any())
                .ToDictionary(x => x, x=> GetExecutableFunctions(x).ToArray());
        }

        private static IEnumerable<MethodInfo> GetExecutableFunctions(Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(methodInfo => methodInfo.GetCustomAttribute<ExecutableFunctionAttribute>() != null);
        }

        public static bool IsScriptMethod(MethodInfo methodInfo)
        {
            if (!methodInfo.IsStatic) return false;
            var parameters = methodInfo.GetParameters();
            if (parameters.Length < 1) return false;
                
            return methodInfo.GetCustomAttribute<ExecutableFunctionAttribute>().IsScriptMethod;
        }
        
        public static bool CanDisplayTarget(MethodInfo methodInfo)
        {
            if (!methodInfo.IsStatic) return false;
            var parameters = methodInfo.GetParameters();
            if (parameters.Length < 1) return false;

            var attribute = methodInfo.GetCustomAttribute<ExecutableFunctionAttribute>();
            if (attribute == null) return false;
            return attribute.IsScriptMethod && attribute.DisplayTarget;
        }
        
        public static bool IsNeedResolveReturnType(MethodInfo methodInfo)
        {
            var parameters = methodInfo.GetParameters();
            if (parameters.Length < 1) return false;
            if (methodInfo.ReturnType == typeof(void)) return false;
                
            return parameters.Any(x=> CeresMetadata.IsDefined(x, ExecutableFunction.RESOLVE_RETURN));
        }
        
        public static bool IsSelfTarget(MethodInfo methodInfo)
        {
            if (!methodInfo.IsStatic) return false;
            var parameters = methodInfo.GetParameters();
            if (parameters.Length < 1) return false;
                
            var attribute = methodInfo.GetCustomAttribute<ExecutableFunctionAttribute>();
            if (attribute == null) return false;
            return attribute.IsSelfTarget;
        }
        
        public static Type GetTargetType(MethodInfo methodInfo)
        {
            if (!methodInfo.IsStatic) return null;
            var parameters = methodInfo.GetParameters();
            if (parameters.Length < 1) return null;

            if (methodInfo.GetCustomAttribute<ExecutableFunctionAttribute>().IsScriptMethod)
            {
                return parameters[0].ParameterType;
            }

            return null;
        }
        
        public static ParameterInfo GetResolveReturnTypeParameter(MethodInfo methodInfo)
        {
            var parameters = methodInfo.GetParameters();
            if (parameters.Length < 1) return null;
            
            return parameters.First(x => CeresMetadata.IsDefined(x, ExecutableFunction.RESOLVE_RETURN));
        }

        public static string GetFunctionName(MethodInfo methodInfo, bool richText = true)
        {
            var labelAttribute = methodInfo.GetCustomAttribute<CeresLabelAttribute>();
            return labelAttribute != null ? labelAttribute.GetLabel(richText) : methodInfo.Name.Replace("Flow_", string.Empty);
        }

        public static ExecutableFunctionRegistry Get()
        {
            return _instance ??= new ExecutableFunctionRegistry();
        }

        public MethodInfo[] GetStaticFunctions()
        {
            return _staticFunctions;
        }
        
        public MethodInfo[] GetFunctions(Type type)
        {
            var functions = _functionTables.GetValueOrDefault(type) ?? Enumerable.Empty<MethodInfo>();
            return  functions.Concat(_libraryFunctionTables.Where(x=> type.IsAssignableTo(x.Key))
                            .SelectMany(x=>x.Value))
                            .ToArray();
        }

        public Type[] GetManagedTypes()
        {
            return _functionTables.Keys.ToArray();
        }
    }
}
