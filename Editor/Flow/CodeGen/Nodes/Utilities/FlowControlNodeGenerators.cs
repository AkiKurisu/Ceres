using System;
using System.Collections.Generic;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Utilities;
using UnityEngine;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    [CustomNodeGenerator(typeof(FlowNode_Sequence))]
    internal sealed class FlowNode_SequenceNodeGenerator : NodeGenerator<FlowNode_Sequence>
    {
        public override void GenerateForward(FlowNode_Sequence node, NodeGenerationContext context)
        {
            foreach (var next in context.GetExecConnections(node, "outputs"))
            {
                context.GenerateForwardConnection(next);
            }
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_Branch))]
    internal sealed class FlowNode_BranchNodeGenerator : NodeGenerator<FlowNode_Branch>
    {
        public override void GenerateForward(FlowNode_Branch node, NodeGenerationContext context)
        {
            context.Source.GenerateBranch(node, context.FrameTypeName, context.FrameVar, context.Indent);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_SwitchString))]
    internal sealed class FlowNode_SwitchStringNodeGenerator : NodeGenerator<FlowNode_SwitchString>
    {
        public override void GenerateForward(FlowNode_SwitchString node, NodeGenerationContext context)
        {
            var source = context.GetValueExpression(node, nameof(FlowNode_SwitchString.sourceValue), typeof(string));
            for (var i = 0; i < node.settings.conditions.Length; i++)
            {
                context.Emit(i == 0 ? $"{context.Indent}if ({source} == \"{NodeGeneratorStringUtility.Escape(node.settings.conditions[i])}\")" :
                    $"{context.Indent}else if ({source} == \"{NodeGeneratorStringUtility.Escape(node.settings.conditions[i])}\")");
                context.Emit($"{context.Indent}{{");
                var target = context.GetExecConnection(node, nameof(FlowNode_SwitchString.outputs), i);
                if (target.IsValid)
                {
                    context.GenerateForwardConnection(target, context.Indent + "    ");
                }

                context.Emit($"{context.Indent}}}");
            }

            if (!node.settings.hasDefault) return;
            context.Emit(node.settings.conditions.Length == 0 ? $"{context.Indent}if (true)" : $"{context.Indent}else");
            context.Emit($"{context.Indent}{{");
            var defaultTarget = context.GetExecConnection(node, nameof(FlowNode_SwitchString.defaultOutput));
            if (defaultTarget.IsValid)
            {
                context.GenerateForwardConnection(defaultTarget, context.Indent + "    ");
            }

            context.Emit($"{context.Indent}}}");
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_SwitchEnum), true)]
    internal sealed class FlowNode_SwitchEnumNodeGenerator : NodeGenerator<FlowNode_SwitchEnum>
    {
        public override void GenerateForward(FlowNode_SwitchEnum node, NodeGenerationContext context)
        {
            var enumType = node.GetType().GetGenericArguments()[0];
            var source = context.GetValueExpression(node, "sourceValue", enumType);
            var outputCount = Enum.GetValues(enumType).Length;
            var indexName = $"switchIndex_{NodeGeneratorStringUtility.SafeGuid(node.Guid)}";
            context.Emit($"{context.Indent}var {indexName} = {source}.GetHashCode();");
            context.Emit($"{context.Indent}{indexName} = System.Math.Min({indexName}, {outputCount - 1});");
            context.Emit($"{context.Indent}switch ({indexName})");
            context.Emit($"{context.Indent}{{");
            for (var i = 0; i < outputCount; i++)
            {
                context.Emit($"{context.Indent}    case {i}:");
                var target = context.GetExecConnection(node, "outputs", i);
                if (target.IsValid)
                {
                    context.GenerateForwardConnection(target, context.Indent + "        ");
                }

                context.Emit($"{context.Indent}        break;");
            }

            context.Emit($"{context.Indent}    default:");
            context.Emit($"{context.Indent}        throw new System.IndexOutOfRangeException();");
            context.Emit($"{context.Indent}}}");
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_ForEachLoop))]
    internal sealed class FlowNode_ForEachLoopNodeGenerator : NodeGenerator<FlowNode_ForEachLoop>,
        IForwardOutputSlotNodeGenerator
    {
        public override void GenerateForward(FlowNode_ForEachLoop node, NodeGenerationContext context)
        {
            GenerateLoop(node, context, typeof(Array), typeof(object));
        }

        public override bool TryGetOutputSlot(FlowNode_ForEachLoop node, string portId,
            NodeGenerationContext context, out Type outputType, out string slotField)
        {
            if (portId != nameof(FlowNode_ForEachLoop.arrayElement))
            {
                outputType = null;
                slotField = null;
                return false;
            }

            outputType = typeof(object);
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
        }

        internal static void GenerateLoop(CeresNode node, NodeGenerationContext context, Type arrayType,
            Type elementType)
        {
            var array = context.GetValueExpression(node, "array", arrayType);
            var slot = context.EnsureOutputSlot(node, "arrayElement", elementType);
            var elementTypeName = context.GetFriendlyTypeName(elementType);
            context.Emit($"{context.Indent}if ({array} != null)");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    foreach ({elementTypeName} element in {array})");
            context.Emit($"{context.Indent}    {{");
            context.Emit($"{context.Indent}        {context.FrameVar}.{slot} = element;");
            var loopBody = context.GetExecConnection(node, "loopBody");
            if (loopBody.IsValid)
            {
                context.GenerateForwardConnection(loopBody, context.Indent + "        ");
            }

            context.Emit($"{context.Indent}    }}");
            context.Emit($"{context.Indent}}}");
            var completed = context.GetExecConnection(node, "completed");
            if (completed.IsValid)
            {
                context.GenerateForwardConnection(completed);
            }
        }

        public bool IsForwardOutputSlot(CeresNode node, string portId)
        {
            return portId == nameof(FlowNode_ForEachLoop.arrayElement);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_ForEachLoopT<>))]
    internal sealed class FlowNode_ForEachLoopTNodeGenerator : NodeGenerator<FlowNode_ForEachLoopGeneric>,
        IForwardOutputSlotNodeGenerator
    {
        public override void GenerateForward(FlowNode_ForEachLoopGeneric node, NodeGenerationContext context)
        {
            var elementType = node.GetType().GetGenericArguments()[0];
            var arrayType = typeof(IReadOnlyList<>).MakeGenericType(elementType);
            FlowNode_ForEachLoopNodeGenerator.GenerateLoop(node, context, arrayType, elementType);
        }

        public override bool TryGetOutputSlot(FlowNode_ForEachLoopGeneric node, string portId,
            NodeGenerationContext context, out Type outputType, out string slotField)
        {
            if (portId != "arrayElement")
            {
                outputType = null;
                slotField = null;
                return false;
            }

            outputType = node.GetType().GetGenericArguments()[0];
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
        }

        public bool IsForwardOutputSlot(CeresNode node, string portId)
        {
            return portId == "arrayElement";
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_ForLoop))]
    internal class FlowNode_ForLoopNodeGenerator : NodeGenerator<FlowNode_ForLoop>, IForwardOutputSlotNodeGenerator
    {
        public override void GenerateForward(FlowNode_ForLoop node, NodeGenerationContext context)
        {
            GenerateForLoop(node, context, false);
        }

        protected static void GenerateForLoop(FlowNode_ForLoop node, NodeGenerationContext context, bool canBreak)
        {
            var first = context.GetValueExpression(node, nameof(FlowNode_ForLoop.firstIndex), typeof(int));
            var last = context.GetValueExpression(node, nameof(FlowNode_ForLoop.lastIndex), typeof(int));
            var step = context.GetValueExpression(node, nameof(FlowNode_ForLoop.step), typeof(int));
            var indexSlot = context.EnsureOutputSlot(node, nameof(FlowNode_ForLoop.index), typeof(int));
            var stepVar = $"step_{NodeGeneratorStringUtility.SafeGuid(node.Guid)}";
            var indexVar = $"index_{NodeGeneratorStringUtility.SafeGuid(node.Guid)}";
            var breakSlot = canBreak ? context.EnsureOutputSlot(node, "breakRequested", typeof(bool)) : null;
            if (canBreak && context.EntryPortId == nameof(FlowNode_ForLoopWithBreak.breakInput))
            {
                context.Emit($"{context.Indent}{context.FrameVar}.{breakSlot} = true;");
                return;
            }

            if (canBreak)
            {
                context.Emit($"{context.Indent}{context.FrameVar}.{breakSlot} = false;");
            }
            context.Emit($"{context.Indent}var {stepVar} = {step} == 0 ? 1 : {step};");
            context.Emit($"{context.Indent}if ({stepVar} > 0)");
            context.Emit($"{context.Indent}{{");
            EmitLoopBody(context, node, first, last, stepVar, indexVar, indexSlot, breakSlot, "<=");
            context.Emit($"{context.Indent}}}");
            context.Emit($"{context.Indent}else");
            context.Emit($"{context.Indent}{{");
            EmitLoopBody(context, node, first, last, stepVar, indexVar, indexSlot, breakSlot, ">=");
            context.Emit($"{context.Indent}}}");
            context.GenerateForwardConnection(context.GetExecConnection(node, nameof(FlowNode_ForLoop.completed)));
        }

        private static void EmitLoopBody(NodeGenerationContext context, FlowNode_ForLoop node, string first,
            string last, string stepVar, string indexVar, string indexSlot, string breakSlot, string comparison)
        {
            context.Emit($"{context.Indent}    for (var {indexVar} = {first}; {indexVar} {comparison} {last}; {indexVar} += {stepVar})");
            context.Emit($"{context.Indent}    {{");
            context.Emit($"{context.Indent}        {context.FrameVar}.{indexSlot} = {indexVar};");
            var body = context.GetExecConnection(node, nameof(FlowNode_ForLoop.loopBody));
            if (body.IsValid)
            {
                context.GenerateForwardConnection(body, context.Indent + "        ");
            }
            if (breakSlot != null)
            {
                context.Emit($"{context.Indent}        if ({context.FrameVar}.{breakSlot}) break;");
            }
            context.Emit($"{context.Indent}    }}");
        }

        public override bool TryGetOutputSlot(FlowNode_ForLoop node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            if (portId != nameof(FlowNode_ForLoop.index))
            {
                outputType = null;
                slotField = null;
                return false;
            }

            outputType = typeof(int);
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
        }

        public bool IsForwardOutputSlot(CeresNode node, string portId)
        {
            return portId == nameof(FlowNode_ForLoop.index);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_ForLoopWithBreak))]
    internal sealed class FlowNode_ForLoopWithBreakNodeGenerator : FlowNode_ForLoopNodeGenerator
    {
        public override void GenerateForward(FlowNode_ForLoop node, NodeGenerationContext context)
        {
            GenerateForLoop(node, context, true);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_WhileLoop))]
    internal sealed class FlowNode_WhileLoopNodeGenerator : NodeGenerator<FlowNode_WhileLoop>
    {
        public override void GenerateForward(FlowNode_WhileLoop node, NodeGenerationContext context)
        {
            var condition = context.GetValueExpression(node, nameof(FlowNode_WhileLoop.condition), typeof(bool));
            var maxIterations = context.GetValueExpression(node, nameof(FlowNode_WhileLoop.maxIterations), typeof(int));
            var iterationVar = $"iteration_{NodeGeneratorStringUtility.SafeGuid(node.Guid)}";
            context.Emit($"{context.Indent}var {iterationVar} = 0;");
            context.Emit($"{context.Indent}while ({condition})");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    if ({maxIterations} > 0 && {iterationVar}++ >= {maxIterations}) break;");
            var body = context.GetExecConnection(node, nameof(FlowNode_WhileLoop.loopBody));
            if (body.IsValid)
            {
                context.GenerateForwardConnection(body, context.Indent + "    ");
            }
            context.Emit($"{context.Indent}}}");
            context.GenerateForwardConnection(context.GetExecConnection(node, nameof(FlowNode_WhileLoop.completed)));
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_DoOnce))]
    internal sealed class FlowNode_DoOnceNodeGenerator : NodeGenerator<FlowNode_DoOnce>
    {
        public override void GenerateForward(FlowNode_DoOnce node, NodeGenerationContext context)
        {
            var initialized = context.EnsureProgramField(node, "doOnceInitialized", typeof(bool));
            var hasFired = context.EnsureProgramField(node, "doOnceHasFired", typeof(bool));
            if (context.EntryPortId == nameof(FlowNode_DoOnce.reset))
            {
                context.Emit($"{context.Indent}{initialized} = true;");
                context.Emit($"{context.Indent}{hasFired} = false;");
                return;
            }

            var startClosed = context.GetValueExpression(node, nameof(FlowNode_DoOnce.startClosed), typeof(bool));
            context.Emit($"{context.Indent}if (!{initialized})");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    {initialized} = true;");
            context.Emit($"{context.Indent}    {hasFired} = {startClosed};");
            context.Emit($"{context.Indent}}}");
            context.Emit($"{context.Indent}if (!{hasFired})");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    {hasFired} = true;");
            context.GenerateForwardConnection(context.GetExecConnection(node, nameof(FlowNode_DoOnce.exec)),
                context.Indent + "    ");
            context.Emit($"{context.Indent}}}");
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_DoN))]
    internal sealed class FlowNode_DoNNodeGenerator : NodeGenerator<FlowNode_DoN>, IForwardOutputSlotNodeGenerator
    {
        public override void GenerateForward(FlowNode_DoN node, NodeGenerationContext context)
        {
            var count = context.EnsureProgramField(node, "doNCount", typeof(int));
            var counter = context.EnsureOutputSlot(node, nameof(FlowNode_DoN.counter), typeof(int));
            if (context.EntryPortId == nameof(FlowNode_DoN.reset))
            {
                context.Emit($"{context.Indent}{count} = 0;");
                context.Emit($"{context.Indent}{context.FrameVar}.{counter} = {count};");
                return;
            }

            var n = context.GetValueExpression(node, nameof(FlowNode_DoN.n), typeof(int));
            context.Emit($"{context.Indent}if ({count} < System.Math.Max(0, {n}))");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    {count}++;");
            context.Emit($"{context.Indent}    {context.FrameVar}.{counter} = {count};");
            context.GenerateForwardConnection(context.GetExecConnection(node, nameof(FlowNode_DoN.exec)),
                context.Indent + "    ");
            context.Emit($"{context.Indent}}}");
        }

        public override bool TryGetOutputSlot(FlowNode_DoN node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            if (portId != nameof(FlowNode_DoN.counter))
            {
                outputType = null;
                slotField = null;
                return false;
            }

            outputType = typeof(int);
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
        }

        public bool IsForwardOutputSlot(CeresNode node, string portId)
        {
            return portId == nameof(FlowNode_DoN.counter);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_FlipFlop))]
    internal sealed class FlowNode_FlipFlopNodeGenerator : NodeGenerator<FlowNode_FlipFlop>, IForwardOutputSlotNodeGenerator
    {
        public override void GenerateForward(FlowNode_FlipFlop node, NodeGenerationContext context)
        {
            var nextA = context.EnsureProgramField(node, "flipFlopNextA", typeof(bool));
            var initialized = context.EnsureProgramField(node, "flipFlopInitialized", typeof(bool));
            var isA = context.EnsureOutputSlot(node, nameof(FlowNode_FlipFlop.isA), typeof(bool));
            context.Emit($"{context.Indent}if (!{initialized})");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    {initialized} = true;");
            context.Emit($"{context.Indent}    {nextA} = true;");
            context.Emit($"{context.Indent}}}");
            context.Emit($"{context.Indent}{context.FrameVar}.{isA} = {nextA};");
            context.Emit($"{context.Indent}if ({nextA})");
            context.Emit($"{context.Indent}{{");
            context.GenerateForwardConnection(context.GetExecConnection(node, nameof(FlowNode_FlipFlop.a)),
                context.Indent + "    ");
            context.Emit($"{context.Indent}}}");
            context.Emit($"{context.Indent}else");
            context.Emit($"{context.Indent}{{");
            context.GenerateForwardConnection(context.GetExecConnection(node, nameof(FlowNode_FlipFlop.b)),
                context.Indent + "    ");
            context.Emit($"{context.Indent}}}");
            context.Emit($"{context.Indent}{nextA} = !{nextA};");
        }

        public override bool TryGetOutputSlot(FlowNode_FlipFlop node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            if (portId != nameof(FlowNode_FlipFlop.isA))
            {
                outputType = null;
                slotField = null;
                return false;
            }

            outputType = typeof(bool);
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
        }

        public bool IsForwardOutputSlot(CeresNode node, string portId)
        {
            return portId == nameof(FlowNode_FlipFlop.isA);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_Gate))]
    internal sealed class FlowNode_GateNodeGenerator : NodeGenerator<FlowNode_Gate>
    {
        public override void GenerateForward(FlowNode_Gate node, NodeGenerationContext context)
        {
            var initialized = context.EnsureProgramField(node, "gateInitialized", typeof(bool));
            var isOpen = context.EnsureProgramField(node, "gateIsOpen", typeof(bool));
            var startClosed = context.GetValueExpression(node, nameof(FlowNode_Gate.startClosed), typeof(bool));
            context.Emit($"{context.Indent}if (!{initialized})");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    {initialized} = true;");
            context.Emit($"{context.Indent}    {isOpen} = !{startClosed};");
            context.Emit($"{context.Indent}}}");
            switch (context.EntryPortId)
            {
                case nameof(FlowNode_Gate.open):
                    context.Emit($"{context.Indent}{isOpen} = true;");
                    return;
                case nameof(FlowNode_Gate.close):
                    context.Emit($"{context.Indent}{isOpen} = false;");
                    return;
                case nameof(FlowNode_Gate.toggle):
                    context.Emit($"{context.Indent}{isOpen} = !{isOpen};");
                    return;
            }

            context.Emit($"{context.Indent}if ({isOpen})");
            context.Emit($"{context.Indent}{{");
            context.GenerateForwardConnection(context.GetExecConnection(node, nameof(FlowNode_Gate.exit)),
                context.Indent + "    ");
            context.Emit($"{context.Indent}}}");
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_MultiGate))]
    internal sealed class FlowNode_MultiGateNodeGenerator : NodeGenerator<FlowNode_MultiGate>
    {
        public override void GenerateForward(FlowNode_MultiGate node, NodeGenerationContext context)
        {
            var count = node.GetPortArrayLength();
            if (count <= 0) return;

            var nextIndex = context.EnsureProgramField(node, "multiGateNextIndex", typeof(int));
            var remaining = context.EnsureProgramField(node, "multiGateRemaining", typeof(List<int>));
            var randomInitialized = context.EnsureProgramField(node, "multiGateRandomInitialized", typeof(bool));
            var startIndex = context.GetValueExpression(node, nameof(FlowNode_MultiGate.startIndex), typeof(int));
            var loop = context.GetValueExpression(node, nameof(FlowNode_MultiGate.loop), typeof(bool));
            var random = context.GetValueExpression(node, nameof(FlowNode_MultiGate.random), typeof(bool));
            var selected = $"selected_{NodeGeneratorStringUtility.SafeGuid(node.Guid)}";

            context.Emit($"{context.Indent}if ({remaining} == null) {remaining} = new System.Collections.Generic.List<int>();");
            if (context.EntryPortId == nameof(FlowNode_MultiGate.reset))
            {
                EmitMultiGateReset(context, count, nextIndex, remaining, randomInitialized, startIndex);
                return;
            }

            context.Emit($"{context.Indent}var {selected} = -1;");
            context.Emit($"{context.Indent}if ({random})");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    if ({remaining}.Count == 0)");
            context.Emit($"{context.Indent}    {{");
            context.Emit($"{context.Indent}        if ({randomInitialized} && !{loop})");
            context.Emit($"{context.Indent}        {{");
            context.Emit($"{context.Indent}            {selected} = -1;");
            context.Emit($"{context.Indent}        }}");
            context.Emit($"{context.Indent}        else");
            context.Emit($"{context.Indent}        {{");
            context.Emit($"{context.Indent}            {randomInitialized} = true;");
            context.Emit($"{context.Indent}            {remaining}.Clear();");
            for (var i = 0; i < count; i++)
            {
                context.Emit($"{context.Indent}            {remaining}.Add({i});");
            }
            context.Emit($"{context.Indent}        }}");
            context.Emit($"{context.Indent}    }}");
            context.Emit($"{context.Indent}    if ({remaining}.Count > 0)");
            context.Emit($"{context.Indent}    {{");
            context.Emit($"{context.Indent}        var pick = UnityEngine.Random.Range(0, {remaining}.Count);");
            context.Emit($"{context.Indent}        {selected} = {remaining}[pick];");
            context.Emit($"{context.Indent}        {remaining}.RemoveAt(pick);");
            context.Emit($"{context.Indent}    }}");
            context.Emit($"{context.Indent}}}");
            context.Emit($"{context.Indent}else");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    if ({nextIndex} < 0 || {nextIndex} >= {count}) {nextIndex} = Mathf.Clamp({startIndex}, 0, {count - 1});");
            context.Emit($"{context.Indent}    {selected} = {nextIndex};");
            context.Emit($"{context.Indent}    {nextIndex}++;");
            context.Emit($"{context.Indent}    if ({nextIndex} >= {count}) {nextIndex} = {loop} ? 0 : {count};");
            context.Emit($"{context.Indent}    if ({selected} >= {count}) {selected} = -1;");
            context.Emit($"{context.Indent}}}");
            BuiltInNodeGeneratorUtility.EmitSwitchOutputs(context, node, selected, count);
        }

        private static void EmitMultiGateReset(NodeGenerationContext context, int count, string nextIndex,
            string remaining, string randomInitialized, string startIndex)
        {
            context.Emit($"{context.Indent}{nextIndex} = Mathf.Max(0, {startIndex});");
            context.Emit($"{context.Indent}{remaining}.Clear();");
            context.Emit($"{context.Indent}{randomInitialized} = true;");
            for (var i = 0; i < count; i++)
            {
                context.Emit($"{context.Indent}{remaining}.Add({i});");
            }
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_SwitchInt))]
    internal sealed class FlowNode_SwitchIntNodeGenerator : NodeGenerator<FlowNode_SwitchInt>
    {
        public override void GenerateForward(FlowNode_SwitchInt node, NodeGenerationContext context)
        {
            var source = context.GetValueExpression(node, nameof(FlowNode_SwitchInt.sourceValue), typeof(int));
            for (var i = 0; i < node.settings.conditions.Length; i++)
            {
                context.Emit(i == 0 ? $"{context.Indent}if ({source} == {node.settings.conditions[i]})" :
                    $"{context.Indent}else if ({source} == {node.settings.conditions[i]})");
                context.Emit($"{context.Indent}{{");
                var target = context.GetExecConnection(node, nameof(FlowNode_SwitchInt.outputs), i);
                if (target.IsValid)
                {
                    context.GenerateForwardConnection(target, context.Indent + "    ");
                }
                context.Emit($"{context.Indent}}}");
            }

            if (!node.settings.hasDefault) return;
            context.Emit(node.settings.conditions.Length == 0 ? $"{context.Indent}if (true)" : $"{context.Indent}else");
            context.Emit($"{context.Indent}{{");
            var defaultTarget = context.GetExecConnection(node, nameof(FlowNode_SwitchInt.defaultOutput));
            if (defaultTarget.IsValid)
            {
                context.GenerateForwardConnection(defaultTarget, context.Indent + "    ");
            }
            context.Emit($"{context.Indent}}}");
        }
    }
}
