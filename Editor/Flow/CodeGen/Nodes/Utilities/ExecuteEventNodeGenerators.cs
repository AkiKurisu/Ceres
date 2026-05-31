using Ceres.Graph.Flow.Utilities;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    [CustomNodeGenerator(typeof(FlowNode_ExecuteEvent))]
    internal sealed class FlowNode_ExecuteEventNodeGenerator : NodeGenerator<FlowNode_ExecuteEvent>
    {
        public override bool CanGenerate(FlowNode_ExecuteEvent node, NodeGenerationContext context)
        {
            return context.Source.CanGenerateExecuteEvent(node);
        }

        public override void GenerateForward(FlowNode_ExecuteEvent node, NodeGenerationContext context)
        {
            context.Source.GenerateExecuteEvent(node, context.FrameTypeName, context.FrameVar, context.Indent);
        }
    }
}
