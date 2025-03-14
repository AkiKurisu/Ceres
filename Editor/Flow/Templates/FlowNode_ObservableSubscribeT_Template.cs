using System;
using Ceres.Graph;
using Chris;
using R3;

namespace Ceres.Editor.Graph.Flow
{
    internal class FlowNode_ObservableSubscribeT_Template: GenericNodeTemplate
    {
        public override bool RequirePort()
        {
            return true;
        }
        
        public override bool CanFilterPort(Type portValueType)
        {
            if (portValueType == null)
            {
                return false;
            }

            if (!portValueType.IsGenericType)
            {
                return false;
            }

            return ReflectionUtility.IsInheritedFromGenericDefinition(portValueType, typeof(Observable<>));
        }
        
        public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
        {
            return new[] { selectArgumentType };
        }

        public override Type[] GetAvailableArguments(Type portValueType)
        {
            return new[]{ ReflectionUtility.GetGenericArgumentType(portValueType) };
        }
        
        protected override string GetTargetName(Type[] argumentTypes)
        {
            var genericType = typeof(Observable<>).MakeGenericType(argumentTypes[0]);
            return CeresNode.GetTargetSubtitle(genericType);
        }
    }
}