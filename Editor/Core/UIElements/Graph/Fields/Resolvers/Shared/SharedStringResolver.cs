using System.Reflection;
using System;
using UnityEngine;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    public class SharedStringResolver : SharedVariableResolver<SharedString, string, TextField>
    {
        public SharedStringResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override Field CreateEditorField(FieldInfo fieldInfo)
        {
            return new SharedStringField(fieldInfo.Name, fieldInfo.FieldType, fieldInfo);
        }
        
        public class SharedStringField : Field
        {
            private readonly bool _multiline;
            public SharedStringField(string label, Type objectType, FieldInfo fieldInfo) : base(label, objectType, fieldInfo)
            {
                _multiline = fieldInfo.GetCustomAttribute<MultilineAttribute>() != null;
            }
            protected override BaseField<string> CreateValueField()
            {
                var textField = new TextField();
                if (!_multiline) return textField;
                
                textField.multiline = true;
                textField.style.maxWidth = 250;
                textField.style.whiteSpace = WhiteSpace.Normal;
                textField.AddToClassList("multiline");
                return textField;
            }
        }
    }
}