using System.Reflection;
using System;
using UnityEngine.UIElements;
using UnityEngine;
namespace Ceres.Editor.Graph
{
    public abstract class SharedVariableResolver<TVariable, TValue, TField> :
        FieldResolver<SharedVariableResolver<TVariable, TValue, TField>.Field, TVariable> 
        where TVariable : SharedVariable<TValue>, new()
        where TField: BaseField<TValue>, new()
    {
        public class Field : SharedVariableField<TVariable, TValue>
        {
            public Field(string label, Type objectType, FieldInfo fieldInfo) : base(label, objectType, fieldInfo)
            {


            }

            protected override BaseField<TValue> CreateValueField() => new TField();
        }

        protected SharedVariableResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override Field CreateEditorField(FieldInfo fieldInfo)
        {
            return new Field(fieldInfo.Name, fieldInfo.FieldType, fieldInfo);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo _) => fieldValueType == typeof(TVariable);
    }
    
    public class SharedBoolResolver : SharedVariableResolver<SharedBool, bool, Toggle>
    {
        public SharedBoolResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
    }
    
    public class SharedIntResolver : SharedVariableResolver<SharedInt, int, IntegerField>
    {
        public SharedIntResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
    }
    
    public class SharedFloatResolver : SharedVariableResolver<SharedFloat, float, FloatField>
    {
        public SharedFloatResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
    }
    
    public class SharedDoubleResolver : SharedVariableResolver<SharedDouble, double, DoubleField>
    {
        public SharedDoubleResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
    }
    
    public class SharedVector2IntResolver : SharedVariableResolver<SharedVector2Int, Vector2Int, Vector2IntField>
    {
        public SharedVector2IntResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
    }
    
    public class SharedVector2Resolver : SharedVariableResolver<SharedVector2, Vector2, Vector2Field>
    {
        public SharedVector2Resolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
    }
    
    public class SharedVector3IntResolver : SharedVariableResolver<SharedVector3Int, Vector3Int, Vector3IntField>
    {
        public SharedVector3IntResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
    }
    
    public class SharedVector3Resolver : SharedVariableResolver<SharedVector3, Vector3, Vector3Field>
    {
        public SharedVector3Resolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
    }
}
