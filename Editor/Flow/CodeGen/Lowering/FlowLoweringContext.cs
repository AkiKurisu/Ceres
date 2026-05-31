using System;
using Ceres.Graph;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal sealed class FlowLoweringContext
    {
        public FlowCompilationContext Compilation { get; }

        public CsBlock Block { get; }

        public string ContextObjectExpression { get; }

        public string EventBaseExpression { get; }

        public FlowLoweringContext(FlowCompilationContext compilation, CsBlock block,
            string contextObjectExpression = "contextObject", string eventBaseExpression = "evtBase")
        {
            Compilation = compilation;
            Block = block;
            ContextObjectExpression = contextObjectExpression;
            EventBaseExpression = eventBaseExpression;
        }

        public FlowConnection GetExecConnection(CeresNode node, string propertyName)
        {
            return Compilation.CompilationGraph.GetExecConnection(node, propertyName);
        }

        public FlowConnection GetExecConnection(CeresNode node, string propertyName, int arrayIndex)
        {
            return Compilation.CompilationGraph.GetExecConnection(node, propertyName, arrayIndex);
        }

        public bool TryGetInputConnection(CeresNode node, string propertyName, out FlowConnection connection)
        {
            return Compilation.CompilationGraph.TryGetInputConnection(node, propertyName, out connection);
        }

        public void AddUnsupported(CeresNode node, string reason)
        {
            Compilation.AddDiagnostic(new FlowCompilationDiagnostic(FlowCompilationDiagnosticSeverity.Warning,
                reason, node?.Guid));
        }

        public static string GetFriendlyTypeName(Type type)
        {
            return FlowCSharpRuntimeGenerator.GetFriendlyTypeName(type);
        }
    }
}
