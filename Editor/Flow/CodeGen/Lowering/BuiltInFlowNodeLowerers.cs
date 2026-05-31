using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Properties;
using Ceres.Graph.Flow.Utilities;
using UnityEngine;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal sealed class SequenceNodeLowerer : FlowNodeLowerer<FlowNode_Sequence>
    {
        public override void LowerForward(FlowNode_Sequence node, FlowLoweringContext context)
        {
            foreach (var next in context.GetExecConnections(node, "outputs"))
            {
                context.LowerForwardConnection(next);
            }
        }
    }

    internal sealed class BranchNodeLowerer : FlowNodeLowerer<FlowNode_Branch>
    {
        public override void LowerForward(FlowNode_Branch node, FlowLoweringContext context)
        {
            context.LowerBranch(node);
        }
    }

    internal sealed class DebugLogNodeLowerer : FlowNodeLowerer<FlowNode_DebugLog>
    {
        public override void LowerForward(FlowNode_DebugLog node, FlowLoweringContext context)
        {
            var message = context.LowerInput(node, "message", typeof(object));
            context.AddRaw($"Debug.unityLogger.Log(LogType.{node.logType}, {message.Code}, contextObject);");
            context.LowerDefaultNext(node);
        }
    }

    internal sealed class DebugLogStringNodeLowerer : FlowNodeLowerer<FlowNode_DebugLogString>
    {
        public override void LowerForward(FlowNode_DebugLogString node, FlowLoweringContext context)
        {
            var message = context.LowerInput(node, "inString", typeof(string));
            context.AddRaw($"Debug.unityLogger.Log(LogType.{node.logType}, {message.Code}, contextObject);");
            context.LowerDefaultNext(node);
        }
    }

    internal sealed class ExecuteFunctionVoidNodeLowerer : FlowNodeLowerer<FlowNode_ExecuteFunctionVoid>
    {
        public override void LowerForward(FlowNode_ExecuteFunctionVoid node, FlowLoweringContext context)
        {
            var expression = context.BuildFunctionCallExpression(node, out _);
            context.Add(new CsExpressionStatement(expression));
            context.LowerDefaultNext(node);
        }
    }

    internal sealed class ExecuteFunctionReturnNodeLowerer : FlowNodeLowerer<FlowNode_ExecuteFunctionReturn>
    {
        public override void LowerForward(FlowNode_ExecuteFunctionReturn node, FlowLoweringContext context)
        {
            context.MaterializeFunctionReturnIfNeeded(node);
            context.LowerDefaultNext(node);
        }

        public override bool TryLowerValue(FlowNode_ExecuteFunctionReturn node, string portId,
            FlowLoweringContext context, out CsExpression expression)
        {
            if (portId == "output")
            {
                expression = context.BuildFunctionReturnExpression(node);
                return true;
            }

            expression = null;
            return false;
        }
    }

    internal sealed class PropertyValueNodeLowerer : FlowNodeLowerer<PropertyNode_PropertyValue>
    {
        public override bool CanLower(PropertyNode_PropertyValue node, FlowLoweringContext context)
        {
            return FlowCSharpRuntimeGenerator.IsGenericInstance(node, typeof(PropertyNode_GetPropertyTValue<,>)) ||
                   FlowCSharpRuntimeGenerator.IsGenericInstance(node, typeof(PropertyNode_SetPropertyTValue<,>));
        }

        public override void LowerForward(PropertyNode_PropertyValue node, FlowLoweringContext context)
        {
            if (!FlowCSharpRuntimeGenerator.IsGenericInstance(node, typeof(PropertyNode_SetPropertyTValue<,>)))
            {
                base.LowerForward(node, context);
                return;
            }

            context.LowerSetProperty(node);
        }

        public override bool TryLowerValue(PropertyNode_PropertyValue node, string portId,
            FlowLoweringContext context, out CsExpression expression)
        {
            if (portId == "outputValue" &&
                FlowCSharpRuntimeGenerator.IsGenericInstance(node, typeof(PropertyNode_GetPropertyTValue<,>)))
            {
                expression = context.BuildGetPropertyExpression(node);
                return true;
            }

            expression = null;
            return false;
        }
    }

    internal sealed class SharedVariableValueNodeLowerer : FlowNodeLowerer<PropertyNode_SharedVariableValue>
    {
        public override bool CanLower(PropertyNode_SharedVariableValue node, FlowLoweringContext context)
        {
            return FlowCSharpRuntimeGenerator.IsGenericInstance(node, typeof(PropertyNode_GetSharedVariableTValue<,,>)) ||
                   FlowCSharpRuntimeGenerator.IsGenericInstance(node, typeof(PropertyNode_SetSharedVariableTValue<,,>));
        }

        public override void LowerForward(PropertyNode_SharedVariableValue node, FlowLoweringContext context)
        {
            if (!FlowCSharpRuntimeGenerator.IsGenericInstance(node, typeof(PropertyNode_SetSharedVariableTValue<,,>)))
            {
                base.LowerForward(node, context);
                return;
            }

            context.LowerSetSharedVariable(node);
        }

        public override bool TryLowerValue(PropertyNode_SharedVariableValue node, string portId,
            FlowLoweringContext context, out CsExpression expression)
        {
            if (portId == "outputValue" &&
                FlowCSharpRuntimeGenerator.IsGenericInstance(node, typeof(PropertyNode_GetSharedVariableTValue<,,>)))
            {
                expression = context.BuildGetSharedVariableExpression(node);
                return true;
            }

            expression = null;
            return false;
        }
    }

    internal sealed class SelfReferenceNodeLowerer : FlowNodeLowerer<PropertyNode>
    {
        public override bool CanLower(PropertyNode node, FlowLoweringContext context)
        {
            return FlowCSharpRuntimeGenerator.TryGetSelfReferenceTargetType(node, out _);
        }

        public override bool TryLowerValue(PropertyNode node, string portId, FlowLoweringContext context,
            out CsExpression expression)
        {
            if (portId == "outputValue" &&
                FlowCSharpRuntimeGenerator.TryGetSelfReferenceTargetType(node, out var targetType))
            {
                expression = context.BuildSelfReferenceExpression(node, targetType);
                return true;
            }

            expression = null;
            return false;
        }
    }
}
