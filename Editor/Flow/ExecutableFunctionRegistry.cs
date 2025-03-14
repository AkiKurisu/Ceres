using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Annotations;
using Ceres.Graph.Flow.Utilities;
using Ceres.Utilities;

namespace Ceres.Editor.Graph.Flow
{
    /// <summary>
    /// Helper class for query executable functions
    /// </summary>
    public class ExecutableFunctionRegistry
    {
        private readonly Dictionary<Type, MethodInfo[]> _retargetFunctionTables;

        private readonly Dictionary<Type, MethodInfo[]> _instanceFunctionTables;
        
        private readonly MethodInfo[] _staticFunctions;

        private static ExecutableFunctionRegistry _instance;
        
        private ExecutableFunctionRegistry()
        {
            var referencedAssemblies = SubClassSearchUtility.GetRuntimeReferencedAssemblies();
            
            // Collect static functions
            var methodInfos = SubClassSearchUtility.FindSubClassTypes(referencedAssemblies, typeof(ExecutableFunctionLibrary))
                        .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        .Where(methodInfo=> methodInfo.GetCustomAttribute<ExecutableFunctionAttribute>() != null)
                        .Distinct()
                        .ToList();
            var groups = methodInfos.GroupBy(x => ExecutableReflection.GetFunction(x).Attribute.ScriptTargetType)
                .Where(x => x.Key != null)
                .ToArray();
            _staticFunctions = methodInfos.Except(groups.SelectMany(x => x)).ToArray();
            _retargetFunctionTables = groups.ToDictionary(x => x.Key, x => x.ToArray());
            
            // Collect instance functions
            _instanceFunctionTables = referencedAssemblies
                .SelectMany(a => a.GetTypes())
                .Where(x=> !x.IsAbstract && GetInstanceExecutableFunctions(x).Any())
                .ToDictionary(x => x, x=> GetInstanceExecutableFunctions(x).ToArray());
        }

        private static IEnumerable<MethodInfo> GetInstanceExecutableFunctions(Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(methodInfo => methodInfo.GetCustomAttribute<ExecutableFunctionAttribute>() != null);
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
            var functions = _instanceFunctionTables.GetValueOrDefault(type) ?? Enumerable.Empty<MethodInfo>();
            return functions.Concat(_retargetFunctionTables.Where(x=> type.IsAssignableTo(x.Key))
                            .SelectMany(x=>x.Value))
                            .ToArray();
        }

        public Type[] GetManagedTypes()
        {
            return _instanceFunctionTables.Keys.ToArray();
        }
    }
}
