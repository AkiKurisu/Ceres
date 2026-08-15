using System;

namespace Ceres.Configs
{
    /// <summary>
    /// Define config variable can be edited through console.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ConfigVariableAttribute : Attribute
    {
        public string Name { get; private set; }
        
        public bool IsEditor { get; set; }

        public ConfigVariableAttribute(string name)
        {
            Name = name;
        }
    }
}
