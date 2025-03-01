using System;
using System.Linq;
using Ceres.Annotations;
using Ceres.Utilities;
namespace Ceres.Graph.Flow.Utilities.Templates
{
    public class FlowNode_CastT_Template: GenericNodeTemplate
    {
        public override bool RequirePort()
        {
            return true;
        }
        
        public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
        {
            return new[] { portValueType, selectArgumentType };
        }

        public override Type[] GetAvailableArguments(Type portValueType)
        {
            return CeresPort.GetAssignedPortValueTypes()
                            .Concat(ExecutableFunctionRegistry.Get().GetManagedTypes())
                            .Distinct()
                            .Where(x=> x.IsAssignableTo(portValueType) && x != portValueType)
                            .ToArray();
        }
        
        protected override string GetGenericNodeBaseName(string label, Type[] argumentTypes)
        {
            /* Cast to {value type} */
            return string.Format(label, CeresLabel.GetTypeName(argumentTypes[1]));
        }
    }
}