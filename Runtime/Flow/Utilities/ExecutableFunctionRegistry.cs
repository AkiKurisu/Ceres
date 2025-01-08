using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Ceres.Utilities;
namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Derived from this class to add custom static functions
    /// </summary>
    public abstract class ExecutableFunctionLibrary
    {

    }
    
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
            var groups = methodInfos.GroupBy(ExecutableFunction.GetTargetType)
                .Where(x=>x.Key != null)
                .ToArray();
            _staticFunctions = methodInfos.Except(groups.SelectMany(x => x)).ToArray();
            _libraryFunctionTables = groups.ToDictionary(x => x.Key, x => x.ToArray());
            
            // Build managed functions
            _functionTables = SubClassSearchUtility.FindSubClassTypes(typeof(object))
                .Where(x=> GetExecutableFunctions(x).Any())
                .ToDictionary(x => x, x=> GetExecutableFunctions(x).ToArray());
        }

        private static IEnumerable<MethodInfo> GetExecutableFunctions(Type type)
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
