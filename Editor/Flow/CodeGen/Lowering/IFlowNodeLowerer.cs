using System;
using Ceres.Graph;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal interface IFlowNodeLowerer
    {
        bool CanLower(CeresNode node, FlowLoweringContext context);

        void LowerForward(CeresNode node, FlowLoweringContext context);

        bool TryLowerValue(CeresNode node, string portId, FlowLoweringContext context,
            out CsExpression expression);
    }

    internal abstract class FlowNodeLowerer<TNode> : IFlowNodeLowerer where TNode : CeresNode
    {
        public virtual bool CanLower(TNode node, FlowLoweringContext context)
        {
            return true;
        }

        public virtual void LowerForward(TNode node, FlowLoweringContext context)
        {
            throw new FlowCSharpRuntimeGenerationException(
                $"Forward node {node.GetType().Name} ({node.Guid}) is not supported by IR lowering.");
        }

        public virtual bool TryLowerValue(TNode node, string portId, FlowLoweringContext context,
            out CsExpression expression)
        {
            expression = null;
            return false;
        }

        bool IFlowNodeLowerer.CanLower(CeresNode node, FlowLoweringContext context)
        {
            return node is TNode typedNode && CanLower(typedNode, context);
        }

        void IFlowNodeLowerer.LowerForward(CeresNode node, FlowLoweringContext context)
        {
            LowerForward((TNode)node, context);
        }

        bool IFlowNodeLowerer.TryLowerValue(CeresNode node, string portId, FlowLoweringContext context,
            out CsExpression expression)
        {
            return TryLowerValue((TNode)node, portId, context, out expression);
        }
    }

    [Obsolete("Use IFlowNodeLowerer. This adapter keeps legacy node generators compiling while IR lowering is migrated.")]
    internal sealed class LegacyNodeLowererAdapter : IFlowNodeLowerer
    {
        private readonly INodeGenerator _generator;

        public LegacyNodeLowererAdapter(INodeGenerator generator)
        {
            _generator = generator;
        }

        public bool CanLower(CeresNode node, FlowLoweringContext context)
        {
            return false;
        }

        public void LowerForward(CeresNode node, FlowLoweringContext context)
        {
            throw new FlowCSharpRuntimeGenerationException(
                $"Legacy node generator for {node.GetType().Name} can not lower to structured IR.");
        }

        public bool TryLowerValue(CeresNode node, string portId, FlowLoweringContext context,
            out CsExpression expression)
        {
            expression = null;
            return false;
        }

        public INodeGenerator Generator => _generator;
    }
}
