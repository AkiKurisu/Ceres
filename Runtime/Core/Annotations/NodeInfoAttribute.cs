using System;
using System.Reflection;
namespace Ceres.Annotations
{
    /// <summary>
    /// Describe node description in the editor
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class NodeInfoAttribute : Attribute
    {
        public string Description { get; }
        
        public NodeInfoAttribute(string description)
        {
            Description = description;
        }
    }

    public static class NodeInfo
    {
        public static string GetInfo(Type type)
        {
            var attribute = type.GetCustomAttribute<NodeInfoAttribute>();
            return attribute == null ? CeresLabel.GetTypeName(type) : attribute.Description;
        }
    }
}