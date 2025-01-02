using System;
namespace Ceres.Graph.Flow.Annotations
{
    /// <summary>
    /// Attribute for notify Ceres source generator emit code to implement <see cref="IFlowGraphContainer"/>
    /// </summary>
    /// <remarks>Must add partial modifier</remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class GenerateFlowAttribute : Attribute
    {
       
    }
}
