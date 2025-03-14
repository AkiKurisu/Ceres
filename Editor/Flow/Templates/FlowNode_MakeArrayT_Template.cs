using System;
using System.Linq;
using Ceres.Graph;

namespace Ceres.Editor.Graph.Flow
{
    internal class FlowNode_MakeArrayT_Template: GenericNodeTemplate
    {
        public override bool CanFilterPort(Type portValueType)
        {
            return true;
        }

        public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
        {
            return new[] { selectArgumentType };
        }

        public override Type[] GetAvailableArguments(Type portValueType)
        {
            if (portValueType == null)
            {
                var portTypes = CeresPort.GetAssignedPortValueTypes();
                return portTypes.Where(x => !x.IsArray)
                                .Concat(portTypes.Where(x=> x.IsArray).Select(x => x.GetElementType()))
                                .ToArray();
            }
            if (portValueType.IsArray)
            {
                return new[] { portValueType, portValueType.GetElementType() }; 
            }
            return new[] { portValueType };
        }
    }
}