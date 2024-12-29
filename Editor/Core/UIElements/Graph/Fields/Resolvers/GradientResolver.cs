using System;
using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine;
namespace Ceres.Editor.Graph
{
    public class GradientResolver : FieldResolver<GradientField, Gradient>
    {
        public GradientResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override GradientField CreateEditorField(FieldInfo fieldInfo)
        {
            return new GradientField(fieldInfo.Name);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(Gradient);
        }
    }
}