using System;
using System.Collections.Generic;
using Ceres.Graph;
using Ceres.Utilities;
using UnityEngine.Assertions;

namespace Ceres.Editor.Graph.Flow
{
    internal class IReadOnlyListNodeTemplate : GenericNodeTemplate
    {
        public override bool CanFilterPort(Type portValueType)
        {
            if (portValueType == null) return false;
            if (portValueType.IsArray)
            {
                return true;
            }

            return portValueType.IsInheritedFromGenericDefinition(typeof(IReadOnlyList<>), out _);
        }

        public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
        {
            return new[] { selectArgumentType };
        }

        public override Type[] GetAvailableArguments(Type portValueType)
        {
            Type elementType = null;
            if (portValueType.IsArray)
            {
                elementType = portValueType.GetElementType();
            }
            else
            {
                if (portValueType.IsInheritedFromGenericDefinition(typeof(IReadOnlyList<>), out var arguments))
                {
                    elementType = arguments[0];
                }
            }
            Assert.IsNotNull(elementType);
            return new[] { elementType }; 
        }
    }

    internal sealed class FlowNode_GetArrayElementT_Template : IReadOnlyListNodeTemplate
    {
    }
}