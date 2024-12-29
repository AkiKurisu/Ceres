using System;
using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine;
namespace Ceres.Editor.Graph
{
    public class LayerMaskResolver : FieldResolver<LayerMaskField, int>
    {
        public LayerMaskResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override LayerMaskField CreateEditorField(FieldInfo fieldInfo)
        {
            return new LayerMaskField(fieldInfo.Name);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(LayerMask);
        }
    }
}