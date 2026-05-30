using System;
using System.Collections.Generic;
using Ceres.Graph;
using Ceres.Graph.Flow;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    public sealed class NodeGenerationContext
    {
        internal FlowCSharpRuntimeGenerator.SourceContext Source { get; }

        public string FrameTypeName { get; }

        public string FrameVar { get; }

        public string Indent { get; }

        internal NodeGenerationContext(FlowCSharpRuntimeGenerator.SourceContext source, string frameTypeName,
            string frameVar, string indent)
        {
            Source = source;
            FrameTypeName = frameTypeName;
            FrameVar = frameVar;
            Indent = indent;
        }

        public NodeGenerationContext WithIndent(string indent)
        {
            return new NodeGenerationContext(Source, FrameTypeName, FrameVar, indent);
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

        public CeresNode GetExecTarget(CeresNode node, string propertyName, int arrayIndex)
        {
            return Source.GetExecTarget(node, propertyName, arrayIndex);
        }

        public IEnumerable<CeresNode> GetExecTargets(CeresNode node, string propertyName)
        {
            return Source.GetExecTargets(node, propertyName);
        }

        public void GenerateForwardNode(CeresNode node)
        {
            Source.GenerateForwardNode(node, FrameTypeName, FrameVar, Indent);
        }

        public void GenerateForwardNode(CeresNode node, string indent)
        {
            Source.GenerateForwardNode(node, FrameTypeName, FrameVar, indent);
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

        public string GetFriendlyTypeName(Type type)
        {
            return FlowCSharpRuntimeGenerator.SourceContext.GetFriendlyTypeName(type);
        }
    }
}
