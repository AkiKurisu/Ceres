namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal enum FlowCompilationDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    internal readonly struct FlowCompilationDiagnostic
    {
        public readonly FlowCompilationDiagnosticSeverity Severity;

        public readonly string Message;

        public readonly string NodeGuid;

        public FlowCompilationDiagnostic(FlowCompilationDiagnosticSeverity severity, string message,
            string nodeGuid = null)
        {
            Severity = severity;
            Message = message;
            NodeGuid = nodeGuid;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(NodeGuid)
                ? $"{Severity}: {Message}"
                : $"{Severity}: {Message} ({NodeGuid})";
        }
    }
}
