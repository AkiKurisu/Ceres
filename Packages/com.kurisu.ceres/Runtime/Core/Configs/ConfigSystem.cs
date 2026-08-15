using System.Collections.Generic;
using Ceres.Serialization;
using Newtonsoft.Json;
using R3.Ceres;

namespace Ceres.Configs
{
    public static class ConfigSystem
    {
        private readonly struct ConfigFileProviderStructure
        {
            public readonly IConfigFileProvider FileProvider;

            public readonly int Priority;

            public ConfigFileProviderStructure(IConfigFileProvider fileProvider, int priority)
            {
                FileProvider = fileProvider;
                Priority = priority;
            }
        }

        private static readonly List<ConfigFileProviderStructure> ConfigFileProviders = new();

        private static readonly Dictionary<ulong, ConfigBase> ConfigCache = new();

        private static readonly Dictionary<string, IConfigFile> ConfigFileCache = new();
        
        private static readonly JsonSerializerSettings SerializerSettings;
        
        static ConfigSystem()
        {
            // Register ReactivePropertyConverter used in Config
            if (JsonConvert.DefaultSettings == null)
            {
                SerializerSettings = new JsonSerializerSettings();
                JsonConvert.DefaultSettings = GetDefaultSettings;
            }
            else
            {
                SerializerSettings = JsonConvert.DefaultSettings();
            }
            SerializerSettings.Converters.Add(new ReactivePropertyConverter());
            
            // Register config providers
#if UNITY_EDITOR
            RegisterConfigFileProvider(new EditorConfigFileProvider(), 200);        // Platform specific
            RegisterConfigFileProvider(new EditorBaseConfigFileProvider(), 300);    // Project Base
#else
            RegisterConfigFileProvider(new StreamingConfigFileProvider(), 200);
#endif
            RegisterConfigFileProvider(new PersistentConfigFileProvider(), 100);

            // Pre-load ConfigsConfig to initialize serializer configuration
            var location = Config<ConfigsConfig>.Location;
            var projectConfigFile = GetProjectConfigFile(location.FileLocation);
            ConfigsConfig internalConfig = null;
            if (projectConfigFile.TryGetConfig(location, out var config))
            {
                internalConfig = config as ConfigsConfig;
            }
            internalConfig ??= new ConfigsConfig();
            var serializedType = internalConfig.configSerializer;
            ConfigsModule.ConfigSerializer = serializedType.GetObject() ?? TextSerializeFormatter.Instance;
        }

        internal static void ClearCache()
        {
            ConfigFileCache.Clear();
            ConfigCache.Clear();
        }
        
        private static JsonSerializerSettings GetDefaultSettings()
        {
            return SerializerSettings;
        }

        /// <summary>
        /// Get global config
        /// </summary>
        /// <typeparam name="TConfig"></typeparam>
        /// <returns></returns>
        public static TConfig GetConfig<TConfig>() where TConfig : Config<TConfig>, new()
        {
            // Try get from config cache
            ulong id = Config<TConfig>.TypeId;
            if (ConfigCache.TryGetValue(id, out var config))
            {
                return (TConfig)config;
            }
            var location = Config<TConfig>.Location;
            var globalConfig = GetConfigFromFileCache<TConfig>(location);
            ConfigCache.Add(id, globalConfig);
            return globalConfig;
        }

        /// <summary>
        /// Get config from cache by <see cref="IConfigLocation"/>
        /// </summary>
        /// <param name="location"></param>
        /// <typeparam name="TConfig"></typeparam>
        /// <returns></returns>
        private static TConfig GetConfigFromFileCache<TConfig>(IConfigLocation location) where TConfig : Config<TConfig>, new()
        {
            var configFile = GetConfigFile(location.FileLocation);

            // Try to get config
            if (configFile.TryGetConfig(location, out var config))
            {
                return (TConfig)config;
            }

            // Return class default object
            return new TConfig();
        }

        /// <summary>
        /// Merge config files from registered providers. Does not use <see cref="ConfigFileCache"/>.
        /// </summary>
        private static IConfigFile BuildConfigFileFromProviders(ConfigFileLocation location, bool ignoreUserData)
        {
            IConfigFile configFile = null;
            foreach (var provider in ConfigFileProviders)
            {
                if (provider.FileProvider is PersistentConfigFileProvider && ignoreUserData)
                    continue;

                if (provider.FileProvider.TryGetConfigFile(location, out var newConfigFile))
                {
                    if (configFile == null)
                    {
                        configFile = newConfigFile;
                        continue;
                    }

                    configFile.MergeConfigFile(newConfigFile);
                }
            }

            return configFile ?? new ConfigFile(location);
        }

        private static IConfigFile GetConfigFile_Internal(ConfigFileLocation location, bool ignoreUserData = false)
        {
            // Project-only view must not share ConfigFileCache with GetConfigFile (merged + userData), or
            // Persistent-merged keys could be written to project-level files. Rebuild each time; cost is low in editor.
            if (ignoreUserData)
                return BuildConfigFileFromProviders(location, true);

            if (ConfigFileCache.TryGetValue(location.Path, out var cached))
                return cached;

            var configFile = BuildConfigFileFromProviders(location, false);
            ConfigFileCache.Add(location.Path, configFile);
            return configFile;
        }

        /// <summary>
        /// Get <see cref="IConfigFile"/> from <see cref="ConfigFileLocation"/>
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public static IConfigFile GetConfigFile(ConfigFileLocation location)
        {
            return GetConfigFile_Internal(location);
        }
        
        /// <summary>
        /// Get <see cref="IConfigFile"/> from <see cref="ConfigFileLocation"/> without userData (skips persistent storage).
        /// Not cached: always a fresh merge of editor/project providers so it cannot pick up a merged instance from <see cref="GetConfigFile"/>.
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public static IConfigFile GetProjectConfigFile(ConfigFileLocation location)
        {
            return GetConfigFile_Internal(location, true);
        }

        public static void RegisterConfigFileProvider(IConfigFileProvider fileProvider, int priority = 0)
        {
            ConfigFileProviders.Add(new ConfigFileProviderStructure(fileProvider, priority));
            // Higher priority, lower prio.
            ConfigFileProviders.Sort(static (config1, config2) => config2.Priority.CompareTo(config1.Priority));
        }
    }
}