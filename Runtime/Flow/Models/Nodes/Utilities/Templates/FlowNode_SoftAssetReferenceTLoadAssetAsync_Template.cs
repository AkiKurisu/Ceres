using System;
using Chris;
using Chris.Resource;
namespace Ceres.Graph.Flow.Utilities
{
    public class FlowNode_SoftAssetReferenceTLoadAssetAsync_Template: GenericNodeTemplate
    {
        public override bool RequirePort()
        {
            return true;
        }

        public override bool CanFilterPort(Type portValueType)
        {
            if (portValueType == null) return false;
            if (!portValueType.IsGenericType) return false;
            return portValueType.GetGenericTypeDefinition() == typeof(SoftAssetReference<>);
        }

        public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
        {
            return new[] { selectArgumentType };
        }

        public override Type[] GetAvailableArguments(Type portValueType)
        {
            return new[] { ReflectionUtility.GetGenericArgumentType(portValueType)};
        }

        protected override string GetTargetName(Type[] argumentTypes)
        {
            var genericType = typeof(SoftAssetReference<>).MakeGenericType(argumentTypes[0]);
            return CeresNode.GetTargetSubtitle(genericType);
        }
    }
}