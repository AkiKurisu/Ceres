using System;
using System.Reflection;
using Ceres.Annotations;
using UnityEditor.UIElements;
namespace Ceres.Editor.Graph
{
    [Ordered]
    public class LayerResolver : FieldResolver<LayerField, int>
    {
        public LayerResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override LayerField CreateEditorField(FieldInfo fieldInfo)
        {
            return new LayerField(fieldInfo.Name);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(int) && info.GetCustomAttribute<LayerAttribute>() != null;
        }
    }
}