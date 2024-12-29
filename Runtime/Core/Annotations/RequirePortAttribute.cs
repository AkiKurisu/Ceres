using System;
namespace Ceres.Annotations
{
    /// <summary>
    /// Notify editor that this node require input port and validate its value type
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class RequirePortAttribute : Attribute
    {
        /// <summary>
        /// Require port type constraint
        /// </summary>
        public Type PortType { get; }
        
        /// <summary>
        /// Whether allow input port type is subclass of <see cref="PortType"/>
        /// </summary>
        public bool AllowSubclass { get; }

        public RequirePortAttribute(Type portType = null, bool allowSubclass = true)
        {
            PortType = portType;
            AllowSubclass = allowSubclass;
        }
    }

}