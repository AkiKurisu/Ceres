using System;
using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine;
namespace Ceres.Editor.Graph
{
    public class CurveResolver : FieldResolver<CurveField, AnimationCurve>
    {
        public CurveResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override CurveField CreateEditorField(FieldInfo fieldInfo)
        {
            return new CurveField(fieldInfo.Name);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(AnimationCurve);
        }
    }
}