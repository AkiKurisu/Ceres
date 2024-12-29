using System;
namespace Ceres.Annotations
{
    /// <summary>
    /// Nodes are categorized in the editor dropdown menu, and can be sub-categorized with the '/' symbol
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class NodeGroupAttribute : Attribute
    {
        public string Group { get; }
        public NodeGroupAttribute(string group)
        {
            Group = group;
        }
    }

    public static class NodeGroup
    {
        public const string Hidden = "Hidden";
    }
}