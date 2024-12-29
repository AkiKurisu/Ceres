using System.Reflection;
using UnityEditor.UIElements;
using System;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    public class SharedObjectResolver : FieldResolver<SharedObjectField, SharedUObject>
    {
        public SharedObjectResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override SharedObjectField CreateEditorField(FieldInfo fieldInfo)
        {
            return new SharedObjectField(fieldInfo.Name, fieldInfo.FieldType, fieldInfo);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo _)
        {
            return fieldValueType == typeof(SharedUObject);
        }
    }
    public class SharedObjectField : SharedVariableField<SharedUObject, UnityEngine.Object>
    {
        public SharedObjectField(string label, Type objectType, FieldInfo fieldInfo) : base(label, objectType, fieldInfo)
        {
        }
        
        protected override BaseField<UnityEngine.Object> CreateValueField()
        {
            return new ObjectField
            {
                objectType = value.GetValueType()
            };
        }

        protected sealed override void OnRepaint()
        {
            ((ObjectField)ValueField).objectType = value.GetValueType();
        }
    }
}