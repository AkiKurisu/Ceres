using System.Reflection;
using UnityEditor.UIElements;
using System;
using Ceres.Graph;
using UnityEngine.UIElements;
using UObject = UnityEngine.Object;

namespace Ceres.Editor.Graph
{
    public sealed class SharedUObjectResolver : FieldResolver<SharedUObjectField, SharedUObject>
    {
        public SharedUObjectResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override SharedUObjectField CreateEditorField(FieldInfo fieldInfo)
        {
            return new SharedUObjectField(fieldInfo.Name, fieldInfo.FieldType, fieldInfo);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo _)
        {
            return fieldValueType == typeof(SharedUObject);
        }
    }
    
    public sealed class SharedUObjectField : SharedVariableField<SharedUObject, UObject>
    {
        public SharedUObjectField(string label, Type objectType, FieldInfo fieldInfo) : base(label, objectType, fieldInfo)
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