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
            return attribute == null ? GetClassName(type) : attribute.Description;
        }

        public static string GetClassName(Type type)
        {
            if (type.IsGenericType)
            {
                type = type.GetGenericTypeDefinition();
                return type.Name.Split('`')[0];
            }

            return type.Name;
        }
    }
}