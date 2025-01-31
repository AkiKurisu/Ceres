using System;
using System.Reflection;
using UnityEngine.UIElements;
using UnityEngine;
namespace Ceres.Editor.Graph
{
    public class BoundsIntResolver : FieldResolver<BoundsIntField, BoundsInt>
    {
        public BoundsIntResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override BoundsIntField CreateEditorField(FieldInfo fieldInfo)
        {
            return new BoundsIntField(fieldInfo.Name);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(BoundsInt);
        }
    }
}