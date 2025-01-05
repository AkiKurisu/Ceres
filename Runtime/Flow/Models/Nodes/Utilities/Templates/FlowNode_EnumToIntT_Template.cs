using System;
namespace Ceres.Graph.Flow.Utilities.Templates
{
    public class FlowNode_EnumToIntT_Template: GenericNodeTemplate
    {
        public override bool RequirePort()
        {
            return true;
        }

        public override bool CanFilterPort(Type portValueType)
        {
            return portValueType != null && portValueType.IsEnum;
        }

        public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
        {
            return new[] { portValueType };
        }

        public override Type[] GetAvailableArgumentTypes(Type portValueType)
        {
            return new []{ portValueType };
        }
        
        protected override string GetGenericNodeBaseName(string label, Type[] argumentTypes)
        {
            return label;
        }
    }
}