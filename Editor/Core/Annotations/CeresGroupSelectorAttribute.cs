using System;
using UnityEngine;
namespace Ceres.Editor
{
    [AttributeUsage(AttributeTargets.Field)]
    public class CeresGroupSelectorAttribute : PropertyAttribute
    {
        public Type[] Types { get; }

        public CeresGroupSelectorAttribute(params Type[] types)
        {
            Types = types;
        }
    }
}
