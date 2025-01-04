using System;
namespace Ceres.Graph.Flow.Annotations
{
    /// <summary>
    /// Attribute for notifying Ceres source generator to emit implementation code of <see cref="IFlowGraphContainer"/>
    /// </summary>
    /// <remarks>Must add partial modifier</remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class GenerateFlowAttribute : Attribute
    {
       
    }
}
