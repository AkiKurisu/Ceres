using System;
using Ceres.Serialization;
using UnityEngine;

namespace Ceres.Configs
{
    public interface IConfigFileProvider
    {
        bool TryGetConfigFile(ConfigFileLocation location, out IConfigFile config);
    }
    
    [PreferJsonConvert]
    public interface IConfigFile
    {
        (string configName, string configData)[] GetAllConfigData();
        
        bool TryGetConfig(IConfigLocation location, out ConfigBase config);

        void SetConfig(IConfigLocation location, ConfigBase config);
        
        void MergeConfigFile(IConfigFile targetFile);
    }

    internal abstract class ConfigFileProvider : IConfigFileProvider
    {
        protected abstract SaveLoadSerializer GetSerializer();
        
        public virtual bool TryGetConfigFile(ConfigFileLocation location, out IConfigFile config)
        {
            var serializer = GetSerializer();
            ConfigFile configFile = null;
            if (serializer.Exists(location.Path))
            {
                try
                {
                    configFile = serializer.Deserialize<ConfigFile>(location.Path);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            if (configFile != null)
            {
                configFile.Location = location;
                config = configFile;
                return true;
            }

            config = null;
            return false;
        }
    }

    internal class SaveLoadConfigFileProvider : ConfigFileProvider
    {
        private readonly SaveLoadSerializer _serializer;

        protected SaveLoadConfigFileProvider(SaveLoadSerializer serializer)
        {
            _serializer = serializer;
        }

        protected override SaveLoadSerializer GetSerializer()
        {
            return  _serializer;
        }
    }
    
#if UNITY_EDITOR
    internal class EditorBaseConfigFileProvider : SaveLoadConfigFileProvider
    {
        public EditorBaseConfigFileProvider() : 
            base(new SaveLoadSerializer(ConfigsModule.EditorBaseDirectory, ConfigsModule.Extension, TextSerializeFormatter.Instance))
        {
        }
    }
    
    internal class EditorConfigFileProvider : SaveLoadConfigFileProvider
    {
        public EditorConfigFileProvider() : 
            base(new SaveLoadSerializer(ConfigsModule.EditorPlatformDirectory, ConfigsModule.Extension, TextSerializeFormatter.Instance))
        {
        }
    }
#else
    internal class StreamingConfigFileProvider : SaveLoadConfigFileProvider
    {
        public StreamingConfigFileProvider() : 
            base(new SaveLoadSerializer(ConfigsModule.ActualStreamingDirectory, ConfigsModule.Extension, BinarySerializeFormatter.Instance))
        {
        }
    }
#endif
    
    internal class PersistentConfigFileProvider : ConfigFileProvider
    {
        protected override SaveLoadSerializer GetSerializer()
        {
            return ConfigsModule.PersistentSerializer;
        }

        public override bool TryGetConfigFile(ConfigFileLocation location, out IConfigFile config)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                config = null;
                return false;
            }
#endif
            return base.TryGetConfigFile(location, out config);
        }
    }
}