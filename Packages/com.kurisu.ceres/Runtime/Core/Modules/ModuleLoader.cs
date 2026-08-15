using System;
using System.Linq;
using Ceres.Configs;
using UnityEngine;

namespace Ceres.Modules
{
    internal static class ModuleLoader
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeModules()
        {
            ConfigsModule.InitializeInternal();
            
            var config = ModuleConfig.Get();
            var modules = config.Modules.Length > 0
                ? GetModulesFromConfig(config)
                : GetModulesFromAssemblies();
            
            foreach (var module in modules)
            {
                module.Initialize();
            }
        }

        private static RuntimeModule[] GetModulesFromConfig(ModuleConfig config)
        {
            return config.Modules
                .Select(st => st.GetObjectType())
                .Where(type => type != null)
                .Select(type => (RuntimeModule)Activator.CreateInstance(type))
                .OrderBy(module => module.Order)
                .ToArray();
        }

        private static RuntimeModule[] GetModulesFromAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
#if UNITY_EDITOR
                .Where(assembly => !assembly.GetName().Name.Contains(".Editor"))
#endif
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(RuntimeModule).IsAssignableFrom(type) && !type.IsAbstract)
                .Select(type => (RuntimeModule)Activator.CreateInstance(type))
                .OrderBy(module => module.Order)
                .ToArray();
        }
    }
}