using System;
using System.Reflection;
using UnityEngine.UIElements;
using UnityEngine;
namespace Ceres.Editor.Graph
{
    public class BoundsResolver : FieldResolver<BoundsField, Bounds>
    {
        public BoundsResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        protected override BoundsField CreateEditorField(FieldInfo fieldInfo)
        {
            return new BoundsField(fieldInfo.Name);
        }

        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(Bounds);
        }
    }
}