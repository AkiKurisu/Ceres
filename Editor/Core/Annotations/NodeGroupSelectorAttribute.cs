using System;
using UnityEngine;
namespace Ceres.Editor
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class NodeGroupSelectorAttribute : PropertyAttribute
    {
        public Type[] Types { get; }

        public NodeGroupSelectorAttribute(params Type[] types)
        {
            Types = types;
        }
    }
}
