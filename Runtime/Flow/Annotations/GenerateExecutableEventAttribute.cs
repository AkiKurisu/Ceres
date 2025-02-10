using System;

namespace Ceres.Graph.Flow.Annotations
{
    /// <summary>
    /// Allow this event can be implemented in flow graph
    /// </summary>
    /// <remarks>Annotate event custom create method to let source generator generate wrapper node for creating event</remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
    public class GenerateExecutableEventAttribute: Attribute
    {
        
    }
}