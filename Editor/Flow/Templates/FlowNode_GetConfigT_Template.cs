using System;
using System.Linq;
using Ceres.Graph;
using Ceres.Utilities;
using Chris.Configs;

namespace Ceres.Editor.Graph.Flow
{
    internal class FlowNode_GetConfigT_Template: GenericNodeTemplate
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
            _typesCache ??= SubClassSearchUtility.FindSubClassTypes(typeof(ConfigBase))
                .Where(x => x.IsInheritedFromGenericDefinition(typeof(Config<>), out _)).ToArray();
            return _typesCache;
        }
    }
}