using System;
using System.Reflection;
using UnityEngine.UIElements;
using UnityEngine;
namespace Ceres.Editor.Graph
{
    public class RectResolver : FieldResolver<RectField, Rect>
    {
        public RectResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override RectField CreateEditorField(FieldInfo fieldInfo)
        {
            return new RectField(fieldInfo.Name);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(Rect);
        }
    }
}