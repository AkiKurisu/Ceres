using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    public class StringResolver : FieldResolver<TextField, string>
    {
        public StringResolver(FieldInfo fieldInfo) : base(fieldInfo) { }
        protected override TextField CreateEditorField(FieldInfo fieldInfo)
        {
            var multiline = fieldInfo.GetCustomAttribute<MultilineAttribute>() != null;
            var field = new TextField(fieldInfo.Name);
            field.style.minWidth = 200;
            if (multiline)
            {
                field.multiline = true;
                field.style.maxWidth = 250;
                field.style.whiteSpace = WhiteSpace.Normal;
                field.AddToClassList("Multiline");
            }
            return field;
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo _)
        {
            return fieldValueType == typeof(string);
        }
    }
}