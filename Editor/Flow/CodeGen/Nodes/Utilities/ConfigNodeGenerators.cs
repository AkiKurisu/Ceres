using System;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Utilities;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    [CustomNodeGenerator(typeof(FlowNode_GetConfigT<>))]
    internal sealed class FlowNode_GetConfigTNodeGenerator : NodeGenerator<ExecutableNode>
    {
        public override void GenerateDependency(ExecutableNode node, NodeGenerationContext context)
        {
            var configType = node.GetType().GetGenericArguments()[0];
            var slot = context.EnsureOutputSlot(node, "result", configType);
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = Chris.Configs.ConfigSystem.GetConfig<{context.GetFriendlyTypeName(configType)}>();");
        }

        public override bool TryGetOutputSlot(ExecutableNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            if (portId != "result")
            {
                outputType = null;
                slotField = null;
                return false;
            }

            outputType = node.GetType().GetGenericArguments()[0];
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
        }
    }
}
