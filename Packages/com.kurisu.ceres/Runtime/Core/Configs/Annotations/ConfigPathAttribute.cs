using System;

namespace Ceres.Configs
{
    /// <summary>
    /// Assign config path to parent config file
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ConfigPathAttribute : Attribute
    {
        public string Path { get; }

        public ConfigPathAttribute(string path)
        {
            Path = path;
        }
    }
}