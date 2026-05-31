using System;
using System.Collections.Generic;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Properties;
using Ceres.Graph.Flow.Utilities;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal sealed class FlowNodeLowererFactory
    {
        private static FlowNodeLowererFactory _default;

        private readonly Dictionary<Type, IFlowNodeLowerer> _lowerers = new();

        private FlowNodeLowererFactory()
        {
            RegisterBuiltInLowerers();
        }

        public static FlowNodeLowererFactory Get()
        {
            return _default ??= new FlowNodeLowererFactory();
        }

        public void Register(Type nodeType, IFlowNodeLowerer lowerer)
        {
            _lowerers[nodeType] = lowerer;
        }

        private void RegisterBuiltInLowerers()
        {
            Register(typeof(FlowNode_Sequence), new SequenceNodeLowerer());
            Register(typeof(FlowNode_Branch), new BranchNodeLowerer());
            Register(typeof(FlowNode_DebugLog), new DebugLogNodeLowerer());
            Register(typeof(FlowNode_DebugLogString), new DebugLogStringNodeLowerer());
            Register(typeof(FlowNode_ExecuteFunctionVoid), new ExecuteFunctionVoidNodeLowerer());
            Register(typeof(FlowNode_ExecuteFunctionReturn), new ExecuteFunctionReturnNodeLowerer());
            Register(typeof(PropertyNode_SharedVariableValue), new SharedVariableValueNodeLowerer());
            Register(typeof(PropertyNode_PropertyValue), new PropertyValueNodeLowerer());
            Register(typeof(PropertyNode), new SelfReferenceNodeLowerer());
        }

        public bool TryGetLowerer(Type nodeType, out IFlowNodeLowerer lowerer)
        {
            var current = nodeType;
            while (current != null)
            {
                if (_lowerers.TryGetValue(current, out lowerer))
                {
                    return true;
                }

                if (current.IsGenericType &&
                    _lowerers.TryGetValue(current.GetGenericTypeDefinition(), out lowerer))
                {
                    return true;
                }

                current = current.BaseType;
            }

            lowerer = null;
            return false;
        }

        public bool CanLower(CeresNode node, FlowLoweringContext context)
        {
            return TryGetLowerer(node.GetType(), out var lowerer) && lowerer.CanLower(node, context);
        }
    }
}
