using Ceres.Graph.Flow.Utilities;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    [CustomNodeGenerator(typeof(FlowNode_DebugLog))]
    internal sealed class FlowNode_DebugLogNodeGenerator : NodeGenerator<FlowNode_DebugLog>
    {
        public override void GenerateForward(FlowNode_DebugLog node, NodeGenerationContext context)
        {
            context.Source.GenerateDebugLog(node, context.FrameTypeName, context.FrameVar, context.Indent);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_DebugLogString))]
    internal sealed class FlowNode_DebugLogStringNodeGenerator : NodeGenerator<FlowNode_DebugLogString>
    {
        public override void GenerateForward(FlowNode_DebugLogString node, NodeGenerationContext context)
        {
            context.Source.GenerateDebugLogString(node, context.FrameTypeName, context.FrameVar, context.Indent);
        }
    }
}
