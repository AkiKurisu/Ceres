using System;
using System.Linq;
using System.Reflection;
using Ceres.Graph;
using Ceres.Graph.Flow.Annotations;
using Ceres.Utilities;
using Chris.Events;

namespace Ceres.Editor.Graph.Flow
{
    internal class FlowNode_SubscribeEventExecutionT_Template: GenericNodeTemplate
    {
        private Type[] _typesCache;
        
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
            _typesCache ??= SubClassSearchUtility.FindSubClassTypes(typeof(EventBase))
                .Where(x => x.GetCustomAttribute<ExecutableEventAttribute>() != null).ToArray();
            return _typesCache;
        }
    }
    
    internal class FlowNode_SubscribeGlobalEventExecutionT_Template: FlowNode_SubscribeEventExecutionT_Template
    {
        private Type[] _typesCache;
        
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
            _typesCache ??= SubClassSearchUtility.FindSubClassTypes(typeof(EventBase))
                .Where(x => x.GetCustomAttribute<ExecutableEventAttribute>() != null).ToArray();
            return _typesCache;
        }
    }
}