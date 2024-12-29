using System;
namespace Ceres.Graph.Flow.Annotations
{
    /// <summary>
    /// Allow flow graph to execute this method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class ExecutableFunctionAttribute : Attribute
    {
        /// <summary>
        /// Function should use first parameter type as its script type
        /// </summary>
        public bool IsScriptMethod { get; set; }

        /// <summary>
        /// Function should display first parameter as method declare type target, need set <see cref="IsScriptMethod"/> first
        /// </summary>
        public bool DisplayTarget { get; set; } = true;
    }
}