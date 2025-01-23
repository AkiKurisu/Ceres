using System;
namespace Ceres.Graph.Flow.Annotations
{
    /// <summary>
    /// Attribute for notifying Ceres source generator to emit code for partial class implementing <see cref="IFlowGraphContainer"/>
    /// </summary>
    /// <remarks>Must add partial modifier</remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class GenerateFlowAttribute : Attribute
    {
        /// <summary>
        /// Whether add implementation for <see cref="IFlowGraphContainer"/>
        /// </summary>
        public bool GenerateImplementation { get; set; } = true;
        
        /// <summary>
        /// Whether generate runtime implementation for <see cref="IFlowGraphRuntime"/>
        /// </summary>
        public bool GenerateRuntime { get; set; } = true;
    }
}
