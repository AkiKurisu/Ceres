using System;
using System.Linq;
using System.Reflection;
using Ceres.Graph.Flow.Annotations;
using Ceres.Utilities;
using Chris.Events;


namespace Ceres.Graph.Flow.Utilities.Templates
{
    public class FlowNode_SubscribeGlobalEventExecutionT_Template: GenericNodeTemplate
    {
        public override bool RequirePort()
        {
            return false;
        }
        
        public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
        {
            return new[] { selectArgumentType };
        }

        public override Type[] GetAvailableArguments(Type portValueType)
        {
            return SubClassSearchUtility.FindSubClassTypes(typeof(EventBase))
                .Where(x => x.GetCustomAttribute<ExecutableEventAttribute>() != null).ToArray();
        }
    }
}