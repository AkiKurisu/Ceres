using System.Reflection;
using UnityEditor.UIElements;
using System;
using UnityEngine.UIElements;
using UObject = UnityEngine.Object;
namespace Ceres.Editor.Graph
{
    public sealed class SharedObjectResolver : FieldResolver<SharedObjectField, SharedUObject>
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
    
    public sealed class SharedObjectField : SharedVariableField<SharedUObject, UObject>
    {
        public SharedObjectField(string label, Type objectType, FieldInfo fieldInfo) : base(label, objectType, fieldInfo)
        {
        }
        
        protected override BaseField<UObject> CreateValueField()
        {
            return new ObjectField
            {
                objectType = value.GetValueType()
            };
        }

        protected override void OnRepaint()
        {
            ((ObjectField)ValueField).objectType = value.GetValueType();
        }
    }
}