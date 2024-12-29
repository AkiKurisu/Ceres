using System;
namespace Ceres.Graph.Flow.Utilities.Templates
{
    public class FlowNode_ExecuteFunctionT_Template: GenericNodeTemplate
    {
        public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
        {
            return new[] { selectArgumentType };
        }

        public override Type[] GetAvailableArgumentTypes(Type portValueType)
        {
            return new[] { portValueType };
        }
    }
}