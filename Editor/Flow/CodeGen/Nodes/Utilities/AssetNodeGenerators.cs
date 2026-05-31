using System;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Utilities;
using Chris.Events;
using Chris.Resource;
using R3;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
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

    [CustomNodeGenerator(typeof(FlowNode_InstantiateT<>))]
    internal sealed class FlowNode_InstantiateTNodeGenerator : NodeGenerator<FlowNode>
    {
        public override void GenerateForward(FlowNode node, NodeGenerationContext context)
        {
            context.Source.GenerateDependencyCall(node, context.FrameTypeName, context.FrameVar, context.Indent,
                FlowCSharpRuntimeGenerator.SourceContext.DependencyCancellationCheck.AlreadyChecked);
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
}
