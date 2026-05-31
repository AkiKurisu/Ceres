using System;
using System.Collections.Generic;
using Ceres.Graph;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal sealed class FlowNodeLowererFactory
    {
        private static FlowNodeLowererFactory _default;

        private readonly Dictionary<Type, IFlowNodeLowerer> _lowerers = new();

        public static FlowNodeLowererFactory Get()
        {
            return _default ??= new FlowNodeLowererFactory();
        }

        public void Register(Type nodeType, IFlowNodeLowerer lowerer)
        {
            _lowerers[nodeType] = lowerer;
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
