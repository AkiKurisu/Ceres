using System;
using System.Collections.Generic;
using Ceres.Graph;
using Ceres.Utilities;
using UnityEngine.Assertions;

namespace Ceres.Editor.Graph.Flow
{
    internal class IListNodeTemplate : GenericNodeTemplate
    {
        public override bool RequirePort()
        {
            return true;
        }

        public override bool CanFilterPort(Type portValueType)
        {
            return portValueType != null && portValueType.IsInheritedFromGenericDefinition(typeof(IList<>), out _);
        }

        public override Type[] GetGenericArguments(Type portValueType, Type selectArgumentType)
        {
            return new[] { selectArgumentType };
        }

        public override Type[] GetAvailableArguments(Type portValueType)
        {
            Assert.IsTrue(portValueType.IsInheritedFromGenericDefinition(typeof(IList<>), out var arguments));
            return new[] { arguments[0] };
        }
    }
}
