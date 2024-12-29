using System;
using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine;
namespace Ceres.Editor.Graph
{
    public class ColorResolver : FieldResolver<ColorField, Color>
    {
        public ColorResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        protected override ColorField CreateEditorField(FieldInfo fieldInfo)
        {
            return new ColorField(fieldInfo.Name);
        }

        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(Color);
        }
    }
}