using System;

namespace Ceres.Configs
{
    /// <summary>
    /// Identifier for specific <see cref="ConfigBase"/>
    /// </summary>
    public interface IConfigLocation
    {
        ConfigFileLocation FileLocation { get; }

        string ConfigName { get; }

        Type Type { get; }

        bool PreferJsonConvert { get; }
    }
    
    /// <summary>
    /// Identifier for specific <see cref="ConfigFile"/>
    /// </summary>
    public readonly struct ConfigFileLocation
    {
        public readonly string Path;

        private ConfigFileLocation(string path)
        {
            Path = path;
        }

        public static implicit operator ConfigFileLocation(string path)
        {
            return new ConfigFileLocation(path);
        }
        
        public static implicit operator string(ConfigFileLocation location)
        {
            return location.Path;
        }
    }
}