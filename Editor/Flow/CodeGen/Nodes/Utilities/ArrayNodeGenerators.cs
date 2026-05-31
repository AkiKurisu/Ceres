using System;
using System.Collections.Generic;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Utilities;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    [CustomNodeGenerator(typeof(FlowNode_MakeArrayT<>))]
    internal sealed class FlowNode_MakeArrayTNodeGenerator : NodeGenerator<FlowNode_MakeArray>
    {
        public override void GenerateDependency(FlowNode_MakeArray node, NodeGenerationContext context)
        {
            var elementType = node.GetType().GetGenericArguments()[0];
            var arrayType = elementType.MakeArrayType();
            var slot = context.EnsureOutputSlot(node, "array", arrayType);
            var length = node is IReadOnlyPortArrayNode portArrayNode ? portArrayNode.GetPortArrayLength() : 0;
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = new {context.GetFriendlyTypeName(elementType)}[{length}];");
            for (var i = 0; i < length; i++)
            {
                var item = context.GetValueExpression(node, "items", i, elementType);
                context.Emit($"{context.Indent}{context.FrameVar}.{slot}[{i}] = ({context.GetFriendlyTypeName(elementType)})({item});");
            }
        }

        public override bool TryGetOutputSlot(FlowNode_MakeArray node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            if (portId != "array")
            {
                outputType = null;
                slotField = null;
                return false;
            }

            outputType = node.GetType().GetGenericArguments()[0].MakeArrayType();
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_GetArrayElementT<>))]
    internal sealed class FlowNode_GetArrayElementTNodeGenerator : NodeGenerator<ExecutableNode>
    {
        public override void GenerateDependency(ExecutableNode node, NodeGenerationContext context)
        {
            var elementType = node.GetType().GetGenericArguments()[0];
            var arrayType = typeof(IReadOnlyList<>).MakeGenericType(elementType);
            var array = context.GetValueExpression(node, "array", arrayType);
            var index = context.GetValueExpression(node, "index", typeof(int));
            var slot = context.EnsureOutputSlot(node, "element", elementType);
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = {array}[{index}];");
        }

        public override bool TryGetOutputSlot(ExecutableNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            if (portId != "element")
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

    [CustomNodeGenerator(typeof(FlowNode_ArrayLengthT<>))]
    internal sealed class FlowNode_ArrayLengthTNodeGenerator : NodeGenerator<ExecutableNode>, IInlineExpressionNodeGenerator
    {
        public override void GenerateDependency(ExecutableNode node, NodeGenerationContext context)
        {
            var elementType = node.GetType().GetGenericArguments()[0];
            var listType = typeof(IReadOnlyList<>).MakeGenericType(elementType);
            var array = context.GetValueExpression(node, "array", listType);
            var slot = context.EnsureOutputSlot(node, "length", typeof(int));
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = {array}?.Count ?? 0;");
        }

        public override bool TryGetOutputSlot(ExecutableNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            return BuiltInNodeGeneratorUtility.TryGetSingleOutputSlot(node, portId, "length", typeof(int), context, out outputType, out slotField);
        }

        public bool TryGenerateOutputExpression(CeresNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string expression)
        {
            outputType = null;
            expression = null;
            if (portId != "length") return false;
            var elementType = node.GetType().GetGenericArguments()[0];
            var listType = typeof(IReadOnlyList<>).MakeGenericType(elementType);
            var array = context.GetValueExpression(node, "array", listType);
            outputType = typeof(int);
            expression = $"{array}?.Count ?? 0";
            return true;
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_ArrayIsValidIndexT<>))]
    internal sealed class FlowNode_ArrayIsValidIndexTNodeGenerator : NodeGenerator<ExecutableNode>, IInlineExpressionNodeGenerator
    {
        public override void GenerateDependency(ExecutableNode node, NodeGenerationContext context)
        {
            var elementType = node.GetType().GetGenericArguments()[0];
            var listType = typeof(IReadOnlyList<>).MakeGenericType(elementType);
            var array = context.GetValueExpression(node, "array", listType);
            var index = context.GetValueExpression(node, "index", typeof(int));
            var slot = context.EnsureOutputSlot(node, "result", typeof(bool));
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = {array} != null && {index} >= 0 && {index} < {array}.Count;");
        }

        public override bool TryGetOutputSlot(ExecutableNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            return BuiltInNodeGeneratorUtility.TryGetSingleOutputSlot(node, portId, "result", typeof(bool), context, out outputType, out slotField);
        }

        public bool TryGenerateOutputExpression(CeresNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string expression)
        {
            outputType = null;
            expression = null;
            if (portId != "result") return false;
            var elementType = node.GetType().GetGenericArguments()[0];
            var listType = typeof(IReadOnlyList<>).MakeGenericType(elementType);
            var array = context.GetValueExpression(node, "array", listType);
            var index = context.GetValueExpression(node, "index", typeof(int));
            outputType = typeof(bool);
            expression = $"{array} != null && {index} >= 0 && {index} < {array}.Count";
            return true;
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_ArrayContainsT<>))]
    internal sealed class FlowNode_ArrayContainsTNodeGenerator : NodeGenerator<ExecutableNode>
    {
        public override void GenerateDependency(ExecutableNode node, NodeGenerationContext context)
        {
            FlowNode_ArrayIndexOfTNodeGenerator.GenerateIndexOf(node, context, "result", true);
        }

        public override bool TryGetOutputSlot(ExecutableNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            return BuiltInNodeGeneratorUtility.TryGetSingleOutputSlot(node, portId, "result", typeof(bool), context, out outputType, out slotField);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_ArrayIndexOfT<>))]
    internal sealed class FlowNode_ArrayIndexOfTNodeGenerator : NodeGenerator<ExecutableNode>
    {
        public override void GenerateDependency(ExecutableNode node, NodeGenerationContext context)
        {
            GenerateIndexOf(node, context, "index", false);
        }

        internal static void GenerateIndexOf(ExecutableNode node, NodeGenerationContext context, string outputPort,
            bool outputContains)
        {
            var elementType = node.GetType().GetGenericArguments()[0];
            var listType = typeof(IReadOnlyList<>).MakeGenericType(elementType);
            var array = context.GetValueExpression(node, "array", listType);
            var item = context.GetValueExpression(node, "item", elementType);
            var resultType = outputContains ? typeof(bool) : typeof(int);
            var slot = context.EnsureOutputSlot(node, outputPort, resultType);
            var indexVar = $"indexOf_{NodeGeneratorStringUtility.SafeGuid(node.Guid)}";
            context.Emit($"{context.Indent}var {indexVar} = -1;");
            context.Emit($"{context.Indent}if ({array} != null)");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    var comparer = System.Collections.Generic.EqualityComparer<{context.GetFriendlyTypeName(elementType)}>.Default;");
            context.Emit($"{context.Indent}    for (var i = 0; i < {array}.Count; i++)");
            context.Emit($"{context.Indent}    {{");
            context.Emit($"{context.Indent}        if (comparer.Equals({array}[i], {item}))");
            context.Emit($"{context.Indent}        {{");
            context.Emit($"{context.Indent}            {indexVar} = i;");
            context.Emit($"{context.Indent}            break;");
            context.Emit($"{context.Indent}        }}");
            context.Emit($"{context.Indent}    }}");
            context.Emit($"{context.Indent}}}");
            context.Emit(outputContains
                ? $"{context.Indent}{context.FrameVar}.{slot} = {indexVar} >= 0;"
                : $"{context.Indent}{context.FrameVar}.{slot} = {indexVar};");
        }

        public override bool TryGetOutputSlot(ExecutableNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            return BuiltInNodeGeneratorUtility.TryGetSingleOutputSlot(node, portId, "index", typeof(int), context, out outputType, out slotField);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_ArrayFirstT<>))]
    internal sealed class FlowNode_ArrayFirstTNodeGenerator : NodeGenerator<ExecutableNode>
    {
        public override void GenerateDependency(ExecutableNode node, NodeGenerationContext context)
        {
            BuiltInNodeGeneratorUtility.GenerateIndexedElement(node, context, "0");
        }

        public override bool TryGetOutputSlot(ExecutableNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            return BuiltInNodeGeneratorUtility.TryGetArrayItemOutputSlot(node, portId, context, out outputType, out slotField);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_ArrayLastT<>))]
    internal sealed class FlowNode_ArrayLastTNodeGenerator : NodeGenerator<ExecutableNode>
    {
        public override void GenerateDependency(ExecutableNode node, NodeGenerationContext context)
        {
            var elementType = node.GetType().GetGenericArguments()[0];
            var listType = typeof(IReadOnlyList<>).MakeGenericType(elementType);
            var array = context.GetValueExpression(node, "array", listType);
            BuiltInNodeGeneratorUtility.GenerateIndexedElement(node, context, $"{array}.Count - 1", array);
        }

        public override bool TryGetOutputSlot(ExecutableNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            return BuiltInNodeGeneratorUtility.TryGetArrayItemOutputSlot(node, portId, context, out outputType, out slotField);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_ArrayRandomElementT<>))]
    internal sealed class FlowNode_ArrayRandomElementTNodeGenerator : NodeGenerator<ExecutableNode>
    {
        public override void GenerateDependency(ExecutableNode node, NodeGenerationContext context)
        {
            var elementType = node.GetType().GetGenericArguments()[0];
            var listType = typeof(IReadOnlyList<>).MakeGenericType(elementType);
            var array = context.GetValueExpression(node, "array", listType);
            var itemSlot = context.EnsureOutputSlot(node, "item", elementType);
            var indexSlot = context.EnsureOutputSlot(node, "index", typeof(int));
            context.Emit($"{context.Indent}if ({array} == null || {array}.Count == 0)");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    {context.FrameVar}.{itemSlot} = default;");
            context.Emit($"{context.Indent}    {context.FrameVar}.{indexSlot} = -1;");
            context.Emit($"{context.Indent}}}");
            context.Emit($"{context.Indent}else");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    {context.FrameVar}.{indexSlot} = UnityEngine.Random.Range(0, {array}.Count);");
            context.Emit($"{context.Indent}    {context.FrameVar}.{itemSlot} = {array}[{context.FrameVar}.{indexSlot}];");
            context.Emit($"{context.Indent}}}");
        }

        public override bool TryGetOutputSlot(ExecutableNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            if (portId == "index")
            {
                outputType = typeof(int);
                slotField = context.EnsureOutputSlot(node, portId, outputType);
                return true;
            }
            return BuiltInNodeGeneratorUtility.TryGetArrayItemOutputSlot(node, portId, context, out outputType, out slotField);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_ListMutationT<>), true)]
    internal sealed class FlowNode_ListMutationTNodeGenerator : NodeGenerator<FlowNode>
    {
        public override void GenerateForward(FlowNode node, NodeGenerationContext context)
        {
            var elementType = node.GetType().GetGenericArguments()[0];
            var listType = typeof(IList<>).MakeGenericType(elementType);
            var list = context.GetValueExpression(node, "list", listType);
            if (IsGenericInstance(node, typeof(FlowNode_ArraySetElementT<>)))
            {
                var index = context.GetValueExpression(node, "index", typeof(int));
                var item = context.GetValueExpression(node, "item", elementType);
                context.Emit($"{context.Indent}if ({list} != null && {index} >= 0 && {index} < {list}.Count) {list}[{index}] = {item};");
            }
            else if (IsGenericInstance(node, typeof(FlowNode_ArrayAppendT<>)))
            {
                var item = context.GetValueExpression(node, "item", elementType);
                context.Emit($"{context.Indent}{list}?.Add({item});");
            }
            else if (IsGenericInstance(node, typeof(FlowNode_ArrayInsertT<>)))
            {
                var index = context.GetValueExpression(node, "index", typeof(int));
                var item = context.GetValueExpression(node, "item", elementType);
                context.Emit($"{context.Indent}if ({list} != null) {list}.Insert(System.Math.Max(0, System.Math.Min({index}, {list}.Count)), {item});");
            }
            else if (IsGenericInstance(node, typeof(FlowNode_ArrayRemoveAtT<>)))
            {
                var index = context.GetValueExpression(node, "index", typeof(int));
                context.Emit($"{context.Indent}if ({list} != null && {index} >= 0 && {index} < {list}.Count) {list}.RemoveAt({index});");
            }
            else if (IsGenericInstance(node, typeof(FlowNode_ArrayRemoveItemT<>)))
            {
                var item = context.GetValueExpression(node, "item", elementType);
                context.Emit($"{context.Indent}{list}?.Remove({item});");
            }
            else if (IsGenericInstance(node, typeof(FlowNode_ArrayClearT<>)))
            {
                context.Emit($"{context.Indent}{list}?.Clear();");
            }
            else if (IsGenericInstance(node, typeof(FlowNode_ArrayReverseT<>)))
            {
                context.Emit($"{context.Indent}if ({list} != null)");
                context.Emit($"{context.Indent}{{");
                context.Emit($"{context.Indent}    for (var left = 0; left < {list}.Count / 2; left++)");
                context.Emit($"{context.Indent}    {{");
                context.Emit($"{context.Indent}        var right = {list}.Count - 1 - left;");
                context.Emit($"{context.Indent}        var temp = {list}[left];");
                context.Emit($"{context.Indent}        {list}[left] = {list}[right];");
                context.Emit($"{context.Indent}        {list}[right] = temp;");
                context.Emit($"{context.Indent}    }}");
                context.Emit($"{context.Indent}}}");
            }
            else if (IsGenericInstance(node, typeof(FlowNode_ArrayShuffleT<>)))
            {
                context.Emit($"{context.Indent}if ({list} != null)");
                context.Emit($"{context.Indent}{{");
                context.Emit($"{context.Indent}    for (var i = {list}.Count - 1; i > 0; i--)");
                context.Emit($"{context.Indent}    {{");
                context.Emit($"{context.Indent}        var pick = UnityEngine.Random.Range(0, i + 1);");
                context.Emit($"{context.Indent}        var temp = {list}[i];");
                context.Emit($"{context.Indent}        {list}[i] = {list}[pick];");
                context.Emit($"{context.Indent}        {list}[pick] = temp;");
                context.Emit($"{context.Indent}    }}");
                context.Emit($"{context.Indent}}}");
            }
            context.GenerateDefaultNext(node);
        }

        private static bool IsGenericInstance(CeresNode node, Type genericDefinition)
        {
            var type = node.GetType();
            return type.IsGenericType && type.GetGenericTypeDefinition() == genericDefinition;
        }
    }
}
