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

            // Collect static functions
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

                _staticFunctions = distinctStaticMethods.Except(grouped.SelectMany(x => x)).ToArray();
                _retargetFunctionTables = grouped.ToDictionary(x => x.Key, x => x.ToArray());
            }

            // Collect instance functions
            {
                var allAssemblies = referencedAssemblies.Concat(GetAlwaysIncludedAssemblies()).Distinct().ToList();
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

                _instanceFunctionTables = new Dictionary<Type, MethodInfo[]>(instanceFunctionDict);
            }

            // Collect always included properties
            {
                var alwaysIncludedAssemblies = GetAlwaysIncludedAssemblies();

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

                _alwaysIncludedPropertyInfos = results.ToArray();
            }
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
