using System;
namespace Ceres.Annotations
{
    /// <summary>
    /// Notify editor port direction is input
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class InputPortAttribute : Attribute
    {
        /// <summary>
        /// Whether the port should always be connected
        /// </summary>
        public bool AlwaysConnected { get; }
        
        public InputPortAttribute(bool alwaysConnected = false)
        {
            AlwaysConnected = alwaysConnected;
        }
    }
    
    /// <summary>
    /// Notify editor port direction is output
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class OutputPortAttribute : Attribute
    {
        /// <summary>
        /// Whether allow multi connections
        /// </summary>
        public bool AllowMulti { get; }
        
        public OutputPortAttribute(bool allowMulti = true)
        {
            AllowMulti = allowMulti;
        }
    }
}