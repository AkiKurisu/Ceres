using System;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    public sealed class FlowCSharpRuntimeGenerationException : Exception
    {
        public FlowCSharpRuntimeGenerationException(string message) : base(message)
        {
        }
    }
}
