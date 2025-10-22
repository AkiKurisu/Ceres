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
    /// Manage runtime types that Ceres needs to be packed into the build.
    /// </summary>
    public class CeresLinker
    {
        private readonly Dictionary<Assembly, bool> _skippedAssemblyDict = new();

        private readonly HashSet<Type> _visitedTypeSet = new();
        
        private readonly List<Type> _preservedTypeList = new();

        private readonly Func<Assembly, bool> _isAssemblyIncluded;

        public CeresLinker(Func<Assembly, bool> isAssemblyIncluded = null)
        {
            _isAssemblyIncluded = isAssemblyIncluded;
        }

        /// <summary>
        /// Add runtime type to Ceres global linker.
        /// </summary>
        /// <param name="type"></param>
        public void LinkType(Type type)
        {
            if (!_visitedTypeSet.Add(type))
            {
                return;
            }

            var assembly = type.Assembly;
            if (!_skippedAssemblyDict.TryGetValue(assembly, out var isSkipped))
            {
                // Already preserve by assembly.
                isSkipped = assembly.GetCustomAttribute<PreserveAttribute>() != null;
                isSkipped |= assembly.GetName().Name.Contains("mscorlib"); /* Not recommend to link system types in this way. */
                _skippedAssemblyDict.Add(assembly, isSkipped);
            }

            isSkipped |= type.GetCustomAttribute<PreserveAttribute>() != null;
            isSkipped |= type.GetCustomAttribute<CompilerGeneratedAttribute>() != null;
            isSkipped |= type.IsPrimitive || type.IsValueType || type.IsGenericType;
            
            if (type.IsAssignableTo(typeof(UObject)))
            {
                // Link always included UObjects only
                bool? isAlwaysIncluded = _isAssemblyIncluded?.Invoke(type.Assembly);
                isSkipped |= !isAlwaysIncluded ?? true;
            }
            
            if (isSkipped)
            {
                return;
            }
            
            _preservedTypeList.Add(type);
        }

        /// <summary>
        /// Add runtime types to Ceres global linker.
        /// </summary>
        public void LinkTypes(Type[] types)
        {
            foreach (var type in types)
            {
                LinkType(type);
            }
        }

        /// <summary>
        /// Save linker data to settings.
        /// </summary>
        public void Save()
        {
            foreach (var type in _preservedTypeList)
            {
                CeresSettings.AddPreservedType(type);
            }
            CeresSettings.SaveSettings();
        }
    }
}