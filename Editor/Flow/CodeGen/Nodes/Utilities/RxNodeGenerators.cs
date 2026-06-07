using System;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Utilities;
using Chris.Events;
using R3;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    [CustomNodeGenerator(typeof(FlowNode_ObservableSubscribeT<>))]
    internal sealed class FlowNode_ObservableSubscribeTNodeGenerator : NodeGenerator<FlowNode>,
        IForwardOutputSlotNodeGenerator
    {
        public override void GenerateForward(FlowNode node, NodeGenerationContext context)
        {
            var valueType = node.GetType().GetGenericArguments()[0];
            var subjectType = typeof(Observable<>).MakeGenericType(valueType);
            var delegateType = typeof(EventDelegate<>).MakeGenericType(valueType);
            var subject = context.GetValueExpression(node, "subject", subjectType);
            var onNext = context.GetValueExpression(node, "onNext", delegateType);
            var slot = context.EnsureOutputSlot(node, "subscription", typeof(IDisposable));
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = {subject}.Subscribe({onNext});");
            context.GenerateDefaultNext(node);
        }

        public override bool TryGetOutputSlot(FlowNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            if (portId != "subscription")
            {
                outputType = null;
                slotField = null;
                return false;
            }

            outputType = typeof(IDisposable);
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
        }

        public bool IsForwardOutputSlot(CeresNode node, string portId)
        {
            return portId == "subscription";
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_SubscribeEventExecutionT<>))]
    internal sealed class FlowNode_SubscribeEventExecutionTNodeGenerator : NodeGenerator<FlowNode>,
        IForwardOutputSlotNodeGenerator
    {
        public override void GenerateForward(FlowNode node, NodeGenerationContext context)
        {
            var eventType = node.GetType().GetGenericArguments()[0];
            var target = context.GetValueExpression(node, "target", typeof(CallbackEventHandler));
            var slot = context.EnsureOutputSlot(node, "subscription", typeof(IDisposable));
            var runtimeVar = $"runtime_{NodeGeneratorStringUtility.SafeGuid(node.Guid)}";
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = default;");
            context.Emit($"{context.Indent}if ({context.FrameVar}.ContextObject is IFlowGraphRuntime {runtimeVar})");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    {context.FrameVar}.{slot} = ({target}).SubscribeExecution<{context.GetFriendlyTypeName(eventType)}>({runtimeVar});");
            context.Emit($"{context.Indent}}}");
            context.GenerateDefaultNext(node);
        }

        public override bool TryGetOutputSlot(FlowNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            if (portId != "subscription")
            {
                outputType = null;
                slotField = null;
                return false;
            }

            outputType = typeof(IDisposable);
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
        }

        public bool IsForwardOutputSlot(CeresNode node, string portId)
        {
            return portId == "subscription";
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_SubscribeGlobalEventExecutionT<>))]
    internal sealed class FlowNode_SubscribeGlobalEventExecutionTNodeGenerator : NodeGenerator<FlowNode>,
        IForwardOutputSlotNodeGenerator
    {
        public override void GenerateForward(FlowNode node, NodeGenerationContext context)
        {
            var eventType = node.GetType().GetGenericArguments()[0];
            var slot = context.EnsureOutputSlot(node, "subscription", typeof(IDisposable));
            var runtimeVar = $"runtime_{NodeGeneratorStringUtility.SafeGuid(node.Guid)}";
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = default;");
            context.Emit($"{context.Indent}if ({context.FrameVar}.ContextObject is IFlowGraphRuntime {runtimeVar})");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    {context.FrameVar}.{slot} = EventSystem.EventHandler.SubscribeExecution<{context.GetFriendlyTypeName(eventType)}>({runtimeVar});");
            context.Emit($"{context.Indent}}}");
            context.GenerateDefaultNext(node);
        }

        public override bool TryGetOutputSlot(FlowNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            if (portId != "subscription")
            {
                outputType = null;
                slotField = null;
                return false;
            }

            outputType = typeof(IDisposable);
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
        }

        public bool IsForwardOutputSlot(CeresNode node, string portId)
        {
            return portId == "subscription";
        }
    }
}
