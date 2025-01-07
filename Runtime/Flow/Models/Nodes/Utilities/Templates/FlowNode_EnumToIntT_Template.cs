using System;
using System.Linq;
namespace Ceres.Graph.Flow.Utilities.Templates
{
    public class FlowNode_EnumToIntT_Template: GenericNodeTemplate
    {
        public override bool CanFilterPort(Type portValueType)
        {
            return portValueType == null || portValueType.IsEnum;
        }

        public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
        {
            return new[] { portValueType ?? selectArgumentType };
        }

        public override Type[] GetAvailableArgumentTypes(Type portValueType)
        {
            if (portValueType == null)
            {
                return CeresPort.GetAssignedPortValueTypes().Where(x => x.IsEnum).ToArray();
            }
            return new []{ portValueType };
        }
    }
}