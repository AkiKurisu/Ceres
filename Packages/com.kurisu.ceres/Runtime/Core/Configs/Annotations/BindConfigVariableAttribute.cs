using System;

namespace Ceres.Configs
{
    // Forwards the member's value to a config variable owned by another config; the member itself is not registered.
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class BindConfigVariableAttribute : Attribute
    {
        public string Name { get; }

        public BindConfigVariableAttribute(string name)
        {
            Name = name;
        }
    }
}
