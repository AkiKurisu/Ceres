using System;
using System.Collections.Generic;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.CustomFunctions;
using Ceres.Graph.Flow.Properties;
using Ceres.Graph.Flow.Utilities;
using Chris.Events;
using Chris.Resource;
using R3;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    [CustomNodeGenerator(typeof(FlowNode_Sequence))]
    internal sealed class FlowNode_SequenceNodeGenerator : NodeGenerator<FlowNode_Sequence>
    {
        public override void GenerateForward(FlowNode_Sequence node, NodeGenerationContext context)
        {
            foreach (var next in context.GetExecTargets(node, "outputs"))
            {
                context.GenerateForwardNode(next);
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

    [CustomNodeGenerator(typeof(FlowNode_SoftAssetReferenceTLoadAssetAsync<>))]
    internal sealed class FlowNode_SoftAssetReferenceTLoadAssetAsyncNodeGenerator : NodeGenerator<FlowNode>
    {
        public override void GenerateForward(FlowNode node, NodeGenerationContext context)
        {
            var objectType = node.GetType().GetGenericArguments()[0];
            var referenceType = typeof(SoftAssetReference<>).MakeGenericType(objectType);
            var delegateType = typeof(EventDelegate<>).MakeGenericType(objectType);
            var reference = context.GetValueExpression(node, "reference", referenceType);
            var onComplete = context.GetValueExpression(node, "onComplete", delegateType);
            context.Emit($"{context.Indent}{reference}.LoadAsync().AddTo(this).RegisterCallback({onComplete});");
            context.GenerateDefaultNext(node);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_SoftAssetReferenceLoadAssetAsync))]
    internal sealed class FlowNode_SoftAssetReferenceLoadAssetAsyncNodeGenerator :
        NodeGenerator<FlowNode_SoftAssetReferenceLoadAssetAsync>
    {
        public override void GenerateForward(FlowNode_SoftAssetReferenceLoadAssetAsync node,
            NodeGenerationContext context)
        {
            var reference = context.GetValueExpression(node, "reference", typeof(SoftAssetReference));
            var onComplete = context.GetValueExpression(node, "onComplete", typeof(EventDelegate<UObject>));
            context.Emit($"{context.Indent}{reference}.LoadAsync().AddTo(this).RegisterCallback({onComplete});");
            context.GenerateDefaultNext(node);
        }
    }

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
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = default;");
            context.Emit($"{context.Indent}if ({context.FrameVar}.ContextObject is IFlowGraphRuntime runtime)");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    {context.FrameVar}.{slot} = ({target}).SubscribeExecution<{context.GetFriendlyTypeName(eventType)}>(runtime);");
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
            context.Emit($"{context.Indent}{context.FrameVar}.{slot} = default;");
            context.Emit($"{context.Indent}if ({context.FrameVar}.ContextObject is IFlowGraphRuntime runtime)");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    {context.FrameVar}.{slot} = EventSystem.EventHandler.SubscribeExecution<{context.GetFriendlyTypeName(eventType)}>(runtime);");
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

    [CustomNodeGenerator(typeof(FlowNode_ExecuteFunctionVoid), true)]
    internal sealed class FlowNode_ExecuteFunctionVoidNodeGenerator : NodeGenerator<FlowNode_ExecuteFunctionVoid>
    {
        public override bool CanGenerate(FlowNode_ExecuteFunctionVoid node, NodeGenerationContext context)
        {
            return context.Source.CanGenerateExecuteFunctionVoid(node);
        }

        public override void GenerateForward(FlowNode_ExecuteFunctionVoid node, NodeGenerationContext context)
        {
            context.Source.GenerateExecuteFunctionVoid(node, context.FrameTypeName, context.FrameVar, context.Indent);
        }

        public override void GenerateDependency(FlowNode_ExecuteFunctionVoid node, NodeGenerationContext context)
        {
            context.Source.GenerateExecuteFunctionVoidLocal(node, context.FrameTypeName, context.FrameVar,
                context.Indent);
        }
    }

    [CustomNodeGenerator(typeof(FlowNode_ExecuteFunctionReturn), true)]
    internal sealed class FlowNode_ExecuteFunctionReturnNodeGenerator : NodeGenerator<FlowNode_ExecuteFunctionReturn>
    {
        public override bool CanGenerate(FlowNode_ExecuteFunctionReturn node, NodeGenerationContext context)
        {
            return context.Source.CanGenerateExecuteFunctionReturn(node);
        }

        public override void GenerateForward(FlowNode_ExecuteFunctionReturn node, NodeGenerationContext context)
        {
            context.Source.GenerateDependencyCall(node, context.FrameTypeName, context.FrameVar, context.Indent);
            context.GenerateDefaultNext(node);
        }

        public override void GenerateDependency(FlowNode_ExecuteFunctionReturn node, NodeGenerationContext context)
        {
            context.Source.GenerateFunctionReturnLocal(node, context.FrameTypeName, context.FrameVar, context.Indent);
        }

        public override bool TryGetOutputSlot(FlowNode_ExecuteFunctionReturn node, string portId,
            NodeGenerationContext context, out Type outputType, out string slotField)
        {
            return context.Source.TryGetFunctionReturnOutputSlot(node, portId, out outputType, out slotField);
        }
    }

    [CustomNodeGenerator(typeof(PropertyNode_PropertyValue), true)]
    internal sealed class PropertyNode_PropertyValueNodeGenerator : NodeGenerator<PropertyNode_PropertyValue>
    {
        public override bool CanGenerate(PropertyNode_PropertyValue node, NodeGenerationContext context)
        {
            return context.Source.CanGenerateGetProperty(node) || context.Source.CanGenerateSetProperty(node);
        }

        public override void GenerateForward(PropertyNode_PropertyValue node, NodeGenerationContext context)
        {
            if (context.Source.CanGenerateSetProperty(node))
            {
                context.Source.GenerateSetProperty(node, context.FrameTypeName, context.FrameVar, context.Indent);
                return;
            }

            base.GenerateForward(node, context);
        }

        public override void GenerateDependency(PropertyNode_PropertyValue node, NodeGenerationContext context)
        {
            if (context.Source.CanGenerateGetProperty(node))
            {
                context.Source.GenerateGetPropertyLocal(node, context.FrameTypeName, context.FrameVar, context.Indent);
                return;
            }

            base.GenerateDependency(node, context);
        }

        public override bool TryGetOutputSlot(PropertyNode_PropertyValue node, string portId,
            NodeGenerationContext context, out Type outputType, out string slotField)
        {
            return context.Source.TryGetPropertyOutputSlot(node, portId, out outputType, out slotField);
        }
    }

    [CustomNodeGenerator(typeof(PropertyNode_GetSelfTReference<>))]
    internal sealed class PropertyNode_GetSelfTReferenceNodeGenerator : NodeGenerator<PropertyNode>
    {
        public override bool CanGenerate(PropertyNode node, NodeGenerationContext context)
        {
            return context.Source.CanGenerateGetSelfReference(node);
        }

        public override void GenerateDependency(PropertyNode node, NodeGenerationContext context)
        {
            context.Source.GenerateGetSelfReferenceLocal(node, context.FrameTypeName, context.FrameVar,
                context.Indent);
        }

        public override bool TryGetOutputSlot(PropertyNode node, string portId, NodeGenerationContext context,
            out Type outputType, out string slotField)
        {
            return context.Source.TryGetSelfReferenceOutputSlot(node, portId, out outputType, out slotField);
        }
    }

    [CustomNodeGenerator(typeof(PropertyNode_SharedVariableValue), true)]
    internal sealed class PropertyNode_SharedVariableValueNodeGenerator :
        NodeGenerator<PropertyNode_SharedVariableValue>
    {
        public override bool CanGenerate(PropertyNode_SharedVariableValue node, NodeGenerationContext context)
        {
            return context.Source.CanGenerateGetSharedVariable(node) ||
                   context.Source.CanGenerateSetSharedVariable(node);
        }

        public override void GenerateForward(PropertyNode_SharedVariableValue node, NodeGenerationContext context)
        {
            if (context.Source.CanGenerateSetSharedVariable(node))
            {
                context.Source.GenerateSetSharedVariable(node, context.FrameTypeName, context.FrameVar,
                    context.Indent);
                return;
            }

            base.GenerateForward(node, context);
        }

        public override void GenerateDependency(PropertyNode_SharedVariableValue node, NodeGenerationContext context)
        {
            if (context.Source.CanGenerateGetSharedVariable(node))
            {
                context.Source.GenerateGetSharedVariableLocal(node, context.FrameTypeName, context.FrameVar,
                    context.Indent);
                return;
            }

            base.GenerateDependency(node, context);
        }

        public override bool TryGetOutputSlot(PropertyNode_SharedVariableValue node, string portId,
            NodeGenerationContext context, out Type outputType, out string slotField)
        {
            return context.Source.TryGetSharedVariableOutputSlot(node, portId, out outputType, out slotField);
        }
    }

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
            var success = context.GetExecTarget(node, "exec");
            if (success != null)
            {
                context.GenerateForwardNode(success, context.Indent + "    ");
            }

            context.Emit($"{context.Indent}}}");
            context.Emit($"{context.Indent}catch (System.InvalidCastException)");
            context.Emit($"{context.Indent}{{");
            var failed = context.GetExecTarget(node, "castFailed");
            if (failed != null)
            {
                context.GenerateForwardNode(failed, context.Indent + "    ");
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

    [CustomNodeGenerator(typeof(FlowNode_InstantiateT<>))]
    internal sealed class FlowNode_InstantiateTNodeGenerator : NodeGenerator<FlowNode>
    {
        public override void GenerateForward(FlowNode node, NodeGenerationContext context)
        {
            context.Source.GenerateDependencyCall(node, context.FrameTypeName, context.FrameVar, context.Indent);
            context.GenerateNext(node);
        }

        public override void GenerateDependency(FlowNode node, NodeGenerationContext context)
        {
            var objectType = node.GetType().GetGenericArguments()[0];
            var original = context.GetValueExpression(node, "original", objectType);
            var position = context.GetValueExpression(node, "position", typeof(Vector3));
            var rotation = context.GetValueExpression(node, "rotation", typeof(Quaternion));
            var parent = context.GetValueExpression(node, "parent", typeof(Transform));
            var worldPositionStays = context.GetValueExpression(node, "worldPositionStays", typeof(bool));
            var slot = context.EnsureOutputSlot(node, "resultValue", objectType);
            context.Emit($"{context.Indent}if ({worldPositionStays})");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    {context.FrameVar}.{slot} = UObject.Instantiate({original}, {parent}, {worldPositionStays});");
            context.Emit($"{context.Indent}}}");
            context.Emit($"{context.Indent}else");
            context.Emit($"{context.Indent}{{");
            context.Emit($"{context.Indent}    {context.FrameVar}.{slot} = UObject.Instantiate({original}, {position}, {rotation}, {parent});");
            context.Emit($"{context.Indent}}}");
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

            outputType = node.GetType().GetGenericArguments()[0];
            slotField = context.EnsureOutputSlot(node, portId, outputType);
            return true;
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
                var target = context.GetExecTarget(node, nameof(FlowNode_SwitchString.outputs), i);
                if (target != null)
                {
                    context.GenerateForwardNode(target, context.Indent + "    ");
                }

                context.Emit($"{context.Indent}}}");
            }

            if (!node.settings.hasDefault) return;
            context.Emit(node.settings.conditions.Length == 0 ? $"{context.Indent}if (true)" : $"{context.Indent}else");
            context.Emit($"{context.Indent}{{");
            var defaultTarget = context.GetExecTarget(node, nameof(FlowNode_SwitchString.defaultOutput));
            if (defaultTarget != null)
            {
                context.GenerateForwardNode(defaultTarget, context.Indent + "    ");
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
                var target = context.GetExecTarget(node, "outputs", i);
                if (target != null)
                {
                    context.GenerateForwardNode(target, context.Indent + "        ");
                }

                context.Emit($"{context.Indent}        break;");
            }

            context.Emit($"{context.Indent}    default:");
            context.Emit($"{context.Indent}        throw new System.IndexOutOfRangeException();");
            context.Emit($"{context.Indent}}}");
        }
    }

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
            var loopBody = context.GetExecTarget(node, "loopBody");
            if (loopBody != null)
            {
                context.GenerateForwardNode(loopBody, context.Indent + "        ");
            }

            context.Emit($"{context.Indent}    }}");
            context.Emit($"{context.Indent}}}");
            var completed = context.GetExecTarget(node, "completed");
            if (completed != null)
            {
                context.GenerateForwardNode(completed);
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
