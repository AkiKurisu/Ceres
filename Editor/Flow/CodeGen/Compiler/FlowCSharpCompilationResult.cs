using Ceres.Graph.Flow;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal sealed class FlowCSharpCompilationResult
    {
        public string Source { get; }

        public FlowGeneratedFunctionDependencyInfo[] FunctionDependencies { get; }

        public bool UsedLegacyFallback { get; }

        public FlowCSharpCompilationResult(string source,
            FlowGeneratedFunctionDependencyInfo[] functionDependencies,
            bool usedLegacyFallback)
        {
            Source = source;
            FunctionDependencies = functionDependencies;
            UsedLegacyFallback = usedLegacyFallback;
        }
    }
}
