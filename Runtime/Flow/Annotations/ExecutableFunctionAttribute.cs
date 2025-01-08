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
        /// Function can be executed in dependency execution path, only support static method
        /// </summary>
        /// <remarks>Functions executed in dependency mode should not depend on the execution order
        /// between nodes, and only execute based on the input values. For functions whose input
        /// parameters contain reference types, it is more appropriate to use forward path.</remarks>
        public bool ExecuteInDependency { get; set; }

        /// <summary>
        /// Function should display first parameter as method declare type target, need set <see cref="IsScriptMethod"/> first
        /// </summary>
        public bool DisplayTarget { get; set; } = true;
        
        /// <summary>
        /// Function first parameter that should pass graph context object as default value
        /// </summary>
        public bool IsSelfTarget { get; set; }
    }
}