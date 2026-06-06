using System;
using System.Collections.Generic;
using Ceres.Graph;
using Ceres.Graph.Flow;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    public readonly struct ExecConnection
    {
        public readonly CeresNode Node;

        public readonly string PortId;

        public readonly int PortIndex;

        public bool IsValid => Node != null;

        public ExecConnection(CeresNode node, string portId, int portIndex)
        {
            Node = node;
            PortId = portId;
            PortIndex = portIndex;
        }
    }

    public sealed class NodeGenerationContext
    {
        internal FlowCSharpRuntimeGenerator.SourceContext Source { get; }

        public string FrameTypeName { get; }

        public string FrameVar { get; }

        public string Indent { get; }

        public string EntryPortId { get; }

        public int EntryPortIndex { get; }

        internal NodeGenerationContext(FlowCSharpRuntimeGenerator.SourceContext source, string frameTypeName,
            string frameVar, string indent, string entryPortId = null, int entryPortIndex = -1)
        {
            Source = source;
            FrameTypeName = frameTypeName;
            FrameVar = frameVar;
            Indent = indent;
            EntryPortId = entryPortId;
            EntryPortIndex = entryPortIndex;
        }

        public NodeGenerationContext WithIndent(string indent)
        {
            return new NodeGenerationContext(Source, FrameTypeName, FrameVar, indent, EntryPortId, EntryPortIndex);
        }

        public void Emit(string line)
        {
            Source.Emit(line);
        }

        public string GetValueExpression(CeresNode node, string propertyName, Type expectedType)
        {
            return Source.GetValueExpression(node, propertyName, expectedType, FrameTypeName, FrameVar, Indent);
        }

        public string GetValueExpression(CeresNode node, string propertyName, int arrayIndex, Type expectedType)
        {
            return Source.GetValueExpression(node, propertyName, arrayIndex, expectedType, FrameTypeName, FrameVar,
                Indent);
        }

        public CeresNode GetExecTarget(CeresNode node, string propertyName)
        {
            return Source.GetExecTarget(node, propertyName);
        }

        public ExecConnection GetExecConnection(CeresNode node, string propertyName)
        {
            return Source.GetExecConnection(node, propertyName);
        }

        public CeresNode GetExecTarget(CeresNode node, string propertyName, int arrayIndex)
        {
            return Source.GetExecTarget(node, propertyName, arrayIndex);
        }

        public ExecConnection GetExecConnection(CeresNode node, string propertyName, int arrayIndex)
        {
            return Source.GetExecConnection(node, propertyName, arrayIndex);
        }

        public IEnumerable<CeresNode> GetExecTargets(CeresNode node, string propertyName)
        {
            return Source.GetExecTargets(node, propertyName);
        }

        public IEnumerable<ExecConnection> GetExecConnections(CeresNode node, string propertyName)
        {
            return Source.GetExecConnections(node, propertyName);
        }

        public void GenerateForwardNode(CeresNode node)
        {
            Source.GenerateForwardNode(node, FrameTypeName, FrameVar, Indent);
        }

        public void GenerateForwardNode(CeresNode node, string indent)
        {
            Source.GenerateForwardNode(node, FrameTypeName, FrameVar, indent);
        }

        public void GenerateForwardConnection(ExecConnection connection)
        {
            Source.GenerateForwardConnection(connection, FrameTypeName, FrameVar, Indent);
        }

        public void GenerateForwardConnection(ExecConnection connection, string indent)
        {
            Source.GenerateForwardConnection(connection, FrameTypeName, FrameVar, indent);
        }

        public void GenerateDefaultNext(FlowNode node)
        {
            Source.GenerateDefaultNext(node, FrameTypeName, FrameVar, Indent);
        }

        public void GenerateNext(CeresNode node)
        {
            Source.GenerateNext(node, FrameTypeName, FrameVar, Indent);
        }

        public string EnsureOutputSlot(CeresNode node, string portId, Type type)
        {
            return Source.EnsureOutputSlot(node, portId, type);
        }

        public string EnsureProgramField(CeresNode node, string fieldId, Type type)
        {
            return Source.EnsureProgramField(node, fieldId, type);
        }

        public string GetNodeFieldValueExpression(CeresNode node, string fieldName, Type expectedType)
        {
            return Source.GetNodeFieldValueExpression(node, fieldName, expectedType);
        }

        public string GetNodeFieldValueExpression(CeresNode node, string fieldName, int fieldIndex,
            Type expectedType)
        {
            return Source.GetNodeFieldValueExpression(node, fieldName, fieldIndex, expectedType);
        }

        public bool TryGetLocalSharedVariableValueExpression(CeresNode node, string fieldName,
            out Type valueType, out string expression)
        {
            return Source.TryGetLocalSharedVariableValueExpression(node, fieldName, out valueType, out expression);
        }

        public bool TryGetLocalSharedVariableValueExpression(CeresNode node, string fieldName, int index,
            out Type valueType, out string expression)
        {
            return Source.TryGetLocalSharedVariableValueExpression(node, fieldName, index, out valueType,
                out expression);
        }

        public string GetFriendlyTypeName(Type type)
        {
            return FlowCSharpRuntimeGenerator.SourceContext.GetFriendlyTypeName(type);
        }

        public string GetCancellationTokenExpression()
        {
            return Source.GetCancellationTokenExpression(FrameTypeName, FrameVar);
        }
    }
}
