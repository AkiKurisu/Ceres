using System;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Utilities;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    [CustomNodeGenerator(typeof(FlowNode_CastT<,>))]
    internal sealed class FlowNode_CastTNodeGenerator : NodeGenerator<ForwardNode>, IForwardOutputSlotNodeGenerator
    {
        public override void GenerateForward(ForwardNode node, NodeGenerationContext context)
        {
            var arguments = node.GetType().GetGenericArguments();
            var sourceType = arguments[0];
            var targetType = arguments[1];
            var source = context.GetValueExpression(node, "sourceValue", sourceType);
            var slot = context.EnsureOutputSlot(node, "resultValue", targetType);
            var targetTypeName = context.GetFriendlyTypeName(targetType);
            context.Emit($"{context.Indent}try");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    {context.FrameVar}.{slot} = ({targetTypeName})({source});");
            var success = context.GetExecConnection(node, "exec");
            if (success.IsValid)
            {
                context.GenerateForwardConnection(success, context.Indent + "    ");
            }

            context.Emit($"{context.Indent}}}");
            context.Emit($"{context.Indent}catch (System.InvalidCastException)");
            context.Emit($"{context.Indent}{{");
            var failed = context.GetExecConnection(node, "castFailed");
            if (failed.IsValid)
            {
                context.GenerateForwardConnection(failed, context.Indent + "    ");
            }

            context.Emit($"{context.Indent}}}");
        }

        public override bool TryGetOutputSlot(ForwardNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            if (portId != "resultValue")
            {
                outputType = null;
                slotField = null;
                return false;
            }

            outputType = node.GetType().GetGenericArguments()[1];
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
        }

        public bool IsForwardOutputSlot(CeresNode node, string portId)
        {
            return portId == "resultValue";
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_EqualsT<>))]
    internal sealed class FlowNode_EqualsTNodeGenerator : NodeGenerator<FlowNode>
    {
        public override void GenerateForward(FlowNode node, NodeGenerationContext context)
        {
            GenerateLocal(node, context);
            context.GenerateDefaultNext(node);
        }

        public override void GenerateDependency(FlowNode node, NodeGenerationContext context)
        {
            GenerateLocal(node, context);
        }

        public override bool TryGetOutputSlot(FlowNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            if (portId != "resultValue")
            {
                outputType = null;
                slotField = null;
                return false;
            }

            outputType = typeof(bool);
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
        }

        private static void GenerateLocal(FlowNode node, NodeGenerationContext context)
        {
            var valueType = node.GetType().GetGenericArguments()[0];
            var value1 = context.GetValueExpression(node, "value1", valueType);
            var value2 = context.GetValueExpression(node, "value2", valueType);
            var slot = context.EnsureOutputSlot(node, "resultValue", typeof(bool));
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = {value1}.Equals({value2});");
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_NotEqualsT<>))]
    internal sealed class FlowNode_NotEqualsTNodeGenerator : NodeGenerator<ExecutableNode>
    {
        public override void GenerateDependency(ExecutableNode node, NodeGenerationContext context)
        {
            var valueType = node.GetType().GetGenericArguments()[0];
            var value1 = context.GetValueExpression(node, "value1", valueType);
            var value2 = context.GetValueExpression(node, "value2", valueType);
            var slot = context.EnsureOutputSlot(node, "resultValue", typeof(bool));
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = !System.Collections.Generic.EqualityComparer<{context.GetFriendlyTypeName(valueType)}>.Default.Equals({value1}, {value2});");
        }

        public override bool TryGetOutputSlot(ExecutableNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            return BuiltInNodeGeneratorUtility.TryGetSingleOutputSlot(node, portId, "resultValue", typeof(bool), context, out outputType,
                out slotField);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_IsNullT<>))]
    internal sealed class FlowNode_IsNullTNodeGenerator : NodeGenerator<ExecutableNode>
    {
        public override void GenerateDependency(ExecutableNode node, NodeGenerationContext context)
        {
            var valueType = node.GetType().GetGenericArguments()[0];
            var value = context.GetValueExpression(node, "value", valueType);
            var slot = context.EnsureOutputSlot(node, "resultValue", typeof(bool));
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = object.Equals({value}, null);");
        }

        public override bool TryGetOutputSlot(ExecutableNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            return BuiltInNodeGeneratorUtility.TryGetSingleOutputSlot(node, portId, "resultValue", typeof(bool), context, out outputType,
                out slotField);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_IsNotNullT<>))]
    internal sealed class FlowNode_IsNotNullTNodeGenerator : NodeGenerator<ExecutableNode>
    {
        public override void GenerateDependency(ExecutableNode node, NodeGenerationContext context)
        {
            var valueType = node.GetType().GetGenericArguments()[0];
            var value = context.GetValueExpression(node, "value", valueType);
            var slot = context.EnsureOutputSlot(node, "resultValue", typeof(bool));
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = !object.Equals({value}, null);");
        }

        public override bool TryGetOutputSlot(ExecutableNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            return BuiltInNodeGeneratorUtility.TryGetSingleOutputSlot(node, portId, "resultValue", typeof(bool), context, out outputType,
                out slotField);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_CompareT<>))]
    internal sealed class FlowNode_CompareTNodeGenerator : NodeGenerator<ExecutableNode>
    {
        public override void GenerateDependency(ExecutableNode node, NodeGenerationContext context)
        {
            var valueType = node.GetType().GetGenericArguments()[0];
            var value1 = context.GetValueExpression(node, "value1", valueType);
            var value2 = context.GetValueExpression(node, "value2", valueType);
            var slot = context.EnsureOutputSlot(node, "resultValue", typeof(int));
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = System.Collections.Generic.Comparer<{context.GetFriendlyTypeName(valueType)}>.Default.Compare({value1}, {value2});");
        }

        public override bool TryGetOutputSlot(ExecutableNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            return BuiltInNodeGeneratorUtility.TryGetSingleOutputSlot(node, portId, "resultValue", typeof(int), context, out outputType,
                out slotField);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_SelectT<>))]
    internal sealed class FlowNode_SelectTNodeGenerator : NodeGenerator<ExecutableNode>
    {
        public override void GenerateDependency(ExecutableNode node, NodeGenerationContext context)
        {
            var valueType = node.GetType().GetGenericArguments()[0];
            var condition = context.GetValueExpression(node, "condition", typeof(bool));
            var trueValue = context.GetValueExpression(node, "trueValue", valueType);
            var falseValue = context.GetValueExpression(node, "falseValue", valueType);
            var slot = context.EnsureOutputSlot(node, "resultValue", valueType);
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = {condition} ? {trueValue} : {falseValue};");
        }

        public override bool TryGetOutputSlot(ExecutableNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            outputType = null;
            slotField = null;
            if (portId != "resultValue") return false;
            outputType = node.GetType().GetGenericArguments()[0];
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
        }
    }
}
