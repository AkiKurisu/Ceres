using System;
using Chris.Serialization;
namespace Ceres.Graph.Flow.Annotations
{
    /// <summary>
    /// Metadata for function parameter to resolve return type, only support <see cref="SerializedType{T}"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ResolveReturnAttribute : Attribute
    {
        
    }
}