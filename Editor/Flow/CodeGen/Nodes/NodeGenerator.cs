using System;
using Ceres.Graph;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    public interface INodeGenerator
    {
        bool CanGenerate(CeresNode node, NodeGenerationContext context);

        void GenerateForward(CeresNode node, NodeGenerationContext context);

        void GenerateDependency(CeresNode node, NodeGenerationContext context);

        bool TryGetOutputSlot(CeresNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField);
    }

    public interface INodeGeneratorResolver
    {
        INodeGenerator CreateNodeGenerator(Type nodeType);

        bool IsAcceptable(Type nodeType);
    }

    public interface IForwardOutputSlotNodeGenerator
    {
        bool IsForwardOutputSlot(CeresNode node, string portId);
    }

    public interface IInlineExpressionNodeGenerator
    {
        bool TryGenerateOutputExpression(CeresNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string expression);
    }

    public abstract class NodeGenerator<TNode> : INodeGenerator where TNode : CeresNode
    {
        public virtual bool CanGenerate(TNode node, NodeGenerationContext context)
        {
            return true;
        }

        public virtual void GenerateForward(TNode node, NodeGenerationContext context)
        {
            throw new FlowCSharpRuntimeGenerationException(
                $"Forward node {node.GetType().Name} ({node.Guid}) is not supported by generated runtime.");
        }

        public virtual void GenerateDependency(TNode node, NodeGenerationContext context)
        {
            throw new FlowCSharpRuntimeGenerationException(
                $"Dependency node {node.GetType().Name} ({node.Guid}) is not supported by generated runtime.");
        }

        public virtual bool TryGetOutputSlot(TNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            outputType = null;
            slotField = null;
            return false;
        }

        bool INodeGenerator.CanGenerate(CeresNode node, NodeGenerationContext context)
        {
            return node is TNode typedNode && CanGenerate(typedNode, context);
        }

        void INodeGenerator.GenerateForward(CeresNode node, NodeGenerationContext context)
        {
            GenerateForward((TNode)node, context);
        }

        void INodeGenerator.GenerateDependency(CeresNode node, NodeGenerationContext context)
        {
            GenerateDependency((TNode)node, context);
        }

        bool INodeGenerator.TryGetOutputSlot(CeresNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            return TryGetOutputSlot((TNode)node, portId, context, out outputType, out slotField);
        }
    }
}
