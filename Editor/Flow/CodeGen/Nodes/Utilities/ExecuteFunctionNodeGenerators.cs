using System;
using Ceres.Graph;
using Ceres.Graph.Flow.Utilities;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    [CustomNodeGenerator(typeof(FlowNode_ExecuteFunctionVoid), true)]
    internal sealed class FlowNode_ExecuteFunctionVoidNodeGenerator : NodeGenerator<FlowNode_ExecuteFunctionVoid>
    {
        public override bool CanGenerate(FlowNode_ExecuteFunctionVoid node, NodeGenerationContext context)
        {
            return context.Source.CanGenerateExecuteFunctionVoid(node);
        }

        public override void GenerateForward(FlowNode_ExecuteFunctionVoid node, NodeGenerationContext context)
        {
            context.Source.GenerateExecuteFunctionVoid(node, context.FrameTypeName, context.FrameVar, context.Indent);
        }

        public override void GenerateDependency(FlowNode_ExecuteFunctionVoid node, NodeGenerationContext context)
        {
            context.Source.GenerateExecuteFunctionVoidLocal(node, context.FrameTypeName, context.FrameVar,
                context.Indent);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_ExecuteFunctionReturn), true)]
    internal sealed class FlowNode_ExecuteFunctionReturnNodeGenerator : NodeGenerator<FlowNode_ExecuteFunctionReturn>,
        IInlineExpressionNodeGenerator
    {
        public override bool CanGenerate(FlowNode_ExecuteFunctionReturn node, NodeGenerationContext context)
        {
            return context.Source.CanGenerateExecuteFunctionReturn(node);
        }

        public override void GenerateForward(FlowNode_ExecuteFunctionReturn node, NodeGenerationContext context)
        {
            context.Source.GenerateDependencyCall(node, context.FrameTypeName, context.FrameVar, context.Indent,
                FlowCSharpRuntimeGenerator.SourceContext.DependencyCancellationCheck.AlreadyChecked);
            context.GenerateDefaultNext(node);
        }

        public override void GenerateDependency(FlowNode_ExecuteFunctionReturn node, NodeGenerationContext context)
        {
            context.Source.GenerateFunctionReturnLocal(node, context.FrameTypeName, context.FrameVar, context.Indent);
        }

        public override bool TryGetOutputSlot(FlowNode_ExecuteFunctionReturn node, string portId,
            NodeGenerationContext context, out Type outputType, out string slotField)
        {
            return context.Source.TryGetFunctionReturnOutputSlot(node, portId, out outputType, out slotField);
        }

        public bool TryGenerateOutputExpression(CeresNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string expression)
        {
            if (node is FlowNode_ExecuteFunctionReturn functionNode)
            {
                return context.Source.TryGenerateFunctionReturnOutputExpression(functionNode, portId,
                    context.FrameTypeName, context.FrameVar, context.Indent, out outputType, out expression);
            }

            outputType = null;
            expression = null;
            return false;
        }
    }
}
