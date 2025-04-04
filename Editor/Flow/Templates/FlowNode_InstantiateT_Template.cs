using System;
using Ceres.Graph;
using UnityEngine;
using UObject = UnityEngine.Object;
namespace Ceres.Editor.Graph.Flow
{
    internal class FlowNode_InstantiateT_Template: GenericNodeTemplate
    {
        public override bool CanFilterPort(Type portValueType)
        {
            if (portValueType == null) return true;
            return portValueType.IsSubclassOf(typeof(UObject));
        }

        public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
        {
            return new[] { selectArgumentType };
        }

        public override Type[] GetAvailableArguments(Type portValueType)
        {
            /* Default instantiate as GameObject */
            portValueType ??= typeof(GameObject);
            return new[] { portValueType };
        }
    }
}