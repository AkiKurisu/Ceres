using System;
using System.Linq;
namespace Ceres.Graph.Flow.Utilities.Templates
{
    public class FlowNode_SwitchEnumT_Template: GenericNodeTemplate
    {
        public override bool CanFilterPort(Type portValueType)
        {
            return portValueType == null || portValueType.IsEnum;
        }

        public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
        {
            return new[] { portValueType ?? selectArgumentType };
        }

        public override Type[] GetAvailableArguments(Type portValueType)
        {
            if (portValueType == null)
            {
                return CeresPort.GetAssignedPortValueTypes().Where(x => x.IsEnum).ToArray();
            }
            return new []{ portValueType };
        }
    }
}