using System;
using Chris.Serialization;
namespace Ceres.Graph.Flow.Annotations
{
    /// <summary>
    /// Notify graph editor to use function parameter value to display return type, only support <see cref="SerializedType{T}"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ResolveReturnAttribute : Attribute
    {
        
    }
}