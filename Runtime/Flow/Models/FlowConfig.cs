using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Chris.Configs;
using UnityEngine;

namespace Ceres.Graph.Flow
{
    [Serializable]
    [ConfigPath("Ceres.Flow")]
    public class FlowConfig: Config<FlowConfig>
    {
        [SerializeField]
        internal string[] alwaysIncludedAssemblyWildcards = DefaultIncludedAssemblyWildcards.ToArray();

        internal static string[] DefaultIncludedAssemblyWildcards = 
        {
            "Unity.*",
            "UnityEngine",
            "UnityEngine.*"
        };

        private static Dictionary<Assembly, bool> _isIncluded = new();

        public static bool IsIncludedAssembly(Assembly assembly)
        {
            if (_isIncluded.TryGetValue(assembly, out var included))
            {
                return included;
            }
            var assemblyName = assembly.GetName().Name;
            if (assemblyName.Contains(".Editor"))
            {
                included = false;
            }
            else
            {
                included = Get().alwaysIncludedAssemblyWildcards.Any(card => IsMatchPattern(assemblyName, card));
            }
            _isIncluded.Add(assembly, included);
            return included;
        }

        private static bool IsMatchPattern(string assemblyName, string pattern)
        {
            string regexPattern = "^" + Regex.Escape(pattern).Replace(@"\*", ".*") + "$";
            return Regex.IsMatch(assemblyName, regexPattern, RegexOptions.IgnoreCase);
        }
    }
}