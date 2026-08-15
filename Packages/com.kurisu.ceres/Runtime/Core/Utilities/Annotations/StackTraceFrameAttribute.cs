using System;

namespace Ceres
{
    /// <summary>
    /// Indicates that the method or constructor should be used as a reference point for obtaining a specific stack frame.
    /// When used, it helps to locate and retrieve the stack frame that is relevant for tracing purposes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited = false)]
    public sealed class StackTraceFrameAttribute : Attribute
    {

    }
}