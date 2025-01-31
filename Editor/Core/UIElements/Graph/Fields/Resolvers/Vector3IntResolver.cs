using System;
using System.Reflection;
using UnityEngine.UIElements;
using UnityEngine;
namespace Ceres.Editor.Graph
{
    public class Vector3IntResolver : FieldResolver<Vector3IntField, Vector3Int>
    {
        public Vector3IntResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override Vector3IntField CreateEditorField(FieldInfo fieldInfo)
        {
            return new Vector3IntField(fieldInfo.Name);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(Vector3Int);
        }
    }
}