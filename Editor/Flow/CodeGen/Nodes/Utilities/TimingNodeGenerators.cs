using Ceres.Graph.Flow.Utilities;
using UnityEngine;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    [CustomNodeGenerator(typeof(FlowNode_Delay))]
    internal class FlowNode_DelayNodeGenerator : NodeGenerator<FlowNode_Delay>
    {
        public override void GenerateForward(FlowNode_Delay node, NodeGenerationContext context)
        {
            var seconds = context.GetValueExpression(node, nameof(FlowNode_Delay.seconds), typeof(float));
            context.Emit($"{context.Indent}await UniTask.Delay(System.TimeSpan.FromSeconds(Mathf.Max(0f, {seconds})), cancellationToken: {context.GetCancellationTokenExpression()});");
            context.GenerateForwardConnection(context.GetExecConnection(node, nameof(FlowNode_Delay.exec)));
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_RetriggerableDelay))]
    internal sealed class FlowNode_RetriggerableDelayNodeGenerator : NodeGenerator<FlowNode_RetriggerableDelay>
    {
        public override void GenerateForward(FlowNode_RetriggerableDelay node, NodeGenerationContext context)
        {
            var seconds = context.GetValueExpression(node, nameof(FlowNode_RetriggerableDelay.seconds), typeof(float));
            var source = context.EnsureProgramField(node, "delayCancellation", typeof(System.Threading.CancellationTokenSource));
            context.Emit($"{context.Indent}{source}?.Cancel();");
            context.Emit($"{context.Indent}{source}?.Dispose();");
            context.Emit($"{context.Indent}{source} = System.Threading.CancellationTokenSource.CreateLinkedTokenSource({context.GetCancellationTokenExpression()});");
            context.Emit($"{context.Indent}try");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    await UniTask.Delay(System.TimeSpan.FromSeconds(Mathf.Max(0f, {seconds})), cancellationToken: {source}.Token);");
            context.GenerateForwardConnection(context.GetExecConnection(node, nameof(FlowNode_RetriggerableDelay.exec)),
                context.Indent + "    ");
            context.Emit($"{context.Indent}}}");
            context.Emit($"{context.Indent}catch (System.OperationCanceledException)");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}}}");
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_DelayFrame))]
    internal sealed class FlowNode_DelayFrameNodeGenerator : NodeGenerator<FlowNode_DelayFrame>
    {
        public override void GenerateForward(FlowNode_DelayFrame node, NodeGenerationContext context)
        {
            var frames = context.GetValueExpression(node, nameof(FlowNode_DelayFrame.frames), typeof(int));
            context.Emit($"{context.Indent}await UniTask.DelayFrame(Mathf.Max(0, {frames}), cancellationToken: {context.GetCancellationTokenExpression()});");
            context.GenerateForwardConnection(context.GetExecConnection(node, nameof(FlowNode_DelayFrame.exec)));
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_NextFrame))]
    internal sealed class FlowNode_NextFrameNodeGenerator : NodeGenerator<FlowNode_NextFrame>
    {
        public override void GenerateForward(FlowNode_NextFrame node, NodeGenerationContext context)
        {
            context.Emit($"{context.Indent}await UniTask.NextFrame({context.GetCancellationTokenExpression()});");
            context.GenerateForwardConnection(context.GetExecConnection(node, nameof(FlowNode_NextFrame.exec)));
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_WaitUntil))]
    internal sealed class FlowNode_WaitUntilNodeGenerator : NodeGenerator<FlowNode_WaitUntil>
    {
        public override void GenerateForward(FlowNode_WaitUntil node, NodeGenerationContext context)
        {
            var condition = context.GetValueExpression(node, nameof(FlowNode_WaitUntil.condition), typeof(bool));
            context.Emit($"{context.Indent}await UniTask.WaitUntil(() => {condition}, cancellationToken: {context.GetCancellationTokenExpression()});");
            context.GenerateForwardConnection(context.GetExecConnection(node, nameof(FlowNode_WaitUntil.exec)));
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_WaitWhile))]
    internal sealed class FlowNode_WaitWhileNodeGenerator : NodeGenerator<FlowNode_WaitWhile>
    {
        public override void GenerateForward(FlowNode_WaitWhile node, NodeGenerationContext context)
        {
            var condition = context.GetValueExpression(node, nameof(FlowNode_WaitWhile.condition), typeof(bool));
            context.Emit($"{context.Indent}await UniTask.WaitWhile(() => {condition}, cancellationToken: {context.GetCancellationTokenExpression()});");
            context.GenerateForwardConnection(context.GetExecConnection(node, nameof(FlowNode_WaitWhile.exec)));
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_Timeout))]
    internal sealed class FlowNode_TimeoutNodeGenerator : NodeGenerator<FlowNode_Timeout>
    {
        public override void GenerateForward(FlowNode_Timeout node, NodeGenerationContext context)
        {
            var seconds = context.GetValueExpression(node, nameof(FlowNode_Timeout.seconds), typeof(float));
            context.Emit($"{context.Indent}await UniTask.Delay(System.TimeSpan.FromSeconds(Mathf.Max(0f, {seconds})), cancellationToken: {context.GetCancellationTokenExpression()});");
            context.GenerateForwardConnection(context.GetExecConnection(node, nameof(FlowNode_Timeout.completed)));
        }
    }
}
