using System;
using Ceres.Annotations;
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

        public override Type[] GetAvailableArguments(Type portValueType)
        {
            return new[]{ portValueType };
        }
        
        public override string GetGenericNodeEntryName(string label, Type selectArgumentType)
        {
            return $"{label} {CeresLabel.GetTypeName(selectArgumentType)}";
        }
    }
}