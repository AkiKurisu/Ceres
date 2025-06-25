using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ceres.Utilities;
using UnityEngine.Scripting;
using UObject = UnityEngine.Object;

namespace Ceres.Editor
{
    /// <summary>
    /// Global linker to manage runtime types that Ceres needs to be packed into the build.
    /// </summary>
    public static class CeresLinker
    {
        private static readonly Dictionary<Assembly, bool> SkippedAssemblyDict = new();

        private static readonly HashSet<Type> VisitedTypeSet = new();
        
        private static readonly List<Type> PreservedTypeList = new();
        
        /// <summary>
        /// Add runtime type to Ceres global linker.
        /// </summary>
        /// <param name="type"></param>
        public static void LinkType(Type type)
        {
            if (!VisitedTypeSet.Add(type))
            {
                return;
            }

            var assembly = type.Assembly;
            if (!SkippedAssemblyDict.TryGetValue(assembly, out var isSkipped))
            {
                // Already preserve by assembly.
                isSkipped = assembly.GetCustomAttribute<PreserveAttribute>() != null;
                isSkipped |= assembly.GetName().Name.Contains("mscorlib"); /* Not recommend to link system types in this way. */
                SkippedAssemblyDict.Add(assembly, isSkipped);
            }

            isSkipped |= type.GetCustomAttribute<PreserveAttribute>() != null;
            isSkipped |= type.GetCustomAttribute<CompilerGeneratedAttribute>() != null;
            isSkipped |= type.IsPrimitive || type.IsValueType || type.IsGenericType;
            isSkipped |= type.IsAssignableTo(typeof(UObject));
            
            if (isSkipped)
            {
                return;
            }
            
            PreservedTypeList.Add(type);
        }

        /// <summary>
        /// Add runtime types to Ceres global linker.
        /// </summary>
        public static void LinkTypes(Type[] types)
        {
            foreach (var type in types)
            {
                LinkType(type);
            }
        }

        /// <summary>
        /// Save linker data to settings.
        /// </summary>
        public static void Save()
        {
            foreach (var type in PreservedTypeList)
            {
                CeresSettings.AddPreservedType(type);
            }
            CeresSettings.SaveSettings();
        }
    }
}