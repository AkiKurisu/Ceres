using System;
namespace Ceres.Editor
{
    /// <summary>
    /// Tells a node view class which run-time node type it's an editor for.
    /// </summary>
    public sealed class CustomNodeViewAttribute : Attribute
    {
        /// <summary>
        /// View bound node type
        /// </summary>
        public Type NodeType { get; }
        
        /// <summary>
        /// Whether this view can be used for inherit types of <see cref="NodeType"/>
        /// </summary>
        public bool CanInherit { get; }
        
        public CustomNodeViewAttribute(Type nodeType)
        {
            NodeType = nodeType;
            CanInherit = false;
        }
        
        public CustomNodeViewAttribute(Type nodeType, bool canInherit)
        {
            NodeType = nodeType;
            CanInherit = canInherit;
        }
    }
}