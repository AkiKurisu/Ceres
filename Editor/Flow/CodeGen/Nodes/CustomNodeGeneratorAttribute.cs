using System;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    /// <summary>
    /// Tells a node generator which runtime node type it emits generated C# for.
    /// </summary>
    public sealed class CustomNodeGeneratorAttribute : Attribute
    {
        public Type NodeType { get; }

        public bool CanInherit { get; }

        public CustomNodeGeneratorAttribute(Type nodeType)
        {
            NodeType = nodeType;
            CanInherit = false;
        }

        public CustomNodeGeneratorAttribute(Type nodeType, bool canInherit)
        {
            NodeType = nodeType;
            CanInherit = canInherit;
        }
    }
}
