using System.Collections.Generic;
using System.IO;
using Ceres.Serialization;

namespace Ceres.Configs.Editor
{
    internal static class ConfigsEditorUtils
    {
        private static readonly SaveLoadSerializer PlatformSerializer = new(
            ConfigsModule.EditorPlatformDirectory,
            ConfigsModule.Extension,
            TextSerializeFormatter.Instance);

        private static readonly SaveLoadSerializer ProjectWideSerializer = new(
            ConfigsModule.EditorBaseDirectory,
            ConfigsModule.Extension,
            TextSerializeFormatter.Instance);

        /// <summary>
        /// Get config serializer in editor, cache will be clean up before using.
        /// </summary>
        /// <param name="platformSpecific">Whether config is platform specific or project-wide as base config</param>
        /// <returns></returns>
        public static SaveLoadSerializer GetConfigSerializer(bool platformSpecific = true)
        {
            ConfigSystem.ClearCache();
            return platformSpecific ? PlatformSerializer : ProjectWideSerializer;
        }

        public static void ExportAndArchiveConfigs()
        {
            // Clear streaming directory
            if (Directory.Exists(ConfigsModule.StreamingDirectory))
            {
                Directory.Delete(ConfigsModule.StreamingDirectory, true);
            }
            Directory.CreateDirectory(ConfigsModule.StreamingDirectory);

            // Create serializers
            var baseSerializer = new SaveLoadSerializer(
                ConfigsModule.EditorBaseDirectory,
                ConfigsModule.Extension,
                TextSerializeFormatter.Instance);

            var platformSerializer = new SaveLoadSerializer(
                ConfigsModule.EditorPlatformDirectory,
                ConfigsModule.Extension,
                TextSerializeFormatter.Instance);

            var streamingSerializer = new SaveLoadSerializer(
                ConfigsModule.StreamingDirectory,
                ConfigsModule.Extension,
                BinarySerializeFormatter.Instance);

            // Collect all config file paths from base directory
            var baseConfigFiles = new HashSet<string>();
            if (Directory.Exists(ConfigsModule.EditorBaseDirectory))
            {
                foreach (var filePath in Directory.GetFiles(ConfigsModule.EditorBaseDirectory, $"*.{ConfigsModule.Extension}"))
                {
                    baseConfigFiles.Add(Path.GetFileNameWithoutExtension(filePath));
                }
            }

            // Collect all config file paths from platform directory
            var platformConfigFiles = new HashSet<string>();
            if (Directory.Exists(ConfigsModule.EditorPlatformDirectory))
            {
                foreach (var filePath in Directory.GetFiles(ConfigsModule.EditorPlatformDirectory, $"*.{ConfigsModule.Extension}"))
                {
                    platformConfigFiles.Add(Path.GetFileNameWithoutExtension(filePath));
                }
            }

            // Merge all config files
            var allConfigFiles = new HashSet<string>(baseConfigFiles);
            allConfigFiles.UnionWith(platformConfigFiles);

            foreach (var configFileName in allConfigFiles)
            {
                IConfigFile mergedConfigFile = null;

                // Load base config first (lower priority)
                if (baseSerializer.Exists(configFileName))
                {
                    mergedConfigFile = baseSerializer.Deserialize<ConfigFile>(configFileName);
                }

                // Load and merge platform config (higher priority)
                if (platformSerializer.Exists(configFileName))
                {
                    var platformConfigFile = platformSerializer.Deserialize<ConfigFile>(configFileName);
                    if (mergedConfigFile == null)
                    {
                        mergedConfigFile = platformConfigFile;
                    }
                    else
                    {
                        mergedConfigFile.MergeConfigFile(platformConfigFile);
                    }
                }

                // Save merged config to streaming directory
                if (mergedConfigFile != null)
                {
                    streamingSerializer.Serialize(configFileName, mergedConfigFile);
                }
            }

#if !UNITY_STANDALONE_WIN
            // For platforms can not access to streaming assets dir, make archive file
            var files = Directory.GetFiles(ConfigsModule.StreamingDirectory);
            if (files.Length > 0)
            {
                if (!ZipWrapper.Zip(files, ConfigsModule.StreamingDirectory + ".zip"))
                {
                    throw new IOException("[Ceres] Archive configs failed");
                }
            }
            // Cleanup temporal directory
            Directory.Delete(ConfigsModule.StreamingDirectory, true);
            File.Delete(ConfigsModule.StreamingDirectory + ".meta");
#endif
        }
    }
}