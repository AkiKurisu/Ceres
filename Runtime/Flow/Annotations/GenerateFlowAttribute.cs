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
        /// Whether add bridge methods for <see cref="ImplementableEventAttribute"/>
        /// </summary>
        public bool GenerateBridges { get; set; } = true;
    }
}
