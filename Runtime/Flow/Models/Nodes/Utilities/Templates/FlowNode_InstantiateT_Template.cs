using System;
using UnityEngine;
using UObject = UnityEngine.Object;
namespace Ceres.Graph.Flow.Utilities.Templates
{
    public class FlowNode_InstantiateT_Template: GenericNodeTemplate
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

        public override Type[] GetAvailableArgumentTypes(Type portValueType)
        {
            /* Default instantiate as GameObject */
            portValueType ??= typeof(GameObject);
            return new[] { portValueType };
        }
    }
}