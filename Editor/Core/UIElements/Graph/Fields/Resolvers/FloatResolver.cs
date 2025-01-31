using System;
using System.Reflection;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    public class FloatResolver : FieldResolver<FloatField, float>
    {
        public FloatResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override FloatField CreateEditorField(FieldInfo fieldInfo)
        {
            return new FloatField(fieldInfo.Name);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(float);
        }
    }
}