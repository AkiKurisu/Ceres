using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Scripting;

namespace Ceres.Editor
{
    /// <summary>
    /// Global linker to manage runtime types that Ceres needs to be packed into the build.
    /// </summary>
    public static class CeresLinker
    {
        private static readonly Dictionary<Assembly, bool> SkippedAssemblyDict = new();

        private static readonly HashSet<Type> PreservedTypeSet = new();
        
        /// <summary>
        /// Add runtime type to Ceres global linker.
        /// </summary>
        /// <param name="type"></param>
        public static void LinkType(Type type)
        {
            if (PreservedTypeSet.Contains(type))
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
            
            if (isSkipped)
            {
                return;
            }
            
            PreservedTypeSet.Add(type);
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
        public static void SaveLinker()
        {
            foreach (var type in PreservedTypeSet)
            {
                CeresSettings.AddPreservedType(type);
            }
            CeresSettings.SaveSettings();
        }
    }
}