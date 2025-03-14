using System;
using System.Linq;
using Ceres.Annotations;
using Ceres.Graph;
using Ceres.Utilities;

namespace Ceres.Editor.Graph.Flow
{
    internal class FlowNode_CastT_Template: GenericNodeTemplate
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