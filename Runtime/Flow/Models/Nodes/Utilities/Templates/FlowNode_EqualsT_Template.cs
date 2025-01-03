using System;
namespace Ceres.Graph.Flow.Utilities.Templates
{
    public class FlowNode_EqualsT_Template: GenericNodeTemplate
    {
        public override bool RequirePort()
        {
            return true;
        }
        
        public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
        {
            return new[] { portValueType };
        }

        public override Type[] GetAvailableArgumentTypes(Type portValueType)
        {
            return new[]{ portValueType };
        }
        
        public override string GetGenericNodeEntryName(string label, Type selectArgumentType)
        {
            return $"{label}<{selectArgumentType.Name}>";
        }
    }
}