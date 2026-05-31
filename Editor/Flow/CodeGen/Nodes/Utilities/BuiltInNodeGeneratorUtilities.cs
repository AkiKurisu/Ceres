using System;
using System.Collections.Generic;
using Ceres.Graph;
using Ceres.Graph.Flow;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal static class BuiltInNodeGeneratorUtility
    {
        public static void EmitSwitchOutputs(NodeGenerationContext context, CeresNode node, string indexExpression,
            int count)
        {
            context.Emit($"{context.Indent}switch ({indexExpression})");
            context.Emit($"{context.Indent}{{");
            for (var i = 0; i < count; i++)
            {
                context.Emit($"{context.Indent}    case {i}:");
                var target = context.GetExecConnection(node, "outputs", i);
                if (target.IsValid)
                {
                    context.GenerateForwardConnection(target, context.Indent + "        ");
                }
                context.Emit($"{context.Indent}        break;");
            }
            context.Emit($"{context.Indent}}}");
        }

        public static bool TryGetSingleOutputSlot(CeresNode node, string portId, string expectedPortId, Type type,
            NodeGenerationContext context, out Type outputType, out string slotField)
        {
            if (portId != expectedPortId)
            {
                outputType = null;
                slotField = null;
                return false;
            }

            outputType = type;
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
        }

        public static void GenerateIndexedElement(ExecutableNode node, NodeGenerationContext context,
            string indexExpression, string arrayExpression = null)
        {
            var elementType = node.GetType().GetGenericArguments()[0];
            var listType = typeof(IReadOnlyList<>).MakeGenericType(elementType);
            var array = arrayExpression ?? context.GetValueExpression(node, "array", listType);
            var slot = context.EnsureOutputSlot(node, "item", elementType);
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = {array} != null && {array}.Count > 0 ? {array}[{indexExpression}] : default;");
        }

        public static bool TryGetArrayItemOutputSlot(CeresNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            outputType = null;
            slotField = null;
            if (portId != "item") return false;
            outputType = node.GetType().GetGenericArguments()[0];
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
        }
    }

    internal static class NodeGeneratorStringUtility
    {
        public static string Escape(string value)
        {
            return value?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? string.Empty;
        }

        public static string SafeGuid(string guid)
        {
            return guid.Replace("-", "_");
        }
    }
}
