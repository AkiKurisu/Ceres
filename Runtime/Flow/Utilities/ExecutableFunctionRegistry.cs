using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ceres.Graph.Flow.Annotations;
using Ceres.Utilities;
using UnityEngine;

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
        /// Register static executable function pointer to the reflection system
        /// </summary>
        /// <param name="functionName"></param>
        /// <param name="parameterCount"></param>
        /// <param name="functionPtr"></param>
        /// <typeparam name="TLibrary"></typeparam>
        protected static unsafe void RegisterExecutableFunctionPtr<TLibrary>(string functionName, int parameterCount, void* functionPtr) 
            where TLibrary: ExecutableFunctionLibrary
        {
            ExecutableReflection<TLibrary>.RegisterStaticExecutableFunctionPtr(functionName, parameterCount, functionPtr);
        }
        
        /// <summary>
        /// Register static executable function file info to the reflection system, only works in editor and development build
        /// </summary>
        /// <param name="functionName"></param>
        /// <param name="parameterCount"></param>
        /// <param name="filePath"></param>
        /// <param name="lineNumber"></param>
        /// <typeparam name="TLibrary"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void RegisterExecutableFunctionFileInfo<TLibrary>(string functionName, int parameterCount, string filePath, int lineNumber) 
            where TLibrary: ExecutableFunctionLibrary
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            /* In editor, we use mono cecil instead */
            if (Application.isEditor) return;
            ExecutableReflection<TLibrary>.RegisterStaticExecutableFunctionFileInfo(functionName, parameterCount, filePath, lineNumber);
#endif
        }
    }
    
    /// <summary>
    /// Helper class for query executable functions
    /// </summary>
    internal class ExecutableFunctionRegistry
    {
        private readonly Dictionary<Type, MethodInfo[]> _retargetFunctionTables;

        private readonly Dictionary<Type, MethodInfo[]> _instanceFunctionTables;
        
        private readonly MethodInfo[] _staticFunctions;

        private static ExecutableFunctionRegistry _instance;

#if UNITY_EDITOR
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
#endif

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
