using System.Collections.Generic;
using Ceres.Graph.Flow;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal sealed class FlowCompilationContext
    {
        private readonly List<FlowCompilationDiagnostic> _diagnostics = new();

        public FlowGraph Graph { get; }

        public string DisplayName { get; }

        public string ClassName { get; }

        public FlowCSharpCompilationOptions Options { get; }

        public FlowCompilationGraph CompilationGraph { get; }

        public IReadOnlyList<FlowCompilationDiagnostic> Diagnostics => _diagnostics;

        public FlowCompilationContext(FlowGraph graph, string displayName, string className,
            FlowCSharpCompilationOptions options)
        {
            Graph = graph;
            DisplayName = displayName;
            ClassName = className;
            Options = options;
            CompilationGraph = new FlowCompilationGraph(graph);
        }

        public void AddDiagnostic(FlowCompilationDiagnostic diagnostic)
        {
            _diagnostics.Add(diagnostic);
        }
    }
}
