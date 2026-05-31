using System;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Properties;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
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
}
