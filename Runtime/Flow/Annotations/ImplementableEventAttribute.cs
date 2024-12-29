using System;
namespace Ceres.Graph.Flow.Annotations
{
    /// <summary>
    /// Allow flow graph to execute event from this method, event execution should be implemented inside this method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class ImplementableEventAttribute : Attribute
    {

    }
}