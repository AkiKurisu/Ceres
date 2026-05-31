using System;
using Ceres.Graph;
using Ceres.Graph.Flow.CustomFunctions;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    [CustomNodeGenerator(typeof(FlowNode_ExecuteCustomFunction), true)]
    internal sealed class FlowNode_ExecuteCustomFunctionNodeGenerator : NodeGenerator<FlowNode_ExecuteCustomFunction>,
        IForwardOutputSlotNodeGenerator
    {
        public override bool CanGenerate(FlowNode_ExecuteCustomFunction node, NodeGenerationContext context)
        {
            return context.Source.CanGenerateCustomFunction(node);
        }

        public override void GenerateForward(FlowNode_ExecuteCustomFunction node, NodeGenerationContext context)
        {
            context.Source.GenerateCustomFunctionCall(node, context.FrameTypeName, context.FrameVar, context.Indent);
        }

        public override bool TryGetOutputSlot(FlowNode_ExecuteCustomFunction node, string portId,
            NodeGenerationContext context, out Type outputType, out string slotField)
        {
            return context.Source.TryGetCustomFunctionOutputSlot(node, portId, out outputType, out slotField);
        }

        public bool IsForwardOutputSlot(CeresNode node, string portId)
        {
            return portId == "output";
        }
    }

    [CustomNodeGenerator(typeof(CustomFunctionOutput))]
    internal sealed class CustomFunctionOutputNodeGenerator : NodeGenerator<CustomFunctionOutput>
    {
        public override void GenerateForward(CustomFunctionOutput node, NodeGenerationContext context)
        {
            context.Source.GenerateCustomFunctionOutput(node, context.FrameTypeName, context.FrameVar,
                context.Indent);
        }
    }
}
