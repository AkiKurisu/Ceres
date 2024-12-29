using System;
using Chris.Serialization;
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
        
        /// <summary>
        /// Function first parameter that should pass graph context object as default value
        /// </summary>
        public bool IsSelfTarget { get; set; } = false;
    }
    
    
    public static class ExecutableFunction
    {
        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// Metadata for function parameter to resolve return type, only support <see cref="SerializedType{T}"/>
        /// </summary>
        public const string RESOLVE_RETURN = nameof(RESOLVE_RETURN);
    }
}