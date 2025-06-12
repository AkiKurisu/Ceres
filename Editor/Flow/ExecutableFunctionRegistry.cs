using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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

        private static Assembly[] _alwaysIncludedAssemblies;
        
        private readonly (Type type, PropertyInfo[] propertyInfos)[] _alwaysIncludedPropertyInfos;
        
        private ExecutableFunctionRegistry()
        {
            var referencedAssemblies = SubClassSearchUtility.GetRuntimeReferencedAssemblies();
            var alwaysIncludedAssemblies = GetAlwaysIncludedAssemblies();

            Task<(MethodInfo[] staticFunctions, Dictionary<Type, MethodInfo[]> retargetFunctionTables)> staticTask = Task.Run(() =>
            {
                var staticMethodInfos = new ConcurrentBag<MethodInfo>();
                var staticTypes =
                    SubClassSearchUtility.FindSubClassTypes(referencedAssemblies, typeof(ExecutableFunctionLibrary));

                Parallel.ForEach(staticTypes, type =>
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Where(methodInfo => methodInfo.GetCustomAttribute<ExecutableFunctionAttribute>() != null);

                    foreach (var method in methods)
                    {
                        staticMethodInfos.Add(method);
                    }
                });

                var distinctStaticMethods = staticMethodInfos.Distinct().ToList();
                var grouped = distinctStaticMethods
                    .GroupBy(x => ExecutableReflection.GetFunction(x).Attribute.ScriptTargetType)
                    .Where(x => x.Key != null)
                    .ToArray();

                var staticFunctions = distinctStaticMethods.Except(grouped.SelectMany(x => x)).ToArray();
                var retargetFunctionTables = grouped.ToDictionary(x => x.Key, x => x.ToArray());
                return (staticFunctions, retargetFunctionTables);
            });

            Task<Dictionary<Type, MethodInfo[]>> instanceTask = Task.Run(() =>
            {
                var allAssemblies = referencedAssemblies.Concat(alwaysIncludedAssemblies).Distinct().ToList();
                var instanceFunctionDict = new ConcurrentDictionary<Type, MethodInfo[]>();

                Parallel.ForEach(allAssemblies, assembly =>
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsAbstract) continue;

                        var functions = ExecutableReflection.GetInstanceExecutableFunctions(type).ToArray();
                        if (functions.Any())
                        {
                            instanceFunctionDict[type] = functions;
                        }
                    }
                });

                return new Dictionary<Type, MethodInfo[]>(instanceFunctionDict);
            });

            Task<(Type type, PropertyInfo[] propertyInfos)[]> propertyTask = Task.Run(() =>
            {
                var results = new ConcurrentBag<(Type Type, PropertyInfo[] Properties)>();

                Parallel.ForEach(alwaysIncludedAssemblies, assembly =>
                {
                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t != null).ToArray();
                    }

                    foreach (var type in types)
                    {
                        if (!type.IsPublic) continue;

                        var properties = type
                            .GetProperties(BindingFlags.Public | BindingFlags.Static)
                            .Where(p => p.CanRead || p.CanWrite)
                            .ToArray();

                        if (properties.Length > 0)
                        {
                            results.Add((type, properties));
                        }
                    }
                });

                return results.ToArray();
            });

            Task.WaitAll(staticTask, instanceTask, propertyTask);

            _staticFunctions = staticTask.Result.staticFunctions;
            _retargetFunctionTables = staticTask.Result.retargetFunctionTables;
            _instanceFunctionTables = instanceTask.Result;
            _alwaysIncludedPropertyInfos = propertyTask.Result;
        }

        public (Type type, PropertyInfo[] propertyInfos)[] GetAlwaysIncludedProperties()
        {
            return _alwaysIncludedPropertyInfos;
        }

        public static Assembly[] GetAlwaysIncludedAssemblies()
        {
            _alwaysIncludedAssemblies ??= AppDomain.CurrentDomain.GetAssemblies()
                .Where(FlowRuntimeSettings.IsIncludedAssembly)
                .ToArray();
            return _alwaysIncludedAssemblies;
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
