using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Graph.Flow.Annotations;
using Ceres.Utilities;
namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Derived from this class to add custom static functions
    /// </summary>
    /// <remarks>Must add partial modifier</remarks> 
    public abstract class ExecutableFunctionLibrary
    {
        /// <summary>
        /// Collect all static executable functions in this library
        /// </summary>
        protected virtual void CollectExecutableFunctions()
        {
            
        }

        protected ExecutableFunctionLibrary()
        {
            CollectExecutableFunctions();
        }
        
        /// <summary>
        /// Register static executable function to reflection system
        /// </summary>
        /// <param name="functionName"></param>
        /// <param name="parameterCount"></param>
        /// <param name="functionPtr"></param>
        /// <typeparam name="TLibrary"></typeparam>
        protected static unsafe void RegisterExecutableFunction<TLibrary>(string functionName, int parameterCount, void* functionPtr) 
            where TLibrary: ExecutableFunctionLibrary
        {
            ExecutableReflection<TLibrary>.RegisterStaticExecutableFunction(functionName, parameterCount, functionPtr);
        }
    }
    
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
            // Collect static functions
            var methodInfos = SubClassSearchUtility.FindSubClassTypes(typeof(ExecutableFunctionLibrary))
                        .SelectMany(x => x.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        .Where(x=>x.GetCustomAttribute<ExecutableFunctionAttribute>() != null)
                        .Distinct()
                        .ToList();
            var groups = methodInfos.GroupBy(ExecutableFunction.GetTargetType)
                .Where(x=>x.Key != null)
                .ToArray();
            _staticFunctions = methodInfos.Except(groups.SelectMany(x => x)).ToArray();
            _retargetFunctionTables = groups.ToDictionary(x => x.Key, x => x.ToArray());
            
            // Collect instance functions
            _instanceFunctionTables = AppDomain.CurrentDomain.GetAssemblies()
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
