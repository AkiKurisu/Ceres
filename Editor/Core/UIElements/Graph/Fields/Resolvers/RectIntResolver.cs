using System;
using System.Reflection;
using UnityEngine.UIElements;
using UnityEngine;
namespace Ceres.Editor.Graph
{
    public class RectIntResolver : FieldResolver<RectIntField, RectInt>
    {
        public RectIntResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override RectIntField CreateEditorField(FieldInfo fieldInfo)
        {
            return new RectIntField(fieldInfo.Name);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(RectInt);
        }
    }
}